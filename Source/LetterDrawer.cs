using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

public static class LetterDrawer
{
    private const float MinRowHeight = 26f;
    private static readonly Dictionary<string, string> TruncateCache = new Dictionary<string, string>();
    private static readonly List<Letter> BundledLetters = new List<Letter>();
    private static readonly List<Letter> VisibleScratch = new List<Letter>();
    private static readonly FieldInfo LastTopYField = AccessTools.Field(typeof(LetterStack), "lastTopYInt");
    private static readonly FastInvokeHandler? PostProcessedLabelInvoke =
        AccessTools.Method(typeof(Letter), "PostProcessedLabel") is MethodInfo post
            ? MethodInvoker.GetHandler(post)
            : null;
    private static readonly FastInvokeHandler? MouseoverTextInvoke =
        AccessTools.Method(typeof(Letter), "GetMouseoverText") is MethodInfo mouse
            ? MethodInvoker.GetHandler(mouse)
            : null;
    private static Texture2D? fadeTexture;

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
        int maxVisible = Mathf.Max(1, Mathf.FloorToInt(available / rowHeight));
        int hideCount = Math.Max(letters.Count - maxVisible, 0);
        if (hideCount > 0)
        {
            hideCount++;
        }

        bool reverse = UiPlusMod.Settings.reverseNotificationOrder;
        float drawBaseY = reverse ? baseY - alertsHeight : baseY;
        CollectVisible(letters, hideCount, reverse);
        if (!DrawVisible(drawBaseY, rowHeight, mouseover: false))
        {
            float topAfterDismiss = drawBaseY - VisibleScratch.Count * rowHeight;
            if (hideCount > 0)
            {
                topAfterDismiss -= rowHeight;
            }

            LastTopYField.SetValue(stack, topAfterDismiss);
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

            float bundleY = drawBaseY - VisibleLetterCount(letters.Count, hideCount) * rowHeight - rowHeight;
            stack.BundleLetter.SetLetters(BundledLetters);
            DrawButton(stack.BundleLetter, bundleY, rowHeight);
            BundledLetters.Clear();
        }

        float topY = drawBaseY - (VisibleLetterCount(letters.Count, hideCount) + (hideCount > 0 ? 1 : 0)) * rowHeight;
        LastTopYField.SetValue(stack, topY);

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

    private static int VisibleLetterCount(int total, int hideCount)
    {
        return total - hideCount;
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
            y -= rowHeight;
            Letter letter = VisibleScratch[i];
            if (mouseover)
            {
                DrawMouseover(letter, y, rowHeight);
                continue;
            }

            if (DrawButton(letter, y, rowHeight))
            {
                return false;
            }
        }

        return true;
    }

    private static bool DrawButton(Letter letter, float topY, float height)
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

            float iconPad = 2f;
            float iconSize = height - iconPad * 2f;
            Rect iconRect = new Rect(drawn.x + AlertDrawer.HorizontalPad, drawn.y + iconPad, iconSize, iconSize);
            if (letter.def.Icon != null)
            {
                Color iconColor = letter.def.color;
                iconColor.a = fill.a;
                GUI.color = iconColor;
                Widgets.DrawTextureFitted(iconRect, letter.def.Icon, 1f);
            }

            GUI.color = Color.white;
            string label = PostProcessedLabel(letter);
            float labelX = iconRect.xMax + 3f;
            float labelWidth = drawn.xMax - AlertDrawer.HorizontalPad - labelX;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            bool oldWrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(new Rect(labelX, drawn.y, labelWidth, height), label.Truncate(labelWidth, TruncateCache));
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
        string? text = MouseoverTextInvoke?.Invoke(letter) as string;
        if (text.NullOrEmpty())
        {
            return;
        }

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        float infoHeight = Text.CalcHeight(text, 310f) + 20f;
        float x = bar.x - 330f - 10f;
        float y = Mathf.Max(topY - infoHeight / 2f, 0f);
        Rect infoRect = new Rect(x, y, 330f, infoHeight);
        Find.WindowStack.ImmediateWindow(2768333, infoRect, WindowLayer.Super, delegate
        {
            Text.Font = GameFont.Small;
            Rect inner = infoRect.AtZero().ContractedBy(10f);
            Widgets.BeginGroup(inner);
            Widgets.Label(new Rect(0f, 0f, inner.width, inner.height), text);
            Widgets.EndGroup();
        });
    }

    private static string PostProcessedLabel(Letter letter)
    {
        if (PostProcessedLabelInvoke != null)
        {
            return PostProcessedLabelInvoke(letter) as string ?? letter.Label;
        }

        return letter.Label;
    }
}
