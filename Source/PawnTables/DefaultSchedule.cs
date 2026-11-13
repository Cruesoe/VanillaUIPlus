using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// 24-hour schedules saved from the Schedule tab, one for colonists and one for slaves, kept with the mod settings and copied onto each pawn that joins.
/// </summary>
public static class DefaultSchedule
{
    private const int HoursPerDay = 24;

    // Stored as defNames; unknown names apply as Anything.
    private static List<string>? Get(bool slave)
    {
        return slave ? UiPlusMod.Settings.defaultScheduleSlave : UiPlusMod.Settings.defaultSchedule;
    }

    public static bool IsSet(bool slave) => Get(slave) is { Count: HoursPerDay };

    public static bool Matches(Pawn_TimetableTracker? timetable, bool slave)
    {
        List<string>? schedule = Get(slave);
        if (!IsSet(slave) || timetable?.times == null || timetable.times.Count != HoursPerDay)
        {
            return false;
        }

        for (int hour = 0; hour < HoursPerDay; hour++)
        {
            if (timetable.times[hour]?.defName != schedule![hour])
            {
                return false;
            }
        }

        return true;
    }

    public static void SetFrom(Pawn_TimetableTracker timetable, bool slave)
    {
        List<string> schedule = timetable.times
            .Select(assignment => (assignment ?? TimeAssignmentDefOf.Anything).defName)
            .ToList();
        if (slave)
        {
            UiPlusMod.Settings.defaultScheduleSlave = schedule;
        }
        else
        {
            UiPlusMod.Settings.defaultSchedule = schedule;
        }

        UiPlusMod.Instance.WriteSettings();
    }

    public static void Clear(bool slave)
    {
        if (slave)
        {
            UiPlusMod.Settings.defaultScheduleSlave = null;
        }
        else
        {
            UiPlusMod.Settings.defaultSchedule = null;
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
        Pawn_TimetableTracker? timetable = pawn.timetable;
        if (!UiPlusMod.Settings.applyDefaultSchedule || !IsSet(slave) || timetable?.times == null || !NewColonistDefaults.IsColonyHumanlike(pawn))
        {
            return;
        }

        List<string> schedule = Get(slave)!;
        timetable.times.Clear();
        for (int hour = 0; hour < HoursPerDay; hour++)
        {
            timetable.times.Add(DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(schedule[hour]) ?? TimeAssignmentDefOf.Anything);
        }
    }
}
