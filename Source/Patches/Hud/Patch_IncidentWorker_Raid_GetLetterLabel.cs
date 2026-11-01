using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

// Adds "(Preparing)" or "(Immediate)" to hostile raid letter titles, from the raid's strategy.
[HarmonyPatch]
public static class Patch_IncidentWorker_Raid_GetLetterLabel
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (Type type in GenTypes.AllSubclassesNonAbstract(typeof(IncidentWorker_Raid)))
        {
            if (AccessTools.DeclaredMethod(type, "GetLetterLabel", new[] { typeof(IncidentParms) }) is MethodInfo method)
            {
                yield return method;
            }
        }
    }

    public static void Postfix(IncidentParms __0, ref string __result)
    {
        if (!UiPlusMod.Settings.raidLetterStrategy || __0?.raidStrategy == null || __0.faction == null
            || !__0.faction.HostileTo(Faction.OfPlayer) || __result.NullOrEmpty())
        {
            return;
        }

        string suffix = " (" + (Prepares(__0.raidStrategy.Worker)
            ? TextCache.Key("VUIP.RaidLetterPreparing") : TextCache.Key("VUIP.RaidLetterImmediate")) + ")";
        if (!__result.EndsWith(suffix))
        {
            __result += suffix;
        }
    }

    // Raiders who gather before attacking: staging and sieges (including the psychic ritual siege).
    private static bool Prepares(RaidStrategyWorker worker)
    {
        return worker is RaidStrategyWorker_StageThenAttack or RaidStrategyWorker_Siege or RaidStrategyWorker_PsychicRitualSiege;
    }
}
