using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public class SavedWorldSearch : IExposable
{
    public string name = "";
    public CriterionGroup root = new CriterionGroup(CriterionGroupMode.All);

    public void ExposeData()
    {
        Scribe_Values.Look(ref name, "name", "");
        Scribe_Deep.Look(ref root, "root");
        root ??= new CriterionGroup(CriterionGroupMode.All);
        root.mode = CriterionGroupMode.All;
    }
}

/// <summary>
/// The advanced world search: conditions dragged from a palette into an all-of list, with nested groups. Shown left of the results.
/// </summary>
public static class WorldQueryPanel
{
    public const float ColumnWidth = 290f;
    public const float ColumnGap = 10f;
    public const float Width = ColumnWidth * 2f + ColumnGap * 2f;

    private const float HeaderHeight = 26f;
    private const float Pad = 4f;
    private const float GroupIndent = 8f;
    private const float EmptyGroupHeight = 26f;
    private const float PaletteRowHeight = 28f;
    private const float IconSize = 20f;
    private const float DragThreshold = 4f;

    private static readonly Color DropLineColor = new Color(1f, 1f, 1f, 0.7f);
    private static readonly Color RemoveTint = new Color(1f, 0.3f, 0.3f, 0.12f);
    private static readonly Color DraggedSourceTint = new Color(0f, 0f, 0f, 0.45f);

    public static bool Open;
    public static bool Paused;
    private static CriterionGroup root = new CriterionGroup(CriterionGroupMode.All);
    private static bool changed;

    private static readonly QuickSearchWidget paletteSearch = new QuickSearchWidget();
    private static List<WorldCriterion>? palette;
    private static Vector2 queryScroll;
    private static Vector2 paletteScroll;

    private static WorldCriterion? pressed;
    private static bool pressedFromPalette;
    private static Vector2 pressScreen;
    private static WorldCriterion? dragging;
    private static bool dragFromPalette;
    private static readonly List<DropZone> zones = new List<DropZone>();
    private static Rect queryScreenRect;
    private static Texture2D? savedIcon;

    private sealed class DropZone
    {
        public CriterionGroup? Group;
        public Rect ScreenRect;
        public readonly List<Rect> ChildScreenRects = new List<Rect>();
    }

    public static bool Available => UiPlusMod.Settings.worldSearchAllTiles && UiPlusMod.Settings.worldSearchAdvanced;

    public static bool Active => Available && Open && !Paused && root.children.Count > 0;

    private static Texture2D SavedIcon => savedIcon ??= ContentFinder<Texture2D>.Get(MainButtonPainter.ExtraIconFolder + "/folder-open", reportFailure: false)
        ?? TexButton.Save;

    public static bool Matches(Tile tile)
    {
        return root.Matches(tile);
    }

    /// <summary>Marks the query changed; the open search restarts on its next draw.</summary>
    public static void NotifyChanged()
    {
        changed = true;
    }

    public static bool ConsumeChange()
    {
        bool was = changed;
        changed = false;
        return was;
    }

    public static void Draw(Rect rect)
    {
        zones.Clear();
        Text.Font = GameFont.Small;
        Rect queryRect = new Rect(rect.x, rect.y, ColumnWidth, rect.height);
        Rect paletteRect = new Rect(queryRect.xMax + ColumnGap, rect.y, ColumnWidth, rect.height);
        DrawQueryColumn(queryRect);
        DrawPaletteColumn(paletteRect);
        HandleDrag();
    }

    private static void DrawQueryColumn(Rect rect)
    {
        Rect header = new Rect(rect.x, rect.y, rect.width, 24f);
        Rect icon = new Rect(header.xMax - IconSize, header.y + 2f, IconSize, IconSize);
        if (IconButton(icon, TexButton.Search, (Paused ? "VUIP.WorldQueryResume" : "VUIP.WorldQueryPause").Translate(), Paused ? Widgets.InactiveColor : Color.white))
        {
            Paused = !Paused;
            NotifyChanged();
        }

        icon.x -= IconSize + 4f;
        if (IconButton(icon.ContractedBy(2f), TexButton.CloseXSmall, "VUIP.WorldQueryClear".Translate()) && root.children.Count > 0)
        {
            root.children.Clear();
            NotifyChanged();
        }

        icon.x -= IconSize + 4f;
        if (IconButton(icon, SavedIcon, "VUIP.WorldQuerySaved".Translate()))
        {
            OpenSavedMenu();
        }

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(header.x, header.y, icon.x - header.x - 4f, header.height), "VUIP.WorldQueryHeader".Translate());
        Text.Anchor = TextAnchor.UpperLeft;

