using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

public enum MainButtonPlacement
{
    Bar,
    Dropdown,
    Hidden
}

public sealed class MainButtonLayoutEntry : IExposable
{
    public string defName = string.Empty;
    public MainButtonPlacement placement = MainButtonPlacement.Bar;
    public MainButtonLook look = MainButtonLook.Unset;
    public string iconPath = string.Empty;
    public MainButtonDef? CachedDef;
    public float CacheWidth = -1f;
    public MainButtonLook CacheLook;
    public string CacheLabel = string.Empty;
    public string CacheDrawn = string.Empty;
    public float CacheTextLeft = -1f;
    public string CacheIconPath = string.Empty;

    public void ExposeData()
    {
        Scribe_Values.Look(ref defName, "defName", string.Empty);
        if (defName == null)
        {
            defName = string.Empty;
        }
        Scribe_Values.Look(ref placement, "placement", MainButtonPlacement.Bar);
        Scribe_Values.Look(ref look, "look", MainButtonLook.Unset);
        Scribe_Values.Look(ref iconPath, "iconPath", string.Empty);
        if (iconPath == null)
        {
            iconPath = string.Empty;
        }
    }
}

public static class MainButtonLayout
{
    public const string MoreId = "VUIP_More";

    public static int DropdownClosedOnFrame = -1;

    private static readonly List<MainButtonLayoutEntry> DropdownScratch = new List<MainButtonLayoutEntry>();
    private static readonly List<MainButtonLayoutEntry> BarSlots = new List<MainButtonLayoutEntry>();
    private static readonly List<bool> BarCompact = new List<bool>();
    private static readonly HashSet<string> KnownNames = new HashSet<string>();
    private static readonly Color SelectedFill = new Color(0.22f, 0.50f, 0.82f, 0.95f);
    private static readonly Color RowAlt = new Color(1f, 1f, 1f, 0.04f);
    private static readonly Color DropLine = new Color(0.45f, 0.78f, 1f, 1f);
    private static Rect moreRect;
    private static bool merged;
    private static int dragFrom = -1;
    private static string? moreLabel;
    private static string? moreTip;

    private const float SettingsRowH = 28f;
    private const float SettingsRowGap = 6f;

    public static void EnsureInitialized()
    {
        if (merged || DefDatabase<MainButtonDef>.DefCount == 0)
        {
            return;
        }
        List<MainButtonLayoutEntry> entries = UiPlusMod.Settings.mainButtons;
        if (entries == null)
        {
            entries = new List<MainButtonLayoutEntry>();
            UiPlusMod.Settings.mainButtons = entries;
        }

        KnownNames.Clear();
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            MainButtonLayoutEntry entry = entries[i];
            if (IsMore(entry))
            {
                KnownNames.Add(entry.defName);
                continue;
            }

            MainButtonDef? def = DefDatabase<MainButtonDef>.GetNamedSilentFail(entry.defName);
            if (def == null || IsInspect(def))
            {
                entries.RemoveAt(i);
                continue;
            }

            entry.CachedDef = def;
            KnownNames.Add(entry.defName);
        }

        ResolveLooks(entries);

        bool existingLayout = false;
        for (int i = 0; i < entries.Count; i++)
        {
            if (!IsMore(entries[i]))
            {
                existingLayout = true;
                break;
            }
        }

        List<MainButtonDef> all = DefDatabase<MainButtonDef>.AllDefsListForReading;
        List<MainButtonDef> added = new List<MainButtonDef>();
        for (int i = 0; i < all.Count; i++)
        {
            MainButtonDef def = all[i];
            if (IsInspect(def) || KnownNames.Contains(def.defName))
            {
                continue;
            }

            added.Add(def);
        }

        added.Sort(CompareVanillaOrder);
        for (int i = 0; i < added.Count; i++)
        {
            MainButtonDef def = added[i];
            MainButtonPlacement placement = MainButtonPlacement.Bar;
            if (!def.buttonVisible)
            {
                placement = MainButtonPlacement.Hidden;
            }
            else if (existingLayout)
            {
                placement = MainButtonPlacement.Dropdown;
            }

            entries.Add(new MainButtonLayoutEntry
            {
                defName = def.defName,
                placement = placement,
                look = def.Icon != null ? MainButtonLook.IconOnly : MainButtonLook.TextOnly,
                CachedDef = def
            });
        }

