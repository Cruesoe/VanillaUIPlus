using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Moves World Domination's world threat alert out of the alert stack and onto a HUD bar under the wealth line.
/// </summary>
public static class WorldDominationThreat
{
    private static readonly Type? AlertType = AccessTools.TypeByName("TSA_WorldDomination.Alert_WDWorldThreat");
    private static readonly Type? MidGameAlertType = AccessTools.TypeByName("TSA_WorldDomination.Alert_WDMidGameActive");
    private static readonly Type? LateGameAlertType = AccessTools.TypeByName("TSA_WorldDomination.Alert_WDLateGameActive");
    private static readonly Type? SpreadManagerType = AccessTools.TypeByName("TSA_WorldDomination.WorldComponent_SpreadManager");
    private static readonly FieldInfo? StageField = SpreadManagerType == null ? null : AccessTools.Field(SpreadManagerType, "cachedEscalationStage");
    private static readonly string[] StageKeys = { "VUIP.WorldThreatEarly", "VUIP.WorldThreatMid", "VUIP.WorldThreatLate" };

    private static Alert? instance;
    private static WorldComponent? spreadManager;
    private static string sourceLabel = string.Empty;
    private static int sourceStage = -1;
    private static string label = string.Empty;

    // Captures the threat alert so the HUD can draw it, and takes WD's stage alerts off the stack; the stage shows on the threat bar.
    public static bool Claim(Alert alert)
    {
        Type type = alert.GetType();
        if (AlertType != null && type == AlertType)
        {
            instance = alert;
            return true;
        }

        return (MidGameAlertType != null && type == MidGameAlertType) || (LateGameAlertType != null && type == LateGameAlertType);
    }

    public static void Forget()
    {
        instance = null;
        spreadManager = null;
        sourceLabel = string.Empty;
        sourceStage = -1;
        label = string.Empty;
    }

    // WD's escalation stage: 0 None (Early), 1 Mid, 2 Late; -1 when it can't be read.
    private static int Stage()
    {
        if (StageField == null || Find.World == null)
        {
            return -1;
        }

        if (spreadManager == null || spreadManager.world != Find.World)
        {
            spreadManager = Find.World.components.Find(c => c.GetType() == SpreadManagerType);
        }

        return spreadManager == null ? -1 : Convert.ToInt32(StageField.GetValue(spreadManager));
    }

    public static bool Visible => instance != null && instance.Active && !SnoozeTracker.IsSnoozed(instance);

    public static void DrawBar(Rect rect)
    {
        Alert? alert = instance;
        if (alert == null)
        {
            return;
        }

        // WD's label ends in "<near>|<far>"; only that part is kept.
        string current = alert.Label ?? string.Empty;
        int stage = Stage();
        if (current != sourceLabel || stage != sourceStage)
        {
            sourceLabel = current;
            sourceStage = stage;
            string[] points = current.Substring(current.LastIndexOf(' ') + 1).Split('|');
            if (points.Length != 2)
            {
                label = current;
            }
            else if (stage >= 0 && stage < StageKeys.Length)
            {
                label = "VUIP.WorldThreatWithStage".Translate(points[0].Trim(), points[1].Trim(), StageKeys[stage].Translate()).ToString();
            }
            else
            {
                label = "VUIP.WorldThreat".Translate(points[0].Trim(), points[1].Trim()).ToString();
            }
        }

        Color fill = default;
        if (alert.Priority == AlertPriority.High)
        {
            fill = AlertDrawer.LetterFillColor(LetterDefOf.ThreatBig.color);
            fill.a = ReadoutDrawer.InfoFillAlpha;
        }

        ReadoutDrawer.DrawBar(rect, label, fill);
        if (Mouse.IsOver(rect))
        {
            AlertDrawer.DrawInfoPane(alert);
        }

        if (Widgets.ButtonInvisible(rect))
        {
            if (Event.current.button == 1)
            {
                SnoozeTracker.Snooze(alert);
            }
            else
            {
                AlertDrawer.Click(alert);
            }
        }
    }
}
