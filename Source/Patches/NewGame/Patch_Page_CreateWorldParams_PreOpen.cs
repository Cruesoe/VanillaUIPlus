using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VanillaUIPlus;

// Applies only on the page's first open, so Back then Next keeps edits and "Reset all" still restores the game's values.
[HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.PreOpen))]
public static class Patch_Page_CreateWorldParams_PreOpen
{
    public static void Prefix(bool ___initialized, ref bool __state)
    {
        __state = !___initialized;
    }

    public static void Postfix(
        bool __state,
        ref float ___planetCoverage,
        ref OverallRainfall ___rainfall,
        ref OverallTemperature ___temperature,
        ref OverallPopulation ___population,
        ref LandmarkDensity ___landmarkDensity,
        ref float ___pollution,
        List<FactionDef> ___factions,
        List<FactionDef> ___initialFactions)
    {
        WorldDefaults? defaults = UiPlusMod.Settings.worldDefaults;
        if (!__state || !NewGameDefaults.Enabled || defaults == null)
        {
            return;
        }

        ___planetCoverage = defaults.planetCoverage;
        ___rainfall = defaults.rainfall;
        ___temperature = defaults.temperature;
        ___population = defaults.population;
        if (ModsConfig.OdysseyActive)
        {
            ___landmarkDensity = defaults.landmarkDensity;
        }

        if (ModsConfig.BiotechActive)
        {
            ___pollution = defaults.pollution;
        }

        if (Find.GameInitData != null)
        {
            Find.GameInitData.mapSize = defaults.mapSize;
            Find.GameInitData.startingSeason = defaults.startingSeason;
        }

        if (___factions != null && ___initialFactions != null)
        {
            NewGameDefaults.ApplyFactions(___factions, ___initialFactions);
        }
    }
}
