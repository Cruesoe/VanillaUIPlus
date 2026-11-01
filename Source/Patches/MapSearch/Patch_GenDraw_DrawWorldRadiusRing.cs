using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawWorldRadiusRing))]
public static class Patch_GenDraw_DrawWorldRadiusRing
{
    public static void Prefix(PlanetTile center, int radius)
    {
        WorldSearchRange.NotifyRingDrawn(center, radius);
    }
}
