using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// What a full growing zone harvests and how many people it feeds as simple meals, from the crop, the simple meal recipe and a human's food need.
/// </summary>
public static class CropYield
{
    // Plants grow every 2000 ticks (Plant.TickLong), only between 25% and 80% of the day (Plant.Resting).
    private const int StepTicks = 2000;

    public const int MaxCycleDays = 120;

    // Resolved once per session: defs don't change after loading.
    private static bool resolved;
    private static RecipeDef? mealRecipe;
    private static IngredientCount? mealIngredient;
    private static float mealNutrition;
    private static float dailyNeed;

    /// <summary>The simple meal the people fed figure assumes, or null if its recipe can't be found.</summary>
    public static ThingDef? Meal
    {
        get
        {
            Resolve();
            return mealIngredient != null ? mealRecipe!.products[0].thingDef : null;
        }
    }

    /// <summary>Nutrition a human adult eats in a day (Need_Food.BaseHungerRate).</summary>
    public static float DailyNeed
    {
        get
        {
            Resolve();
            return dailyNeed;
        }
    }

    public static float MealNutrition
    {
        get
        {
            Resolve();
            return mealNutrition;
        }
    }

    private static void Resolve()
    {
        if (resolved)
        {
            return;
        }

        resolved = true;
        mealRecipe = DefDatabase<RecipeDef>.GetNamedSilentFail("CookMealSimple");
        if (mealRecipe != null && mealRecipe.ingredients.Count == 1 && mealRecipe.products.Count == 1 && mealRecipe.products[0].thingDef != null)
        {
            mealIngredient = mealRecipe.ingredients[0];
            mealNutrition = mealRecipe.products[0].thingDef.GetStatValueAbstract(StatDefOf.Nutrition) * mealRecipe.products[0].count;
        }

        LifeStageDef adult = ThingDefOf.Human.race.lifeStageAges[ThingDefOf.Human.race.lifeStageAges.Count - 1].def;
        dailyNeed = Need_Food.BaseHungerRate(adult, ThingDefOf.Human) * GenDate.TicksPerDay;
    }

    /// <summary>Items one plant gives when harvested at full growth (Plant.YieldNow), before the harvester's chance to fail.</summary>
    public static float PerPlant(ThingDef crop)
    {
        float yield = crop.plant.harvestYield;
        return crop.plant.harvestYieldAffectedByDifficulty ? yield * Find.Storyteller.difficulty.cropYieldFactor : yield;
    }

    /// <summary>
    /// Days from sowing now to full growth, stepping as plants tick (Plant.TickLong) through the growing part of each day.
    /// Outdoors it uses the seasonal and daily temperature and, under open sky, the sun's light; indoors the cell's current temperature and full light.
    /// Infinity when the crop won't mature within <see cref="MaxCycleDays"/>.
    /// </summary>
    public static float CycleDays(ThingDef crop, float fertilityFactor, Map map, IntVec3 cell)
    {
        if (fertilityFactor <= 0f || crop.plant.growDays <= 0f)
        {
            return float.PositiveInfinity;
        }

        PlanetTile tile = map.Tile;
        bool seasonal = tile.Valid && !map.IsPocketMap && cell.UsesOutdoorTemperature(map);
        bool sunlit = seasonal && !cell.Roofed(map);
        float fixedTemperature = cell.GetTemperature(map);
        float longitude = tile.Valid ? Find.WorldGrid.LongLatOf(tile).x : 0f;
        float perStep = StepTicks / (GenDate.TicksPerDay * crop.plant.growDays) * fertilityFactor;
        int start = Find.TickManager.TicksAbs;
        float growth = 0f;
        for (int step = 1; step <= MaxCycleDays * GenDate.TicksPerDay / StepTicks; step++)
        {
            int tick = start + step * StepTicks;
            float dayPercent = GenDate.DayPercent(tick, longitude);
            if (dayPercent < 0.25f || dayPercent > 0.8f)
            {
                continue;
            }

            float temperature = seasonal ? GenTemperature.GetTemperatureFromSeasonAtTile(tick, tile) + GenTemperature.OffsetFromSunCycle(tick, tile) : fixedTemperature;
            if (temperature <= crop.plant.minGrowthTemperature || temperature >= crop.plant.maxGrowthTemperature)
            {
                continue;
            }

            float rate = PlantUtility.GrowthRateFactorFor_Temperature(crop, temperature);
            if (sunlit)
            {
                rate *= PlantUtility.GrowthRateFactorFor_Light(crop, GenCelestial.CelestialSunGlow(tile, tick));
            }

            growth += perStep * rate;
            if (growth >= 1f)
            {
                return (float)step * StepTicks / GenDate.TicksPerDay;
            }
        }

        return float.PositiveInfinity;
    }

    /// <summary>Simple meals one unit of the product makes, or 0 when the recipe doesn't take it.</summary>
    public static float MealsPerUnit(ThingDef product)
    {
        Resolve();
        if (mealIngredient == null || !mealIngredient.filter.Allows(product))
        {
            return 0f;
        }

        float perMeal = mealIngredient.GetBaseCount();
        return perMeal > 0f ? mealRecipe!.IngredientValueGetter.ValuePerUnitOf(product) / perMeal : 0f;
    }
}
