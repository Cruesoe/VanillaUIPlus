using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

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
