using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// A growing zone's pane is only its inspect text under the name; the readout is drawn in its place.
[HarmonyPatch(typeof(InspectPaneFiller), nameof(InspectPaneFiller.DrawInspectStringFor))]
public static class Patch_InspectPaneFiller_DrawInspectStringFor_GrowingZone
{
    public static bool Prefix(ISelectable sel, Rect rect)
    {
        if (sel is not Zone_Growing zone || GrowingZoneReadout.SelectedZone() != zone)
        {
            return true;
        }

        PawnReadoutDrawer.DrawGrowingZoneBody(zone, rect);
        return false;
    }
}
