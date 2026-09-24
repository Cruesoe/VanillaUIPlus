using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

// Marked so the lazily built fade texture's type initializes on the main thread.
[StaticConstructorOnStartup]
public static class LetterDrawer
{
    private const float MinRowHeight = 26f;
    private const float IconPad = 2f;
    private const float LabelGap = 3f;
    private static readonly List<Letter> BundledLetters = new List<Letter>();
    private static readonly List<Letter> VisibleScratch = new List<Letter>();

    private static readonly AccessTools.FieldRef<LetterStack, float>? LastTopY =
        ReflectionGuard.FieldRef<LetterStack, float>(nameof(LetterStack), "lastTopYInt", AccessTools.Field(typeof(LetterStack), "lastTopYInt"));

    // Both are protected virtual on Letter; the delegates call the override.
    private static readonly Func<Letter, string>? PostProcessedLabelOf =
        ReflectionGuard.Delegate<Func<Letter, string>>(nameof(Letter), "PostProcessedLabel", AccessTools.Method(typeof(Letter), "PostProcessedLabel"));
    private static readonly Func<Letter, string>? MouseoverTextOf =
        ReflectionGuard.Delegate<Func<Letter, string>>(nameof(Letter), "GetMouseoverText", AccessTools.Method(typeof(Letter), "GetMouseoverText"));
    private static Texture2D? fadeTexture;
    private static Rect mouseoverRect;
    private static string mouseoverText = string.Empty;
    private static readonly Action DrawMouseoverContents = delegate
    {
        Text.Font = GameFont.Small;
        Rect inner = mouseoverRect.AtZero().ContractedBy(10f);
        Widgets.BeginGroup(inner);
        Widgets.Label(new Rect(0f, 0f, inner.width, inner.height), mouseoverText);
        Widgets.EndGroup();
    };

