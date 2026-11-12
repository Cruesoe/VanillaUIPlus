using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// Buildings, items and stockpiles with cards keep the base game's bar row; the cards and the inspect text they leave are drawn in place of its text.
[HarmonyPatch(typeof(InspectPaneFiller), nameof(InspectPaneFiller.DrawInspectStringFor))]
public static class Patch_InspectPaneFiller_DrawInspectStringFor_Object
{
    public static bool Prefix(ISelectable sel, Rect rect)
    {
        if (sel is Pawn or Zone_Growing || ObjectReadout.Selected() != sel)
        {
            return true;
        }

        ObjectReadout.Draw(sel, rect);
        return false;
    }
}
