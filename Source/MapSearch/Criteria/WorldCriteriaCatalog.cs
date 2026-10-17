using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VanillaUIPlus;

/// <summary>Every criterion offered in the palette, built once defs are loaded.</summary>
public static class WorldCriteriaCatalog
{
    private static List<WorldCriterion>? all;

    public static List<WorldCriterion> All => all ??= Build();

    private static List<WorldCriterion> Build()
    {
        List<WorldCriterion> list = new List<WorldCriterion>
        {
            new CriterionGroup(CriterionGroupMode.All),
            new CriterionGroup(CriterionGroupMode.Any),
            new CriterionGroup(CriterionGroupMode.None),
            new CriterionCanSettle(false),
            new CriterionCanSettle(true),
            new CriterionSettlement(),
            new CriterionSettlementDistance()
        };

        foreach (Hilliness hilliness in new[] { Hilliness.Flat, Hilliness.SmallHills, Hilliness.LargeHills, Hilliness.Mountainous, Hilliness.Impassable })
        {
            list.Add(new CriterionTerrain(hilliness));
        }

        list.Add(new CriterionLink(river: false));
        list.Add(new CriterionLink(river: true));
        list.Add(new CriterionNumber(TileStat.Movement));
        list.AddRange(CriterionDefSet<ThingDef>.ByLabel(DefDatabase<ThingDef>.AllDefs.Where(d => d.IsNonResourceNaturalRock)).Select(g => new CriterionStone(g)));
        list.Add(new CriterionNumber(TileStat.Elevation));
        list.AddRange(CriterionDefSet<LandmarkDef>.ByLabel(DefDatabase<LandmarkDef>.AllDefs).Select(g => new CriterionLandmark(g)));
        list.AddRange(CriterionDefSet<TileMutatorDef>.ByLabel(DefDatabase<TileMutatorDef>.AllDefs).Select(g => new CriterionFeature(g)));
        list.Add(new CriterionNumber(TileStat.Temperature));
        list.Add(new CriterionNumber(TileStat.GrowingPeriod));
        list.Add(new CriterionNumber(TileStat.Rainfall));
        list.Add(new CriterionNumber(TileStat.Forageability));
        list.Add(new CriterionNumber(TileStat.Pollution));
        list.Add(new CriterionNumber(TileStat.NearbyPollution));
        list.Add(new CriterionNumber(TileStat.DiseaseFrequency));
        list.Add(new CriterionNumber(TileStat.TimeZone));
        list.AddRange(CriterionDefSet<BiomeDef>.ByLabel(DefDatabase<BiomeDef>.AllDefs.Where(b => !b.isWaterBiome && b.implemented)).Select(g => new CriterionBiome(g)));
        list.Add(new CriterionNumber(TileStat.Latitude));
        list.Add(new CriterionNumber(TileStat.Longitude));
        return list;
    }
}
