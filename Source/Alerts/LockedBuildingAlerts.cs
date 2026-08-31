using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Vanilla's "need a research bench" alert assumes the bench is buildable from the start,
/// which it is in an unmodded game. Tech progression mods can put it behind research, and
/// the alert then nags for a building the colony has no way to make yet.
///
/// This hides that alert until at least one research bench has actually been unlocked. In
/// an unmodded game the simple research bench needs no research, so this never suppresses
/// anything and the alert behaves exactly as it always did.
/// </summary>
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
        // Any bench will do, including one added by a mod, so this looks at the thing
        // class rather than a specific def.
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
