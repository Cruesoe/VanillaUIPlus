using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

public enum MainButtonLook
{
    // Read from older settings only; loaded as TextAndIcon.
    Unset,
    IconOnly,
    TextAndIcon,
    TextOnly
}

// The label measurement for one button, reused until its width, look, label or icon changes.
public sealed class MainButtonDrawCache
{
    public float Width = -1f;
    public MainButtonLook Look;
    public string Label = string.Empty;
    public string Drawn = string.Empty;
    public float TextLeft = -1f;
    public string IconPath = string.Empty;
}

public static class MainButtonPainter
{
    public const string NonePath = "none";
    public const string ExtraIconFolder = "UI/Icons/MainButtons";
    private const float IconSize = 32f;
    private static readonly Dictionary<string, Texture2D?> Cache = new Dictionary<string, Texture2D?>();
    private static readonly Dictionary<string, string> TipCache = new Dictionary<string, string>();
    private static List<string>? iconPaths;

    // An empty path means the def's own icon; "none" means no icon.
    public static Texture2D? ResolveIcon(MainButtonDef? def, MainButtonLayoutEntry entry)
    {
        return entry.iconPath.NullOrEmpty() ? def?.Icon : LoadPath(entry.iconPath);
    }

    public static bool Compact(MainButtonDef? def, MainButtonLayoutEntry entry)
    {
        return entry.look == MainButtonLook.IconOnly && ResolveIcon(def, entry) != null;
    }

    public static void DrawTab(MainButtonDef def, MainButtonLayoutEntry entry, Rect rect)
    {
        Draw(
            rect,
            entry,
            def,
            def.LabelCap,
            def.ShortenedLabelCap,
            def.Worker.Disabled,
            def.Worker.ButtonBarPercent,
            null,
            def);
        if (Find.MainTabsRoot.OpenTab != def && !Find.WindowStack.NonImmediateDialogWindowOpen)
        {
            UIHighlighter.HighlightOpportunity(rect, def.cachedHighlightTagClosed);
        }

        if (Mouse.IsOver(rect) && !def.description.NullOrEmpty())
        {
            if (!TipCache.TryGetValue(def.defName, out string tip))
            {
                tip = def.LabelCap.Colorize(ColorLibrary.Yellow) + "\n\n" + def.description;
                TipCache[def.defName] = tip;
            }

            TooltipHandler.TipRegion(rect, tip);
        }
    }

    public static void Draw(
        Rect rect,
        MainButtonLayoutEntry entry,
        MainButtonDef? def,
        string label,
        string shortened,
        bool disabled,
        float barPercent,
        Action? onClick,
        MainButtonDef? activate = null)
    {
        Text.Font = GameFont.Small;
        Texture2D? icon = ResolveIcon(def, entry);
        MainButtonLook look = entry.look;
        bool showIcon = look != MainButtonLook.TextOnly && icon != null;
        bool showText = look != MainButtonLook.IconOnly || !showIcon;
        if (disabled)
        {
            Widgets.DrawAtlas(rect, Widgets.ButtonSubtleAtlas);
            if (Event.current.type == EventType.MouseDown && Mouse.IsOver(rect))
            {
                Event.current.Use();
            }

            return;
        }

        string drawnLabel = "";
        float textLeft = -1f;
        if (showText)
        {
            MainButtonDrawCache cache = entry.DrawCache;
            if (cache.Width != rect.width || cache.Look != look || cache.Label != label || cache.IconPath != entry.iconPath)
            {
                if (!showIcon)
                {
                    drawnLabel = Text.CalcSize(label).x > rect.width - 2f ? shortened : label;
                    textLeft = Text.CalcSize(drawnLabel).x > 0.85f * rect.width - 1f ? 2f : -1f;
                }
                else
                {
                    float iconLeft = rect.width * 0.1f;
                    textLeft = iconLeft + IconSize + 4f;
                    float maxWidth = Mathf.Max(0f, rect.width - textLeft - 4f);
                    drawnLabel = Text.CalcSize(label).x > maxWidth ? shortened : label;
                    if (Text.CalcSize(drawnLabel).x > maxWidth)
                    {
                        drawnLabel = "";
                    }
                }

                cache.Width = rect.width;
                cache.Look = look;
                cache.Label = label;
                cache.Drawn = drawnLabel;
                cache.TextLeft = textLeft;
                cache.IconPath = entry.iconPath;
            }
            else
            {
                drawnLabel = cache.Drawn;
                textLeft = cache.TextLeft;
            }
        }

        if (Widgets.ButtonTextSubtle(rect, drawnLabel, barPercent, textLeft, SoundDefOf.Mouseover_Category))
        {
            if (activate != null)
            {
                activate.Worker.InterfaceTryActivate();
            }
            else
            {
                onClick?.Invoke();
            }
        }

        if (!showIcon)
        {
            return;
        }

        Vector2 pos = rect.center - new Vector2(IconSize / 2f, IconSize / 2f);
        if (showText && drawnLabel != "")
        {
            pos.x = rect.x + rect.width * 0.1f;
        }

        if (Mouse.IsOver(rect))
        {
            pos += new Vector2(2f, -2f);
        }

        GUI.DrawTexture(new Rect(pos.x, pos.y, IconSize, IconSize), icon);
    }

    public static List<string> IconPaths()
    {
        if (iconPaths != null)
        {
            return iconPaths;
        }

        iconPaths = new List<string>();
        HashSet<string> seen = new HashSet<string>();
        List<MainButtonDef> defs = DefDatabase<MainButtonDef>.AllDefsListForReading;
        for (int i = 0; i < defs.Count; i++)
        {
            string? path = defs[i].iconPath;
            if (path.NullOrEmpty() || !seen.Add(path) || LoadPath(path) == null)
            {
                continue;
            }

            iconPaths.Add(path);
        }

        AddFolder(ExtraIconFolder, seen, iconPaths);
        return iconPaths;
    }

    public static Texture2D? LoadPath(string path)
    {
        if (path.NullOrEmpty() || path == NonePath)
        {
            return null;
        }

        if (!Cache.TryGetValue(path, out Texture2D? tex))
        {
            tex = ContentFinder<Texture2D>.Get(path, reportFailure: false);
            Cache[path] = tex;
        }

        return tex;
    }

    private static void AddFolder(string folder, HashSet<string> seen, List<string> into)
    {
        try
        {
            foreach (Texture2D tex in ContentFinder<Texture2D>.GetAllInFolder(folder))
            {
                if (tex == null || tex.name.NullOrEmpty())
                {
                    continue;
                }

                string path = folder + "/" + tex.name;
                if (seen.Add(path))
                {
                    into.Add(path);
                    Cache[path] = tex;
                }
            }
        }
        catch (Exception exception)
        {
            Log.Warning($"[Vanilla UI+] Could not list the icons in {folder}; the icon picker shows only the game's own icons.\n{exception}");
        }
    }
}
