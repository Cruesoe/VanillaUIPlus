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

// Right-click-to-snooze is dispatched by AlertDrawer.DrawAt, which owns the whole
// alert draw path, so there is no need to patch OnClick on every Alert subclass.

[HarmonyPatch(typeof(AlertsReadout), "CheckAddOrRemoveAlert")]
public static class Patch_AlertsReadout_CheckAddOrRemoveAlert
{
    public static void Prefix(Alert alert, ref bool forceRemove)
    {
        // Pulling the hostiles alert out of the stack is a HUD concern: it is only
        // removed here because the mod redraws it pinned above the letters.
        if (UiPlusMod.Enabled && alert is Alert_HostilesPresent)
        {
            forceRemove = true;
            return;
        }

        // Snoozing belongs to the notifications, so it still applies with the HUD
        // left vanilla. IsSnoozed already honours the enableSnooze setting.
        if (SnoozeTracker.IsSnoozed(alert) || LockedBuildingAlerts.ShouldHide(alert))
        {
            forceRemove = true;
        }
    }
}

[HarmonyPatch(typeof(AlertsReadout), nameof(AlertsReadout.AlertsReadoutOnGUI))]
public static class Patch_AlertsReadoutOnGUI
{
    private static readonly FieldInfo ActiveAlertsField = AccessTools.Field(typeof(AlertsReadout), "activeAlerts");
    private static readonly FieldInfo LastFinalYField = AccessTools.Field(typeof(AlertsReadout), "lastFinalY");
    private static readonly FieldInfo MouseoverIndexField = AccessTools.Field(typeof(AlertsReadout), "mouseoverAlertIndex");
    private static readonly FieldInfo PriosField = AccessTools.Field(typeof(AlertsReadout), "PriosInDrawOrder");
    private static readonly MethodInfo CheckAddOrRemoveAlertMethod = AccessTools.Method(typeof(AlertsReadout), "CheckAddOrRemoveAlert");
    private static readonly object[] CheckAddOrRemoveArgs = new object[] { null!, false };

    private static readonly bool Ready =
        ReflectionGuard.Found(nameof(AlertsReadout), "activeAlerts", ActiveAlertsField)
        & ReflectionGuard.Found(nameof(AlertsReadout), "lastFinalY", LastFinalYField)
        & ReflectionGuard.Found(nameof(AlertsReadout), "mouseoverAlertIndex", MouseoverIndexField)
        & ReflectionGuard.Found(nameof(AlertsReadout), "PriosInDrawOrder", PriosField)
        & ReflectionGuard.Found(nameof(AlertsReadout), "CheckAddOrRemoveAlert", CheckAddOrRemoveAlertMethod);

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

        List<Alert> activeAlerts = (List<Alert>)ActiveAlertsField.GetValue(__instance);
        if (activeAlerts == null || activeAlerts.Count == 0)
        {
            return false;
        }

        List<AlertPriority> prios = (List<AlertPriority>)PriosField.GetValue(__instance);
        Alert? hovered = null;
        AlertPriority firstPriority = AlertPriority.Critical;
        bool sawPriority = false;
        float alertsHeight = __instance.AlertsHeight;
        bool reverse = UiPlusMod.Settings.reverseNotificationOrder;
        float top = reverse
            ? LetterDrawer.HudBaseY - alertsHeight
            : Find.LetterStack.LastTopY - alertsHeight;
        Rect stackRect = new Rect(UI.screenWidth - AlertDrawer.BarWidth, top, AlertDrawer.BarWidth, (float)LastFinalYField.GetValue(__instance) - top);
        float dark = GenUI.BackgroundDarkAlphaForText();
        if (dark > 0.001f)
        {
            GUI.color = new Color(1f, 1f, 1f, dark);
            Widgets.DrawShadowAround(stackRect);
            GUI.color = Color.white;
        }

        float y = top < 0f ? 0f : top;
        int mouseoverIndex = -1;
        int pStart = reverse ? prios.Count - 1 : 0;
        int pEnd = reverse ? -1 : prios.Count;
        int pStep = reverse ? -1 : 1;
        for (int p = pStart; p != pEnd; p += pStep)
        {
            AlertPriority priority = prios[p];
            int iStart = reverse ? activeAlerts.Count - 1 : 0;
            int iEnd = reverse ? -1 : activeAlerts.Count;
            int iStep = reverse ? -1 : 1;
            for (int i = iStart; i != iEnd; i += iStep)
            {
                Alert alert = activeAlerts[i];
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
                    mouseoverIndex = i;
                }

                y += drawn.height;
            }
        }

        LastFinalYField.SetValue(__instance, y);
        MouseoverIndexField.SetValue(__instance, mouseoverIndex);
        UIHighlighter.HighlightOpportunity(stackRect, "Alerts");
        if (hovered != null)
        {
            AlertDrawer.DrawInfoPane(hovered);
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Alerts, KnowledgeAmount.FrameDisplayed);
            CheckAddOrRemoveArgs[0] = hovered;
            CheckAddOrRemoveAlertMethod.Invoke(__instance, CheckAddOrRemoveArgs);
        }

        return false;
    }
}
