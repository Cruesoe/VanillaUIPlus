using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Truncation and wrapped-height caches keyed by text and width, for GameFont.Small; cleared on UI scale change and capped in size.
/// </summary>
public static class TextCache
{
    private const int MaxEntries = 512;

    private static readonly Dictionary<(int Width, string Text), string> Truncated =
        new Dictionary<(int, string), string>();

    private static readonly Dictionary<(int Width, string Text), float> WrappedHeights =
        new Dictionary<(int, string), float>();

    private static float cachedScale = -1f;

    // GenText.Truncate, cached per width; the game's own cache is keyed by text alone.
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

    // Text.CalcHeight for wrapped label text, cached per width.
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
