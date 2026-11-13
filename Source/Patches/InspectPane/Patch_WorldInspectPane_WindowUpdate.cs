using HarmonyLib;
using RimWorld.Planet;

namespace VanillaUIPlus;

// The base game sizes the world pane once when it opens; this keeps its height and top in step with the cards.
[HarmonyPatch(typeof(WorldInspectPane), nameof(WorldInspectPane.WindowUpdate))]
public static class Patch_WorldInspectPane_WindowUpdate
{
    public static void Postfix(WorldInspectPane __instance)
    {
        float height = ObjectReadout.WorldPaneBaseHeight + ObjectReadout.WorldPaneExtra();
        if (__instance.windowRect.height != height)
        {
            __instance.windowRect.height = height;
            __instance.windowRect.y = __instance.PaneTopY;
        }

        if (__instance.RecentHeight < height)
        {
            __instance.RecentHeight = height;
        }
    }
}
