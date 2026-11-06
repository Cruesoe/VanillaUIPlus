using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// What a growing zone harvests and how many people it feeds as simple meals, worked out the same way as Nice Plants Menu.
/// </summary>
public static class CropYield
{
    // Ticks of growth in a full day, and the share of a grown plant's growth a cycle is counted to.
    private const int GrowingTicksPerDay = 32500;
    private const float CycleGrowth = 1.1f;
    private const int MaxDays = 360;

    // Simple meals a person eats a day, and the step people fed are rounded to.
    public const float MealsPerPerson = 2f;
    private const float PeopleStep = 0.5f;

    // Plants that die of cold do so below this temperature.
    private const float LeaflessTemperature = -3f;

    private static readonly Func<GameConditionManager, float> TemperatureOffset =
        AccessTools.MethodDelegate<Func<GameConditionManager, float>>(AccessTools.Method(typeof(GameConditionManager), "AggregateTemperatureOffset"));

    // Resolved once per session: defs don't change after loading.
    private static bool resolved;
    private static RecipeDef? mealRecipe;

    /// <summary>The simple meal the people fed figure assumes, or null if its recipe can't be found.</summary>
    public static ThingDef? Meal
    {
        get
        {
            Resolve();
            return mealRecipe?.products[0].thingDef;
        }
    }

    private static void Resolve()
    {
        if (resolved)
        {
            return;
        }

        resolved = true;
        RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail("CookMealSimple");
        if (recipe != null && recipe.ingredients.Count > 0 && recipe.products.Count == 1 && recipe.products[0].thingDef != null)
        {
            mealRecipe = recipe;
        }
    }

    /// <summary>The estimate for a full zone of the crop.</summary>
    public struct Estimate
    {
        public int Cells;
        public int Yield;
        public int Days;
        public bool IdealTemperature;
        public int ItemsPerMeal;
        public float MealsPerDay;
        public float People;
    }

    public static Estimate For(Zone_Growing zone, ThingDef crop)
    {
        Map map = zone.Map;
        List<IntVec3> cells = zone.Cells;
        Estimate estimate = new Estimate();
        float fertilitySum = 0f;
        int roofed = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            bool isRoofed = map.roofGrid.Roofed(cells[i]);
            if (isRoofed)
            {
                roofed++;
            }

            float fertility = map.fertilityGrid.FertilityAt(cells[i]);
            if ((fertility >= crop.plant.fertilityMin || crop.plant.completelyIgnoreFertility) && (!crop.plant.diesToLight || isRoofed))
            {
                estimate.Cells++;
                fertilitySum += fertility;
            }
        }

        estimate.Yield = Mathf.RoundToInt(PerPlant(crop) * estimate.Cells);
        if (estimate.Cells == 0)
        {
            return estimate;
        }

        bool covered = (float)roofed / cells.Count > 0.75f || DarkBiome(map);
        Room room = zone.Position.GetRoom(map);
        bool greenhouse = room != null && !room.UsesOutdoorTemperature;
        float fertilityFactor = PlantUtility.GrowthRateFactorFor_Fertility(crop, fertilitySum / estimate.Cells);
        estimate.Days = DaysToHarvest(map, crop, fertilityFactor, covered, greenhouse);
        if (estimate.Days < 0 && !greenhouse)
        {
            estimate.Days = DaysToHarvest(map, crop, fertilityFactor, covered, true);
            estimate.IdealTemperature = true;
        }

        ThingDef? product = crop.plant.harvestedThingDef;
        if (estimate.Days > 0 && product != null)
        {
            estimate.ItemsPerMeal = ItemsPerMeal(product);
            if (estimate.ItemsPerMeal > 0)
            {
                estimate.MealsPerDay = Mathf.Floor((float)estimate.Yield / estimate.ItemsPerMeal) / estimate.Days;
                estimate.People = Mathf.Round(estimate.MealsPerDay / MealsPerPerson / PeopleStep) * PeopleStep;
            }
        }

