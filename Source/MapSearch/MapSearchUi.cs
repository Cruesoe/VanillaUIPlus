using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Draws the map and world search dialogs with grouped results, and the world dialog's advanced search panel and options.
/// </summary>
[StaticConstructorOnStartup]
public static class MapSearchUi
{
    private const float ButtonSize = 18f;
    private const float ButtonSpace = 22f;
    private const int MaxGroupArrows = 100;

    private static readonly Action<Window>? SetInitialSizeAndPosition = ReflectionGuard.Delegate<Action<Window>>(
        nameof(Window), "SetInitialSizeAndPosition", AccessTools.Method(typeof(Window), "SetInitialSizeAndPosition"));

    private static Dialog_MapSearch? mapDialog;
    private static SearchResultList<Thing>? mapList;
    private static Dialog_WorldSearch? worldDialog;
    private static SearchResultList<WorldSearchElement>? worldList;
    private static Texture2D? cogIcon;

    private static Texture2D CogIcon => cogIcon ??= ContentFinder<Texture2D>.Get(MainButtonPainter.ExtraIconFolder + "/cog", reportFailure: false)
        ?? TexButton.OpenInspectSettings;

    public static bool MapReady => UiPlusMod.Settings.groupSearchResults && SearchDialogAccess<Thing>.Ready && SetInitialSizeAndPosition != null;

    public static bool WorldReady => SearchDialogAccess<WorldSearchElement>.Ready && SetInitialSizeAndPosition != null
        && (UiPlusMod.Settings.groupSearchResults || UiPlusMod.Settings.worldSearchAllTiles || UiPlusMod.Settings.worldSearchHighlight);

    private static bool AdvancedButton => WorldQueryPanel.Available;

    private static bool PanelShown => AdvancedButton && WorldQueryPanel.Open;

    public static void DrawMap(Dialog_MapSearch dialog, Rect inRect)
    {
        if (mapDialog != dialog || mapList == null)
        {
            mapDialog = dialog;
            mapList = new SearchResultList<Thing>(MapKey, MapGroupLabel, () => true);
        }

        bool hasQuery = dialog.CommonSearchWidget.filter.Text.Length > 0;
        DrawResults(dialog, inRect, mapList, hasQuery);
    }

    public static void DrawWorld(Dialog_WorldSearch dialog, Rect inRect)
    {
        if (worldDialog != dialog || worldList == null)
        {
            worldDialog = dialog;
            worldList = new SearchResultList<WorldSearchElement>(WorldKey, WorldGroupLabel, () => UiPlusMod.Settings.groupSearchResults,
                WorldSearchTiles.CoordinateLabel);
        }

        Rect resultsRect = inRect;
        if (PanelShown)
        {
            WorldQueryPanel.Draw(new Rect(inRect.x, inRect.y, WorldQueryPanel.Width - WorldQueryPanel.ColumnGap, inRect.height));
            resultsRect.xMin += WorldQueryPanel.Width;
        }

        bool hasQuery = dialog.CommonSearchWidget.filter.Text.Length > 0 || WorldQueryPanel.Active;
        DrawResults(dialog, resultsRect, worldList, hasQuery);
        WorldSearchHighlight.Update(dialog, SearchDialogAccess<WorldSearchElement>.Results!(dialog));

        Rect button = new Rect(resultsRect.x, resultsRect.yMax - ButtonSpace, ButtonSize, ButtonSize);
        if (AdvancedButton)
        {
            string tip = (WorldQueryPanel.Open ? "VUIP.WorldQueryClose" : "VUIP.WorldQueryOpen").Translate();
            if (WorldQueryPanel.IconButton(button, WorldQueryPanel.Open ? TexUI.ArrowTexLeft : TexUI.ArrowTexRight, tip))
            {
                WorldQueryPanel.Open = !WorldQueryPanel.Open;
                WorldQueryPanel.NotifyChanged();
                SetInitialSizeAndPosition!(dialog);
            }
        }

        button.x = resultsRect.xMax - ButtonSize;
        if (WorldQueryPanel.IconButton(button, CogIcon, "VUIP.WorldSearchOptions".Translate()))
        {
            Find.WindowStack.Add(new FloatMenu(OptionsMenu()));
        }

        if (WorldQueryPanel.ConsumeChange())
        {
            dialog.Notify_CommonSearchChanged();
        }
    }

