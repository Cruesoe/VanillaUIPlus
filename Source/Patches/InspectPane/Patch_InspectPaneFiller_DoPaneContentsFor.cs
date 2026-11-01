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
