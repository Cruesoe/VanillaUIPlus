using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

public static class MainButtonLayout
{
    public const string MoreId = "VUIP_More";
    public const string MenuPrefix = "VUIP_Menu_";
    private const string NewMenuIcon = "UI/Icons/MainButtons/folder-open";

    public static int DropdownClosedOnFrame = -1;
    public static string DropdownClosedMenu = string.Empty;

    private static readonly Dictionary<string, int> MenuCounts = new Dictionary<string, int>();
    private static readonly List<MainButtonLayoutEntry> BarSlots = new List<MainButtonLayoutEntry>();
    private static readonly List<bool> BarCompact = new List<bool>();
    private static readonly HashSet<string> KnownNames = new HashSet<string>();
    private static readonly Color SelectedFill = new Color(0.22f, 0.50f, 0.82f, 0.95f);
    private static readonly Color RowAlt = new Color(1f, 1f, 1f, 0.04f);
    private static readonly Color DropLine = new Color(0.45f, 0.78f, 1f, 1f);
    private static bool merged;
    private static int mergedDefCount;
    private static int dragFrom = -1;
    private static string? moreLabel;
    private static string? menuTip;

    private const float SettingsRowH = 28f;
    private const float SettingsRowGap = 6f;

    // Shipped icon and look per button, applied only when its entry is first created; an empty path keeps the def's icon.
    private static readonly Dictionary<string, (string IconPath, MainButtonLook Look)> DefaultLooks =
        new Dictionary<string, (string, MainButtonLook)>
        {
            { "Architect", ("UI/Icons/MainButtons/bank", MainButtonLook.IconOnly) },
            { "Work", ("UI/Icons/MainButtons/hammer-screwdriver", MainButtonLook.TextAndIcon) },
            { "Schedule", ("UI/Icons/MainButtons/calendar-month", MainButtonLook.TextAndIcon) },
            { "Assign", ("UI/Icons/MainButtons/account-details", MainButtonLook.TextAndIcon) },
            { "Animals", ("UI/Icons/MainButtons/barn", MainButtonLook.TextAndIcon) },
            { "Mechs", ("UI/Icons/MainButtons/robot", MainButtonLook.TextAndIcon) },
            { "Wildlife", ("UI/Icons/MainButtons/paw", MainButtonLook.TextAndIcon) },
            { "Research", ("UI/Icons/MainButtons/flask-outline", MainButtonLook.TextAndIcon) },
            { "Quests", ("UI/Icons/MainButtons/chat-alert", MainButtonLook.TextAndIcon) },
            { "World", ("UI/Icons/MainButtons/earth", MainButtonLook.TextAndIcon) },
            { "History", ("UI/Icons/MainButtons/library", MainButtonLook.IconOnly) },
            { "Factions", ("UI/Icons/MainButtons/home-group", MainButtonLook.IconOnly) },
            { "Ideos", ("UI/Icons/MainButtons/head-lightbulb", MainButtonLook.IconOnly) },
            { "Menu", (string.Empty, MainButtonLook.IconOnly) },
            { MoreId, ("UI/Icons/MainButtons/apps", MainButtonLook.IconOnly) },
            { "WQ_CharactersMenu", ("UI/Icons/MainButtons/account-edit", MainButtonLook.TextAndIcon) },
        };

    // Buttons without a shipped default keep the def's icon and draw text and icon.
    private static void ApplyDefaults(MainButtonLayoutEntry entry)
    {
        if (DefaultLooks.TryGetValue(entry.defName, out (string IconPath, MainButtonLook Look) preset))
        {
            entry.iconPath = preset.IconPath;
            entry.look = preset.Look;
            return;
        }

        entry.look = MainButtonLook.TextAndIcon;
    }

