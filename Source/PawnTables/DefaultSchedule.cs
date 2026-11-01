using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// A 24-hour schedule pinned from the Schedule tab, saved with the mod settings and copied onto each pawn that joins.
/// </summary>
public static class DefaultSchedule
{
    private const int HoursPerDay = 24;

    // Stored as defNames; unknown names apply as Anything.
    public static bool IsSet => UiPlusMod.Settings.defaultSchedule is { Count: HoursPerDay };

    public static bool Matches(Pawn_TimetableTracker? timetable)
    {
        List<string>? schedule = UiPlusMod.Settings.defaultSchedule;
        if (!IsSet || timetable?.times == null || timetable.times.Count != HoursPerDay)
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

    public static void SetFrom(Pawn_TimetableTracker timetable)
    {
        UiPlusMod.Settings.defaultSchedule = timetable.times
            .Select(assignment => (assignment ?? TimeAssignmentDefOf.Anything).defName)
            .ToList();
        UiPlusMod.Instance.WriteSettings();
    }

    public static void Clear()
    {
        UiPlusMod.Settings.defaultSchedule = null;
        UiPlusMod.Instance.WriteSettings();
    }

    public static void ApplyTo(Pawn pawn)
    {
        Pawn_TimetableTracker? timetable = pawn.timetable;
        if (!UiPlusMod.Settings.applyDefaultSchedule || !IsSet || timetable?.times == null || !NewColonistDefaults.IsColonyHumanlike(pawn))
        {
            return;
        }

        List<string> schedule = UiPlusMod.Settings.defaultSchedule!;
        timetable.times.Clear();
        for (int hour = 0; hour < HoursPerDay; hour++)
        {
            timetable.times.Add(DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(schedule[hour]) ?? TimeAssignmentDefOf.Anything);
        }
    }
}