        Rect outRect = new Rect(rect.x, header.yMax + 4f, rect.width, rect.yMax - header.yMax - 4f);
        Widgets.DrawMenuSection(outRect);
        queryScreenRect = ToScreen(outRect);
        DropZone rootZone = new DropZone { Group = root, ScreenRect = queryScreenRect };
        zones.Add(rootZone);

        Rect inner = outRect.ContractedBy(Pad);
        float height = ChildrenHeight(root);
        bool scrollbar = height > inner.height;
        Rect view = new Rect(0f, 0f, inner.width - (scrollbar ? 16f : 0f), Mathf.Max(height, inner.height));
        Widgets.BeginScrollView(inner, ref queryScroll, view);
        if (root.children.Count == 0)
        {
            DrawHint(view, "VUIP.WorldQueryEmpty".Translate());
        }
        else
        {
            DrawChildren(root, new Rect(0f, 0f, view.width, height), rootZone);
        }

        Widgets.EndScrollView();
    }

    private static void DrawChildren(CriterionGroup group, Rect rect, DropZone zone)
    {
        float y = rect.y;
        foreach (WorldCriterion child in group.children.ToList())
        {
            Rect childRect = new Rect(rect.x, y, rect.width, Height(child));
            zone.ChildScreenRects.Add(ToScreen(childRect));
            DrawCriterion(childRect, child, group);
            y = childRect.yMax + Pad;
        }
    }

    private static void DrawCriterion(Rect rect, WorldCriterion criterion, CriterionGroup parent)
    {
        Widgets.DrawMenuSection(rect);
        Rect header = new Rect(rect.x + Pad, rect.y + 1f, rect.width - Pad * 2f, HeaderHeight);
        Rect remove = new Rect(header.xMax - 16f, header.y + 5f, 16f, 16f);
        if (Widgets.ButtonImage(remove, TexButton.CloseXSmall))
        {
            parent.children.Remove(criterion);
            NotifyChanged();
            return;
        }

        Rect handle = new Rect(header.x, header.y, remove.x - header.x - 4f, header.height);
        DrawLabel(handle, criterion);
        Widgets.DrawHighlightIfMouseover(handle);
        TooltipHandler.TipRegion(handle, "VUIP.WorldQueryDragTip".Translate());
        TryPress(handle, criterion, fromPalette: false);

        float y = header.yMax;
        if (criterion.ControlsHeight > 0f)
        {
            Rect controls = new Rect(rect.x + Pad, y, rect.width - Pad * 2f, criterion.ControlsHeight);
            bool edited = false;
            criterion.DrawControls(controls, ref edited);
            if (edited)
            {
                NotifyChanged();
            }

            y = controls.yMax + Pad;
        }

        if (criterion is CriterionGroup group)
        {
            Rect area = new Rect(rect.x + GroupIndent, y, rect.width - GroupIndent - Pad, ChildrenHeight(group));
            DropZone zone = new DropZone { Group = group, ScreenRect = Intersect(ToScreen(area), queryScreenRect) };
            zones.Add(zone);
            if (group.children.Count == 0)
            {
                DrawHint(area, "VUIP.WorldQueryEmptyGroup".Translate());
            }
            else
            {
                DrawChildren(group, area, zone);
            }
        }

        if (criterion == dragging && Event.current.type == EventType.Repaint)
        {
            Widgets.DrawBoxSolid(rect, DraggedSourceTint);
        }
    }

    private static void DrawPaletteColumn(Rect rect)
    {
        string hint = "VUIP.WorldQueryPaletteHint".Translate();
        float hintHeight = Text.CalcHeight(hint, rect.width);
        GUI.color = SettingsWidgets.MutedColor;
        Widgets.Label(new Rect(rect.x, rect.y, rect.width, hintHeight), hint);
        GUI.color = Color.white;

        Rect searchRect = new Rect(rect.x, rect.yMax - QuickSearchWidget.WidgetHeight, rect.width, QuickSearchWidget.WidgetHeight);
        paletteSearch.OnGUI(searchRect, () => palette = null);
        Rect outRect = new Rect(rect.x, rect.y + hintHeight + 4f, rect.width, searchRect.y - rect.y - hintHeight - 8f);
        zones.Add(new DropZone { Group = null, ScreenRect = ToScreen(outRect) });
        if (dragging != null && !dragFromPalette && Event.current.type == EventType.Repaint && ToScreen(outRect).Contains(MouseScreen))
        {
            Widgets.DrawBoxSolid(outRect, RemoveTint);
        }

        palette ??= WorldCriteriaCatalog.All.Where(c => !paletteSearch.filter.Active || paletteSearch.filter.Matches(c.Label)).ToList();
        float height = palette.Count * PaletteRowHeight;
        Rect view = new Rect(0f, 0f, outRect.width - (height > outRect.height ? 16f : 0f), height);
        Widgets.BeginScrollView(outRect, ref paletteScroll, view);
        int first = Mathf.Max(0, Mathf.FloorToInt(paletteScroll.y / PaletteRowHeight));
        int last = Mathf.Min(palette.Count - 1, Mathf.CeilToInt((paletteScroll.y + outRect.height) / PaletteRowHeight));
        for (int i = first; i <= last; i++)
        {
            Rect row = new Rect(0f, i * PaletteRowHeight, view.width, PaletteRowHeight - 2f);
            WorldCriterion criterion = palette[i];
            Widgets.DrawMenuSection(row);
            Rect labelRect = row.ContractedBy(Pad, 0f);
            if (criterion.LockedReason is { } locked)
            {
                GUI.color = Widgets.InactiveColor;
                DrawLabel(labelRect, criterion);
                GUI.color = Color.white;
                TooltipHandler.TipRegion(row, locked);
                continue;
            }

            DrawLabel(labelRect, criterion);
            Widgets.DrawHighlightIfMouseover(row);
            TooltipHandler.TipRegion(row, "VUIP.WorldQueryPaletteTip".Translate());
            TryPress(row, criterion, fromPalette: true);
        }

        Widgets.EndScrollView();
    }

    private static void TryPress(Rect handle, WorldCriterion criterion, bool fromPalette)
    {
        Event e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 0 || !Mouse.IsOver(handle) || dragging != null)
        {
            return;
        }

        pressed = criterion;
        pressedFromPalette = fromPalette;
        pressScreen = MouseScreen;
        e.Use();
    }

    private static void HandleDrag()
    {
        Event e = Event.current;
        if (pressed is { } held && dragging == null && Input.GetMouseButton(0) && (MouseScreen - pressScreen).magnitude > DragThreshold)
        {
            dragging = pressedFromPalette ? held.Copy() : held;
            dragFromPalette = pressedFromPalette;
            pressed = null;
        }

        if (e.type == EventType.MouseUp && e.button == 0 && (pressed != null || dragging != null))
        {
            if (dragging != null)
            {
                Drop();
            }
            else if (pressed != null && pressedFromPalette)
            {
                root.children.Add(pressed.Copy());
                NotifyChanged();
            }

            pressed = null;
            dragging = null;
            e.Use();
            return;
        }

        if (e.type != EventType.Repaint || (pressed == null && dragging == null))
        {
            return;
        }

        // A release outside the window never reaches it; drop the drag instead of leaving it stuck.
        if (!Input.GetMouseButton(0))
        {
            pressed = null;
            dragging = null;
            return;
        }

        if (dragging != null)
        {
            DrawDropLine();
            Vector2 mouse = Event.current.mousePosition;
            Rect ghost = new Rect(mouse.x - 10f, mouse.y - 13f, ColumnWidth - 20f, HeaderHeight);
            Widgets.DrawWindowBackground(ghost);
            DrawLabel(ghost.ContractedBy(Pad, 0f), dragging);
        }
    }

    private static DropZone? TargetZone()
    {
        Vector2 mouse = MouseScreen;
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            DropZone zone = zones[i];
            if (!zone.ScreenRect.Contains(mouse))
            {
                continue;
            }

            if (zone.Group != null && dragging is CriterionGroup group && (zone.Group == group || group.Contains(zone.Group)))
            {
                continue;
            }

            return zone;
        }

        return null;
    }

    private static int InsertIndex(DropZone zone)
    {
        float y = MouseScreen.y;
        int index = 0;
        foreach (Rect child in zone.ChildScreenRects)
        {
            if (child.center.y < y)
            {
                index++;
            }
        }

        return index;
    }

    private static void Drop()
    {
        if (dragging == null || TargetZone() is not { } zone)
        {
            return;
        }

        CriterionGroup? from = dragFromPalette ? null : root.ParentOf(dragging);
        if (zone.Group == null)
        {
            if (from != null)
            {
                from.children.Remove(dragging);
                NotifyChanged();
            }

            return;
        }

        int index = InsertIndex(zone);
        if (from != null)
        {
            int old = from.children.IndexOf(dragging);
            from.children.RemoveAt(old);
            if (from == zone.Group && old < index)
            {
                index--;
            }
        }

        zone.Group.children.Insert(Mathf.Clamp(index, 0, zone.Group.children.Count), dragging);
        NotifyChanged();
    }

    private static void DrawDropLine()
    {
        if (TargetZone() is not { Group: not null } zone)
        {
            return;
        }

        int index = InsertIndex(zone);
        float y;
        if (zone.ChildScreenRects.Count == 0)
        {
            y = zone.ScreenRect.center.y;
        }
        else if (index == 0)
        {
            y = zone.ChildScreenRects[0].yMin - Pad / 2f;
        }
        else
        {
            y = zone.ChildScreenRects[index - 1].yMax + Pad / 2f;
        }

        Vector2 start = GUIUtility.ScreenToGUIPoint(new Vector2(zone.ScreenRect.x + 2f, y));
        Widgets.DrawBoxSolid(new Rect(start.x, start.y - 1f, zone.ScreenRect.width - 4f, 2f), DropLineColor);
    }

    private static float Height(WorldCriterion criterion)
    {
        float height = 1f + HeaderHeight;
        if (criterion.ControlsHeight > 0f)
        {
            height += criterion.ControlsHeight + Pad;
        }

        if (criterion is CriterionGroup group)
        {
            height += ChildrenHeight(group) + Pad;
        }

        return height + Pad;
    }

    private static float ChildrenHeight(CriterionGroup group)
    {
        if (group.children.Count == 0)
        {
            return EmptyGroupHeight;
        }

        float height = 0f;
        foreach (WorldCriterion child in group.children)
        {
            height += Height(child);
        }

        return height + Pad * (group.children.Count - 1);
    }

    private static void DrawLabel(Rect rect, WorldCriterion criterion)
    {
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(rect, criterion.Label.Truncate(rect.width));
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private static void DrawHint(Rect rect, string text)
    {
        GUI.color = SettingsWidgets.MutedColor;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, text);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
    }

    public static bool IconButton(Rect rect, Texture2D tex, string tip, Color? color = null)
    {
        TooltipHandler.TipRegion(rect, tip);
        return Widgets.ButtonImage(rect, tex, color ?? Color.white);
    }

    private static Vector2 MouseScreen => GUIUtility.GUIToScreenPoint(Event.current.mousePosition);

    private static Rect ToScreen(Rect rect)
    {
        Vector2 min = GUIUtility.GUIToScreenPoint(rect.min);
        return new Rect(min, rect.size);
    }

    private static Rect Intersect(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMax = Mathf.Min(a.yMax, b.yMax);
        return xMax > xMin && yMax > yMin ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : Rect.zero;
    }

    private static void OpenSavedMenu()
    {
        List<SavedWorldSearch> saved = UiPlusMod.Settings.savedWorldSearches;
        List<FloatMenuOption> options = new List<FloatMenuOption>();
        foreach (SavedWorldSearch search in saved.OrderBy(s => s.name))
        {
            SavedWorldSearch entry = search;
            options.Add(new FloatMenuOption(entry.name, () => Load(entry), extraPartWidth: IconSize * 2f + 8f,
                extraPartOnGUI: r => SavedEntryButtons(r, entry)));
        }

        System.Action? saveAction = null;
        if (root.children.Count > 0)
        {
            saveAction = () => Find.WindowStack.Add(new Dialog_NameWorldSearch(root));
        }

        FloatMenuOption save = new FloatMenuOption("VUIP.WorldQuerySaveAs".Translate(), saveAction);
        if (saveAction == null)
        {
            save.tooltip = new TipSignal("VUIP.WorldQuerySaveEmpty".Translate());
        }

        options.Add(save);
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static bool SavedEntryButtons(Rect rect, SavedWorldSearch entry)
    {
        Rect overwrite = new Rect(rect.x, rect.center.y - IconSize / 2f, IconSize, IconSize);
        Rect delete = new Rect(overwrite.xMax + 4f, overwrite.y, IconSize, IconSize);
        if (IconButton(overwrite, TexButton.Save, "VUIP.WorldQueryOverwrite".Translate(entry.name)) && root.children.Count > 0)
        {
            entry.root = (CriterionGroup)root.Copy();
            UiPlusMod.Instance.WriteSettings();
            return true;
        }

        if (IconButton(delete, TexButton.Delete, "VUIP.WorldQueryDelete".Translate(entry.name)))
        {
            UiPlusMod.Settings.savedWorldSearches.Remove(entry);
            UiPlusMod.Instance.WriteSettings();
            return true;
        }

        return false;
    }

    private static void Load(SavedWorldSearch entry)
    {
        root = (CriterionGroup)entry.root.Copy();
        root.mode = CriterionGroupMode.All;
        Paused = false;
        NotifyChanged();
    }

    public static void Save(string name, CriterionGroup query)
    {
        List<SavedWorldSearch> saved = UiPlusMod.Settings.savedWorldSearches;
        saved.RemoveAll(s => s.name == name);
        saved.Add(new SavedWorldSearch { name = name, root = (CriterionGroup)query.Copy() });
        UiPlusMod.Instance.WriteSettings();
    }
}

