using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(InspectPaneFiller), nameof(InspectPaneFiller.DoPaneContentsFor))]
public static class Patch_InspectPaneFiller_DoPaneContentsFor
{
    public static bool Prefix(ISelectable sel, Rect rect)
    {
        if (sel is not Pawn pawn || PawnReadout.SelectedPawn() != pawn)
        {
            return true;
        }

        PawnReadoutDrawer.Draw(pawn, rect);
        return false;
    }
}

// Creatures keep the base game's bar row; the cards and the inspect text it leaves are drawn in place of its text.
[HarmonyPatch(typeof(InspectPaneFiller), nameof(InspectPaneFiller.DrawInspectStringFor))]
public static class Patch_InspectPaneFiller_DrawInspectStringFor_Creature
{
    public static bool Prefix(ISelectable sel, Rect rect)
    {
        if (sel is not Pawn pawn || PawnReadout.CardPawn() != pawn)
        {
            return true;
        }

        PawnReadoutDrawer.DrawCreatureBody(pawn, rect);
        return false;
    }
}

[HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.DoInspectPaneButtons))]
public static class Patch_MainTabWindow_Inspect_DoInspectPaneButtons
{
    public static void Postfix(Rect rect, ref float lineEndWidth)
    {
        if (PawnReadout.CardPawn() is Pawn creature)
        {
            if (UiPlusMod.Settings.pawnPaneShowIdentity)
            {
                PawnReadoutDrawer.DrawIdentityIcons(creature, rect, ref lineEndWidth);
            }

            return;
        }

        if (PawnReadout.SelectedPawn() is not Pawn pawn)
        {
            return;
        }

        PawnReadoutDrawer.DrawNeedsViewButton(pawn, rect, ref lineEndWidth);
        if (UiPlusMod.Settings.pawnPaneShowSelfTend)
        {
            PawnReadoutDrawer.DrawSelfTendButton(pawn, rect, ref lineEndWidth);
        }

        if (UiPlusMod.Settings.pawnPaneShowRename)
        {
            PawnReadoutDrawer.DrawRenameButton(pawn, rect, ref lineEndWidth);
        }

        if (UiPlusMod.Settings.pawnPaneShowIdentity)
        {
            PawnReadoutDrawer.DrawIdentityIcons(pawn, rect, ref lineEndWidth);
        }
    }
}

// Width drives the pane, the tab row and the gizmo grid's left edge.
[HarmonyPatch(typeof(InspectPaneUtility), nameof(InspectPaneUtility.PaneWidthFor))]
public static class Patch_InspectPaneUtility_PaneWidthFor
{
    public static void Postfix(IInspectPane pane, ref float __result)
    {
        if (pane is MainTabWindow_Inspect && PawnReadout.SelectedPawn() != null)
        {
            __result = PawnReadout.PaneWidth(__result);
        }
    }
}

[HarmonyPatch(typeof(InspectPaneUtility), nameof(InspectPaneUtility.PaneSizeFor))]
public static class Patch_InspectPaneUtility_PaneSizeFor
{
    public static void Postfix(IInspectPane pane, ref Vector2 __result)
    {
        if (pane is MainTabWindow_Inspect && (PawnReadout.SelectedPawn() != null || PawnReadout.CardPawn() != null))
        {
            __result.y = PawnReadout.CurrentPaneHeight();
        }
    }
}

// The tab row and open tabs sit on PaneTopY, which the base game fixes at a 165px pane.
[HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.PaneTopY), MethodType.Getter)]
public static class Patch_MainTabWindow_Inspect_PaneTopY
{
    public static void Postfix(ref float __result)
    {
        __result -= PawnReadout.CurrentPaneHeight() - InspectPaneUtility.PaneHeight;
    }
}

// Keeps camera edge-scrolling from starting under the taller pane.
[HarmonyPatch(typeof(InspectPaneUtility), nameof(InspectPaneUtility.InspectPaneOnGUI))]
public static class Patch_InspectPaneUtility_InspectPaneOnGUI
{
    public static void Postfix(IInspectPane pane)
    {
        if (pane is MainTabWindow_Inspect && pane.RecentHeight < PawnReadout.CurrentPaneHeight())
        {
            pane.RecentHeight = PawnReadout.CurrentPaneHeight();
        }
    }
}

// The base game calls this when a game is unloaded; per-game caches are cleared with it.
[HarmonyPatch(typeof(Verse.Profile.MemoryUtility), nameof(Verse.Profile.MemoryUtility.ClearAllMapsAndWorld))]
public static class Patch_MemoryUtility_ClearAllMapsAndWorld
{
    public static void Postfix()
    {
        GrowingZoneFertility.Restore();
        PawnReadout.Forget();
        PawnReadoutDrawer.Forget();
        PawnReadoutDrawer.ForgetCreature();
        WildAnimalYields.Forget();
        AlertDrawer.Forget();
        Alert_HostilesPresent.Forget();
        WorldSearchHighlight.Forget();
        CharacterTabSize.Forget();
    }
}

// Gear and genes tabs cap their height with the base game's 165px pane; use the current pane height instead.
[HarmonyPatch]
public static class Patch_TabHeightCaps
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ITab_Pawn_Gear), "FillTab");
        yield return AccessTools.Method(typeof(GeneUIUtility), nameof(GeneUIUtility.DrawGenesInfo));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo paneHeight = AccessTools.Method(typeof(PawnReadout), nameof(PawnReadout.CurrentPaneHeight));
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == System.Reflection.Emit.OpCodes.Ldc_R4 && instruction.operand is float value && value == InspectPaneUtility.PaneHeight)
            {
                yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, paneHeight).MoveLabelsFrom(instruction);
            }
            else
            {
                yield return instruction;
            }
        }
    }
}
