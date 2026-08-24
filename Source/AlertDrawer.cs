using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus.Alerts;

public static class AlertDrawer
{
    public const float BarWidth = 172f;
    public const float HorizontalPad = 3f;
    public const float TextWidth = BarWidth - HorizontalPad * 2f;
    public static readonly Color BarColor = new Color(0.08f, 0.08f, 0.08f, 0.78f);
    private static readonly Dictionary<string, string> TruncateCache = new Dictionary<string, string>();
    private static readonly FieldInfo AlertBounceField = AccessTools.Field(typeof(Alert), "alertBounce");
    private static readonly MethodInfo BounceOffsetMethod = AccessTools.Method("RimWorld.AlertBounce:CalculateHorizontalOffset");

    public static Color LetterFillColor(Color letterColor)
    {
        Color.RGBToHSV(letterColor, out float hue, out float sat, out float val);
        Color fill = Color.HSVToRGB(hue, Mathf.Min(1f, sat * 1.1f), val * 0.52f);
        fill.a = letterColor.a;
        return fill;
    }

    public static void DrawBarBackground(Rect rect)
    {
        if (AlertsMod.Settings.showBarBackgrounds)
        {
            Widgets.DrawBoxSolid(rect, BarColor);
        }
    }

    public static float HeightFor(Alert alert)
    {
        Text.Font = GameFont.Small;
        if (AlertsMod.Settings.wrapText)
        {
            return Text.CalcHeight(alert.Label, TextWidth);
        }

        return Text.LineHeight;
    }

    public static Rect DrawAt(Alert alert, float topY)
    {
        float height = HeightFor(alert);
        Rect rect = new Rect(UI.screenWidth - BarWidth, topY, BarWidth, height);

        object? bounce = AlertBounceField.GetValue(alert);
        if (bounce != null && BounceOffsetMethod != null)
        {
            rect.x -= (float)BounceOffsetMethod.Invoke(bounce, null);
        }

        DrawBarBackground(rect);
        Color bg = GetBackgroundColor(alert);
        if (bg.a > 0.001f)
        {
            Widgets.DrawBoxSolid(rect, bg);
        }

        Widgets.BeginGroup(rect);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        bool oldWrap = Text.WordWrap;
        Text.WordWrap = AlertsMod.Settings.wrapText;
        string label = alert.Label ?? string.Empty;
        if (!AlertsMod.Settings.wrapText)
        {
            label = label.Truncate(TextWidth, TruncateCache);
        }

        Widgets.Label(new Rect(HorizontalPad, 0f, TextWidth, height), label);
        Text.WordWrap = oldWrap;
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.EndGroup();

        if (Mouse.IsOver(rect))
        {
            Widgets.DrawHighlight(rect);
            TooltipHandler.TipRegion(rect, "VUIA.SnoozeTip".Translate(Mathf.Clamp(AlertsMod.Settings.snoozeDays, 1, 15)));
        }

        if (Widgets.ButtonInvisible(rect))
        {
            if (Event.current.button == 1)
            {
                SnoozeTracker.Snooze(alert);
            }
            else
            {
                AccessTools.Method(alert.GetType(), "OnClick")?.Invoke(alert, null);
            }
        }

        return rect;
    }

    private static Color GetBackgroundColor(Alert alert)
    {
        MethodInfo? getter = AccessTools.PropertyGetter(alert.GetType(), "BGColor");
        if (getter == null)
        {
            return Color.clear;
        }

        return (Color)getter.Invoke(alert, null);
    }
}
