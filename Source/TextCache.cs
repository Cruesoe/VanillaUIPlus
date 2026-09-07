using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Width-aware caches for the two text measurements the HUD repeats every frame.
///
/// <see cref="GenText.Truncate(string, float, Dictionary{string, string})"/> keys its own
/// cache by the string alone, so one dictionary shared between call sites of different
/// widths hands back whichever truncation happened to be computed first. The bars are
/// 172px, the alert stack 166px and the right-hand column of a split bar 124px or 86px,
/// so a label common to two of them (a game condition and an alert often share one) came
/// back measured for the wrong column. Keying on the width as well keeps them apart.
///
/// Every caller draws in <see cref="GameFont.Small"/>, so the font is not part of the key.
/// The caches are dropped when the UI scale changes, since every entry was measured at
/// the old scale, and capped so a long session cannot accumulate an entry for each label
/// a counter has ever produced.
/// </summary>
public static class TextCache
{
    private const int MaxEntries = 512;

    private static readonly Dictionary<(int Width, string Text), string> Truncated =
        new Dictionary<(int, string), string>();

    private static readonly Dictionary<(int Width, string Text), float> WrappedHeights =
        new Dictionary<(int, string), float>();

    private static float cachedScale = -1f;

    /// <summary>
    /// <see cref="GenText.Truncate(string, float, Dictionary{string, string})"/> with a
    /// cache that accounts for the width the text is measured against.
    /// </summary>
    public static string Truncate(string? text, float width)
    {
        if (text.NullOrEmpty())
        {
            return string.Empty;
        }

        EnsureFresh();
        (int, string) key = (Mathf.RoundToInt(width), text!);
        if (Truncated.TryGetValue(key, out string cached))
        {
            return cached;
        }

        // No cache passed to the game's own call: this dictionary is the cache.
        string result = GenText.Truncate(text!, width, null);
        Store(Truncated, key, result);
        return result;
    }

    /// <summary>
    /// <see cref="Text.CalcHeight"/> for wrapped label text. Vanilla measures alert
    /// heights afresh on every access and the stack is measured several times a frame,
    /// so the result is worth keeping between frames.
    /// </summary>
    public static float WrappedHeight(string? text, float width)
    {
        if (text.NullOrEmpty())
        {
            return Text.LineHeight;
        }

        EnsureFresh();
        (int, string) key = (Mathf.RoundToInt(width), text!);
        if (WrappedHeights.TryGetValue(key, out float cached))
        {
            return cached;
        }

        float result = Text.CalcHeight(text!, width);
        Store(WrappedHeights, key, result);
        return result;
    }

    private static void Store<T>(Dictionary<(int, string), T> cache, (int, string) key, T value)
    {
        if (cache.Count >= MaxEntries)
        {
            cache.Clear();
        }

        cache[key] = value;
    }

    private static void EnsureFresh()
    {
        float scale = Prefs.UIScale;
        if (scale == cachedScale)
        {
            return;
        }

        cachedScale = scale;
        Truncated.Clear();
        WrappedHeights.Clear();
    }
}
