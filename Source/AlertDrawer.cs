using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public static class AlertDrawer
{
    public const float BarWidth = 172f;
    public const float HorizontalPad = 3f;
    public const float TextWidth = BarWidth - HorizontalPad * 2f;
    private static readonly Color BarRgb = new Color(0.08f, 0.08f, 0.08f, 1f);
    private static readonly Dictionary<string, string> TruncateCache = new Dictionary<string, string>();
    private static readonly Dictionary<Type, Func<Alert, Color>> BgColorGetters = new Dictionary<Type, Func<Alert, Color>>();
    private static readonly Dictionary<Type, Action<Alert>?> OnClickActions = new Dictionary<Type, Action<Alert>?>();
    private static readonly FieldInfo AlertBounceField = AccessTools.Field(typeof(Alert), "alertBounce");
    private static readonly MethodInfo BounceOffsetMethod = AccessTools.Method("RimWorld.AlertBounce:CalculateHorizontalOffset");

    public static Color BarColor
    {
        get
        {
            Color color = BarRgb;
            color.a = UiPlusMod.Settings.barBackgroundOpacity;
            return color;
        }
    }

    public static Color LetterFillColor(Color letterColor)
    {
        Color.RGBToHSV(letterColor, out float hue, out float sat, out float val);
        Color fill = Color.HSVToRGB(hue, Mathf.Min(1f, sat * 1.1f), val * 0.52f);
        fill.a = letterColor.a;
        return fill;
    }

    public static void DrawBarBackground(Rect rect)
    {
        Color color = BarColor;
        if (color.a > 0.001f)
        {
            Widgets.DrawBoxSolid(rect, color);
        }
    }

    public static float HeightFor(Alert alert)
    {
        Text.Font = GameFont.Small;
        if (UiPlusMod.Settings.wrapText)
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
        Text.WordWrap = UiPlusMod.Settings.wrapText;
        string label = alert.Label ?? string.Empty;
        if (!UiPlusMod.Settings.wrapText)
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
            TooltipHandler.TipRegion(rect, "VUIP.SnoozeTip".Translate(Mathf.Clamp(UiPlusMod.Settings.snoozeDays, 1, 15)));
        }

        if (Widgets.ButtonInvisible(rect))
        {
            if (Event.current.button == 1)
            {
                SnoozeTracker.Snooze(alert);
            }
            else
            {
                OnClickFor(alert.GetType())?.Invoke(alert);
            }
        }

        return rect;
    }

    private static Color GetBackgroundColor(Alert alert)
    {
        Type type = alert.GetType();
        if (!BgColorGetters.TryGetValue(type, out Func<Alert, Color> getter))
        {
            MethodInfo? method = AccessTools.PropertyGetter(type, "BGColor");
            getter = method == null
                ? (_ => Color.clear)
                : AccessTools.MethodDelegate<Func<Alert, Color>>(method);
            BgColorGetters[type] = getter;
        }

        return getter(alert);
    }

    private static Action<Alert>? OnClickFor(Type type)
    {
        if (OnClickActions.TryGetValue(type, out Action<Alert>? action))
        {
            return action;
        }

        MethodInfo? method = AccessTools.Method(type, "OnClick");
        action = method == null ? null : AccessTools.MethodDelegate<Action<Alert>>(method);
        OnClickActions[type] = action;
        return action;
    }
}
