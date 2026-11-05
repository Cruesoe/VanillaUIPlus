using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// What a full growing zone harvests and how many people it feeds as simple meals, from the crop, the simple meal recipe and a human's food need.
/// </summary>
public static class CropYield
{
    // Plants grow only between 25% and 80% of the day (Plant.Resting).
    public const float GrowingDayFraction = 0.55f;

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

    /// <summary>Days from sowing to full growth at the given growth rate, counting only the part of each day plants grow.</summary>
    public static float CycleDays(ThingDef crop, float growthRate)
    {
        return growthRate > 0f ? crop.plant.growDays / GrowingDayFraction / growthRate : float.PositiveInfinity;
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
