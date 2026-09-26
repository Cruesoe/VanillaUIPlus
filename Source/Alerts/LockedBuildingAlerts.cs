using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>Hides the "need a research bench" alert until at least one research bench is unlocked.</summary>
public static class LockedBuildingAlerts
{
    private static List<ThingDef>? researchBenches;

    public static bool ShouldHide(Alert alert)
    {
        if (alert is not Alert_NeedResearchBench)
        {
            return false;
        }

        return UiPlusMod.Settings.hideLockedResearchBenchAlert && !AnyResearchBenchUnlocked();
    }

    private static bool AnyResearchBenchUnlocked()
    {
        // Any def whose thing class is a research bench, including modded ones.
        if (researchBenches == null)
        {
            researchBenches = new List<ThingDef>();
            List<ThingDef> all = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                ThingDef def = all[i];
                if (def.thingClass != null && typeof(Building_ResearchBench).IsAssignableFrom(def.thingClass))
                {
                    researchBenches.Add(def);
                }
            }
        }

        // Nothing recognisable to unlock: stay out of the way and let vanilla decide.
        if (researchBenches.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < researchBenches.Count; i++)
        {
            if (researchBenches[i].IsResearchFinished)
            {
                return true;
            }
        }

        return false;
    }
}
