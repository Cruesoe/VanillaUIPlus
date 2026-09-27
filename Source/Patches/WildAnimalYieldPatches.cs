using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// Adds butchering and shearing yields and the revenge chance to a wild animal's inspect text.
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
public static class Patch_Pawn_GetInspectString_WildYields
{
    public static void Postfix(Pawn __instance, ref string __result)
    {
        if (!UiPlusMod.Settings.showWildAnimalYields || __instance.Dead || !__instance.RaceProps.Animal || __instance.Faction != null)
        {
            return;
        }

        string yields = WildAnimalYields.Cached(__instance);
        if (yields.Length > 0)
        {
            __result = __result.NullOrEmpty() ? yields : __result.TrimEndNewlines() + "\n" + yields;
        }
    }
}

public static class WildAnimalYields
{
    // The inspect text is rebuilt every frame for the selected animal; the yields are recalculated this often instead.
    private const float RefreshSeconds = 2f;

    private static Pawn? cachedPawn;
    private static float cachedAt;
    private static string cachedText = string.Empty;

    public static string Cached(Pawn pawn)
    {
        float now = Time.realtimeSinceStartup;
        if (pawn != cachedPawn || now - cachedAt >= RefreshSeconds || now < cachedAt)
        {
            cachedPawn = pawn;
            cachedAt = now;
            cachedText = Describe(pawn);
        }

        return cachedText;
    }

    // Drops the cached animal so an unloaded game isn't kept in memory.
    public static void Forget()
    {
        cachedPawn = null;
        cachedText = string.Empty;
    }

    // Meat and leather use the same stats as Pawn.ButcherProducts, before the butcher's efficiency.
    public static string Describe(Pawn pawn)
    {
        StringBuilder sb = new StringBuilder();
        RaceProperties race = pawn.RaceProps;
        string? meat = Amount(pawn, race.meatDef, StatDefOf.MeatAmount);
        string? leather = Amount(pawn, race.leatherDef, StatDefOf.LeatherAmount);
        if (meat != null || leather != null)
        {
            string products = meat != null && leather != null ? meat + ", " + leather : meat ?? leather!;
            sb.Append("VUIP.WildYieldButchering".Translate(products));
        }

        CompProperties_Shearable? shearable = pawn.def.GetCompProperties<CompProperties_Shearable>();
        if (shearable?.woolDef != null && shearable.woolAmount > 0)
        {
            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            sb.Append("VUIP.WildYieldShearing".Translate(shearable.woolAmount, shearable.woolDef.label, shearable.shearIntervalDays));
        }

        // Same figure as the Wildlife tab's revenge column: the race's chance, the difficulty factor and scaria, before distance or the attacker's stealth.
        if (sb.Length > 0)
        {
            sb.Append('\n');
        }

        sb.Append("HarmedRevengeChance".Translate()).Append(": ").Append(PawnUtility.GetManhunterOnDamageChance(pawn).ToStringPercent());
        return sb.ToString();
    }

    private static string? Amount(Pawn pawn, ThingDef? def, StatDef stat)
    {
        if (def == null)
        {
            return null;
        }

        int amount = Mathf.RoundToInt(pawn.GetStatValue(stat));
        return amount > 0 ? amount + " " + def.label : null;
    }
}
