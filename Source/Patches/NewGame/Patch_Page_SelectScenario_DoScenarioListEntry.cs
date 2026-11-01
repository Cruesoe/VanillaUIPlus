using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// A thin bar beside each scenario, colored by its starting faction's tech level.
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