    private static Texture2D FadeTexture
    {
        get
        {
            if (fadeTexture == null)
            {
                fadeTexture = new Texture2D(128, 1, TextureFormat.ARGB32, mipChain: false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                for (int i = 0; i < 128; i++)
                {
                    float t = i / 127f;
                    fadeTexture.SetPixel(i, 0, new Color(1f, 1f, 1f, Mathf.Pow(1f - t, 1.75f)));
                }

                fadeTexture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            }

            return fadeTexture;
        }
    }

    public static float HudBaseY { get; private set; }

    public static void DrawLetters(LetterStack stack, float baseY)
    {
        HudBaseY = baseY;
        List<Letter> letters = stack.LettersListForReading;
        Text.Font = GameFont.Small;
        float rowHeight = Mathf.Max(Text.LineHeight, MinRowHeight);
        float alertsHeight = Find.Alerts.AlertsHeight;
        float available = baseY - alertsHeight;
        int hideCount = CountHidden(letters, available, rowHeight);

        bool reverse = UiPlusMod.Settings.reverseNotificationOrder;
        float drawBaseY = reverse ? baseY - alertsHeight : baseY;
        CollectVisible(letters, hideCount, reverse);
        float visibleHeight = VisibleHeight(rowHeight);
        float topY = drawBaseY - visibleHeight - (hideCount > 0 ? rowHeight : 0f);
        if (!DrawVisible(drawBaseY, rowHeight, mouseover: false))
        {
            SetLastTopY(stack, topY);
            VisibleScratch.Clear();
            return;
        }

        if (hideCount > 0)
        {
            BundledLetters.Clear();
            int bundleLimit = Math.Min(hideCount, letters.Count);
            for (int i = 0; i < bundleLimit; i++)
            {
                BundledLetters.Add(letters[i]);
            }

            stack.BundleLetter.SetLetters(BundledLetters);
            DrawButton(stack.BundleLetter, topY, rowHeight, rowHeight);
            BundledLetters.Clear();
        }

        SetLastTopY(stack, topY);

        if (Event.current.type != EventType.Repaint)
        {
            VisibleScratch.Clear();
            return;
        }

        DrawVisible(drawBaseY, rowHeight, mouseover: true);
        if (hideCount > 0)
        {
            DrawMouseover(stack.BundleLetter, topY, rowHeight);
        }

        VisibleScratch.Clear();
    }

    // How many of the oldest letters go into the bundle row; the newest that fit are shown.
    private static int CountHidden(List<Letter> letters, float available, float rowHeight)
    {
        int count = letters.Count;
        float total = 0f;
        for (int i = 0; i < count; i++)
        {
            total += RowHeight(letters[i], rowHeight);
        }

        // Vanilla always shows at least one letter, even when there is less than a row free.
        if (total <= Mathf.Max(available, rowHeight))
        {
            return 0;
        }

        float budget = available - rowHeight;
        float used = 0f;
        int shown = 0;
        for (int i = count - 1; i >= 0; i--)
        {
            used += RowHeight(letters[i], rowHeight);
            if (used > budget)
            {
                break;
            }

            shown++;
        }

        return count - shown;
    }

    private static float VisibleHeight(float rowHeight)
    {
        float height = 0f;
        for (int i = 0; i < VisibleScratch.Count; i++)
        {
            height += RowHeight(VisibleScratch[i], rowHeight);
        }

        return height;
    }

    // Uses the one-line icon size, so the label width doesn't depend on the wrapped height.
    private static float LabelWidth(float rowHeight)
    {
        return AlertDrawer.BarWidth - AlertDrawer.HorizontalPad * 2f - IconSize(rowHeight) - LabelGap;
    }

    private static float IconSize(float rowHeight)
    {
        return rowHeight - IconPad * 2f;
    }

    private static float RowHeight(Letter letter, float rowHeight)
    {
        if (!UiPlusMod.Settings.wrapLetterText)
        {
            return rowHeight;
        }

        float wrapped = TextCache.WrappedHeight(PostProcessedLabel(letter), LabelWidth(rowHeight)) + IconPad * 2f;
        return Mathf.Max(rowHeight, wrapped);
    }

    private static void SetLastTopY(LetterStack stack, float value)
    {
        if (LastTopY == null)
        {
            return;
        }

        LastTopY(stack) = value;
    }


    private static void CollectVisible(List<Letter> letters, int hideCount, bool reverse)
    {
        VisibleScratch.Clear();
        int count = letters.Count;
        if (reverse)
        {
            for (int i = hideCount; i < count; i++)
            {
                VisibleScratch.Add(letters[i]);
            }
        }
        else
        {
            for (int i = count - 1; i >= hideCount; i--)
            {
                VisibleScratch.Add(letters[i]);
            }
        }
    }

    private static bool DrawVisible(float baseY, float rowHeight, bool mouseover)
    {
        float y = baseY;
        for (int i = 0; i < VisibleScratch.Count; i++)
        {
            Letter letter = VisibleScratch[i];
            float height = RowHeight(letter, rowHeight);
            y -= height;
            if (mouseover)
            {
                DrawMouseover(letter, y, height);
                continue;
            }

            if (DrawButton(letter, y, height, rowHeight))
            {
                return false;
            }
        }

        return true;
    }

    // rowHeight is the one-line height; height is this letter's row, taller when it wraps.
    private static bool DrawButton(Letter letter, float topY, float height, float rowHeight)
    {
        Rect rest = new Rect(UI.screenWidth - AlertDrawer.BarWidth, topY, AlertDrawer.BarWidth, height);
        Rect drawn = rest;
        Color fill = letter.def.color;
        float age = Time.time - letter.arrivalTime;
        if (age < 1f)
        {
            drawn.y -= (1f - age) * 200f;
            fill.a = age;
        }
        else
        {
            fill.a = 1f;
        }

        if (!Mouse.IsOver(rest) && letter.def.bounce && age > 15f && age % 5f < 1f)
        {
            float t = 2f * (age % 1f) - 1f;
            drawn.x -= UI.screenWidth * 0.06f * (1f - t * t);
        }

        if (Event.current.type == EventType.Repaint)
        {
            if (letter.def.flashInterval > 0f)
            {
                float flashAge = Time.time - (letter.arrivalTime + 1f);
                if (flashAge > 0f && flashAge % letter.def.flashInterval < 1f)
                {
                    GenUI.DrawFlash(rest.x, topY, UI.screenWidth * 0.6f, Pulser.PulseBrightness(1f, 1f, flashAge) * 0.55f, letter.def.flashColor);
                }
            }

            AlertDrawer.DrawBarBackground(drawn);
            Color tint = AlertDrawer.LetterFillColor(letter.def.color);
            tint.a = fill.a;
            GUI.color = tint;
            GUI.DrawTexture(drawn, FadeTexture);

            float iconSize = IconSize(rowHeight);
            Rect iconRect = new Rect(drawn.x + AlertDrawer.HorizontalPad, drawn.y + (height - iconSize) / 2f, iconSize, iconSize);
            if (letter.def.Icon != null)
            {
                Color iconColor = letter.def.color;
                iconColor.a = fill.a;
                GUI.color = iconColor;
                Widgets.DrawTextureFitted(iconRect, letter.def.Icon, 1f);
            }

            GUI.color = Color.white;
            string label = PostProcessedLabel(letter);
            float labelX = iconRect.xMax + LabelGap;
            float labelWidth = LabelWidth(rowHeight);
            bool wrap = UiPlusMod.Settings.wrapLetterText;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            bool oldWrap = Text.WordWrap;
            Text.WordWrap = wrap;
            Widgets.Label(new Rect(labelX, drawn.y, labelWidth, height), wrap ? label : TextCache.Truncate(label, labelWidth));
            Text.WordWrap = oldWrap;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        if (Mouse.IsOver(drawn))
        {
            Widgets.DrawHighlight(drawn);
        }

        if (letter.CanDismissWithRightClick && Event.current.type == EventType.MouseDown && Event.current.button == 1 && Mouse.IsOver(drawn))
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            Find.LetterStack.RemoveLetter(letter);
            Event.current.Use();
            return true;
        }

        if (Widgets.ButtonInvisible(drawn))
        {
            letter.OpenLetter();
            Event.current.Use();
        }

        return false;
    }

    private static void DrawMouseover(Letter letter, float topY, float height)
    {
        Rect bar = new Rect(UI.screenWidth - AlertDrawer.BarWidth, topY, AlertDrawer.BarWidth, height);
        if (!Mouse.IsOver(bar))
        {
            return;
        }

        Find.LetterStack.Notify_LetterMouseover(letter);
        string? text = MouseoverTextOf?.Invoke(letter);
        if (text.NullOrEmpty())
        {
            return;
        }

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        float infoHeight = TextCache.WrappedHeight(text, 310f) + 20f;
        float x = bar.x - 330f - 10f;
        float y = Mathf.Max(topY - infoHeight / 2f, 0f);
        mouseoverRect = new Rect(x, y, 330f, infoHeight);
        mouseoverText = text!;
        Find.WindowStack.ImmediateWindow(2768333, mouseoverRect, WindowLayer.Super, DrawMouseoverContents);
    }

    private static string PostProcessedLabel(Letter letter)
    {
        return PostProcessedLabelOf?.Invoke(letter) ?? letter.Label;
    }
}