public class Dialog_NameWorldSearch : Window
{
    private const int MaxNameLength = 40;
    private readonly CriterionGroup query;
    private string name;
    private bool focused;

    public Dialog_NameWorldSearch(CriterionGroup query)
    {
        this.query = query;
        name = UniqueName();
        forcePause = false;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
        doCloseX = true;
    }

    public override Vector2 InitialSize => new Vector2(360f, 150f);

    private static string UniqueName()
    {
        List<SavedWorldSearch> saved = UiPlusMod.Settings.savedWorldSearches;
        for (int i = 1; ; i++)
        {
            string candidate = "VUIP.WorldQueryDefaultName".Translate(i);
            if (!saved.Any(s => s.name == candidate))
            {
                return candidate;
            }
        }
    }

    private bool Valid => !name.Trim().NullOrEmpty();

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 26f), "VUIP.WorldQueryNamePrompt".Translate());
        GUI.SetNextControlName("VUIP.WorldSearchName");
        name = Widgets.TextField(new Rect(inRect.x, inRect.y + 30f, inRect.width, 30f), name);
        if (name.Length > MaxNameLength)
        {
            name = name.Substring(0, MaxNameLength);
        }
        if (!focused)
        {
            UI.FocusControl("VUIP.WorldSearchName", this);
            focused = true;
        }

        bool replaces = UiPlusMod.Settings.savedWorldSearches.Any(s => s.name == name.Trim());
        Rect buttons = new Rect(inRect.x, inRect.yMax - 32f, inRect.width, 32f);
        if (Widgets.ButtonText(buttons.LeftHalf().ContractedBy(4f, 0f), "Cancel".Translate()))
        {
            Close();
        }

        string accept = (replaces ? "VUIP.WorldQueryReplace" : "Save").Translate();
        if (Widgets.ButtonText(buttons.RightHalf().ContractedBy(4f, 0f), accept, active: Valid))
        {
            OnAcceptKeyPressed();
        }
    }

    public override void OnAcceptKeyPressed()
    {
        if (!Valid)
        {
            return;
        }

        WorldQueryPanel.Save(name.Trim(), query);
        Close();
    }
}
