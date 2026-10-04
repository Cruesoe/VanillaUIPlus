using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// One condition in an advanced world search. Defs are kept by defName, since saved searches load before defs exist.
/// </summary>
public abstract class WorldCriterion : IExposable
{
    public const float ControlRowHeight = 28f;

    public abstract string Label { get; }

    /// <summary>Why this criterion can't be used (a missing DLC), or null.</summary>
    public virtual string? LockedReason => null;

    public virtual float ControlsHeight => 0f;

    public virtual void DrawControls(Rect rect, ref bool changed)
    {
    }

    public abstract bool Matches(Tile tile);

    public virtual WorldCriterion Copy()
    {
        return (WorldCriterion)MemberwiseClone();
    }

    public virtual void ExposeData()
    {
    }

    protected static string Titled(string title, string name)
    {
        return title + ": " + name;
    }
}

public enum CriterionGroupMode
{
    All,
    Any,
    None
}

/// <summary>Combines other criteria. An empty group matches every tile.</summary>
public class CriterionGroup : WorldCriterion
{
    public CriterionGroupMode mode;
    public List<WorldCriterion> children = new List<WorldCriterion>();

    public CriterionGroup()
    {
    }

    public CriterionGroup(CriterionGroupMode mode)
    {
        this.mode = mode;
    }

    public override string Label => mode switch
    {
        CriterionGroupMode.Any => "VUIP.WorldQueryAnyOf".Translate(),
        CriterionGroupMode.None => "VUIP.WorldQueryNoneOf".Translate(),
        _ => "VUIP.WorldQueryAllOf".Translate()
    };

    public override bool Matches(Tile tile)
    {
        if (children.Count == 0)
        {
            return true;
        }

        switch (mode)
        {
            case CriterionGroupMode.Any:
                foreach (WorldCriterion child in children)
                {
                    if (child.Matches(tile))
                    {
                        return true;
                    }
                }

                return false;
            case CriterionGroupMode.None:
                foreach (WorldCriterion child in children)
                {
                    if (child.Matches(tile))
                    {
                        return false;
                    }
                }

                return true;
            default:
                foreach (WorldCriterion child in children)
                {
                    if (!child.Matches(tile))
                    {
                        return false;
                    }
                }

                return true;
        }
    }

    public bool Contains(WorldCriterion criterion)
    {
        foreach (WorldCriterion child in children)
        {
            if (child == criterion || (child is CriterionGroup group && group.Contains(criterion)))
            {
                return true;
            }
        }

        return false;
    }

    public CriterionGroup? ParentOf(WorldCriterion criterion)
    {
        foreach (WorldCriterion child in children)
        {
            if (child == criterion)
            {
                return this;
            }

            if (child is CriterionGroup group && group.ParentOf(criterion) is { } parent)
            {
                return parent;
            }
        }

        return null;
    }

    public override WorldCriterion Copy()
    {
        CriterionGroup copy = (CriterionGroup)MemberwiseClone();
        copy.children = children.Select(c => c.Copy()).ToList();
        return copy;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref mode, "mode");
        Scribe_Collections.Look(ref children, "children", LookMode.Deep);
        children ??= new List<WorldCriterion>();
        children.RemoveAll(c => c == null);
    }
}

public enum CompareMode
{
    AtMost,
    AtLeast,
    Exactly
}

/// <summary>A value with a comparison, drawn as a mode button, a slider and the formatted value.</summary>
public static class CompareControls
{
    private const float ModeWidth = 74f;
    private const float ValueWidth = 76f;

    public static bool Test(float actual, float target, CompareMode mode, float step)
    {
        return mode switch
        {
            CompareMode.AtMost => actual <= target + 0.0001f,
            CompareMode.AtLeast => actual >= target - 0.0001f,
            _ => Mathf.Abs(actual - target) < step / 2f
        };
    }

