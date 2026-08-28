using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

public class SnoozeTracker : WorldComponent
{
    private static readonly FieldInfo ActiveAlertsField = AccessTools.Field(typeof(AlertsReadout), "activeAlerts");

    private Dictionary<string, int> snoozedUntilTick = new Dictionary<string, int>();

    public SnoozeTracker(World world) : base(world)
    {
    }

    public static SnoozeTracker? Current()
    {
        return Find.World?.GetComponent<SnoozeTracker>();
    }

    public static string KeyFor(Alert alert)
    {
        string key = alert.GetType().FullName ?? alert.GetType().Name;
        if (alert is Alert_Custom || alert is Alert_CustomCritical)
        {
            string label = alert.GetLabel();
            if (!label.NullOrEmpty())
            {
                key += ":" + label;
            }
        }

        return key;
    }

    /// <summary>
    /// Snoozing is a property of the notifications, not of the custom HUD, so both entry
    /// points answer only to their own setting. Gating here keeps every caller correct:
    /// the HUD supplies the right-click gesture but does not own the feature.
    /// </summary>
    public static bool IsSnoozed(Alert alert)
    {
        if (!UiPlusMod.Settings.enableSnooze)
        {
            return false;
        }

        SnoozeTracker? tracker = Current();
        return tracker != null && tracker.IsSnoozedNow(alert);
    }

    public bool IsSnoozedNow(Alert alert)
    {
        string key = KeyFor(alert);
        if (!snoozedUntilTick.TryGetValue(key, out int untilTick))
        {
            return false;
        }

        if (Find.TickManager.TicksGame >= untilTick)
        {
            snoozedUntilTick.Remove(key);
            return false;
        }

        return true;
    }

    public static void Snooze(Alert alert)
    {
        if (!UiPlusMod.Settings.enableSnooze)
        {
            return;
        }

        SnoozeTracker? tracker = Current();
        if (tracker == null)
        {
            return;
        }

        int days = Mathf.Clamp(UiPlusMod.Settings.snoozeDays, 1, 15);
        tracker.snoozedUntilTick[KeyFor(alert)] = Find.TickManager.TicksGame + days * GenDate.TicksPerDay;
        RemoveFromReadout(alert);
        SoundDefOf.Click.PlayOneShotOnCamera();
    }

    public int ClearAll()
    {
        int count = snoozedUntilTick.Count;
        snoozedUntilTick.Clear();
        return count;
    }

    private static void RemoveFromReadout(Alert alert)
    {
        if (ActiveAlertsField == null || Find.UIRoot is not UIRoot_Play play)
        {
            return;
        }

        if (ActiveAlertsField.GetValue(play.alerts) is List<Alert> active)
        {
            active.Remove(alert);
        }
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref snoozedUntilTick, "snoozedUntilTick", LookMode.Value, LookMode.Value);
        if (snoozedUntilTick == null)
        {
            snoozedUntilTick = new Dictionary<string, int>();
        }
    }
}
