using HarmonyLib;
using RimWorld.Planet;

namespace VanillaUIPlus;

// The world pane and its tabs sit higher while a world object's cards make it taller.
[HarmonyPatch(typeof(WorldInspectPane), nameof(WorldInspectPane.PaneTopY), MethodType.Getter)]
public static class Patch_WorldInspectPane_PaneTopY
{
    public static void Postfix(ref float __result)
    {
        __result -= ObjectReadout.WorldPaneExtra();
    }
}
