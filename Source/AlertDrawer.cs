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
    private static readonly AccessTools.FieldRef<Alert, object?> AlertBounceRef =
        AccessTools.FieldRefAccess<Alert, object?>("alertBounce");
    private static readonly FastInvokeHandler? BounceOffset =
        AccessTools.Method("RimWorld.AlertBounce:CalculateHorizontalOffset") is MethodInfo method
            ? MethodInvoker.GetHandler(method)
            : null;
    private static readonly Dictionary<int, Color> LetterFillCache = new Dictionary<int, Color>();
    private static int snoozeSuffixDays = -1;
    private static string snoozeSuffix = string.Empty;
    private static int barColorFrame = -1;
    private static Color barColorCached;

    public static Color BarColor
    {
        get
        {
            if (barColorFrame != Time.frameCount)
            {
                barColorFrame = Time.frameCount;
                barColorCached = BarRgb;
                barColorCached.a = UiPlusMod.Settings.barBackgroundOpacity;
            }

            return barColorCached;
        }
    }

    public static Dictionary<string, string> SharedTruncateCache => TruncateCache;

    public static Color LetterFillColor(Color letterColor)
    {
        int key = letterColor.GetHashCode();
        if (LetterFillCache.TryGetValue(key, out Color cached))
        {
            return cached;
        }

        Color.RGBToHSV(letterColor, out float hue, out float sat, out float val);
        Color fill = Color.HSVToRGB(hue, Mathf.Min(1f, sat * 1.1f), val * 0.52f);
        fill.a = letterColor.a;
        LetterFillCache[key] = fill;
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

        object? bounce = AlertBounceRef(alert);
        if (bounce != null && BounceOffset != null)
        {
            rect.x -= (float)BounceOffset(bounce);
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

    public static void DrawInfoPane(Alert alert)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        alert.Recalculate();
        if (!alert.Active)
        {
            return;
        }

        TaggedString explanation = alert.GetExplanation();
        int snoozeDays = Mathf.Clamp(UiPlusMod.Settings.snoozeDays, 1, 15);
        if (snoozeDays != snoozeSuffixDays)
        {
            snoozeSuffixDays = snoozeDays;
            snoozeSuffix = "\n\n" + "VUIP.SnoozeTip".Translate(snoozeDays);
        }

        explanation += snoozeSuffix;
        if (alert.GetReport().AnyCulpritValid)
        {
            explanation += "\n\n(" + alert.GetJumpToTargetsText + ")";
        }

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        const float paneWidth = 330f;
        float height = Text.CalcHeight(explanation, paneWidth - 20f) + 20f;
        Rect infoRect = new Rect(
            UI.screenWidth - BarWidth - paneWidth - 8f,
            Mathf.Max(Mathf.Min(Event.current.mousePosition.y, UI.screenHeight - height), 0f),
            paneWidth,
            height);
        if (infoRect.yMax > UI.screenHeight)
        {
            infoRect.y -= UI.screenHeight - infoRect.yMax;
        }

        if (infoRect.y < 0f)
        {
            infoRect.y = 0f;
        }

        Find.WindowStack.ImmediateWindow(138956, infoRect, WindowLayer.Super, delegate
        {
            Text.Font = GameFont.Small;
            Rect inner = infoRect.AtZero();
            Widgets.DrawWindowBackground(inner);
            Rect textRect = inner.ContractedBy(10f);
            Widgets.BeginGroup(textRect);
            Widgets.Label(new Rect(0f, 0f, textRect.width, textRect.height), explanation);
            Widgets.EndGroup();
        }, doBackground: false);
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
