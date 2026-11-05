using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Growing zone inspect pane readout: which zone gets it, the pane size, and a cached snapshot of its crop.
/// </summary>
public static class GrowingZoneReadout
{
    private const int RefreshTicks = 60;
    private const float RefreshSeconds = 1f;

    private static Snapshot? snapshot;
    private static int selectedFrame = -1;
    private static Zone_Growing? selectedZone;

    /// <summary>The single selected growing zone when it gets the readout, otherwise null; worked out once per frame.</summary>
    public static Zone_Growing? SelectedZone()
    {
        int frame = Time.frameCount;
        if (frame != selectedFrame)
        {
            selectedFrame = frame;
            selectedZone = UiPlusMod.Settings.growingZonePaneEnabled && Find.Selector is { NumSelected: 1 } selector
                && selector.SelectedZone is Zone_Growing zone && zone.Map != null && zone.Cells.Count > 0 ? zone : null;
        }

        return selectedZone;
    }

    public static float PaneHeight(Zone_Growing zone)
    {
        return Mathf.Max(InspectPaneUtility.PaneHeight, PawnReadoutDrawer.GrowingZoneBodyHeight(For(zone)) + PawnReadoutDrawer.PaneChromeHeight);
    }

    public static Snapshot For(Zone_Growing zone)
    {
        Snapshot? current = snapshot;
        int ticks = Find.TickManager?.TicksGame ?? 0;
        ThingDef crop = zone.GetPlantDefToGrow();
        if (current == null || current.Zone != zone || current.Crop != crop || current.CellCount != zone.Cells.Count
            || ticks - current.Tick >= RefreshTicks || ticks < current.Tick || Time.realtimeSinceStartup - current.RealTime >= RefreshSeconds)
        {
            current = new Snapshot(zone, crop, ticks);
            snapshot = current;
        }

        return current;
    }

    // Drops the cached zone and snapshot so an unloaded game isn't kept in memory.
    public static void Forget()
    {
        snapshot = null;
        selectedZone = null;
        selectedFrame = -1;
    }

    public class Snapshot
    {
        public readonly Zone_Growing Zone;
        public readonly ThingDef Crop;
        public readonly int Tick;
        public readonly float RealTime;
        public readonly int CellCount;
        public readonly int Plants;
        public readonly int Ready;
        public readonly bool Harvestable;
        public readonly float AverageGrowth;
        public readonly float MaxGrowth;
        public readonly bool GrowingNow;
        public readonly string CropLabel;
        public readonly string PlantedText;
        public readonly string GrowthText;
        public readonly string ReadyText;
        public readonly string StatusText;

        // Rows under the cards: the outdoor growing period, the oldest plant, blight and fertility.
        public readonly List<PawnReadout.HealthRow> Rows = new List<PawnReadout.HealthRow>();

        private readonly int averageAgeTicks;
        private readonly int oldestAgeTicks;

