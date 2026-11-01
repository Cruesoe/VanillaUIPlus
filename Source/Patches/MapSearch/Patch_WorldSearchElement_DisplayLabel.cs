using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

// Plain tiles have no place, landmark or feature to name them, so they show their biome.
[HarmonyPatch(typeof(WorldSearchElement), nameof(WorldSearchElement.DisplayLabel), MethodType.Getter)]
public static class Patch_WorldSearchElement_DisplayLabel
{
    public static void Postfix(WorldSearchElement __instance, ref string? __result)
    {
        if (__result == null && __instance.tile.Valid)
        {
            __result = __instance.tile.Tile.PrimaryBiome.LabelCap;
        }
    }
}