        return estimate;
    }

    /// <summary>Items one plant gives when harvested, rounded up as Nice Plants Menu does.</summary>
    public static int PerPlant(ThingDef crop)
    {
        float yield = crop.plant.harvestYield;
        return Mathf.CeilToInt(crop.plant.harvestYieldAffectedByDifficulty ? yield * Find.Storyteller.difficulty.cropYieldFactor : yield);
    }

    /// <summary>Items of the product one simple meal takes, or 0 when the recipe doesn't take it.</summary>
    public static int ItemsPerMeal(ThingDef product)
    {
        Resolve();
        if (mealRecipe == null)
        {
            return 0;
        }

        IngredientCount? ingredient = null;
        for (int i = 0; i < mealRecipe.ingredients.Count; i++)
        {
            IngredientCount candidate = mealRecipe.ingredients[i];
            if (candidate.IsFixedIngredient ? candidate.FixedIngredient == product : candidate.filter.Allows(product))
            {
                ingredient = candidate;
                break;
            }
        }

        if (ingredient == null)
        {
            return 0;
        }

        float value = mealRecipe.IngredientValueGetter.ValuePerUnitOf(product);
        return value > 0f ? Mathf.Min(Mathf.CeilToInt(ingredient.GetBaseCount() / value), product.stackLimit) : 0;
    }

    private static bool DarkBiome(Map map)
    {
        if (!ModsConfig.OdysseyActive)
        {
            return false;
        }

        List<GameConditionDef> conditions = map.Biome.biomeMapConditions;
        for (int i = 0; i < conditions.Count; i++)
        {
            if (conditions[i].conditionClass == typeof(GameCondition_NoSunlight_Instant))
            {
                return true;
            }
        }

        return false;
    }

    // Whole days from sowing today until the crop reaches CycleGrowth, a day at a time at the day's mean temperature; -1 if it won't.
    private static int DaysToHarvest(Map map, ThingDef crop, float fertilityFactor, bool covered, bool greenhouse)
    {
        if (fertilityFactor == 0f || (!covered && map.gameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout)))
        {
            return -1;
        }

        PlanetTile tile = map.Tile;
        float longitude = tile.Valid ? Find.WorldGrid.LongLatOf(tile).x : 0f;
        float absLongitude = Math.Abs(longitude);
        int shift = (int)(30000f * (absLongitude > 90f ? 90f / absLongitude : absLongitude / 90f));
        int sunrise = 15000 + shift;
        int sunset = 47500 + shift;
        int hour = GenDate.HourOfDay(Find.TickManager.TicksAbs, longitude);
        int today = GenDate.DayOfYear(Find.TickManager.TicksAbs, longitude);
        float sunLow = SunEffect(today, sunrise, longitude);
        float sunHigh = SunEffect(today, sunset, longitude);
        float baseTemperature = tile.Valid && Find.WorldGrid[tile] is Tile worldTile ? worldTile.temperature : 21f;
        float offset = TemperatureOffset(map.gameConditionManager);

        int target = (int)(crop.plant.growDays * GenDate.TicksPerDay * CycleGrowth);
        int lifespan = (int)crop.plant.LifespanDays;
        int growth = 0;
        int days = 0;
        while (growth < target)
        {
            int ticks = covered || crop.plant.diesToLight ? GrowingTicksPerDay : days == 0 ? RemainingLightTicks(hour) : sunset - sunrise;
            int gained = (int)(ticks * fertilityFactor);
            int day = days + today;
            float morning = baseTemperature + SeasonOffset(day * GenDate.TicksPerDay + sunrise, tile) + sunLow + offset;
            float evening = baseTemperature + SeasonOffset(day * GenDate.TicksPerDay + sunset, tile) + sunHigh + offset;
            if (!greenhouse)
            {
                if (crop.plant.dieIfLeafless && Math.Min(morning, evening) < LeaflessTemperature)
                {
                    return -1;
                }

                gained = (int)(gained * PlantUtility.GrowthRateFactorFor_Temperature(crop, (morning + evening) / 2f));
            }

            growth += gained;
            if (++days > MaxDays || lifespan < days)
            {
                return -1;
            }
        }

        return days;
    }

    private static float SeasonOffset(int absTick, PlanetTile tile)
    {
        return tile.Valid ? GenTemperature.OffsetFromSeasonCycle(absTick, tile) : 0f;
    }

    private static float SunEffect(int day, int timeOfDay, float longitude)
    {
        return Mathf.Cos(2f * Mathf.PI * (GenDate.DayPercent(day * GenDate.TicksPerDay + timeOfDay, longitude) + 0.32f)) * 7f;
    }

    // Growing ticks left today, by the hour.
    private static int RemainingLightTicks(int hour)
    {
        int hours = 14 - hour;
        if (hours < 1)
        {
            hours = hours < -3 ? 13 : 0;
        }

        return 2500 * hours;
    }
}