    public static void Draw(Rect rect, ref float value, ref CompareMode mode, float min, float max, float step, bool allowExactly,
        Func<float, string> format, ref bool changed)
    {
        Rect modeRect = new Rect(rect.x, rect.y + 2f, ModeWidth, rect.height - 4f);
        string modeKey = mode switch
        {
            CompareMode.AtMost => "VUIP.WorldQueryAtMost",
            CompareMode.AtLeast => "VUIP.WorldQueryAtLeast",
            _ => "VUIP.WorldQueryExactly"
        };
        TooltipHandler.TipRegion(modeRect, "VUIP.WorldQueryCompareTip".Translate());
        if (Widgets.ButtonText(modeRect, modeKey.Translate()))
        {
            mode = mode switch
            {
                CompareMode.AtMost => CompareMode.AtLeast,
                CompareMode.AtLeast => allowExactly ? CompareMode.Exactly : CompareMode.AtMost,
                _ => CompareMode.AtMost
            };
            changed = true;
        }

        Rect valueRect = new Rect(rect.xMax - ValueWidth, rect.y, ValueWidth, rect.height);
        Rect sliderRect = new Rect(modeRect.xMax + 6f, rect.y, valueRect.x - modeRect.xMax - 12f, rect.height);
        float newValue = Widgets.HorizontalSlider(sliderRect, value, min, max, middleAlignment: true, roundTo: step);
        newValue = Mathf.Clamp(Mathf.Round(newValue / step) * step, min, max);
        if (Mathf.Abs(newValue - value) > step / 4f)
        {
            value = newValue;
            changed = true;
        }

        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(valueRect, format(value).Truncate(valueRect.width));
        Text.Anchor = TextAnchor.UpperLeft;
    }
}

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

public enum FactionMatch
{
    Any,
    NotPlayer,
    NotHostile,
    Hostile,
    NotPermanentEnemy,
    PermanentEnemy,
    Specific
}

/// <summary>Picks which factions' settlements count. A specific faction missing from the current game counts as any.</summary>
public static class FactionFilter
{
    public static bool Matches(Faction? faction, FactionMatch match, int factionId)
    {
        if (faction == null)
        {
            return false;
        }

        Faction player = Faction.OfPlayer;
        return match switch
        {
            FactionMatch.NotPlayer => !faction.IsPlayer,
            FactionMatch.NotHostile => !faction.HostileTo(player),
            FactionMatch.Hostile => faction.HostileTo(player),
            FactionMatch.NotPermanentEnemy => !faction.def.PermanentlyHostileTo(player.def),
            FactionMatch.PermanentEnemy => faction.def.PermanentlyHostileTo(player.def),
            FactionMatch.Specific => Specific(factionId) is not { } specific || faction == specific,
            _ => true
        };
    }

    public static string Label(FactionMatch match, int factionId)
    {
        return match switch
        {
            FactionMatch.NotPlayer => "VUIP.WorldQueryFactionNotPlayer".Translate(),
            FactionMatch.NotHostile => "VUIP.WorldQueryFactionNotHostile".Translate(),
            FactionMatch.Hostile => "VUIP.WorldQueryFactionHostile".Translate(),
            FactionMatch.NotPermanentEnemy => "VUIP.WorldQueryFactionNotPermanentEnemy".Translate(),
            FactionMatch.PermanentEnemy => "VUIP.WorldQueryFactionPermanentEnemy".Translate(),
            FactionMatch.Specific when Specific(factionId) is { } faction => faction.Name,
            _ => "VUIP.WorldQueryFactionAny".Translate()
        };
    }

    public static void Draw(Rect rect, FactionMatch match, int factionId, Action<FactionMatch, int> set)
    {
        Rect button = new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 4f);
        if (!Widgets.ButtonText(button, Label(match, factionId).Truncate(button.width - 12f)))
        {
            return;
        }

        List<FloatMenuOption> options = new List<FloatMenuOption>();
        foreach (FactionMatch kind in Enum.GetValues(typeof(FactionMatch)))
        {
            if (kind != FactionMatch.Specific)
            {
                FactionMatch chosen = kind;
                options.Add(new FloatMenuOption(Label(kind, -1), () => set(chosen, -1)));
            }
        }

        if (Find.FactionManager != null)
        {
            foreach (Faction faction in Find.FactionManager.AllFactionsVisibleInViewOrder)
            {
                int id = faction.loadID;
                options.Add(new FloatMenuOption(faction.Name, () => set(FactionMatch.Specific, id), faction.def.FactionIcon, faction.Color));
            }
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static Faction? Specific(int factionId)
    {
        return factionId < 0 ? null : Find.FactionManager?.AllFactions.FirstOrDefault(f => f.loadID == factionId);
    }
}

public class CriterionSettlement : WorldCriterion
{
    public FactionMatch faction;
    public int factionId = -1;

    public override string Label => "VUIP.WorldQueryHasSettlement".Translate();

    public override float ControlsHeight => ControlRowHeight;

    public override void DrawControls(Rect rect, ref bool changed)
    {
        FactionFilter.Draw(rect, faction, factionId, (match, id) =>
        {
            faction = match;
            factionId = id;
            WorldQueryPanel.NotifyChanged();
        });
    }

