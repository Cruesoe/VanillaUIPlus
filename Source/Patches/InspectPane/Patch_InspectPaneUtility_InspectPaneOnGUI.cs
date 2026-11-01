using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

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
