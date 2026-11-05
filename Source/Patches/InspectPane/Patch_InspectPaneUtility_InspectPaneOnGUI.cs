using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace VanillaUIPlus;

// Keeps camera edge-scrolling from starting under the taller pane, and draws the pawn's name on the header's icon row.
[HarmonyPatch(typeof(InspectPaneUtility), nameof(InspectPaneUtility.InspectPaneOnGUI))]
public static class Patch_InspectPaneUtility_InspectPaneOnGUI
{
    public static void Prefix(IInspectPane pane)
    {
        PawnPaneName.Begin(pane);
    }

    public static void Postfix(Rect inRect, IInspectPane pane)
    {
        PawnPaneName.End(inRect);
        if (pane is MainTabWindow_Inspect && pane.RecentHeight < PawnReadout.CurrentPaneHeight())
        {
            pane.RecentHeight = PawnReadout.CurrentPaneHeight();
        }
    }
}