    public override bool Matches(Tile tile)
    {
        return FactionFilter.Matches(Find.WorldObjects.SettlementAt(tile.tile)?.Faction, faction, factionId);
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref faction, "faction");
        Scribe_Values.Look(ref factionId, "factionId", -1);
    }
}

public class CriterionSettlementDistance : WorldCriterion
{
    private const float Step = 1f;

    public FactionMatch faction = FactionMatch.NotPlayer;
    public int factionId = -1;
    public float value = 5f;
    public CompareMode mode = CompareMode.AtLeast;

    public override string Label => "VUIP.WorldQuerySettlementDistance".Translate();

    public override float ControlsHeight => ControlRowHeight * 2f;

    public override void DrawControls(Rect rect, ref bool changed)
    {
        FactionFilter.Draw(rect.TopHalf(), faction, factionId, (match, id) =>
        {
            faction = match;
            factionId = id;
            WorldQueryPanel.NotifyChanged();
        });
        CompareControls.Draw(rect.BottomHalf(), ref value, ref mode, 1f, 50f, Step, allowExactly: false,
            v => "VUIP.WorldQueryTiles".Translate(v.ToString("F0")), ref changed);
    }

    public override bool Matches(Tile tile)
    {
        float nearest = float.MaxValue;
        PlanetLayer layer = tile.Layer;
        foreach (Settlement settlement in Find.WorldObjects.Settlements)
        {
            if (settlement.Tile.Layer == layer && FactionFilter.Matches(settlement.Faction, faction, factionId))
            {
                nearest = Mathf.Min(nearest, layer.ApproxDistanceInTiles(tile.tile, settlement.Tile));
            }
        }

        return CompareControls.Test(nearest, value, mode, Step);
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref faction, "faction", FactionMatch.NotPlayer);
        Scribe_Values.Look(ref factionId, "factionId", -1);
        Scribe_Values.Look(ref value, "value", 5f);
        Scribe_Values.Look(ref mode, "mode", CompareMode.AtLeast);
    }
}

public class CriterionCanSettle : WorldCriterion
{
    public bool gravship;

    public CriterionCanSettle()
    {
    }

    public CriterionCanSettle(bool gravship)
    {
        this.gravship = gravship;
    }

    public override string Label => (gravship ? "VUIP.WorldQueryCanLandGravship" : "VUIP.WorldQueryCanSettle").Translate();

    public override string? LockedReason => gravship && !ModsConfig.OdysseyActive ? "VUIP.WorldQueryRequiresOdyssey".Translate().ToString() : null;

    public override bool Matches(Tile tile)
    {
        return TileFinder.IsValidTileForNewSettlement(tile.tile, null, gravship);
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref gravship, "gravship");
    }
}

public class CriterionTerrain : WorldCriterion
{
    public Hilliness hilliness;

    public CriterionTerrain()
    {
    }

    public CriterionTerrain(Hilliness hilliness)
    {
        this.hilliness = hilliness;
    }

    public override string Label => Titled("Terrain".Translate(), hilliness.GetLabelCap());

    public override bool Matches(Tile tile)
    {
        return tile.HillinessLabel == hilliness;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref hilliness, "hilliness");
    }
}

public class CriterionLink : WorldCriterion
{
    public bool river;

    public CriterionLink()
    {
    }

    public CriterionLink(bool river)
    {
        this.river = river;
    }

    public override string Label => (river ? "River" : "Road").Translate();

    public override bool Matches(Tile tile)
    {
        if (tile is not SurfaceTile surface)
        {
            return false;
        }

        return river ? !surface.Rivers.NullOrEmpty() : !surface.Roads.NullOrEmpty();
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref river, "river");
    }
}

/// <summary>Matches any of a set of defs that share a label, so same-named defs from different mods count as one.</summary>
public abstract class CriterionDefSet<T> : WorldCriterion
    where T : Def
{
    public List<string> defNames = new List<string>();
    private List<T>? resolved;

    protected CriterionDefSet()
    {
    }

    protected CriterionDefSet(IEnumerable<T> defs)
    {
        defNames = defs.Select(d => d.defName).ToList();
    }

    protected abstract string Title { get; }

    protected List<T> Defs => resolved ??= defNames.Select(n => DefDatabase<T>.GetNamedSilentFail(n)).Where(d => d != null).ToList();

    public override string Label => Titled(Title, Defs.Count > 0 ? Defs[0].LabelCap.ToString() : defNames.FirstOrDefault() ?? "?");

    public override WorldCriterion Copy()
    {
        CriterionDefSet<T> copy = (CriterionDefSet<T>)MemberwiseClone();
        copy.defNames = new List<string>(defNames);
        return copy;
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref defNames, "defs", LookMode.Value);
        defNames ??= new List<string>();
        resolved = null;
    }

    public static IEnumerable<IGrouping<string, T>> ByLabel(IEnumerable<T> defs)
    {
        return defs.GroupBy(d => d.LabelCap.ToString()).OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);
    }
}

