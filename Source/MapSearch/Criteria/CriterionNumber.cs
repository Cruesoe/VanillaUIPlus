using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public enum TileStat
{
    Temperature,
    GrowingPeriod,
    Rainfall,
    Elevation,
    Movement,
    Forageability,
    Pollution,
    NearbyPollution,
    DiseaseFrequency,
    TimeZone,
    Latitude,
    Longitude
}

/// <summary>A numeric tile value compared against a threshold; values match the world map's Terrain tab.</summary>
public class CriterionNumber : WorldCriterion
{
    public TileStat stat;
    public float value;
    public CompareMode mode;

    private sealed class Spec
    {
        public readonly string Label;
        public readonly float Initial;
        public readonly float Min;
        public readonly float Max;
        public readonly float Step;
        public readonly CompareMode Mode;
        public readonly bool AllowExactly;
        public readonly bool Biotech;
        public readonly Func<Tile, float> Value;
        public readonly Func<float, string> Format;

        public Spec(string label, float initial, float min, float max, float step, CompareMode mode, Func<Tile, float> value,
            Func<float, string> format, bool allowExactly = false, bool biotech = false)
        {
            Label = label;
            Initial = initial;
            Min = min;
            Max = max;
            Step = step;
            Mode = mode;
            Value = value;
            Format = format;
            AllowExactly = allowExactly;
            Biotech = biotech;
        }
    }

    private static Dictionary<TileStat, Spec>? specs;

    private static Dictionary<TileStat, Spec> Specs => specs ??= new Dictionary<TileStat, Spec>
    {
        [TileStat.Temperature] = new Spec("AvgTemp".Translate(), 10f, -60f, 60f, 1f, CompareMode.AtLeast,
            t => t.temperature, v => v.ToStringTemperature("F0")),
        [TileStat.GrowingPeriod] = new Spec("OutdoorGrowingPeriod".Translate(), 30f, 0f, 60f, 5f, CompareMode.AtLeast,
            t => GenTemperature.TwelfthsInAverageTemperatureRange(t.tile, 6f, 42f).Count * 5f, FormatGrowingDays),
        [TileStat.Rainfall] = new Spec("Rainfall".Translate(), 600f, 0f, 3000f, 50f, CompareMode.AtLeast,
            t => t.rainfall, v => v.ToString("F0") + "mm"),
        [TileStat.Elevation] = new Spec("Elevation".Translate(), 500f, 0f, 3000f, 50f, CompareMode.AtMost,
            t => t.elevation, v => v.ToString("F0") + "m"),
        [TileStat.Movement] = new Spec("MovementDifficulty".Translate(), 1.5f, 0.5f, 5f, 0.1f, CompareMode.AtMost,
            MovementDifficulty, v => v.ToString("0.#")),
        [TileStat.Forageability] = new Spec("Forageability".Translate(), 0.5f, 0f, 1f, 0.05f, CompareMode.AtLeast,
            t => t.PrimaryBiome.foragedFood != null && t.PrimaryBiome.forageability > 0f ? t.PrimaryBiome.forageability : 0f, v => v.ToStringPercent()),
        [TileStat.Pollution] = new Spec("VUIP.WorldQueryPollution".Translate(), 0f, 0f, 1f, 0.05f, CompareMode.AtMost,
            t => t.pollution, v => v.ToStringPercent(), biotech: true),
        [TileStat.NearbyPollution] = new Spec("VUIP.WorldQueryNearbyPollution".Translate(), 0f, 0f, 40f, 0.5f, CompareMode.AtMost,
            t => WorldPollutionUtility.CalculateNearbyPollutionScore(t.tile), v => v.ToString("0.##"), biotech: true),
        [TileStat.DiseaseFrequency] = new Spec("AverageDiseaseFrequency".Translate(), 1f, 0f, 5f, 0.1f, CompareMode.AtMost,
            t => 60f / t.PrimaryBiome.diseaseMtbDays, v => v.ToString("F1") + " " + "PerYear".Translate()),
        [TileStat.TimeZone] = new Spec("TimeZone".Translate(), 0f, -12f, 12f, 1f, CompareMode.Exactly,
            t => GenDate.TimeZoneAt(Find.WorldGrid.LongLatOf(t.tile).x), v => Mathf.RoundToInt(v).ToStringWithSign(), allowExactly: true),
        [TileStat.Latitude] = new Spec("VUIP.WorldQueryLatitude".Translate(), 0f, -90f, 90f, 5f, CompareMode.AtMost,
            t => Find.WorldGrid.LongLatOf(t.tile).y, WorldSearchTiles.FormatLatitude),
        [TileStat.Longitude] = new Spec("VUIP.WorldQueryLongitude".Translate(), 0f, -180f, 180f, 10f, CompareMode.AtMost,
            t => Find.WorldGrid.LongLatOf(t.tile).x, WorldSearchTiles.FormatLongitude)
    };

    public CriterionNumber()
    {
    }

    public CriterionNumber(TileStat stat)
    {
        this.stat = stat;
        Spec spec = Specs[stat];
        value = spec.Initial;
        mode = spec.Mode;
    }

    private Spec StatSpec => Specs[stat];

    public override string Label => StatSpec.Label;

    public override string? LockedReason => StatSpec.Biotech && !ModsConfig.BiotechActive ? "VUIP.WorldQueryRequiresBiotech".Translate().ToString() : null;

    public override float ControlsHeight => ControlRowHeight;

    public override void DrawControls(Rect rect, ref bool changed)
    {
        Spec spec = StatSpec;
        CompareControls.Draw(rect, ref value, ref mode, spec.Min, spec.Max, spec.Step, spec.AllowExactly, spec.Format, ref changed);
    }

    public override bool Matches(Tile tile)
    {
        Spec spec = StatSpec;
        return CompareControls.Test(spec.Value(tile), value, mode, spec.Step);
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref stat, "stat");
        Scribe_Values.Look(ref value, "value");
        Scribe_Values.Look(ref mode, "mode");
    }

    private static float MovementDifficulty(Tile tile)
    {
        if (Find.World.Impassable(tile.tile))
        {
            return float.MaxValue;
        }

        return WorldPathGrid.CalculatedMovementDifficultyAt(tile.tile, false, null, null)
            * Find.WorldGrid.GetRoadMovementDifficultyMultiplier(tile.tile, PlanetTile.Invalid, null);
    }

    private static string FormatGrowingDays(float days)
    {
        int rounded = Mathf.RoundToInt(days);
        if (rounded <= 0)
        {
            return "NoGrowingPeriod".Translate();
        }

        return rounded >= 60 ? "GrowYearRound".Translate() : "PeriodDays".Translate(rounded + "/60");
    }
}