    // Merges again when the def count changes: mods such as Character Editor add their tab after startup
    public static void EnsureInitialized()
    {
        int defCount = DefDatabase<MainButtonDef>.DefCount;
        if (defCount == 0 || merged && defCount == mergedDefCount)
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
        bool migrated = false;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            MainButtonLayoutEntry entry = entries[i];
            if (IsMenu(entry))
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
            if (entry.LegacyHidden)
            {
                entry.LegacyHidden = false;
                if (!def.buttonVisible)
                {
                    entry.placement = MainButtonPlacement.Dropdown;
                    entry.menu = string.Empty;
                    entry.followsMod = true;
                    migrated = true;
                }
            }
        }

        bool existingLayout = false;
        for (int i = 0; i < entries.Count; i++)
        {
            if (!IsMenu(entries[i]))
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
            MainButtonLayoutEntry entry = new MainButtonLayoutEntry
            {
                defName = def.defName,
                placement = existingLayout ? MainButtonPlacement.Dropdown : MainButtonPlacement.Bar,
                followsMod = !def.buttonVisible,
                CachedDef = def
            };
            ApplyDefaults(entry);
            entries.Add(entry);
        }

        merged = true;
        mergedDefCount = defCount;
        EnsureMoreEntry();
        ClearMissingMenus();
        if (existingLayout && added.Count > 0 || migrated)
        {
            UiPlusMod.Instance.WriteSettings();
        }
    }

    public static void ResetToDefaults()
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
        CountMenuTabs();

        BarSlots.Clear();
        BarCompact.Clear();
        List<MainButtonLayoutEntry> entries = UiPlusMod.Settings.mainButtons;
        float weight = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            MainButtonLayoutEntry entry = entries[i];
            if (IsMenu(entry))
            {
                if (MenuCounts.TryGetValue(entry.defName, out int tabs) && tabs > 0)
                {
                    bool menuCompact = MainButtonPainter.Compact(null, entry);
                    BarSlots.Add(entry);
                    BarCompact.Add(menuCompact);
                    weight += menuCompact ? 0.5f : 1f;
                }

                continue;
            }

            if (entry.placement != MainButtonPlacement.Bar)
            {
                continue;
            }

            MainButtonDef? def = DefOf(entry);
            if (def == null || !ShouldDraw(def, entry))
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
            MainButtonDef? def = IsMenu(slot) ? null : DefOf(slot);
            int width = BarCompact[i] ? half : unit;
            if (last)
            {
                width = UI.screenWidth - x;
            }

            Rect rect = new Rect(x, UI.screenHeight - 35, width, 36f);
            if (IsMenu(slot))
            {
                DrawMenuButton(slot, rect);
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

        // Snapshot: row actions such as deleting a menu change the list while it is drawn.
        List<MainButtonLayoutEntry> entries = new List<MainButtonLayoutEntry>(UiPlusMod.Settings.mainButtons);
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

        list.Gap(6f);
        string newMenu = "VUIP.MainBarNewMenu".Translate();
        Rect button = list.GetRect(30f);
        button.width = Mathf.Max(160f, Text.CalcSize(newMenu).x + 24f);
        TooltipHandler.TipRegion(button, "VUIP.MainBarNewMenuTip".Translate());
        if (dragFrom < 0 && Widgets.ButtonText(button, newMenu))
        {
            PromptNewMenu(null);
        }
    }

    private static void DrawRow(Rect row, MainButtonLayoutEntry entry, int index)
    {
        bool menu = IsMenu(entry);
        MainButtonDef? def = menu ? null : DefOf(entry);
        if (!menu && def == null)
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
        float lookW = SegmentGroupWidth(126f, LookKeys);
        float placeW = PlaceGroupWidth();
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
        if (!menu && (entry.placement == MainButtonPlacement.Hidden || entry.followsMod && !def!.buttonVisible))
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
        }

        Text.Anchor = TextAnchor.MiddleLeft;
        Text.WordWrap = false;
        Widgets.Label(label, menu ? MenuLabel(entry) : MainButtonPainter.TabLabel(def!, entry));
        Text.WordWrap = true;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = old;

        if (menu)
        {
            TooltipHandler.TipRegion(label, (IsMore(entry) ? "VUIP.MainBarMoreRowTip" : "VUIP.MainBarMenuRowTip").Translate());
        }
        else if (Mouse.IsOver(label))
        {
            string renameTip = "VUIP.MainBarTabRenameTip".Translate(def!.LabelCap);
            TooltipHandler.TipRegion(label, def.description.NullOrEmpty() ? renameTip : def.description + "\n\n" + renameTip);
        }

        DrawLookChoices(look, entry);
        if (menu)
        {
            DrawMenuActions(place, entry);
        }
        else
        {
            DrawPlaceChoices(place, entry);
        }

        if (dragFrom < 0)
        {
            Rect gripHit = new Rect(grip.x, row.y, grip.width + 2f, row.height);
            Widgets.DraggableResult labelResult = Widgets.DraggableResult.Idle;
            if (Widgets.ButtonInvisibleDraggable(gripHit) == Widgets.DraggableResult.Dragged
                || (labelResult = Widgets.ButtonInvisibleDraggable(label)) == Widgets.DraggableResult.Dragged)
            {
                dragFrom = index;
            }
            // Clicking a tab's name without dragging renames it
            else if (!menu && labelResult == Widgets.DraggableResult.Pressed)
            {
                Find.WindowStack.Add(new Dialog_NameMainButtonTab(MainButtonPainter.TabLabel(def!, entry), name => RenameTab(def!, entry, name)));
            }
        }
    }

    private static readonly string[] LookKeys = { "VUIP.MainBarLookIcon", "VUIP.MainBarLookBoth", "VUIP.MainBarLookText" };
    private static readonly string[] PlaceSideKeys = { "VUIP.MainBarOnBar", "VUIP.MainBarHidden", "VUIP.MainBarShow" };
    private const string MenuArrow = " ▾";
    private const float MaxMenuSegmentWidth = 200f;

    // Bar and Hide/Show fit their labels; the menu segment fits the longest menu name with its arrow.
    private static float PlaceSideWidth()
    {
        float widest = 0f;
        foreach (string key in PlaceSideKeys)
        {
            widest = Mathf.Max(widest, TextCache.Width(TextCache.Key(key)));
        }

        return Mathf.Max(50f, widest + 16f);
    }

    private static float MenuSegmentWidth()
    {
        float widest = TextCache.Width(TextCache.Key("VUIP.MainBarDropdown") + MenuArrow);
        foreach (MainButtonLayoutEntry entry in UiPlusMod.Settings.mainButtons)
        {
            if (IsMenu(entry))
            {
                widest = Mathf.Max(widest, TextCache.Width(MenuLabel(entry) + MenuArrow));
            }
        }

        return Mathf.Min(MaxMenuSegmentWidth, widest + 16f);
    }

    private static float PlaceGroupWidth()
    {
        return Mathf.Max(150f, PlaceSideWidth() * 2f + MenuSegmentWidth());
    }

    // Three equal segments wide enough for the widest label in the normal font, and never narrower than the default width.
    private static float SegmentGroupWidth(float minWidth, string[] keys)
    {
        float widest = 0f;
        foreach (string key in keys)
        {
            widest = Mathf.Max(widest, TextCache.Width(TextCache.Key(key)));
        }

        return Mathf.Max(minWidth, 3f * (widest + 12f));
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

    private static void DrawLookChoices(Rect rect, MainButtonLayoutEntry entry)
    {
        MainButtonLook look = entry.look;
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
        float side = PlaceSideWidth();
        float middle = rect.width - side * 2f;
        DrawSegment(
            new Rect(rect.x, rect.y, side, rect.height),
            "VUIP.MainBarOnBar".Translate(),
            "VUIP.MainBarOnBarTip".Translate(),
            entry.placement == MainButtonPlacement.Bar,
            () => SetPlacement(entry, MainButtonPlacement.Bar));
        bool inMenu = entry.placement == MainButtonPlacement.Dropdown;
        string menuName = inMenu ? MenuLabel(FindMenu(MenuKey(entry))) : "VUIP.MainBarDropdown".Translate().ToString();
        DrawSegment(
            new Rect(rect.x + side, rect.y, middle, rect.height),
            TextCache.Truncate(menuName, middle - 12f - TextCache.Width(MenuArrow)) + MenuArrow,
            "VUIP.MainBarDropdownTip".Translate(),
            inMenu,
            () => OpenMenuPicker(entry));
        bool hidden = entry.placement == MainButtonPlacement.Hidden;
        DrawSegment(
            new Rect(rect.x + side + middle, rect.y, rect.width - side - middle, rect.height),
            hidden ? "VUIP.MainBarShow".Translate() : "VUIP.MainBarHidden".Translate(),
            hidden ? "VUIP.MainBarShowTip".Translate() : "VUIP.MainBarHiddenTip".Translate(),
            hidden,
            () => ToggleHidden(entry, hidden));
        Widgets.DrawBox(rect);
    }

    // Rename and Delete for a menu row; More can be renamed but not deleted.
    private static void DrawMenuActions(Rect rect, MainButtonLayoutEntry entry)
    {
        float w = rect.width / 2f;
        DrawSegment(
            new Rect(rect.x, rect.y, w, rect.height),
            "VUIP.MainBarMenuRename".Translate(),
            "VUIP.MainBarMenuRenameTip".Translate(),
            false,
            () => Find.WindowStack.Add(new Dialog_NameMainButtonMenu(MenuLabel(entry), name => RenameMenu(entry, name))));
        bool more = IsMore(entry);
        DrawSegment(
            new Rect(rect.x + w, rect.y, rect.width - w, rect.height),
            "VUIP.MainBarMenuDelete".Translate(),
            (more ? "VUIP.MainBarMenuDeleteMoreTip" : "VUIP.MainBarMenuDeleteTip").Translate(),
            false,
            () => DeleteMenu(entry),
            active: !more);
        Widgets.DrawBox(rect);
    }

    private static void DrawSegment(Rect rect, string label, string tip, bool selected, System.Action onClick, bool active = true)
    {
        if (selected)
        {
            Widgets.DrawBoxSolid(rect, SelectedFill);
        }

        Color old = GUI.color;
        if (active)
        {
            Widgets.DrawHighlightIfMouseover(rect);
        }
        else
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
        }

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, label);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = old;
        TooltipHandler.TipRegion(rect, tip);
        if (active && dragFrom < 0 && Widgets.ButtonInvisible(rect))
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
            Text.Font = GameFont.Small;
            Widgets.Label(rect, "▾");
            Text.Anchor = TextAnchor.UpperLeft;
        }

        Widgets.DrawHighlightIfMouseover(rect);
        TooltipHandler.TipRegion(rect, "VUIP.MainBarIconTip".Translate());
        if (dragFrom < 0 && Widgets.ButtonInvisible(rect))
        {
            Find.WindowStack.Add(new MainButtonIconPicker(entry));
        }
    }

    private static void DrawMenuButton(MainButtonLayoutEntry entry, Rect rect)
    {
        MainButtonDropdownWindow? window = Find.WindowStack.WindowOfType<MainButtonDropdownWindow>();
        bool open = window != null && window.MenuId == entry.defName;
        if (open)
        {
            Widgets.DrawHighlight(rect);
        }

        bool wasOpen = open;
        MainButtonPainter.Draw(
            rect,
            entry,
            null,
            MenuLabel(entry),
            "▾",
            disabled: false,
            barPercent: 0f,
            () => ToggleMenu(entry, rect, wasOpen));
        if (!Mouse.IsOver(rect))
        {
            return;
        }

        menuTip ??= "VUIP.MainBarMoreTip".Translate();
        TooltipHandler.TipRegion(rect, menuTip);
    }

    private static void ToggleMenu(MainButtonLayoutEntry entry, Rect rect, bool open)
    {
        if (open || (DropdownClosedOnFrame == Time.frameCount && DropdownClosedMenu == entry.defName))
        {
            CloseDropdown();
            return;
        }

        OpenMenu(entry, rect);
    }

    private static void OpenMenu(MainButtonLayoutEntry menu, Rect rect)
    {
        CloseDropdown();
        // Opening a menu closes the open main button window or tab, as opening a tab does
        if (MainButtonPainter.AnyOpened())
        {
            MainButtonPainter.CloseOpened(doCloseSound: true);
        }

        if (Find.MainTabsRoot.OpenTab != null)
        {
            Find.MainTabsRoot.EscapeCurrentTab();
        }

        List<MainButtonLayoutEntry> tabs = new List<MainButtonLayoutEntry>();
        List<MainButtonLayoutEntry> entries = UiPlusMod.Settings.mainButtons;
        for (int i = 0; i < entries.Count; i++)
        {
            MainButtonLayoutEntry entry = entries[i];
            if (IsVisibleMenuTab(entry) && MenuKey(entry) == menu.defName)
            {
                tabs.Add(entry);
            }
        }

        Find.WindowStack.Add(new MainButtonDropdownWindow(menu.defName, tabs, rect));
    }

    // Visible tabs per menu id, so empty menus stay off the bar.
    private static void CountMenuTabs()
    {
        MenuCounts.Clear();
        List<MainButtonLayoutEntry> entries = UiPlusMod.Settings.mainButtons;
        for (int i = 0; i < entries.Count; i++)
        {
            MainButtonLayoutEntry entry = entries[i];
            if (!IsVisibleMenuTab(entry))
            {
                continue;
            }

            string key = MenuKey(entry);
            MenuCounts.TryGetValue(key, out int count);
            MenuCounts[key] = count + 1;
        }
    }

    private static bool IsVisibleMenuTab(MainButtonLayoutEntry entry)
    {
        if (IsMenu(entry) || entry.placement != MainButtonPlacement.Dropdown)
        {
            return false;
        }

        MainButtonDef? def = DefOf(entry);
        return def != null && ShouldDraw(def, entry);
    }

    private static string MenuKey(MainButtonLayoutEntry entry)
    {
        return entry.menu.NullOrEmpty() ? MoreId : entry.menu;
    }

    private static string MenuLabel(MainButtonLayoutEntry? menu)
    {
        if (menu != null && !menu.label.NullOrEmpty())
        {
            return menu.label;
        }

        moreLabel ??= "VUIP.MainBarMore".Translate();
        return moreLabel;
    }

    private static MainButtonLayoutEntry? FindMenu(string id)
    {
        List<MainButtonLayoutEntry> entries = UiPlusMod.Settings.mainButtons;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].defName == id && IsMenu(entries[i]))
            {
                return entries[i];
            }
        }

        return null;
    }

    private static void OpenMenuPicker(MainButtonLayoutEntry tab)
    {
        List<FloatMenuOption> options = new List<FloatMenuOption>();
        List<MainButtonLayoutEntry> entries = UiPlusMod.Settings.mainButtons;
        for (int i = 0; i < entries.Count; i++)
        {
            MainButtonLayoutEntry menu = entries[i];
            if (IsMenu(menu))
            {
                options.Add(new FloatMenuOption(MenuLabel(menu), () => SetMenu(tab, menu.defName)));
            }
        }

        options.Add(new FloatMenuOption("VUIP.MainBarNewMenu".Translate(), () => PromptNewMenu(tab)));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void SetMenu(MainButtonLayoutEntry tab, string menuId)
    {
        tab.placement = MainButtonPlacement.Dropdown;
        tab.menu = menuId == MoreId ? string.Empty : menuId;
        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        CloseDropdown();
        UiPlusMod.Instance.WriteSettings();
    }

    // Names a new menu; made from a tab's row, the menu takes that tab's place and the tab moves into it.
    private static void PromptNewMenu(MainButtonLayoutEntry? tab)
    {
        Find.WindowStack.Add(new Dialog_NameMainButtonMenu(UniqueMenuName(), name =>
        {
            List<MainButtonLayoutEntry> entries = UiPlusMod.Settings.mainButtons;
            MainButtonLayoutEntry menu = new MainButtonLayoutEntry
            {
                defName = UniqueMenuId(),
                placement = MainButtonPlacement.Bar,
                look = MainButtonLook.TextAndIcon,
                iconPath = NewMenuIcon,
                label = name
            };
            int at = tab == null ? -1 : entries.IndexOf(tab);
            entries.Insert(at < 0 ? entries.Count : at, menu);
            if (tab != null)
            {
                SetMenu(tab, menu.defName);
                return;
            }

            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            UiPlusMod.Instance.WriteSettings();
        }));
    }

    private static void RenameMenu(MainButtonLayoutEntry menu, string name)
    {
        menu.label = name;
        UiPlusMod.Instance.WriteSettings();
    }

    // The tab's own label clears the custom name, so it follows language changes again
    private static void RenameTab(MainButtonDef def, MainButtonLayoutEntry tab, string name)
    {
        tab.label = name == def.LabelCap.ToString() ? string.Empty : name;
        MainButtonPainter.ForgetTip(def.defName);
        UiPlusMod.Instance.WriteSettings();
    }

    // Moves the menu's tabs into More.
    private static void DeleteMenu(MainButtonLayoutEntry menu)
    {
        List<MainButtonLayoutEntry> entries = UiPlusMod.Settings.mainButtons;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].menu == menu.defName)
            {
                entries[i].menu = string.Empty;
            }
        }

        entries.Remove(menu);
        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        CloseDropdown();
        UiPlusMod.Instance.WriteSettings();
    }

    private static string UniqueMenuId()
    {
        for (int i = 1; ; i++)
        {
            string id = MenuPrefix + i;
            if (FindMenu(id) == null)
            {
                return id;
            }
        }
    }

    private static string UniqueMenuName()
    {
        List<MainButtonLayoutEntry> entries = UiPlusMod.Settings.mainButtons;
        for (int i = 1; ; i++)
        {
            string name = "VUIP.MainBarMenuDefaultName".Translate(i);
            bool taken = false;
            for (int j = 0; j < entries.Count; j++)
            {
                if (IsMenu(entries[j]) && MenuLabel(entries[j]) == name)
                {
                    taken = true;
                    break;
                }
            }

            if (!taken)
            {
                return name;
            }
        }
    }

    // Tabs whose menu no longer exists fall back to More.
    private static void ClearMissingMenus()
    {
        List<MainButtonLayoutEntry> entries = UiPlusMod.Settings.mainButtons;
        for (int i = 0; i < entries.Count; i++)
        {
            MainButtonLayoutEntry entry = entries[i];
            if (!IsMenu(entry) && !entry.menu.NullOrEmpty() && FindMenu(entry.menu) == null)
            {
                entry.menu = string.Empty;
            }
        }
    }

    private static bool ShouldDraw(MainButtonDef def, MainButtonLayoutEntry entry)
    {
        if (def.Worker.Visible)
        {
            return true;
        }

        return !def.buttonVisible && !entry.followsMod && entry.placement != MainButtonPlacement.Hidden;
    }

    // Showing a tab its mod has hidden forces it on; one its mod already shows keeps following the mod.
    private static void ToggleHidden(MainButtonLayoutEntry entry, bool hidden)
    {
        if (hidden && DefOf(entry) is { buttonVisible: false })
        {
            entry.followsMod = false;
        }

        SetPlacement(entry, hidden ? MainButtonPlacement.Bar : MainButtonPlacement.Hidden);
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

        MainButtonLayoutEntry more = new MainButtonLayoutEntry
        {
            defName = MoreId,
            placement = MainButtonPlacement.Bar
        };
        ApplyDefaults(more);
        entries.Add(more);
    }

    private static bool IsMore(MainButtonLayoutEntry entry)
    {
        return entry.defName == MoreId;
    }

    public static bool IsMenu(MainButtonLayoutEntry entry)
    {
        return entry.defName == MoreId || entry.defName.StartsWith(MenuPrefix, System.StringComparison.Ordinal);
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