    private static void DrawResults<T>(Dialog_Search<T> dialog, Rect rect, SearchResultList<T> list, bool hasQuery)
        where T : class
    {
        Text.Font = GameFont.Small;
        SearchDialogAccess<T>.Highlighted!(dialog) = null!;
        SortedList<string, T> results = SearchDialogAccess<T>.Results!(dialog);
        bool searching = SearchDialogAccess<T>.Searching!(dialog);
        if (list.Refresh(dialog, results, searching))
        {
            SetInitialSizeAndPosition!(dialog);
        }

        string searchLabel = SearchDialogAccess<T>.SearchLabel!(dialog);
        float labelHeight = Text.CalcHeight(searchLabel, rect.width);
        Rect labelRect = new Rect(rect.x, rect.yMax - QuickSearchWidget.WidgetHeight - labelHeight, rect.width, labelHeight);
        string status;
        if (searching)
        {
            status = QuickSearchUtility.CurrentSearchText;
        }
        else if (!hasQuery)
        {
            status = searchLabel;
        }
        else
        {
            status = results.Count == 1 ? "MapSearchResultSingular".Translate() : "MapSearchResults".Translate(results.Count);
        }

        GUI.color = ColoredText.SubtleGrayColor;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, status);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        Rect outRect = new Rect(rect.x, rect.y, rect.width, labelRect.yMin - rect.y);
        Vector2 scroll = SearchDialogAccess<T>.ScrollPos!(dialog);
        list.Draw(dialog, outRect, ref scroll);
        SearchDialogAccess<T>.ScrollPos!(dialog) = scroll;

