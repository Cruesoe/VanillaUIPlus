using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

public class SnoozeTracker : WorldComponent
{
    private static readonly AccessTools.FieldRef<AlertsReadout, List<Alert>>? ActiveAlerts =
        ReflectionGuard.FieldRef<AlertsReadout, List<Alert>>(nameof(AlertsReadout), "activeAlerts", AccessTools.Field(typeof(AlertsReadout), "activeAlerts"));

    // Keys for alert types whose key doesn't depend on the label.
    private static readonly Dictionary<Type, string> TypeKeys = new Dictionary<Type, string>();

    private static SnoozeTracker? current;

    private Dictionary<string, int> snoozedUntilTick = new Dictionary<string, int>();

    public SnoozeTracker(World world) : base(world)
    {
        current = this;
    }

    public static SnoozeTracker? Current()
    {
        World? world = Find.World;
        if (world == null)
        {
            return null;
        }

        if (current?.world != world)
        {
            current = world.GetComponent<SnoozeTracker>();
        }

        return current;
    }

    // Custom alerts share a type, so their label is part of the key.
    public static string KeyFor(Alert alert)
    {
        Type type = alert.GetType();
        if (!TypeKeys.TryGetValue(type, out string key))
        {
            key = type.FullName ?? type.Name;
            TypeKeys[type] = key;
        }

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

    // Answers only to the snooze setting, not the custom HUD toggle.
    public static bool IsSnoozed(Alert alert)
    {
        if (!UiPlusMod.Settings.enableSnooze)
        {
            return false;
        }

        SnoozeTracker? tracker = Current();
        return tracker != null && tracker.snoozedUntilTick.Count > 0 && tracker.IsSnoozedNow(alert);
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
        if (ActiveAlerts == null || Find.UIRoot is not UIRoot_Play play)
        {
            return;
        }

        ActiveAlerts(play.alerts)?.Remove(alert);
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref snoozedUntilTick, "snoozedUntilTick", LookMode.Value, LookMode.Value);
        snoozedUntilTick ??= new Dictionary<string, int>();
    }
}
