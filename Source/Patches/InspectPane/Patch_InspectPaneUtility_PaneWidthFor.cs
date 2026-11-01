using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

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
