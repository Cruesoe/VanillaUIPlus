using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(Alert), nameof(Alert.DrawAt))]
public static class Patch_Alert_DrawAt
{
    public static bool Prefix(Alert __instance, float topY, ref Rect __result)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        __result = AlertDrawer.DrawAt(__instance, topY);
        return false;
    }
}

[HarmonyPatch(typeof(Alert), nameof(Alert.Height), MethodType.Getter)]
public static class Patch_Alert_Height
{
    public static bool Prefix(Alert __instance, ref float __result)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        __result = AlertDrawer.HeightFor(__instance);
        return false;
    }
}

// Right-click snoozing is handled in AlertDrawer.DrawAt.

[HarmonyPatch(typeof(AlertsReadout), "CheckAddOrRemoveAlert")]
public static class Patch_AlertsReadout_CheckAddOrRemoveAlert
{
    public static void Prefix(Alert alert, ref bool forceRemove)
    {
        // The custom HUD draws the hostiles alert pinned above the letters instead.
        if (UiPlusMod.Enabled && alert is Alert_HostilesPresent)
        {
            forceRemove = true;
            return;
        }

        // Snoozes apply with the custom HUD off too.
        if (SnoozeTracker.IsSnoozed(alert) || LockedBuildingAlerts.ShouldHide(alert))
        {
            forceRemove = true;
        }
    }
}

[HarmonyPatch(typeof(AlertsReadout), nameof(AlertsReadout.AlertsReadoutOnGUI))]
public static class Patch_AlertsReadoutOnGUI
{
    private static readonly List<Alert> AlertsToDraw = new List<Alert>();
    private static readonly List<AlertPriority> PrioritiesToDraw = new List<AlertPriority>();

    private static readonly FieldInfo? ActiveAlertsField = AccessTools.Field(typeof(AlertsReadout), "activeAlerts");
    private static readonly FieldInfo? LastFinalYField = AccessTools.Field(typeof(AlertsReadout), "lastFinalY");
    private static readonly FieldInfo? MouseoverIndexField = AccessTools.Field(typeof(AlertsReadout), "mouseoverAlertIndex");
    private static readonly FieldInfo? PriosField = AccessTools.Field(typeof(AlertsReadout), "PriosInDrawOrder");
    private static readonly MethodInfo? CheckAddOrRemoveAlertMethod = AccessTools.Method(typeof(AlertsReadout), "CheckAddOrRemoveAlert");

    private static readonly AccessTools.FieldRef<AlertsReadout, List<Alert>>? ActiveAlerts =
        ReflectionGuard.FieldRef<AlertsReadout, List<Alert>>(nameof(AlertsReadout), "activeAlerts", ActiveAlertsField);
    private static readonly AccessTools.FieldRef<AlertsReadout, List<AlertPriority>>? Prios =
        ReflectionGuard.FieldRef<AlertsReadout, List<AlertPriority>>(nameof(AlertsReadout), "PriosInDrawOrder", PriosField);
    private static readonly AccessTools.FieldRef<AlertsReadout, float>? LastFinalY =
        ReflectionGuard.FieldRef<AlertsReadout, float>(nameof(AlertsReadout), "lastFinalY", LastFinalYField);
    private static readonly AccessTools.FieldRef<AlertsReadout, int>? MouseoverIndex =
        ReflectionGuard.FieldRef<AlertsReadout, int>(nameof(AlertsReadout), "mouseoverAlertIndex", MouseoverIndexField);
    private static readonly Action<AlertsReadout, Alert, bool>? CheckAddOrRemoveAlert =
        ReflectionGuard.Delegate<Action<AlertsReadout, Alert, bool>>(nameof(AlertsReadout), "CheckAddOrRemoveAlert", CheckAddOrRemoveAlertMethod);

    private static readonly bool Ready =
        ActiveAlerts != null
        && Prios != null
        && LastFinalY != null
        && MouseoverIndex != null
        && CheckAddOrRemoveAlert != null;

    public static bool Prefix(AlertsReadout __instance)
    {
        if (!UiPlusMod.Enabled || !Ready)
        {
            return true;
        }

        if (Event.current.type == EventType.Layout || Event.current.type == EventType.MouseDrag)
        {
            return false;
        }

        List<Alert> activeAlerts = ActiveAlerts!(__instance);
        if (activeAlerts == null || activeAlerts.Count == 0)
        {
            return false;
        }

        List<AlertPriority> prios = Prios!(__instance);
        if (prios == null || prios.Count == 0)
        {
            return false;
        }

        // Draws from reused snapshots, since drawing an alert can add or remove alerts.
        AlertsToDraw.Clear();
        AlertsToDraw.AddRange(activeAlerts);
        PrioritiesToDraw.Clear();
        PrioritiesToDraw.AddRange(prios);

        Alert? hovered = null;
        AlertPriority firstPriority = AlertPriority.Critical;
        bool sawPriority = false;
        float alertsHeight = __instance.AlertsHeight;
        bool reverse = UiPlusMod.Settings.reverseNotificationOrder;
        float top = reverse
            ? LetterDrawer.HudBaseY - alertsHeight
            : Find.LetterStack.LastTopY - alertsHeight;
        Rect stackRect = new Rect(UI.screenWidth - AlertDrawer.BarWidth, top, AlertDrawer.BarWidth, LastFinalY!(__instance) - top);
        float dark = GenUI.BackgroundDarkAlphaForText();
        if (dark > 0.001f)
        {
            GUI.color = new Color(1f, 1f, 1f, dark);
            Widgets.DrawShadowAround(stackRect);
            GUI.color = Color.white;
        }

        float y = top < 0f ? 0f : top;
        int mouseoverIndex = -1;
        int pStart = reverse ? PrioritiesToDraw.Count - 1 : 0;
        int pEnd = reverse ? -1 : PrioritiesToDraw.Count;
        int pStep = reverse ? -1 : 1;
        for (int p = pStart; p != pEnd; p += pStep)
        {
            AlertPriority priority = PrioritiesToDraw[p];
            int iStart = reverse ? AlertsToDraw.Count - 1 : 0;
            int iEnd = reverse ? -1 : AlertsToDraw.Count;
            int iStep = reverse ? -1 : 1;
            for (int i = iStart; i != iEnd; i += iStep)
            {
                Alert alert = AlertsToDraw[i];
                if (alert.Priority != priority)
                {
                    continue;
                }

                if (!sawPriority)
                {
                    firstPriority = priority;
                    sawPriority = true;
                }

                Rect drawn = alert.DrawAt(y, priority != firstPriority);
                if (Mouse.IsOver(drawn))
                {
                    hovered = alert;
                    mouseoverIndex = activeAlerts.IndexOf(alert);
                }

                y += drawn.height;
            }
        }

        LastFinalY!(__instance) = y;
        MouseoverIndex!(__instance) = mouseoverIndex;
        UIHighlighter.HighlightOpportunity(stackRect, "Alerts");
        if (hovered != null)
        {
            AlertDrawer.DrawInfoPane(hovered);
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Alerts, KnowledgeAmount.FrameDisplayed);
            CheckAddOrRemoveAlert!(__instance, hovered, false);
        }

        return false;
    }
}
