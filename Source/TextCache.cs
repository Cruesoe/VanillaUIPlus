using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Truncation, wrapped-height, width and translation caches for GameFont.Small; cleared on UI scale or language change and capped in size.
/// </summary>
public static class TextCache
{
    private const int MaxEntries = 512;

    private static readonly Dictionary<(int Width, string Text), string> Truncated =
        new Dictionary<(int, string), string>();

    private static readonly Dictionary<(int Width, string Text), float> WrappedHeights =
        new Dictionary<(int, string), float>();

    private static readonly Dictionary<string, float> Widths = new Dictionary<string, float>();
    private static readonly Dictionary<string, string> Translations = new Dictionary<string, string>();

    private static float cachedScale = -1f;
    private static LoadedLanguage? cachedLanguage;

    // Text.CalcSize(text).x in the Small font, cached per text.
    public static float Width(string text)
    {
        EnsureFresh();
        if (Widths.TryGetValue(text, out float cached))
        {
            return cached;
        }

        GameFont font = Text.Font;
        Text.Font = GameFont.Small;
        float result = Text.CalcSize(text).x;
        Text.Font = font;
        if (Widths.Count >= MaxEntries)
        {
            Widths.Clear();
        }

        Widths[text] = result;
        return result;
    }

    // A keyed string with no arguments, translated once per language.
    public static string Key(string key)
    {
        EnsureFresh();
        if (!Translations.TryGetValue(key, out string cached))
        {
            cached = key.Translate();
            Translations[key] = cached;
        }

        return cached;
    }

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
        LoadedLanguage language = LanguageDatabase.activeLanguage;
        if (scale == cachedScale && language == cachedLanguage)
        {
            return;
        }

        cachedScale = scale;
        cachedLanguage = language;
        Truncated.Clear();
        WrappedHeights.Clear();
        Widths.Clear();
        Translations.Clear();
    }
}
