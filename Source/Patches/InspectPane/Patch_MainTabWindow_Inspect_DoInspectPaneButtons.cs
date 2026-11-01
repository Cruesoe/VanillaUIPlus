using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

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
