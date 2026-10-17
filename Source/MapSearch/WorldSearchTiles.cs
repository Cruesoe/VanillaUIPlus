using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>Adds every land tile on the surface to the world search, labeled by biome.</summary>
public static class WorldSearchTiles
{
    public static void AddLandTiles(List<WorldSearchElement> searchSet)
    {
        HashSet<PlanetTile> listed = new HashSet<PlanetTile>();
        foreach (WorldSearchElement element in searchSet)
        {
            listed.Add(element.tile);
        }

        SurfaceLayer surface = Find.WorldGrid.Surface;
        for (int i = 0; i < surface.TilesCount; i++)
        {
            Tile tile = surface[i];
            if (tile.PrimaryBiome.isWaterBiome || listed.Contains(tile.tile))
            {
                continue;
            }

            searchSet.Add(new WorldSearchElement
            {
                tile = tile.tile,
                mutators = tile.mutatorsNullable
            });
        }
    }

    public static bool IsPlainTile(WorldSearchElement element)
    {
        return element.worldObject == null && element.landmark == null;
    }

    /// <summary>A plain tile's label with its coordinates, or null for places and landmarks.</summary>
    public static string? CoordinateLabel(WorldSearchElement element)
    {
        if (!IsPlainTile(element) || !element.tile.Valid)
        {
            return null;
        }

        Vector2 longLat = Find.WorldGrid.LongLatOf(element.tile);
        return element.DisplayLabel + " (" + FormatLatitude(longLat.y) + ", " + FormatLongitude(longLat.x) + ")";
    }

    public static string FormatLatitude(float degrees)
    {
        return Mathf.Abs(degrees).ToString("F0") + "°" + (degrees < 0f ? "VUIP.WorldQuerySouth" : "VUIP.WorldQueryNorth").Translate();
    }

    public static string FormatLongitude(float degrees)
    {
        return Mathf.Abs(degrees).ToString("F0") + "°" + (degrees < 0f ? "VUIP.WorldQueryWest" : "VUIP.WorldQueryEast").Translate();
    }
}
