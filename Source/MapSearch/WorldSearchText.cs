using System;
using RimWorld;
using RimWorld.Planet;
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
            if (Contains(TextCache.Key("Road"), filter))
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
            if (Contains(TextCache.Key("River"), filter))
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
