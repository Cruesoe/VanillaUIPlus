using System.Collections.Generic;
using System.Linq;
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

// Sorts the scenario list low-tech to high-tech; the page asks every frame, so each category's order is reused until its contents change.
[HarmonyPatch(typeof(ScenarioLister), nameof(ScenarioLister.ScenariosInCategory))]
public static class Patch_ScenarioLister_ScenariosInCategory
{
    private static readonly List<Scenario> Current = new List<Scenario>();
    private static readonly Dictionary<ScenarioCategory, (List<Scenario> Source, List<Scenario> Sorted)> Sorted =
        new Dictionary<ScenarioCategory, (List<Scenario>, List<Scenario>)>();

    public static void Postfix(ScenarioCategory cat, ref IEnumerable<Scenario> __result)
    {
        if (!UiPlusMod.Settings.sortScenarioListByTechLevel)
        {
            return;
        }

        Current.Clear();
        Current.AddRange(__result);
        if (Sorted.TryGetValue(cat, out (List<Scenario> Source, List<Scenario> Sorted) cached) && SameScenarios(cached.Source, Current))
        {
            __result = cached.Sorted;
            return;
        }

        List<Scenario> source = new List<Scenario>(Current);
        List<Scenario> sorted = source
            .OrderBy(NotTheBeginning)
            .ThenBy(ScenarioTechLevelUtility.TechLevelOf)
            .ToList();
        Sorted[cat] = (source, sorted);
        __result = sorted;
    }

    private static bool SameScenarios(List<Scenario> a, List<Scenario> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (!ReferenceEquals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    // Genesis's "The Beginning" always leads the list; it shares a faction with VFE Tribals' "Wild Men", so it's matched by name.
    private static bool NotTheBeginning(Scenario scen)
    {
        return scen.name != "The Beginning";
    }
}
