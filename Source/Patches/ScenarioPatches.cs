using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public static class ScenarioTechLevelUtility
{
    private static readonly FieldInfo PlayerFactionField = AccessTools.Field(typeof(Scenario), "playerFaction");
    private static readonly FieldInfo FactionDefField = AccessTools.Field(typeof(ScenPart_PlayerFaction), "factionDef");

    public static FactionDef? FactionDefOf(Scenario scen)
    {
        var playerFaction = PlayerFactionField.GetValue(scen) as ScenPart_PlayerFaction;
        return playerFaction != null ? FactionDefField.GetValue(playerFaction) as FactionDef : null;
    }

    public static TechLevel TechLevelOf(Scenario scen) => FactionDefOf(scen)?.techLevel ?? TechLevel.Undefined;

    // Matches the palette used by Semi Random Research P-Fork Continued's research tree tech-level coloring.
    public static Color ColorFor(TechLevel techLevel)
    {
        switch (techLevel)
        {
            case TechLevel.Animal:
                return new Color(0.5f, 0.4f, 0.2f);
            case TechLevel.Neolithic:
                return new Color(0.6f, 0.35f, 0.35f);
            case TechLevel.Medieval:
                return new Color(0.6f, 0.6f, 0.3f);
            case TechLevel.Industrial:
                return new Color(0.4f, 0.6f, 0.3f);
            case TechLevel.Spacer:
                return new Color(0.3f, 0.5f, 0.6f);
            case TechLevel.Ultra:
                return new Color(0.45f, 0.35f, 0.6f);
            case TechLevel.Archotech:
                return new Color(0.6f, 0.35f, 0.6f);
            default:
                return TexUI.AvailResearchColor;
        }
    }
}

// Colors a thin bar next to each entry in the scenario picker by the tech level of that
// scenario's starting faction, so the list reads at a glance instead of requiring every
// label to be opened.
[HarmonyPatch]
public static class Patch_Page_SelectScenario_DoScenarioListEntry
{
    private const float BarWidth = 4f;
    private const float Gap = 6f;

    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(Page_SelectScenario), "DoScenarioListEntry", new[] { typeof(Rect), typeof(Scenario) });
    }

    public static void Prefix(ref Rect rect)
    {
        if (!UiPlusMod.Settings.colorScenarioListByTechLevel)
        {
            return;
        }

        rect.x += BarWidth + Gap;
        rect.width -= BarWidth + Gap;
    }

    public static void Postfix(Rect rect, Scenario scen)
    {
        if (!UiPlusMod.Settings.colorScenarioListByTechLevel)
        {
            return;
        }

        var color = ScenarioTechLevelUtility.ColorFor(ScenarioTechLevelUtility.TechLevelOf(scen));
        var barRect = new Rect(rect.x - BarWidth - Gap, rect.y, BarWidth, rect.height);
        Widgets.DrawBoxSolid(barRect, color);
    }
}

// Sorts the scenario list low-tech to high-tech instead of vanilla's arbitrary order.
[HarmonyPatch(typeof(ScenarioLister), nameof(ScenarioLister.ScenariosInCategory))]
public static class Patch_ScenarioLister_ScenariosInCategory
{
    private static readonly FieldInfo NameField = AccessTools.Field(typeof(Scenario), "name");

    public static void Postfix(ref IEnumerable<Scenario> __result)
    {
        if (!UiPlusMod.Settings.sortScenarioListByTechLevel)
        {
            return;
        }

        __result = __result
            .OrderBy(NotTheBeginning)
            .ThenBy(ScenarioTechLevelUtility.TechLevelOf)
            .ToList();
    }

    // "The Beginning" (added by the Genesis mod) shares its faction (VFET_WildMen) with
    // VFE - Tribals' own "Wild Men" scenario, so a tech-level or faction-based sort key
    // would tie between them. It should always lead the list, so it's singled out by its
    // own scenario label instead. Harmless when Genesis isn't installed: the name simply
    // never matches, so nothing is singled out.
    private static bool NotTheBeginning(Scenario scen)
    {
        return (NameField.GetValue(scen) as string) != "The Beginning";
    }
}