        if (!SearchDialogAccess<T>.TriedToFocus!(dialog) && SearchDialogAccess<T>.OpenFrames!(dialog) == 2)
        {
            dialog.CommonSearchWidget.Focus();
            SearchDialogAccess<T>.TriedToFocus!(dialog) = true;
        }
    }

    /// <summary>Sizes the window to its rows instead of its result count, and widens the world dialog for the advanced panel.</summary>
    public static void AfterSetSize(Window window)
    {
        // Rows stay -1 before the first draw, which keeps vanilla's height.
        int rows = -1;
        bool widen = false;
        if (window is Dialog_MapSearch)
        {
            if (window != mapDialog || !MapReady || mapList == null)
            {
                return;
            }

            rows = mapList.RowCount;
        }
        else if (window is Dialog_WorldSearch)
        {
            if (!WorldReady)
            {
                return;
            }

            if (window == worldDialog && worldList != null)
            {
                rows = worldList.RowCount;
            }

            widen = PanelShown;
        }
        else
        {
            return;
        }

        Vector2 size = window.InitialSize;
        float height = window.windowRect.height;
        if (rows >= 0)
        {
            height = Mathf.Clamp(size.y + rows * SearchResultList<Thing>.RowHeight, size.y, UI.screenHeight / 2f);
        }

        float width = size.x;
        if (widen)
        {
            width += WorldQueryPanel.Width;
            height = UI.screenHeight / 2f;
        }

        window.windowRect = new Rect(UI.screenWidth - width, UI.screenHeight - height - 35f, width, height).Rounded();
    }

    /// <summary>Keeps the world dialog's text box clear of the panel and its two buttons.</summary>
    public static void AdjustQuickSearchRect(Window window, ref Rect rect)
    {
        if (window is not Dialog_WorldSearch || !WorldReady)
        {
            return;
        }

        float left = (PanelShown ? WorldQueryPanel.Width : 0f) + (AdvancedButton ? ButtonSpace : 0f);
        rect.x += left;
        rect.width -= left + ButtonSpace;
    }

    public static void DrawGroupArrows(Window window)
    {
        if (window != mapDialog || mapList?.HoveredGroup is not { } group)
        {
            return;
        }

        int count = 0;
        foreach (Thing thing in group.Items)
        {
            if (thing.Destroyed || thing.MapHeld != Find.CurrentMap)
            {
                continue;
            }

            GenDraw.DrawArrowPointingAt(thing.PositionHeld.ToVector3Shifted());
            if (++count >= MaxGroupArrows)
            {
                break;
            }
        }
    }

    private static object? MapKey(Thing thing)
    {
        if (thing is MinifiedThing { InnerThing: { } inner })
        {
            return (inner.def, 2);
        }

        if (thing is Building_Casket casket)
        {
            return (thing.def, casket.HasAnyContents ? 1 : 0);
        }

        return thing.def;
    }

    private static string MapGroupLabel(object key)
    {
        if (key is not ValueTuple<ThingDef, int> sub)
        {
            return ((ThingDef)key).LabelCap;
        }

        string state = sub.Item2 switch
        {
            2 => "VUIP.MapSearchMinified",
            1 => "VUIP.MapSearchOccupied",
            _ => "VUIP.MapSearchEmpty"
        };
        return sub.Item1.LabelCap + " (" + state.Translate() + ")";
    }

    private static object? WorldKey(WorldSearchElement element)
    {
        if (element.worldObject != null)
        {
            return element.worldObject.def;
        }

        if (element.landmark != null)
        {
            return element.landmark.def;
        }

        return element.tile.Valid ? element.tile.Tile.PrimaryBiome : null;
    }

    private static string WorldGroupLabel(object key)
    {
        return ((Def)key).LabelCap;
    }

    private static List<FloatMenuOption> OptionsMenu()
    {
        UiPlusSettings s = UiPlusMod.Settings;
        string? sourcesLocked = s.worldSearchAllTiles ? null : SettingsWidgets.RequiresSetting("VUIP.WorldSearchAllTiles");
        List<FloatMenuOption> options = new List<FloatMenuOption>
        {
            Toggle("VUIP.WorldSearchSourcePlaces", () => s.worldSearchPlaces, v => s.worldSearchPlaces = v, sourcesLocked),
            Toggle("VUIP.WorldSearchSourceLandmarks", () => s.worldSearchLandmarks, v => s.worldSearchLandmarks = v, sourcesLocked),
            Toggle("VUIP.WorldSearchSourceFeatures", () => s.worldSearchFeatures, v => s.worldSearchFeatures = v, sourcesLocked),
            Toggle("VUIP.WorldSearchSourceBiomes", () => s.worldSearchBiomes, v => s.worldSearchBiomes = v, sourcesLocked),
            Toggle("VUIP.WorldSearchSourceTerrain", () => s.worldSearchTerrain, v => s.worldSearchTerrain = v, sourcesLocked),
            Toggle("VUIP.WorldSearchSourceRoadsRivers", () => s.worldSearchRoadsRivers, v => s.worldSearchRoadsRivers = v, sourcesLocked),
            Toggle("VUIP.WorldSearchSourceStone", () => s.worldSearchStone, v => s.worldSearchStone = v, sourcesLocked)
        };

        Action? resetAction = null;
        if (sourcesLocked == null)
        {
            resetAction = () =>
            {
                WorldSearchText.ResetSources();
                OptionChanged();
            };
        }

        FloatMenuOption reset = new FloatMenuOption("VUIP.WorldSearchResetSources".Translate(), resetAction);
        if (sourcesLocked != null)
        {
            reset.tooltip = new TipSignal(sourcesLocked);
        }

        options.Add(reset);
        options.Add(Toggle("VUIP.WorldSearchHighlight", () => s.worldSearchHighlight, v => s.worldSearchHighlight = v, null));
        string? rangeLocked = WorldSearchRange.RingActive ? null : "VUIP.WorldSearchLimitRangeUnavailable".Translate().ToString();
        options.Add(Toggle("VUIP.WorldSearchLimitRange", () => WorldSearchRange.LimitEnabled, v => WorldSearchRange.LimitEnabled = v, rangeLocked));
        return options;
    }

    // Toggling reopens the menu, so several options can be changed in a row.
    private static FloatMenuOption Toggle(string labelKey, Func<bool> get, Action<bool> set, string? lockedReason)
    {
        Action? action = null;
        if (lockedReason == null)
        {
            action = () =>
            {
                set(!get());
                OptionChanged();
                Find.WindowStack.Add(new FloatMenu(OptionsMenu()));
            };
        }

        FloatMenuOption option = new FloatMenuOption(labelKey.Translate(), action, extraPartWidth: 28f, extraPartOnGUI: r =>
        {
            Widgets.CheckboxDraw(r.x + 2f, r.center.y - 12f, get(), lockedReason != null, 24f);
            return false;
        });
        if (lockedReason != null)
        {
            option.tooltip = new TipSignal(lockedReason);
        }

        return option;
    }

    private static void OptionChanged()
    {
        UiPlusMod.Instance.WriteSettings();
        WorldQueryPanel.NotifyChanged();
    }
}
