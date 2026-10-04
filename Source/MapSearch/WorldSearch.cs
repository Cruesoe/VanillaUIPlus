using System;
using System.Collections;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>Which tile details a world text search looks in, beyond vanilla's places, landmarks and features.</summary>
public static class WorldSearchText
{
    public static bool Matches(WorldSearchElement element, string filter)
    {
        UiPlusSettings s = UiPlusMod.Settings;
        if (s.worldSearchPlaces && element.worldObject != null && Contains(element.worldObject.Label, filter))
        {
            return true;
        }

        if (s.worldSearchLandmarks && element.landmark != null
            && (Contains(element.landmark.name, filter) || Contains(element.landmark.def.label, filter)))
        {
            return true;
        }

        if (s.worldSearchFeatures && !element.mutators.NullOrEmpty())
        {
            foreach (TileMutatorDef mutator in element.mutators)
            {
                if (Contains(mutator.label, filter) || Contains(mutator.Label(element.tile), filter))
                {
                    return true;
                }
            }
        }

        if (!element.tile.Valid)
        {
            return false;
        }

        Tile tile = element.tile.Tile;
        if (s.worldSearchBiomes)
        {
            foreach (BiomeDef biome in tile.Biomes)
            {
                if (Contains(biome.label, filter))
                {
                    return true;
                }
            }
        }

        if (s.worldSearchTerrain && tile.HillinessLabel != Hilliness.Undefined && Contains(tile.HillinessLabel.GetLabel(), filter))
        {
            return true;
        }

        if (s.worldSearchRoadsRivers && tile is SurfaceTile surface && RoadOrRiverMatches(surface, filter))
        {
            return true;
        }

        if (s.worldSearchStone && tile.PrimaryBiome.canBuildBase)
        {
            foreach (ThingDef rock in Find.World.NaturalRockTypesIn(element.tile))
            {
                if (Contains(rock.label, filter))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool RoadOrRiverMatches(SurfaceTile surface, string filter)
    {
        if (!surface.Roads.NullOrEmpty())
        {
            if (Contains("Road".Translate(), filter))
            {
                return true;
            }

            foreach (SurfaceTile.RoadLink link in surface.Roads)
            {
                if (Contains(link.road.label, filter))
                {
                    return true;
                }
            }
        }

        if (!surface.Rivers.NullOrEmpty())
        {
            if (Contains("River".Translate(), filter))
            {
                return true;
            }

            foreach (SurfaceTile.RiverLink link in surface.Rivers)
            {
                if (Contains(link.river.label, filter))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Same test as vanilla's Dialog_Search.TextMatch.
    private static bool Contains(string? text, string filter)
    {
        return !text.NullOrEmpty() && text!.IndexOf(filter, StringComparison.InvariantCultureIgnoreCase) >= 0;
    }

    public static void ResetSources()
    {
        UiPlusSettings s = UiPlusMod.Settings;
        s.worldSearchPlaces = true;
        s.worldSearchLandmarks = true;
        s.worldSearchFeatures = true;
        s.worldSearchBiomes = true;
        s.worldSearchTerrain = false;
        s.worldSearchRoadsRivers = true;
        s.worldSearchStone = true;
    }
}

/// <summary>Adds every land tile on the surface to the world search, labelled by biome.</summary>
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

/// <summary>The tiles of the open world search's results, for the highlight layer.</summary>
public static class WorldSearchHighlight
{
    private static Dialog_WorldSearch? dialog;
    private static int resultCount = -1;
    private static readonly List<PlanetTile> tiles = new List<PlanetTile>();

    public static int Version { get; private set; }

    public static List<PlanetTile> Tiles => tiles;

    public static bool Showing => UiPlusMod.Settings.worldSearchHighlight && dialog != null && tiles.Count > 0 && Find.WindowStack.IsOpen(dialog);

    public static void Update(Dialog_WorldSearch current, SortedList<string, WorldSearchElement> results)
    {
        if (current == dialog && results.Count == resultCount)
        {
            return;
        }

        dialog = current;
        resultCount = results.Count;
        tiles.Clear();
        foreach (WorldSearchElement element in results.Values)
        {
            if (element != null && element.tile.Valid)
            {
                tiles.Add(element.tile);
            }
        }

        Version++;
    }
}

/// <summary>Tints the tiles of the open world search's results. Added to the surface layer by a patch.</summary>
public class WorldDrawLayer_SearchHighlight : WorldDrawLayer
{
    private const int RebuildInterval = 15;
    private static Material? material;
    private readonly List<Vector3> verts = new List<Vector3>();
    private int drawnVersion = -1;
    private int builtFrame = -100;

    private static Material Material => material ??= MaterialPool.MatFrom(new MaterialRequest(BaseContent.WhiteTex,
        ShaderDatabase.WorldOverlayTransparent, new Color(0.3f, 0.85f, 1f, 0.35f)) { renderQueue = 3560 });

    public override bool Visible => base.Visible && WorldSearchHighlight.Showing;

    public override bool VisibleInBackground => false;

    public override bool ShouldRegenerate => base.ShouldRegenerate
        || (WorldSearchHighlight.Version != drawnVersion && Time.frameCount - builtFrame >= RebuildInterval);

    public override IEnumerable Regenerate()
    {
        foreach (object item in base.Regenerate())
        {
            yield return item;
        }

        drawnVersion = WorldSearchHighlight.Version;
        builtFrame = Time.frameCount;
        foreach (PlanetTile tile in WorldSearchHighlight.Tiles)
        {
            if (tile.Layer != planetLayer)
            {
                continue;
            }

            LayerSubMesh subMesh = GetSubMesh(Material);
            Find.WorldGrid.GetTileVertices(tile, verts);
            int start = subMesh.verts.Count;
            for (int i = 0; i < verts.Count; i++)
            {
                subMesh.verts.Add(verts[i] + verts[i].normalized * 0.015f);
                subMesh.uvs.Add((GenGeo.RegularPolygonVertexPosition(verts.Count, i) + Vector2.one) / 2f);
                if (i < verts.Count - 2)
                {
                    subMesh.tris.Add(start + i + 2);
                    subMesh.tris.Add(start + i + 1);
                    subMesh.tris.Add(start);
                }
            }
        }

        FinalizeMesh(MeshParts.All);
    }
}