        public Snapshot(Zone_Growing zone, ThingDef crop, int tick)
        {
            Zone = zone;
            Crop = crop;
            Tick = tick;
            RealTime = Time.realtimeSinceStartup;
            Map map = zone.Map;
            List<IntVec3> cells = zone.Cells;
            CellCount = cells.Count;
            Harvestable = crop.plant != null && crop.plant.Harvestable;

            // Same plant counts and ages as the base game's zone text (Zone_Growing.ContentsStatistics), plus readiness, blight and fertility.
            float growthSum = 0f;
            long ageSum = 0;
            int blighted = 0;
            float fertilitySum = 0f;
            float fertilityFactorSum = 0f;
            for (int i = 0; i < cells.Count; i++)
            {
                float fertility = map.fertilityGrid.FertilityAt(cells[i]);
                fertilitySum += fertility;
                if (crop.plant != null)
                {
                    fertilityFactorSum += PlantUtility.GrowthRateFactorFor_Fertility(crop, fertility);
                }

                List<Thing> things = cells[i].GetThingList(map);
                for (int j = 0; j < things.Count; j++)
                {
                    if (things[j] is not Plant plant)
                    {
                        continue;
                    }

                    if (plant.Blighted)
                    {
                        blighted++;
                    }

                    if (plant.def != crop)
                    {
                        continue;
                    }

                    Plants++;
                    growthSum += plant.Growth;
                    ageSum += plant.Age;
                    MaxGrowth = Mathf.Max(MaxGrowth, plant.Growth);
                    oldestAgeTicks = Mathf.Max(oldestAgeTicks, plant.Age);
                    if (plant.HarvestableNow)
                    {
                        Ready++;
                    }
                }
            }

            if (Plants > 0)
            {
                AverageGrowth = growthSum / Plants;
                averageAgeTicks = (int)(ageSum / Plants);
            }

            GrowingNow = PlantUtility.GrowthSeasonNow(cells[0], map, crop);
            CropLabel = crop.LabelCap;
            PlantedText = Plants + "/" + CellCount;
            GrowthText = Plants > 0 ? AverageGrowth.ToStringPercent() : "-";
            ReadyText = Harvestable ? Ready.ToString() : "-";
            StatusText = GrowingNow ? "GrowSeasonHereNow".Translate().Resolve() : "CannotGrowBadSeasonTemperature".Translate().Resolve();

            if (cells[0].UsesOutdoorTemperature(map))
            {
                string period = Zone_Growing.GrowingQuadrumsDescription(map, crop);
                // The same twelfths the base game's period text counts; a pocket map has no tile, so its level follows the season now.
                List<Twelfth>? twelfths = map.IsPocketMap || !map.Tile.Valid ? null : GenTemperature.TwelfthsInAverageTemperatureRange(map.Tile,
                    crop.plant?.minOptimalGrowthTemperature ?? 6f, crop.plant?.maxOptimalGrowthTemperature ?? 42f);
                int growingTwelfths = twelfths?.Count ?? (GrowingNow ? 12 : 0);
                PawnReadout.HealthLevel level = growingTwelfths >= 12 ? PawnReadout.HealthLevel.Good
                    : growingTwelfths == 0 ? PawnReadout.HealthLevel.Critical
                    : PawnReadout.HealthLevel.Warning;
                string label = "OutdoorGrowingPeriod".Translate().Resolve();
                Rows.Add(new PawnReadout.HealthRow(-1, label, period, level,
                    () => label.AsTipTitle() + "\n\n" + period + "\n\n" + "VUIP.ZonePaneGrowingPeriodTip".Translate(crop.label,
                        (crop.plant?.minOptimalGrowthTemperature ?? 6f).ToStringTemperature(), (crop.plant?.maxOptimalGrowthTemperature ?? 42f).ToStringTemperature()).Resolve()
                        .Colorize(ColoredText.SubtleGrayColor),
                    twelfths != null ? twelfths.Count / 12f : null));
            }

            if (Plants > 0)
            {
                string label = "OldestPlantAge".Translate().CapitalizeFirst().Resolve();
                string age = oldestAgeTicks.ToStringTicksToPeriodVerbose();
                float max = MaxGrowth;
                Rows.Add(new PawnReadout.HealthRow(-1, label, MaxGrowth.ToStringPercent(),
                    Harvestable && Ready > 0 ? PawnReadout.HealthLevel.Good : PawnReadout.HealthLevel.Warning,
                    () => label.AsTipTitle() + "\n\n" + age + " (" + "PercentGrowth".Translate(max.ToStringPercent()).Resolve() + ")", MaxGrowth));
            }

            if (blighted > 0)
            {
                string label = "Blighted".Translate().Resolve();
                int count = blighted;
                Rows.Add(new PawnReadout.HealthRow(-1, label, count.ToString(), PawnReadout.HealthLevel.Critical,
                    () => label.AsTipTitle() + "\n\n" + "VUIP.ZonePaneBlightTip".Translate(count).Resolve(), Plants > 0 ? Mathf.Clamp01((float)count / Plants) : 1f));
            }

            float averageFertility = fertilitySum / CellCount;
            float averageFactor = crop.plant != null ? fertilityFactorSum / CellCount : 1f;
            string fertilityLabel = "Fertility".Translate().Resolve();
            Rows.Add(new PawnReadout.HealthRow(-1, fertilityLabel, averageFertility.ToStringPercent(),
                averageFactor >= 0.99f ? PawnReadout.HealthLevel.Good : PawnReadout.HealthLevel.Warning,
                () => fertilityLabel.AsTipTitle() + "\n\n" + "VUIP.ZonePaneFertilityTip".Translate(averageFertility.ToStringPercent(), crop.label,
                    averageFactor.ToStringPercent()).Resolve()));
        }

        public string PlantedTip()
        {
            return CropLabel.AsTipTitle() + "\n\n"
                + "VUIP.ZonePanePlantedTip".Translate(Plants, Find.ActiveLanguageWorker.Pluralize(Crop.label, Plants), CellCount).Resolve() + "\n\n"
                + "ClickForMoreInfo".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor);
        }

        public string GrowthTip()
        {
            string title = "VUIP.ZonePaneGrowthShort".Translate().Resolve().AsTipTitle() + "\n\n";
            if (Plants == 0)
            {
                return title + "VUIP.ZonePaneNoPlants".Translate(Crop.label).Resolve();
            }

            return title + "AveragePlantAge".Translate().CapitalizeFirst() + ": " + averageAgeTicks.ToStringTicksToPeriodVerbose()
                + " (" + "PercentGrowth".Translate(AverageGrowth.ToStringPercent()) + ")\n"
                + "OldestPlantAge".Translate().CapitalizeFirst() + ": " + oldestAgeTicks.ToStringTicksToPeriodVerbose()
                + " (" + "PercentGrowth".Translate(MaxGrowth.ToStringPercent()) + ")";
        }

        public string ReadyTip()
        {
            string title = "VUIP.ZonePaneReadyShort".Translate().Resolve().AsTipTitle() + "\n\n";
            return title + (Harvestable
                ? "VUIP.ZonePaneReadyTip".Translate(Ready, Crop.label, Crop.plant!.harvestMinGrowth.ToStringPercent()).Resolve()
                : "VUIP.ZonePaneNotHarvestable".Translate(Crop.label).Resolve());
        }
    }
}
