using System;
using System.Collections.Generic;
using System.Text;
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
        ThingDef crop = zone.PlantDefToGrow;
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

    public sealed class Card
    {
        public string Caption = string.Empty;
        public string Value = string.Empty;
        public Color ValueColor = Color.white;
        public ThingDef? IconThing;
        public Texture2D? IconTexture;
        public Func<string> Tip = () => string.Empty;
        public Action? Click;
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
        public readonly bool GrowingNow;
        public readonly string StatusText;

        // Crop, growth and ready, then the growing period, fertility and what a full harvest gives.
        public readonly List<Card> Cards = new List<Card>();

        // Under the cards: blight while there is any.
        public readonly List<PawnReadout.HealthRow> Rows = new List<PawnReadout.HealthRow>();

        private readonly int averageAgeTicks;

        public Snapshot(Zone_Growing zone, ThingDef crop, int tick)
        {
            Zone = zone;
            Crop = crop;
            Tick = tick;
            RealTime = Time.realtimeSinceStartup;
            Map map = zone.Map;
            List<IntVec3> cells = zone.Cells;
            CellCount = cells.Count;
            Harvestable = crop.plant.Harvestable;

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
                fertilityFactorSum += PlantUtility.GrowthRateFactorFor_Fertility(crop, fertility);
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
            StatusText = GrowingNow ? "GrowSeasonHereNow".Translate().Resolve() : "CannotGrowBadSeasonTemperature".Translate().Resolve();
            float averageFertility = fertilitySum / CellCount;
            float averageFactor = fertilityFactorSum / CellCount;

            Cards.Add(new Card
            {
                IconThing = crop, Caption = crop.LabelCap, Value = Plants + "/" + CellCount, Tip = PlantedTip,
                Click = () => Find.WindowStack.Add(new Dialog_InfoCard(crop))
            });
            Cards.Add(new Card
            {
                IconTexture = PawnReadoutDrawer.GrowthTex, Caption = TextCache.Key("VUIP.ZonePaneGrowthShort"),
                Value = Plants > 0 ? AverageGrowth.ToStringPercent() : "-", ValueColor = Plants > 0 ? Color.white : PawnReadoutDrawer.MutedColor, Tip = GrowthTip
            });
            Cards.Add(new Card
            {
                IconTexture = PawnReadoutDrawer.HarvestTex, Caption = TextCache.Key("VUIP.ZonePaneReadyShort"), Value = Harvestable ? Ready.ToString() : "-",
                ValueColor = !Harvestable ? PawnReadoutDrawer.MutedColor : Ready > 0 ? PawnReadoutDrawer.GoodColor : Color.white, Tip = ReadyTip
            });
            Cards.Add(GrowingPeriodCard(map, cells[0], crop));
            Cards.Add(new Card
            {
                IconTexture = PawnReadoutDrawer.FertilityTex, Caption = "Fertility".Translate().Resolve(), Value = averageFertility.ToStringPercent(),
                ValueColor = averageFactor >= 0.99f ? Color.white : PawnReadoutDrawer.WarningTextColor,
                Tip = () => "Fertility".Translate().Resolve().AsTipTitle() + "\n\n"
                    + "VUIP.ZonePaneFertilityTip".Translate(averageFertility.ToStringPercent(), crop.label, averageFactor.ToStringPercent()).Resolve()
            });
            Cards.Add(HarvestCard(zone, crop));

            if (blighted > 0)
            {
                string label = "Blighted".Translate().Resolve();
                int count = blighted;
                Rows.Add(new PawnReadout.HealthRow(-1, label, count.ToString(), PawnReadout.HealthLevel.Critical,
                    () => label.AsTipTitle() + "\n\n" + "VUIP.ZonePaneBlightTip".Translate(count).Resolve(), Plants > 0 ? Mathf.Clamp01((float)count / Plants) : 1f));
            }
        }

        // The outdoor growing period as days of the year, from the same twelfths as the base game's text (Zone_Growing.GrowingQuadrumsDescription).
        private Card GrowingPeriodCard(Map map, IntVec3 cell, ThingDef crop)
        {
            string caption = "OutdoorGrowingPeriod".Translate().Resolve();
            Card card = new Card { IconTexture = PawnReadoutDrawer.SeasonTex, Caption = caption };
            float min = crop.plant.minOptimalGrowthTemperature;
            float max = crop.plant.maxOptimalGrowthTemperature;
            string range = "VUIP.ZonePaneGrowingPeriodTip".Translate(crop.label, min.ToStringTemperature(), max.ToStringTemperature()).Resolve()
                .Colorize(ColoredText.SubtleGrayColor);
            if (!cell.UsesOutdoorTemperature(map))
            {
                card.Value = TextCache.Key("VUIP.ZonePaneIndoors");
                card.ValueColor = PawnReadoutDrawer.MutedColor;
                card.Tip = () => caption.AsTipTitle() + "\n\n" + "VUIP.ZonePaneIndoorsTip".Translate().Resolve();
                return card;
            }

            string description = Zone_Growing.GrowingQuadrumsDescription(map, crop);
            card.Tip = () => caption.AsTipTitle() + "\n\n" + description + "\n\n" + range;
            if (map.IsPocketMap || !map.Tile.Valid)
            {
                card.Value = description;
                card.ValueColor = GrowingNow ? Color.white : ColorLibrary.RedReadable;
                return card;
            }

            int twelfths = GenTemperature.TwelfthsInAverageTemperatureRange(map.Tile, min, max).Count;
            if (twelfths >= 12)
            {
                card.Value = "GrowYearRound".Translate().Resolve();
                card.ValueColor = PawnReadoutDrawer.GoodColor;
            }
            else if (twelfths == 0)
            {
                card.Value = "NoGrowingPeriod".Translate().Resolve();
                card.ValueColor = ColorLibrary.RedReadable;
            }
            else
            {
                card.Value = "PeriodDays".Translate(twelfths * GenDate.DaysPerTwelfth + "/" + GenDate.DaysPerYear).Resolve();
                card.ValueColor = PawnReadoutDrawer.WarningTextColor;
            }

            return card;
        }

        // A full zone's harvest: people fed as simple meals for raw food, otherwise the harvested amount; worked out as Nice Plants Menu does.
        private Card HarvestCard(Zone_Growing zone, ThingDef crop)
        {
            ThingDef? product = crop.plant.harvestedThingDef;
            if (!Harvestable || product == null)
            {
                return new Card
                {
                    IconTexture = PawnReadoutDrawer.HarvestTex, Caption = TextCache.Key("VUIP.ZonePaneYieldShort"), Value = "-", ValueColor = PawnReadoutDrawer.MutedColor,
                    Tip = () => TextCache.Key("VUIP.ZonePaneYieldShort").AsTipTitle() + "\n\n" + "VUIP.ZonePaneNotHarvestable".Translate(crop.label).Resolve()
                };
            }

            CropYield.Estimate estimate = CropYield.For(zone, crop);
            float nutrition = product.GetStatValueAbstract(StatDefOf.Nutrition);
            StringBuilder tip = new StringBuilder();
            tip.AppendLine("VUIP.ZonePaneHarvestLine".Translate(estimate.Yield, product.label, estimate.Cells, estimate.Days > 0 ? estimate.Days.ToString() : "-").Resolve());
            if (estimate.IdealTemperature)
            {
                tip.AppendLine("VUIP.ZonePaneIdealTemperature".Translate(crop.label).Resolve());
            }

            if (estimate.ItemsPerMeal > 0 && CropYield.Meal is ThingDef meal)
            {
                string meals = Find.ActiveLanguageWorker.Pluralize(meal.label);
                tip.AppendLine("VUIP.ZonePaneNutritionLine".Translate((estimate.Yield * nutrition).ToString("0.#")).Resolve());
                tip.AppendLine("VUIP.ZonePaneMealsLine".Translate(estimate.MealsPerDay.ToString("0.#"), meals, estimate.ItemsPerMeal, product.label).Resolve());
                tip.AppendLine("VUIP.ZonePaneNeedLine".Translate(CropYield.MealsPerPerson, meals).Resolve());
                string feedsTitle = TextCache.Key("VUIP.ZonePaneFeedsShort");
                string people = estimate.People.ToString("0.#");
                string feedsText = "VUIP.ZonePaneFeedsTip".Translate(people, meals).Resolve() + "\n\n" + tip
                    + "\n" + TextCache.Key("VUIP.ZonePaneHarvestAssumptions").Colorize(ColoredText.SubtleGrayColor);
                return new Card
                {
                    IconThing = meal, Caption = feedsTitle, Value = estimate.Days > 0 ? "VUIP.ZonePaneFeedsValue".Translate(people).Resolve() : "-",
                    ValueColor = estimate.Days <= 0 ? PawnReadoutDrawer.MutedColor : estimate.People >= 1f ? Color.white : PawnReadoutDrawer.WarningTextColor,
                    Tip = () => feedsTitle.AsTipTitle() + "\n\n" + feedsText
                };
            }

            if (estimate.Days > 0)
            {
                tip.AppendLine("VUIP.ZonePanePerDayLine".Translate(((float)estimate.Yield / estimate.Days).ToString("0.#"), product.label).Resolve());
            }

            string yieldTitle = product.LabelCap;
            string yieldText = tip + "\n" + TextCache.Key("VUIP.ZonePaneHarvestAssumptions").Colorize(ColoredText.SubtleGrayColor);
            return new Card
            {
                IconThing = product, Caption = yieldTitle, Value = estimate.Yield.ToString(), Tip = () => yieldTitle.AsTipTitle() + "\n\n" + yieldText
            };
        }

        private string PlantedTip()
        {
            return Crop.LabelCap.Resolve().AsTipTitle() + "\n\n"
                + "VUIP.ZonePanePlantedTip".Translate(Plants, Find.ActiveLanguageWorker.Pluralize(Crop.label, Plants), CellCount).Resolve() + "\n\n"
                + "ClickForMoreInfo".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor);
        }

        private string GrowthTip()
        {
            string title = "VUIP.ZonePaneGrowthShort".Translate().Resolve().AsTipTitle() + "\n\n";
            if (Plants == 0)
            {
                return title + "VUIP.ZonePaneNoPlants".Translate(Crop.label).Resolve();
            }

            return title + "AveragePlantAge".Translate().CapitalizeFirst() + ": " + averageAgeTicks.ToStringTicksToPeriodVerbose()
                + " (" + "PercentGrowth".Translate(AverageGrowth.ToStringPercent()) + ")";
        }

        private string ReadyTip()
        {
            string title = "VUIP.ZonePaneReadyShort".Translate().Resolve().AsTipTitle() + "\n\n";
            return title + (Harvestable
                ? "VUIP.ZonePaneReadyTip".Translate(Ready, Crop.label, Crop.plant.harvestMinGrowth.ToStringPercent()).Resolve()
                : "VUIP.ZonePaneNotHarvestable".Translate(Crop.label).Resolve());
        }
    }
}
