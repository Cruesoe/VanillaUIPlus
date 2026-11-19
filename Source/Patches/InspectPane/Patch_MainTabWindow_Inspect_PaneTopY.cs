using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

// The tab row and open tabs sit on PaneTopY, which the base game fixes at a 165px pane.
[HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.PaneTopY), MethodType.Getter)]
public static class Patch_MainTabWindow_Inspect_PaneTopY
{
    public static void Postfix(ref float __result)
    {
        // RimHUD's prefix already places the tabs on its own pane
        if (RimHudCompat.ModifiesPane)
        {
            return;
        }

        __result -= PawnReadout.CurrentPaneHeight() - InspectPaneUtility.PaneHeight;
    }
}
