using System;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Shared settings-screen widgets. A control given a locked reason is drawn greyed and inert, with the reason in its tooltip.
/// </summary>
public static class SettingsWidgets
{
    public static readonly Color MutedColor = new Color(0.72f, 0.72f, 0.72f);
    private static readonly Color LockedSliderOverlay = new Color(
        Widgets.WindowBGFillColor.r, Widgets.WindowBGFillColor.g, Widgets.WindowBGFillColor.b, 0.6f);

    /// <summary>The locked reason for an option that needs the checkbox labelled by settingKey.</summary>
    public static string RequiresSetting(string settingKey)
    {
        return "VUIP.RequiresSetting".Translate(settingKey.Translate());
    }

    public static void Header(Rect row, string title, string resetLabel, string? resetTip, Action reset)
    {
        Rect resetRect = new Rect(row.xMax - 120f, row.y + 3f, 120f, 30f);
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(row.x, row.y, resetRect.x - row.x - 8f, row.height), title);
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        if (resetTip != null)
        {
            TooltipHandler.TipRegion(resetRect, resetTip);
        }

        if (Widgets.ButtonText(resetRect, resetLabel))
        {
            reset();
        }
    }

    public static void Subheader(Listing_Standard list, string label)
    {
        list.Gap(10f);
        StatusLabel(list, label);
        list.GapLine();
    }

    public static void StatusLabel(Listing_Standard list, string label)
    {
        Color old = GUI.color;
        GUI.color = MutedColor;
        list.Label(label);
        GUI.color = old;
    }

    public static void Checkbox(Listing_Standard list, string label, ref bool value, string? tip, string? lockedReason = null)
    {
        bool locked = lockedReason != null;
        Rect rect = list.GetRect(Text.CalcHeight(label, list.ColumnWidth));
        string? fullTip = CombineTip(tip, lockedReason);
        if (!fullTip.NullOrEmpty())
        {
            if (!locked)
            {
                Widgets.DrawHighlightIfMouseover(rect);
            }

            TooltipHandler.TipRegion(rect, fullTip);
        }

        Color old = GUI.color;
        if (locked)
        {
            GUI.color = Widgets.InactiveColor;
        }

        Widgets.CheckboxLabeled(rect, label, ref value, disabled: locked);
        GUI.color = old;
        list.Gap(list.verticalSpacing);
    }

    public static bool Button(Listing_Standard list, string label, string? lockedReason = null)
    {
        bool locked = lockedReason != null;
        Rect rect = list.GetRect(30f);
        Color old = GUI.color;
        if (locked)
        {
            GUI.color = Widgets.InactiveColor;
            TooltipHandler.TipRegion(rect, lockedReason);
        }

        bool clicked = Widgets.ButtonText(rect, label, doMouseoverSound: !locked, active: !locked);
        GUI.color = old;
        list.Gap(list.verticalSpacing);
        return clicked && !locked;
    }

    public static float Slider(Listing_Standard list, string label, float value, float min, float max, string? tip, string? lockedReason = null)
    {
        if (lockedReason == null)
        {
            return list.SliderLabeled(label, value, min, max, tooltip: tip);
        }

        Rect rect = list.GetRect(30f);
        Color old = GUI.color;
        GUI.color = Widgets.InactiveColor;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(rect.LeftHalf(), label);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = old;

        // Drawn on repaint only, so the slider never sees input; the overlay greys the rail and handle it colours itself.
        if (Event.current.type == EventType.Repaint)
        {
            Rect sliderRect = rect.RightHalf();
            Widgets.HorizontalSlider(sliderRect, value, min, max, middleAlignment: true);
            Widgets.DrawBoxSolid(sliderRect, LockedSliderOverlay);
        }

        TooltipHandler.TipRegion(rect, CombineTip(tip, lockedReason));
        list.Gap(list.verticalSpacing);
        return value;
    }

    public static void AdvancedToggle(Listing_Standard list, ref bool expanded, string? lockedReason = null)
    {
        bool locked = lockedReason != null;
        Rect row = list.GetRect(28f);
        if (locked)
        {
            TooltipHandler.TipRegion(row, lockedReason);
        }
        else
        {
            Widgets.DrawHighlightIfMouseover(row);
        }

        Color old = GUI.color;
        GUI.color = locked ? Widgets.InactiveColor : MutedColor;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(row.ContractedBy(4f, 0f), (expanded ? "▼  " : "▶  ") + "VUIP.Advanced".Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = old;
        if (!locked && Widgets.ButtonInvisible(row))
        {
            expanded = !expanded;
        }
    }

    private static string? CombineTip(string? tip, string? lockedReason)
    {
        if (lockedReason == null)
        {
            return tip;
        }

        return tip.NullOrEmpty() ? lockedReason : tip + "\n\n" + lockedReason;
    }
}
