using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// Applies only while no difficulty is chosen, so going Back then Next keeps the player's edits.
[HarmonyPatch(typeof(Page_SelectStoryteller), nameof(Page_SelectStoryteller.PreOpen))]
public static class Patch_Page_SelectStoryteller_PreOpen
{
    public static void Postfix(ref StorytellerDef ___storyteller, ref DifficultyDef ___difficulty, ref Difficulty ___difficultyValues)
    {
        StorytellerDefaults? defaults = UiPlusMod.Settings.storytellerDefaults;
        if (!NewGameDefaults.Enabled || defaults == null || ___difficulty != null)
        {
            return;
        }

        StorytellerDef? storyteller = defaults.storyteller != null ? DefDatabase<StorytellerDef>.GetNamedSilentFail(defaults.storyteller) : null;
        if (storyteller != null && storyteller.listVisible)
        {
            ___storyteller = storyteller;
        }

        DifficultyDef? difficulty = defaults.difficulty != null ? DefDatabase<DifficultyDef>.GetNamedSilentFail(defaults.difficulty) : null;
        if (difficulty != null)
        {
            ___difficulty = difficulty;
            ___difficultyValues = new Difficulty(difficulty);
            if (defaults.difficultySettings != null)
            {
                NewGameDefaults.RestoreDifficulty(___difficultyValues, defaults.difficultySettings);
            }

            AnomalyPlaystyleDef? playstyle = defaults.anomalyPlaystyle != null
                ? DefDatabase<AnomalyPlaystyleDef>.GetNamedSilentFail(defaults.anomalyPlaystyle)
                : null;
            if (playstyle != null)
            {
                ___difficultyValues.AnomalyPlaystyleDef = playstyle;
            }

            if (ModsConfig.AnomalyActive && Find.Scenario != null && Find.Scenario.standardAnomalyPlaystyleOnly)
            {
                ___difficultyValues.AnomalyPlaystyleDef = AnomalyPlaystyleDefOf.Standard;
            }
        }

        if (defaults.permadeath.HasValue && Find.GameInitData != null)
        {
            Find.GameInitData.permadeathChosen = true;
            Find.GameInitData.permadeath = defaults.permadeath.Value;
        }
    }
}

[HarmonyPatch(typeof(Page_SelectStoryteller), nameof(Page_SelectStoryteller.DoWindowContents))]
public static class Patch_Page_SelectStoryteller_DoWindowContents
{
    public static void Postfix(Rect rect, StorytellerDef ___storyteller, DifficultyDef ___difficulty, Difficulty ___difficultyValues)
    {
        if (!NewGameDefaults.Enabled)
        {
            return;
        }

        Rect button = NewGameDefaults.ButtonRect(rect);
        TooltipHandler.TipRegion(button, "VUIP.SetStorytellerDefaultTip".Translate());
        if (!Widgets.ButtonText(button, "VUIP.SetAsDefault".Translate()))
        {
            return;
        }

        if (___storyteller == null || ___difficulty == null)
        {
            Messages.Message("VUIP.ChooseDifficultyFirst".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        GameInitData? init = Find.GameInitData;
        UiPlusMod.Settings.storytellerDefaults = new StorytellerDefaults
        {
            storyteller = ___storyteller.defName,
            difficulty = ___difficulty.defName,
            difficultySettings = ___difficultyValues != null ? NewGameDefaults.CaptureDifficulty(___difficultyValues) : null,
            anomalyPlaystyle = ___difficultyValues?.AnomalyPlaystyleDef?.defName,
            permadeath = init != null && init.permadeathChosen ? init.permadeath : (bool?)null
        };
        NewGameDefaults.Saved();
    }
}

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

[HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.DoWindowContents))]
public static class Patch_Page_CreateWorldParams_DoWindowContents
{
    public static void Postfix(
        Rect rect,
        float ___planetCoverage,
        OverallRainfall ___rainfall,
        OverallTemperature ___temperature,
        OverallPopulation ___population,
        LandmarkDensity ___landmarkDensity,
        float ___pollution,
        List<FactionDef> ___factions)
    {
        if (!NewGameDefaults.Enabled)
        {
            return;
        }

        Rect button = NewGameDefaults.ButtonRect(rect);
        TooltipHandler.TipRegion(button, "VUIP.SetWorldDefaultTip".Translate());
        if (!Widgets.ButtonText(button, "VUIP.SetAsDefault".Translate()))
        {
            return;
        }

        GameInitData? init = Find.GameInitData;
        UiPlusMod.Settings.worldDefaults = new WorldDefaults
        {
            planetCoverage = ___planetCoverage,
            rainfall = ___rainfall,
            temperature = ___temperature,
            population = ___population,
            landmarkDensity = ___landmarkDensity,
            pollution = ___pollution,
            mapSize = init?.mapSize ?? 250,
            startingSeason = init?.startingSeason ?? Season.Undefined,
            factions = (___factions ?? new List<FactionDef>())
                .Where(f => f.displayInFactionSelection)
                .Select(f => f.defName)
                .ToList()
        };
        NewGameDefaults.Saved();
    }
}
