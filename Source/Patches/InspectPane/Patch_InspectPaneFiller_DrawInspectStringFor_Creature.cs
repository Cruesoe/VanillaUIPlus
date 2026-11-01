using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

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