public class CriterionBiome : CriterionDefSet<BiomeDef>
{
    public CriterionBiome()
    {
    }

    public CriterionBiome(IEnumerable<BiomeDef> defs) : base(defs)
    {
    }

    protected override string Title => "Biome".Translate();

    public override bool Matches(Tile tile)
    {
        foreach (BiomeDef biome in tile.Biomes)
        {
            if (Defs.Contains(biome))
            {
                return true;
            }
        }

        return false;
    }
}

public class CriterionStone : CriterionDefSet<ThingDef>
{
    public CriterionStone()
    {
    }

    public CriterionStone(IEnumerable<ThingDef> defs) : base(defs)
    {
    }

    protected override string Title => "VUIP.WorldQueryStone".Translate();

    public override bool Matches(Tile tile)
    {
        foreach (ThingDef rock in Find.World.NaturalRockTypesIn(tile.tile))
        {
            if (Defs.Contains(rock))
            {
                return true;
            }
        }

        return false;
    }
}

public class CriterionLandmark : CriterionDefSet<LandmarkDef>
{
    public CriterionLandmark()
    {
    }

    public CriterionLandmark(IEnumerable<LandmarkDef> defs) : base(defs)
    {
    }

    protected override string Title => "VUIP.WorldQueryLandmark".Translate();

    public override bool Matches(Tile tile)
    {
        return tile.Landmark is { } landmark && Defs.Contains(landmark.def);
    }
}

/// <summary>A tile feature; an animal habitat can also be narrowed to one animal.</summary>
public class CriterionFeature : CriterionDefSet<TileMutatorDef>
{
    private static List<PawnKindDef>? habitatAnimals;

    public string? animal;

    public CriterionFeature()
    {
    }

    public CriterionFeature(IEnumerable<TileMutatorDef> defs) : base(defs)
    {
    }

    protected override string Title => "VUIP.WorldQueryFeature".Translate();

    private bool IsHabitat => Defs.Any(d => d.Worker is TileMutatorWorker_AnimalHabitat);

    public override float ControlsHeight => IsHabitat ? ControlRowHeight : 0f;

    private static List<PawnKindDef> HabitatAnimals => habitatAnimals ??= DefDatabase<PawnKindDef>.AllDefs
        .Where(k => k.RaceProps is { Animal: true } && DefDatabase<BiomeDef>.AllDefs.Any(b => b.CommonalityOfAnimal(k) > 0f))
        .OrderBy(k => k.label)
        .ToList();

    public override void DrawControls(Rect rect, ref bool changed)
    {
        PawnKindDef? current = animal == null ? null : DefDatabase<PawnKindDef>.GetNamedSilentFail(animal);
        Rect button = new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 4f);
        string label = current?.LabelCap ?? "VUIP.WorldQueryAnyAnimal".Translate();
        if (!Widgets.ButtonText(button, label.Truncate(button.width - 12f)))
        {
            return;
        }

        List<FloatMenuOption> options = new List<FloatMenuOption>
        {
            new FloatMenuOption("VUIP.WorldQueryAnyAnimal".Translate(), () => SetAnimal(null))
        };
        foreach (PawnKindDef kind in HabitatAnimals)
        {
            string defName = kind.defName;
            options.Add(new FloatMenuOption(kind.LabelCap, () => SetAnimal(defName)));
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    private void SetAnimal(string? defName)
    {
        animal = defName;
        WorldQueryPanel.NotifyChanged();
    }

    public override bool Matches(Tile tile)
    {
        foreach (TileMutatorDef mutator in tile.Mutators)
        {
            if (!Defs.Contains(mutator))
            {
                continue;
            }

            if (animal == null || mutator.Worker is not TileMutatorWorker_AnimalHabitat habitat)
            {
                return true;
            }

            if (habitat.GetAnimalKind(tile.tile)?.defName == animal)
            {
                return true;
            }
        }

        return false;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref animal, "animal");
    }
}

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