        merged = true;
        EnsureMoreEntry();
        if (existingLayout && added.Count > 0)
        {
            UiPlusMod.Instance.WriteSettings();
        }
    }

    public static void ResetToVanilla()
    {
        merged = false;
        UiPlusMod.Settings.mainButtons = new List<MainButtonLayoutEntry>();
        EnsureInitialized();
        CloseDropdown();
    }

    public static void DrawBar()
    {
        if (Event.current.type == EventType.Layout)
        {
            return;
        }

        EnsureInitialized();
        CollectVisible(DropdownScratch, MainButtonPlacement.Dropdown);
        bool showMore = DropdownScratch.Count > 0;
        BarSlots.Clear();
        BarCompact.Clear();
        List<MainButtonLayoutEntry> entries = UiPlusMod.Settings.mainButtons;
        float weight = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            MainButtonLayoutEntry entry = entries[i];
            if (IsMore(entry))
            {
                if (showMore)
                {
                    bool moreCompact = MainButtonPainter.Compact(null, entry);
                    BarSlots.Add(entry);
                    BarCompact.Add(moreCompact);
                    weight += moreCompact ? 0.5f : 1f;
                }

                continue;
            }

            if (entry.placement != MainButtonPlacement.Bar)
            {
                continue;
            }

            MainButtonDef? def = DefOf(entry);
            if (def == null || !ShouldDraw(def, entry.placement))
            {
                continue;
            }

            bool compact = MainButtonPainter.Compact(def, entry);
            BarSlots.Add(entry);
            BarCompact.Add(compact);
            weight += compact ? 0.5f : 1f;
        }

        if (weight <= 0f)
        {
            return;
        }

        GUI.color = Color.white;
        int unit = (int)(UI.screenWidth / weight);
        int half = unit / 2;
        int x = 0;
        for (int i = 0; i < BarSlots.Count; i++)
        {
            bool last = i == BarSlots.Count - 1;
            MainButtonLayoutEntry slot = BarSlots[i];
            MainButtonDef? def = IsMore(slot) ? null : DefOf(slot);
            int width = BarCompact[i] ? half : unit;
            if (last)
            {
                width = UI.screenWidth - x;
            }

            Rect rect = new Rect(x, UI.screenHeight - 35, width, 36f);
            if (IsMore(slot))
            {
                moreRect = rect;
                DrawMoreButton(slot, rect);
            }
            else if (def != null)
            {
                MainButtonPainter.DrawTab(def, slot, rect);
            }

            x += width;
        }
    }

    public static void DrawSettings(Listing_Standard list)
    {
        EnsureInitialized();
        EnsureMoreEntry();
        Text.Font = GameFont.Small;
        list.Label("VUIP.MainBarTip".Translate());
        list.Gap(4f);

        List<MainButtonLayoutEntry> entries = UiPlusMod.Settings.mainButtons;
        int count = entries.Count;
        const float stride = SettingsRowH + SettingsRowGap;
        Rect block = list.GetRect(Mathf.Max(0f, count * stride));
        if (dragFrom >= 0)
        {
            if (Event.current.rawType == EventType.MouseUp || Event.current.type == EventType.MouseUp)
            {
                Reorder(dragFrom, InsertIndex(block, stride, count));
                dragFrom = -1;
                if (Event.current.type != EventType.Used)
                {
                    Event.current.Use();
                }
            }
        }

        int insertAt = dragFrom >= 0 ? InsertIndex(block, stride, count) : -1;
        for (int i = 0; i < count; i++)
        {
            Rect row = new Rect(block.x, block.y + i * stride, block.width, SettingsRowH);
            DrawRow(row, entries[i], i);
        }

        if (dragFrom >= 0 && insertAt >= 0 && Event.current.type == EventType.Repaint)
        {
            float y = block.y + insertAt * stride - SettingsRowGap / 2f;
            if (insertAt == 0)
            {
                y = block.y;
            }

            Widgets.DrawBoxSolid(new Rect(block.x, y - 1.5f, block.width, 3f), DropLine);
        }
    }

    private static void DrawRow(Rect row, MainButtonLayoutEntry entry, int index)
    {
        bool more = IsMore(entry);
        MainButtonDef? def = more ? null : DefOf(entry);
        if (!more && def == null)
        {
            return;
        }

        if (index % 2 == 1)
        {
            Widgets.DrawBoxSolid(row, RowAlt);
        }

        if (dragFrom == index)
        {
            Widgets.DrawBoxSolid(row, new Color(0.45f, 0.78f, 1f, 0.18f));
        }
        else if (dragFrom < 0)
        {
            Widgets.DrawHighlightIfMouseover(row);
        }

        const float gripW = 14f;
        const float iconW = 24f;
        const float chipH = 22f;
        const float lookW = 126f;
        const float placeW = 150f;
        const float groupGap = 6f;
        float chipY = row.y + (row.height - chipH) / 2f;
        Rect place = new Rect(row.xMax - placeW, chipY, placeW, chipH);
        Rect look = new Rect(place.x - lookW - groupGap, chipY, lookW, chipH);
        Rect grip = new Rect(row.x + 2f, chipY, gripW, chipH);
        Rect icon = new Rect(grip.xMax + 4f, row.y + (row.height - iconW) / 2f, iconW, iconW);
        Rect label = new Rect(icon.xMax + 6f, row.y, Mathf.Max(24f, look.x - 8f - (icon.xMax + 6f)), row.height);

        DrawGrip(grip);
        DrawIconButton(icon, def, entry);

        Color old = GUI.color;
        if (!more && entry.placement == MainButtonPlacement.Hidden)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
        }

        Text.Anchor = TextAnchor.MiddleLeft;
        Text.WordWrap = false;
        Widgets.Label(label, more ? "VUIP.MainBarMore".Translate() : def!.LabelCap);
        Text.WordWrap = true;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = old;

        if (more)
        {
            TooltipHandler.TipRegion(label, "VUIP.MainBarMoreRowTip".Translate());
        }
        else if (!def!.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(label, def.description);
        }

        DrawLookChoices(look, entry, def);
        if (!more)
        {
            DrawPlaceChoices(place, entry);
        }

        if (dragFrom < 0)
        {
            Rect gripHit = new Rect(grip.x, row.y, grip.width + 2f, row.height);
            if (Widgets.ButtonInvisibleDraggable(gripHit) == Widgets.DraggableResult.Dragged
                || Widgets.ButtonInvisibleDraggable(label) == Widgets.DraggableResult.Dragged)
            {
                dragFrom = index;
            }
        }
    }

    private static void DrawGrip(Rect rect)
    {
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.4f);
        float cx = rect.center.x;
        float cy = rect.center.y;
        for (int i = -1; i <= 1; i++)
        {
            Widgets.DrawLineHorizontal(cx - 4f, cy + i * 3.5f, 8f);
        }

        GUI.color = old;
        TooltipHandler.TipRegion(rect, "VUIP.MainBarDragTip".Translate());
    }

    private static void DrawLookChoices(Rect rect, MainButtonLayoutEntry entry, MainButtonDef? def)
    {
        MainButtonLook look = MainButtonPainter.ResolvedLook(def, entry);
        DrawSegment(
            new Rect(rect.x, rect.y, rect.width / 3f, rect.height),
            "VUIP.MainBarLookIcon".Translate(),
            "VUIP.MainBarLookIconTip".Translate(),
            look == MainButtonLook.IconOnly,
            () => SetLook(entry, MainButtonLook.IconOnly));
        DrawSegment(
            new Rect(rect.x + rect.width / 3f, rect.y, rect.width / 3f, rect.height),
            "VUIP.MainBarLookBoth".Translate(),
            "VUIP.MainBarLookBothTip".Translate(),
            look == MainButtonLook.TextAndIcon,
            () => SetLook(entry, MainButtonLook.TextAndIcon));
        DrawSegment(
            new Rect(rect.x + rect.width * 2f / 3f, rect.y, rect.width - rect.width * 2f / 3f, rect.height),
            "VUIP.MainBarLookText".Translate(),
            "VUIP.MainBarLookTextTip".Translate(),
            look == MainButtonLook.TextOnly,
            () => SetLook(entry, MainButtonLook.TextOnly));
        Widgets.DrawBox(rect);
    }

    private static void DrawPlaceChoices(Rect rect, MainButtonLayoutEntry entry)
    {
        float w = rect.width / 3f;
        DrawSegment(
            new Rect(rect.x, rect.y, w, rect.height),
            "VUIP.MainBarOnBar".Translate(),
            "VUIP.MainBarOnBarTip".Translate(),
            entry.placement == MainButtonPlacement.Bar,
            () => SetPlacement(entry, MainButtonPlacement.Bar));
        DrawSegment(
            new Rect(rect.x + w, rect.y, w, rect.height),
            "VUIP.MainBarDropdown".Translate(),
            "VUIP.MainBarDropdownTip".Translate(),
            entry.placement == MainButtonPlacement.Dropdown,
            () => SetPlacement(entry, MainButtonPlacement.Dropdown));
        bool hidden = entry.placement == MainButtonPlacement.Hidden;
        DrawSegment(
            new Rect(rect.x + w * 2f, rect.y, rect.width - w * 2f, rect.height),
            hidden ? "VUIP.MainBarShow".Translate() : "VUIP.MainBarHidden".Translate(),
            hidden ? "VUIP.MainBarShowTip".Translate() : "VUIP.MainBarHiddenTip".Translate(),
            hidden,
            () => SetPlacement(entry, hidden ? MainButtonPlacement.Bar : MainButtonPlacement.Hidden));
        Widgets.DrawBox(rect);
    }

    private static void DrawSegment(Rect rect, string label, string tip, bool selected, System.Action onClick)
    {
        if (selected)
        {
            Widgets.DrawBoxSolid(rect, SelectedFill);
        }

        Widgets.DrawHighlightIfMouseover(rect);
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, label);
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        TooltipHandler.TipRegion(rect, tip);
        if (dragFrom < 0 && Widgets.ButtonInvisible(rect))
        {
            onClick();
        }
    }

    private static void DrawIconButton(Rect rect, MainButtonDef? def, MainButtonLayoutEntry entry)
    {
        Texture2D? tex = MainButtonPainter.ResolveIcon(def, entry);
        Widgets.DrawBox(rect);
        if (tex != null)
        {
            GUI.DrawTexture(rect.ContractedBy(2f), tex);
        }
        else
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(rect, "▾");
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        Widgets.DrawHighlightIfMouseover(rect);
        TooltipHandler.TipRegion(rect, "VUIP.MainBarIconTip".Translate());
        if (dragFrom < 0 && Widgets.ButtonInvisible(rect))
        {
            Find.WindowStack.Add(new MainButtonIconPicker(entry));
        }
    }

    private static void DrawMoreButton(MainButtonLayoutEntry entry, Rect rect)
    {
        bool open = Find.WindowStack.IsOpen<MainButtonDropdownWindow>();
        if (open)
        {
            Widgets.DrawHighlight(rect);
        }

        moreLabel ??= "VUIP.MainBarMore".Translate();
        MainButtonPainter.Draw(
            rect,
            entry,
            null,
            moreLabel,
            "▾",
            disabled: false,
            barPercent: 0f,
            () => ToggleMore(open));
        if (!Mouse.IsOver(rect))
        {
            return;
        }

        moreTip ??= "VUIP.MainBarMoreTip".Translate();
        TooltipHandler.TipRegion(rect, moreTip);
    }

    private static void ToggleMore(bool open)
    {
        if (open || DropdownClosedOnFrame == Time.frameCount)
        {
            CloseDropdown();
            return;
        }

        List<MainButtonLayoutEntry> snapshot = new List<MainButtonLayoutEntry>(DropdownScratch);
        Find.WindowStack.Add(new MainButtonDropdownWindow(snapshot, moreRect));
    }

    private static void CollectVisible(List<MainButtonLayoutEntry> into, MainButtonPlacement placement)
    {
        into.Clear();
        List<MainButtonLayoutEntry> entries = UiPlusMod.Settings.mainButtons;
        for (int i = 0; i < entries.Count; i++)
        {
            MainButtonLayoutEntry entry = entries[i];
            if (IsMore(entry) || entry.placement != placement)
            {
                continue;
            }

            MainButtonDef? def = DefOf(entry);
            if (def == null || !ShouldDraw(def, entry.placement))
            {
                continue;
            }

            into.Add(entry);
        }
    }

    private static bool ShouldDraw(MainButtonDef def, MainButtonPlacement placement)
    {
        if (def.Worker.Visible)
        {
            return true;
        }

        return !def.buttonVisible && placement != MainButtonPlacement.Hidden;
    }

    private static void SetPlacement(MainButtonLayoutEntry entry, MainButtonPlacement placement)
    {
        if (entry.placement == placement)
        {
            return;
        }

        entry.placement = placement;
        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        CloseDropdown();
        UiPlusMod.Instance.WriteSettings();
    }

    private static void SetLook(MainButtonLayoutEntry entry, MainButtonLook look)
    {
        if (entry.look == look)
        {
            return;
        }

        entry.look = look;
        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        CloseDropdown();
        UiPlusMod.Instance.WriteSettings();
    }

    private static MainButtonDef? DefOf(MainButtonLayoutEntry entry)
    {
        if (entry.CachedDef != null)
        {
            return entry.CachedDef;
        }

        entry.CachedDef = DefDatabase<MainButtonDef>.GetNamedSilentFail(entry.defName);
        return entry.CachedDef;
    }

    private static int InsertIndex(Rect block, float stride, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        int to = Mathf.RoundToInt((Event.current.mousePosition.y - block.y) / stride);
        return Mathf.Clamp(to, 0, count);
    }

    private static void Reorder(int from, int to)
    {
        List<MainButtonLayoutEntry> entries = UiPlusMod.Settings.mainButtons;
        if (from < 0 || from >= entries.Count || to < 0 || to > entries.Count || from == to || from + 1 == to)
        {
            return;
        }

        MainButtonLayoutEntry item = entries[from];
        entries.Insert(to, item);
        entries.RemoveAt(from < to ? from : from + 1);
        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        CloseDropdown();
        UiPlusMod.Instance.WriteSettings();
    }

    private static void EnsureMoreEntry()
    {
        List<MainButtonLayoutEntry> entries = UiPlusMod.Settings.mainButtons;
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (IsMore(entries[i]))
            {
                return;
            }
        }

        entries.Add(new MainButtonLayoutEntry
        {
            defName = MoreId,
            placement = MainButtonPlacement.Bar,
            look = MainButtonLook.TextAndIcon
        });
    }

    private static void ResolveLooks(List<MainButtonLayoutEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            MainButtonLayoutEntry entry = entries[i];
            if (entry.look != MainButtonLook.Unset)
            {
                continue;
            }

            if (IsMore(entry))
            {
                entry.look = MainButtonLook.TextAndIcon;
                continue;
            }

            MainButtonDef? def = DefOf(entry);
            entry.look = def?.Icon != null ? MainButtonLook.IconOnly : MainButtonLook.TextOnly;
        }
    }

    private static bool IsMore(MainButtonLayoutEntry entry)
    {
        return entry.defName == MoreId;
    }

    public static void CloseDropdown()
    {
        Find.WindowStack?.TryRemove(typeof(MainButtonDropdownWindow), doCloseSound: false);
    }

    private static bool IsInspect(MainButtonDef def)
    {
        return def.defName == "Inspect" || def.tabWindowClass == typeof(MainTabWindow_Inspect);
    }

    private static int CompareVanillaOrder(MainButtonDef a, MainButtonDef b)
    {
        int cmp = a.order.CompareTo(b.order);
        if (cmp != 0)
        {
            return cmp;
        }

        return string.CompareOrdinal(a.defName, b.defName);
    }
}
