using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Text for the inspect pane readouts: the Small font at the list view's text size, or the plain Small font in card view.
/// Widths and truncations are cached per size and cleared when the size, UI scale or language changes.
/// </summary>
public static class ReadoutFont
{
    public const int MinSize = 10;
    public const int MaxSize = 15;
    public const int DefaultSize = 12;
    private const int MaxEntries = 512;
    private const float FallbackBaseSize = 14f;

    private static readonly Dictionary<string, float> Widths = new Dictionary<string, float>();
    private static readonly Dictionary<(int Width, string Text), string> Truncated = new Dictionary<(int, string), string>();
    private static readonly Dictionary<(int Width, string Text), float> WrappedHeights = new Dictionary<(int, string), float>();
    private static readonly GUIContent Content = new GUIContent();

    private static GUIStyle? left;
    private static GUIStyle? right;
    private static GUIStyle? center;
    private static GUIStyle? wrapped;
    private static float lineHeight;
    private static int builtSize = -1;
    private static bool builtList;
    private static float builtScale = -1f;
    private static LoadedLanguage? builtLanguage;
    private static Vector2 scrollPos;

    /// <summary>Changes whenever the font in use changes, so callers can drop measurements taken in the old one.</summary>
    public static int Version { get; private set; }

    public static bool ListView => UiPlusMod.Settings.inspectListView;

    /// <summary>The Small font's own size in pixels.</summary>
    public static int BaseSize
    {
        get
        {
            GameFont font = Text.Font;
            Text.Font = GameFont.Small;
            GUIStyle style = Text.CurFontStyle;
            Text.Font = font;
            float size = style.fontSize > 0 ? style.fontSize : style.font != null && style.font.fontSize > 0 ? style.font.fontSize : FallbackBaseSize;
            return Mathf.RoundToInt(size);
        }
    }

    /// <summary>Text size in pixels in use now: the list view's setting, or the Small font's own size in card view.</summary>
    public static int Size => ListView ? UiPlusMod.Settings.inspectTextSize : BaseSize;

    public static float LineHeight
    {
        get
        {
            Ensure();
            return ListView ? lineHeight : Text.LineHeightOf(GameFont.Small);
        }
    }

    /// <summary>Square icon size for list rows: the text's pixel size, so icons stand no taller than the text.</summary>
    public static float IconSize => Mathf.Min(Size, LineHeight - 2f);

    public static float Width(string text)
    {
        if (!ListView)
        {
            return TextCache.Width(text);
        }

        Ensure();
        if (Widths.TryGetValue(text, out float cached))
        {
            return cached;
        }

        Content.text = text;
        float result = left!.CalcSize(Content).x;
        if (Widths.Count >= MaxEntries)
        {
            Widths.Clear();
        }

        Widths[text] = result;
        return result;
    }

    // Cut to fit the width with "...", as GenText.Truncate does for the Small font.
    public static string Truncate(string? text, float width)
    {
        if (!ListView)
        {
            return TextCache.Truncate(text, width);
        }

        if (text.NullOrEmpty())
        {
            return string.Empty;
        }

        Ensure();
        (int, string) key = (Mathf.RoundToInt(width), text!);
        if (Truncated.TryGetValue(key, out string? cached))
        {
            return cached;
        }

        string result = text!;
        if (Width(result) > width)
        {
            int length = result.Length;
            do
            {
                length--;
                result = text!.Substring(0, length) + "...";
            }
            while (length > 0 && Width(result) > width);
        }

        if (Truncated.Count >= MaxEntries)
        {
            Truncated.Clear();
        }

        Truncated[key] = result;
        return result;
    }

    // Height of the text wrapped to the width.
    public static float WrappedHeight(string text, float width)
    {
        if (!ListView)
        {
            GameFont font = Text.Font;
            Text.Font = GameFont.Small;
            float height = Text.CalcHeight(text, width);
            Text.Font = font;
            return height;
        }

        Ensure();
        (int, string) key = (Mathf.RoundToInt(width), text);
        if (WrappedHeights.TryGetValue(key, out float cached))
        {
            return cached;
        }

        Content.text = text;
        float result = wrapped!.CalcHeight(Content, width);
        if (WrappedHeights.Count >= MaxEntries)
        {
            WrappedHeights.Clear();
        }

        WrappedHeights[key] = result;
        return result;
    }

    // One line of text, cut to fit the rect.
    public static void Label(Rect rect, string text, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        string shown = Truncate(text, Mathf.Max(0f, rect.width));
        GUI.color = color;
        if (ListView)
        {
            Ensure();
            GUI.Label(rect, shown, anchor == TextAnchor.MiddleRight ? right : anchor == TextAnchor.MiddleCenter ? center : left);
        }
        else
        {
            Text.Font = GameFont.Small;
            Text.Anchor = anchor;
            Text.WordWrap = false;
            Widgets.Label(rect, shown);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
        }

        GUI.color = Color.white;
    }

    // The inspect text left under a readout, scrolling when it is taller than the rect, as InspectPaneFiller.DrawInspectString does.
    public static void InspectText(string text, Rect rect)
    {
        if (!ListView)
        {
            InspectPaneFiller.DrawInspectString(text, rect);
            return;
        }

        // The shared Small style is sized only for this call, so the base game's scrolling label measures and draws at the list size.
        Text.Font = GameFont.Small;
        GUIStyle style = Text.CurFontStyle;
        int previous = style.fontSize;
        style.fontSize = Size;
        try
        {
            Widgets.LabelScrollable(rect, text, ref scrollPos, dontConsumeScrollEventsIfNoScrollbar: true);
        }
        finally
        {
            style.fontSize = previous;
        }
    }

    // Rebuilds the styles when the size, UI scale or language changes.
    private static void Ensure()
    {
        int size = Size;
        float scale = Prefs.UIScale;
        LoadedLanguage language = LanguageDatabase.activeLanguage;
        bool list = ListView;
        if (size == builtSize && scale == builtScale && language == builtLanguage && list == builtList && left != null)
        {
            return;
        }

        builtList = list;
        builtSize = size;
        builtScale = scale;
        builtLanguage = language;
        Widths.Clear();
        Truncated.Clear();
        WrappedHeights.Clear();
        GameFont font = Text.Font;
        Text.Font = GameFont.Small;
        GUIStyle baseStyle = Text.CurFontStyle;
        Text.Font = font;
        left = new GUIStyle(baseStyle) { fontSize = size, alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip };
        right = new GUIStyle(left) { alignment = TextAnchor.MiddleRight };
        center = new GUIStyle(left) { alignment = TextAnchor.MiddleCenter };
        wrapped = new GUIStyle(left) { alignment = TextAnchor.UpperLeft, wordWrap = true };
        Content.text = "W";
        lineHeight = Mathf.Ceil(left.CalcHeight(Content, 999f));
        Version++;
    }

    /// <summary>Current version after checking the font in use, for callers that cache measurements.</summary>
    public static int CurrentVersion
    {
        get
        {
            Ensure();
            return Version;
        }
    }
}
