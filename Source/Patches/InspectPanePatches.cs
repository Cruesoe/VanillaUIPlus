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

[HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.DoInspectPaneButtons))]
public static class Patch_MainTabWindow_Inspect_DoInspectPaneButtons
{
    public static void Postfix(Rect rect, ref float lineEndWidth)
    {
        if (PawnReadout.SelectedPawn() is Pawn pawn && UiPlusMod.Settings.pawnPaneShowSelfTend)
        {
            PawnReadoutDrawer.DrawSelfTendButton(pawn, rect, ref lineEndWidth);
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
        if (pane is MainTabWindow_Inspect && PawnReadout.SelectedPawn() != null)
        {
            __result.y = PawnReadout.PaneHeight;
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
