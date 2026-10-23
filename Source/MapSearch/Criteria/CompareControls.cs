using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>A value with a comparison, drawn as a mode button, a slider and the formatted value.</summary>
public static class CompareControls
{
    private const float ModeWidth = 74f;
    private const float ValueWidth = 76f;

    public static bool Test(float actual, float target, CompareMode mode, float step)
    {
        return mode switch
        {
            CompareMode.AtMost => actual <= target + 0.0001f,
            CompareMode.AtLeast => actual >= target - 0.0001f,
            _ => Mathf.Abs(actual - target) < step / 2f
        };
    }

    public static void Draw(Rect rect, ref float value, ref CompareMode mode, float min, float max, float step, bool allowExactly,
        Func<float, string> format, ref bool changed)
    {
        Rect modeRect = new Rect(rect.x, rect.y + 2f, ModeWidth, rect.height - 4f);
        string modeKey = mode switch
        {
            CompareMode.AtMost => "VUIP.WorldQueryAtMost",
            CompareMode.AtLeast => "VUIP.WorldQueryAtLeast",
            _ => "VUIP.WorldQueryExactly"
        };
        TooltipHandler.TipRegion(modeRect, "VUIP.WorldQueryCompareTip".Translate());
        if (Widgets.ButtonText(modeRect, modeKey.Translate()))
        {
            mode = mode switch
            {
                CompareMode.AtMost => CompareMode.AtLeast,
                CompareMode.AtLeast => allowExactly ? CompareMode.Exactly : CompareMode.AtMost,
                _ => CompareMode.AtMost
            };
            changed = true;
        }

        Rect valueRect = new Rect(rect.xMax - ValueWidth, rect.y, ValueWidth, rect.height);
        Rect sliderRect = new Rect(modeRect.xMax + 6f, rect.y, valueRect.x - modeRect.xMax - 12f, rect.height);
        float newValue = Widgets.HorizontalSlider(sliderRect, value, min, max, middleAlignment: true, roundTo: step);
        newValue = Mathf.Clamp(Mathf.Round(newValue / step) * step, min, max);
        if (Mathf.Abs(newValue - value) > step / 4f)
        {
            value = newValue;
            changed = true;
        }

        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(valueRect, format(value).Truncate(valueRect.width));
        Text.Anchor = TextAnchor.UpperLeft;
    }
}
