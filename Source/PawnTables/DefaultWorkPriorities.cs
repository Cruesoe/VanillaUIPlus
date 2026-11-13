using System.Collections.Generic;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Work priorities saved from the Work tab, one set for colonists and one for slaves, kept with the mod settings and applied when a pawn's work settings are first set up.
/// </summary>
public static class DefaultWorkPriorities
{
    // Keyed by defName; work types that no longer exist or the saved pawn couldn't do are skipped.
    private static Dictionary<string, int>? Get(bool slave)
    {
        return slave ? UiPlusMod.Settings.defaultWorkPrioritiesSlave : UiPlusMod.Settings.defaultWorkPriorities;
    }

    public static bool IsSet(bool slave) => Get(slave) is { Count: > 0 };

    public static bool CanReceiveDefaults(Pawn pawn)
    {
        return pawn.workSettings != null && pawn.workSettings.EverWork && NewColonistDefaults.IsColonyHumanlike(pawn);
    }

    public static bool Matches(Pawn pawn, bool slave)
    {
        Dictionary<string, int>? priorities = Get(slave);
        if (priorities is not { Count: > 0 } || pawn.workSettings == null || !pawn.workSettings.EverWork)
        {
            return false;
        }

        bool comparedAny = false;
        foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
        {
            if (pawn.WorkTypeIsDisabled(workType) || !priorities.TryGetValue(workType.defName, out int priority))
            {
                continue;
            }

            if (pawn.workSettings.GetPriority(workType) != priority)
            {
                return false;
            }

            comparedAny = true;
        }

        return comparedAny;
    }

    public static void SetFrom(Pawn pawn, bool slave)
    {
        Dictionary<string, int> priorities = new Dictionary<string, int>();
        foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
        {
            if (!pawn.WorkTypeIsDisabled(workType))
            {
                priorities[workType.defName] = pawn.workSettings.GetPriority(workType);
            }
        }

        if (slave)
        {
            UiPlusMod.Settings.defaultWorkPrioritiesSlave = priorities;
        }
        else
        {
            UiPlusMod.Settings.defaultWorkPriorities = priorities;
        }

        UiPlusMod.Instance.WriteSettings();
    }

    public static void Clear(bool slave)
    {
        if (slave)
        {
            UiPlusMod.Settings.defaultWorkPrioritiesSlave = null;
        }
        else
        {
            UiPlusMod.Settings.defaultWorkPriorities = null;
        }

        UiPlusMod.Instance.WriteSettings();
    }

    // A joining slave gets the slave default, or the colonist default when there's none.
    public static void ApplyTo(Pawn pawn)
    {
        bool slave = NewColonistDefaults.JoinsAsSlave(pawn) && IsSet(true);
        Load(pawn, slave);
    }

    public static void Load(Pawn pawn, bool slave)
    {
        Dictionary<string, int>? priorities = Get(slave);
        if (!UiPlusMod.Settings.applyDefaultWorkPriorities || priorities is not { Count: > 0 } || !CanReceiveDefaults(pawn))
        {
            return;
        }

        foreach (KeyValuePair<string, int> entry in priorities)
        {
            WorkTypeDef? workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(entry.Key);
            if (workType == null || entry.Value < 0 || pawn.WorkTypeIsDisabled(workType))
            {
                continue;
            }

            pawn.workSettings.SetPriority(workType, entry.Value);
        }
    }
}
