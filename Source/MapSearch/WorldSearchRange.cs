using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>While a range ring is drawn (picking a destination), the world search can be limited to tiles inside it.</summary>
public static class WorldSearchRange
{
    public static bool LimitEnabled = true;

    private static PlanetTile ringCenter = PlanetTile.Invalid;
    private static int ringRadius = -1;
    private static int ringFrame = -100;
    private static PlanetTile builtCenter = PlanetTile.Invalid;
    private static int builtRadius = -1;
    private static readonly HashSet<PlanetTile> inRange = new HashSet<PlanetTile>();
    private static readonly List<WorldSearchElement> limited = new List<WorldSearchElement>();

    public static bool RingActive => ringCenter.Valid && Time.frameCount - ringFrame <= 2;

    public static bool ShouldLimit => LimitEnabled && RingActive;

    public static void NotifyRingDrawn(PlanetTile center, int radius)
    {
        ringCenter = center;
        ringRadius = radius;
        ringFrame = Time.frameCount;
    }

    public static List<WorldSearchElement> Filter(List<WorldSearchElement> all)
    {
        if (builtCenter != ringCenter || builtRadius != ringRadius)
        {
            builtCenter = ringCenter;
            builtRadius = ringRadius;
            inRange.Clear();
            int radius = ringRadius;
            ringCenter.Layer.Filler.FloodFill(ringCenter, _ => true, (Predicate<PlanetTile, int>)((tile, distance) =>
            {
                if (distance > radius)
                {
                    return true;
                }

                inRange.Add(tile);
                return false;
            }));
        }

        limited.Clear();
        foreach (WorldSearchElement element in all)
        {
            if (inRange.Contains(element.tile))
            {
                limited.Add(element);
            }
        }

        return limited;
    }
}
