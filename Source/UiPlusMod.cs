using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public class UiPlusMod : Mod
{
    public const float DefaultBarOpacity = 0.78f;
    public const float MinHudWidth = 140f;
    public const float MaxHudWidth = 320f;
    public const int MaxLetterSlots = 20;

    public static UiPlusSettings Settings = null!;
    public static UiPlusMod Instance = null!;

    public static bool Enabled => Settings.enabled;

    private const int PageCount = 6;

    private SettingsPage settingsPage;
    private readonly Vector2[] pageScroll = new Vector2[PageCount];
    private readonly float[] pageHeight = new float[PageCount];
    private bool speedAdvancedExpanded;
    private bool filterSizeAdvancedExpanded;
    private static string? filterTabWidthBuffer;
    private static string? filterTabHeightBuffer;

    // The locked reason for options that only work with the custom HUD; its text never changes while the game runs.
    private static string? hudRequiredReason;

    private static string? HudLocked => Settings.enabled ? null : hudRequiredReason ??= SettingsWidgets.RequiresSetting("VUIP.Enabled");

    private enum SettingsPage
    {
        Hud,
        Alerts,
        Inspect,
        Colony,
        NewGame,
        Controls
    }

    public UiPlusMod(ModContentPack content) : base(content)
    {
        Instance = this;
        Settings = GetSettings<UiPlusSettings>();
    }

    public override string SettingsCategory()
    {
        return "VUIP.SettingsCategory".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Rect pageRect = inRect;
        pageRect.yMin += TabDrawer.TabHeight;
        List<TabRecord> tabs = new List<TabRecord>
        {
            Tab("VUIP.HudTab", SettingsPage.Hud),
            Tab("VUIP.AlertsTab", SettingsPage.Alerts),
            Tab("VUIP.InspectTab", SettingsPage.Inspect),
            Tab("VUIP.ColonyTab", SettingsPage.Colony),
            Tab("VUIP.NewGameTab", SettingsPage.NewGame),
            Tab("VUIP.ControlsTab", SettingsPage.Controls)
        };
        TabDrawer.DrawTabs(pageRect, tabs);

        switch (settingsPage)
        {
            case SettingsPage.Hud:
                DrawPage(pageRect, "VUIP.HudTab", "VUIP.HudResetTip", ResetHudSettings, DrawHudPageContents);
                break;
            case SettingsPage.Alerts:
                DrawPage(pageRect, "VUIP.AlertsTab", "VUIP.AlertsResetTip", ResetAlertSettings, DrawAlertsPageContents);
                break;
            case SettingsPage.Inspect:
                DrawPage(pageRect, "VUIP.InspectTab", "VUIP.InspectResetTip", ResetInspectSettings, DrawInspectPageContents);
                break;
            case SettingsPage.Colony:
                DrawPage(pageRect, "VUIP.ColonyTab", "VUIP.ColonyResetTip", ResetColonySettings, DrawColonyPageContents);
                break;
            case SettingsPage.NewGame:
                DrawPage(pageRect, "VUIP.NewGameTab", "VUIP.NewGameResetTip", ResetNewGameSettings, DrawNewGamePageContents);
                break;
            case SettingsPage.Controls:
                DrawPage(pageRect, "VUIP.ControlsTab", "VUIP.KeybindsResetTip", ResetKeybindsSettings, DrawControlsPageContents);
                break;
        }
    }

    private TabRecord Tab(string labelKey, SettingsPage page)
    {
        return new TabRecord(labelKey.Translate(), () => settingsPage = page, settingsPage == page);
    }

    private void DrawPage(Rect inRect, string titleKey, string resetTipKey, Action reset, Action<Listing_Standard> drawContents)
    {
        int pageIndex = (int)settingsPage;
        float viewWidth = inRect.width - 16f;
        Rect view = new Rect(0f, 0f, viewWidth, Mathf.Max(pageHeight[pageIndex], inRect.height));
        Vector2 scroll = pageScroll[pageIndex];
        Widgets.BeginScrollView(inRect, ref scroll, view);
        Listing_Standard list = new Listing_Standard
        {
            maxOneColumn = true
        };
        list.Begin(view);

        string resetTip = resetTipKey.Translate();
        SettingsWidgets.Header(list.GetRect(36f), titleKey.Translate(), "VUIP.ResetPage".Translate(), resetTip, () =>
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "VUIP.ResetPageConfirm".Translate(titleKey.Translate(), resetTip), reset, destructive: true)));
        list.GapLine();
        drawContents(list);

        list.End();
        pageHeight[pageIndex] = list.CurHeight + 12f;
        pageScroll[pageIndex] = scroll;
        Widgets.EndScrollView();
    }

    private void DrawHudPageContents(Listing_Standard list)
    {
        string? hudLocked = HudLocked;
        SettingsWidgets.Subheader(list, "VUIP.HudGeneral".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.Enabled".Translate(), ref Settings.enabled, "VUIP.EnabledTip".Translate());

        int opacityPercent = Mathf.RoundToInt(Settings.barBackgroundOpacity * 100f);
        Settings.barBackgroundOpacity = SettingsWidgets.Slider(list, "VUIP.BarOpacity".Translate(opacityPercent),
            Settings.barBackgroundOpacity, 0f, 1f, "VUIP.BarOpacityTip".Translate(), hudLocked);
        Settings.barBackgroundOpacity = Mathf.Clamp01(Mathf.Round(Settings.barBackgroundOpacity * 100f) / 100f);
        Settings.hudWidth = Mathf.Round(SettingsWidgets.Slider(list, "VUIP.HudWidth".Translate(Settings.hudWidth.ToString("0")),
            Settings.hudWidth, MinHudWidth, MaxHudWidth, "VUIP.HudWidthTip".Translate(), hudLocked));

        // Letter content (raid timing, threat points) is on the Alerts page.
        SettingsWidgets.Subheader(list, "VUIP.HudAlerts".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.WrapText".Translate(), ref Settings.wrapText, "VUIP.WrapTextTip".Translate(), hudLocked);
        SettingsWidgets.Checkbox(list, "VUIP.WrapLetterText".Translate(), ref Settings.wrapLetterText, "VUIP.WrapLetterTextTip".Translate(), hudLocked);
        // 0 means no limit
        string slotsValue = Settings.maxLetterSlots == 0 ? "VUIP.MaxLetterSlotsNone".Translate().ToString() : Settings.maxLetterSlots.ToString();
        Settings.maxLetterSlots = Mathf.RoundToInt(SettingsWidgets.Slider(list, "VUIP.MaxLetterSlots".Translate(slotsValue),
            Settings.maxLetterSlots, 0f, MaxLetterSlots, "VUIP.MaxLetterSlotsTip".Translate(), hudLocked));
        SettingsWidgets.Checkbox(list, "VUIP.ReverseOrder".Translate(), ref Settings.reverseNotificationOrder, "VUIP.ReverseOrderTip".Translate(), hudLocked);

        SettingsWidgets.Subheader(list, "VUIP.HudDateTemp".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ColorTemperature".Translate(), ref Settings.colorTemperature,
            "VUIP.ColorTemperatureTip".Translate(ReadoutDrawer.ComfortMinCelsius.ToStringTemperature("F0"), ReadoutDrawer.ComfortMaxCelsius.ToStringTemperature("F0")), hudLocked);
        SettingsWidgets.Checkbox(list, "VUIP.OutdoorTemperature".Translate(), ref Settings.outdoorTemperature, "VUIP.OutdoorTemperatureTip".Translate(), hudLocked);
        SettingsWidgets.Checkbox(list, "VUIP.ColorDayNight".Translate(), ref Settings.colorDayNight, "VUIP.ColorDayNightTip".Translate(), hudLocked);
        SettingsWidgets.Checkbox(list, "VUIP.ShowPreciseTime".Translate(), ref Settings.showPreciseTime, "VUIP.ShowPreciseTimeTip".Translate(), hudLocked);
        SettingsWidgets.Checkbox(list, "VUIP.ShowColonyDay".Translate(), ref Settings.showColonyDay, "VUIP.ShowColonyDayTip".Translate(), hudLocked);
        SettingsWidgets.Checkbox(list, "VUIP.ShowColonyWealth".Translate(), ref Settings.showColonyWealth, "VUIP.ShowColonyWealthTip".Translate(), hudLocked);

        SettingsWidgets.Subheader(list, "VUIP.HudTimeSpeed".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.HideSpeedButtons".Translate(), ref Settings.hideSpeedButtons, "VUIP.HideSpeedButtonsTip".Translate(), hudLocked);
        bool ultrafastWasDisabled = Settings.disableUltrafast;
        SettingsWidgets.Checkbox(list, "VUIP.DisableUltrafast".Translate(), ref Settings.disableUltrafast, "VUIP.DisableUltrafastTip".Translate(), hudLocked);
        if (Settings.disableUltrafast && !ultrafastWasDisabled)
        {
            TimeSpeedControls.DropUltrafastIfDisabled();
        }

        string eventLabel = "VUIP.EventSpeedSetting".Translate(TimeSpeedControls.EventSpeedLabel(Settings.eventSpeedMode));
        if (hudLocked != null)
        {
            SettingsWidgets.Button(list, eventLabel, hudLocked);
        }
        else
        {
            list.Gap(6f);
            Rect eventRect = list.GetRect(30f);
            if (Widgets.ButtonText(eventRect, eventLabel))
            {
                TimeSpeedControls.ShowEventSpeedMenu();
            }

            TooltipHandler.TipRegion(eventRect, "VUIP.EventSpeedTip".Translate());
        }

        SettingsWidgets.AdvancedToggle(list, ref speedAdvancedExpanded, hudLocked);
        if (speedAdvancedExpanded)
        {
            Settings.speedNormal = DrawSpeedSlider(list, "VUIP.SpeedNormal", Settings.speedNormal, 0.1f, 3f, hudLocked);
            Settings.speedFast = DrawSpeedSlider(list, "VUIP.SpeedFast", Settings.speedFast, 0.1f, 6f, hudLocked);
            Settings.speedSuperfast = DrawSpeedSlider(list, "VUIP.SpeedSuperfast", Settings.speedSuperfast, 0.1f, 15f, hudLocked);
            Settings.speedUltrafast = DrawSpeedSlider(list, "VUIP.SpeedUltrafast", Settings.speedUltrafast, 0.1f, 150f, hudLocked);
        }

        SettingsWidgets.Subheader(list, "VUIP.HudPlayButtons".Translate());
        DrawPlayButtonSummary(list, hudLocked);

        // Works with or without the custom HUD.
        SettingsWidgets.Subheader(list, "VUIP.ColonistBarSection".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShiftColonistBarInDevMode".Translate(), ref Settings.shiftColonistBarInDevMode, "VUIP.ShiftColonistBarInDevModeTip".Translate());
        Settings.colonistBarDevOffset = Mathf.Round(SettingsWidgets.Slider(list,
            "VUIP.ColonistBarDevOffset".Translate(Settings.colonistBarDevOffset.ToString("0")),
            Settings.colonistBarDevOffset, 0f, 48f, "VUIP.ColonistBarDevOffsetTip".Translate(),
            Settings.shiftColonistBarInDevMode ? null : SettingsWidgets.RequiresSetting("VUIP.ShiftColonistBarInDevMode")));
    }

    private static void DrawAlertsPageContents(Listing_Standard list)
    {
        string? hudLocked = HudLocked;
        SettingsWidgets.Subheader(list, "VUIP.NotificationsAdded".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowBleedingOutAlert".Translate(), ref Settings.showBleedingOutAlert, "VUIP.ShowBleedingOutAlertTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowHostilesPresentAlert".Translate(), ref Settings.showHostilesPresentAlert, "VUIP.ShowHostilesPresentAlertTip".Translate(), hudLocked);
        SettingsWidgets.Checkbox(list, "VUIP.ShowTraderPresentAlert".Translate(), ref Settings.showTraderPresentAlert, "VUIP.ShowTraderPresentAlertTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowBatteriesLowAlert".Translate(), ref Settings.showBatteriesLowAlert, "VUIP.ShowBatteriesLowAlertTip".Translate());
        string? batteryLocked = Settings.showBatteriesLowAlert ? null : SettingsWidgets.RequiresSetting("VUIP.ShowBatteriesLowAlert");
        Settings.batteryLowPercent = Mathf.Round(SettingsWidgets.Slider(list,
            "VUIP.BatteryLowPercent".Translate(Settings.batteryLowPercent.ToString("0")),
            Settings.batteryLowPercent, 1f, 99f, "VUIP.BatteryLowPercentTip".Translate(), batteryLocked));
        Settings.batteryLowHours = Mathf.Round(SettingsWidgets.Slider(list,
            "VUIP.BatteryLowHours".Translate(Settings.batteryLowHours.ToString("0")),
            Settings.batteryLowHours, 1f, 24f, "VUIP.BatteryLowHoursTip".Translate(), batteryLocked));

        SettingsWidgets.Subheader(list, "VUIP.NotificationsVanilla".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.HideLockedResearchBenchAlert".Translate(), ref Settings.hideLockedResearchBenchAlert, "VUIP.HideLockedResearchBenchAlertTip".Translate());

        // Snoozing is a right-click on the custom HUD's alerts.
        SettingsWidgets.Subheader(list, "VUIP.NotificationsSnooze".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.EnableSnooze".Translate(), ref Settings.enableSnooze, "VUIP.EnableSnoozeTip".Translate(), hudLocked);
        Settings.snoozeDays = Mathf.Clamp(Settings.snoozeDays, 1, 15);
        Settings.snoozeDays = Mathf.Clamp(Mathf.RoundToInt(SettingsWidgets.Slider(list,
            "VUIP.SnoozeDays".Translate(Settings.snoozeDays), Settings.snoozeDays, 1f, 15f, "VUIP.SnoozeDaysTip".Translate(),
            hudLocked ?? (Settings.enableSnooze ? null : SettingsWidgets.RequiresSetting("VUIP.EnableSnooze")))), 1, 15);

        // Offered with snoozing off too: turning it off leaves existing snoozes saved.
        if (SettingsWidgets.Button(list, "VUIP.ClearSnoozes".Translate()))
        {
            if (Current.ProgramState == ProgramState.Playing && SnoozeTracker.Current() is SnoozeTracker tracker)
            {
                int count = tracker.ClearAll();
                Messages.Message("VUIP.ClearSnoozesDone".Translate(count), MessageTypeDefOf.PositiveEvent, historical: false);
            }
            else
            {
                Messages.Message("VUIP.ClearSnoozesNeedGame".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            }
        }

        // Changes the letters themselves, so it works with or without the custom HUD.
        SettingsWidgets.Subheader(list, "VUIP.LettersSection".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.RaidLetterStrategy".Translate(), ref Settings.raidLetterStrategy, "VUIP.RaidLetterStrategyTip".Translate());
        string? threatPointsLocked = ThreatPoints.OtherModActive ? "VUIP.ThreatPointsOtherMod".Translate().ToString() : null;
        SettingsWidgets.Checkbox(list, "VUIP.ThreatPointsInText".Translate(), ref Settings.threatPointsInText, "VUIP.ThreatPointsInTextTip".Translate(), threatPointsLocked);
        SettingsWidgets.Checkbox(list, "VUIP.ThreatPointsInLabel".Translate(), ref Settings.threatPointsInLabel, "VUIP.ThreatPointsInLabelTip".Translate(), threatPointsLocked);
        SettingsWidgets.Checkbox(list, "VUIP.ThreatPointsFriendly".Translate(), ref Settings.threatPointsFriendly, "VUIP.ThreatPointsFriendlyTip".Translate(), threatPointsLocked);
    }

    private static void DrawInspectPageContents(Listing_Standard list)
    {
        SettingsWidgets.Subheader(list, "VUIP.PawnPaneSection".Translate());
        DrawPawnPaneSection(list);

        SettingsWidgets.Subheader(list, "VUIP.GearTabSection".Translate());
        string? gearLocked = GearTab.ConflictingMod is { } conflict ? "VUIP.GearTabOtherModActive".Translate(conflict).ToString() : null;
        SettingsWidgets.Checkbox(list, "VUIP.GearTabEnabled".Translate(), ref Settings.gearTabEnabled, "VUIP.GearTabEnabledTip".Translate(), gearLocked);

        SettingsWidgets.Subheader(list, "VUIP.ObjectPaneSection".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowWildAnimalYields".Translate(), ref Settings.showWildAnimalYields, "VUIP.ShowWildAnimalYieldsTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.GrowingZonePaneEnabled".Translate(), ref Settings.growingZonePaneEnabled, "VUIP.GrowingZonePaneEnabledTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.WorkbenchPaneEnabled".Translate(), ref Settings.workbenchPaneEnabled, "VUIP.WorkbenchPaneEnabledTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.PowerPaneEnabled".Translate(), ref Settings.powerPaneEnabled, "VUIP.PowerPaneEnabledTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ItemPaneEnabled".Translate(), ref Settings.itemPaneEnabled, "VUIP.ItemPaneEnabledTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.StoragePaneEnabled".Translate(), ref Settings.storagePaneEnabled, "VUIP.StoragePaneEnabledTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.PlantPaneEnabled".Translate(), ref Settings.plantPaneEnabled, "VUIP.PlantPaneEnabledTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.BuildingPaneEnabled".Translate(), ref Settings.buildingPaneEnabled, "VUIP.BuildingPaneEnabledTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.WorldPaneEnabled".Translate(), ref Settings.worldPaneEnabled, "VUIP.WorldPaneEnabledTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowRecreationType".Translate(), ref Settings.showRecreationType, "VUIP.ShowRecreationTypeTip".Translate());
    }

    private void DrawColonyPageContents(Listing_Standard list)
    {
        SettingsWidgets.Subheader(list, "VUIP.PawnTableSection".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowShiftScheduleArrows".Translate(), ref Settings.showShiftScheduleArrows, "VUIP.ShowShiftScheduleArrowsTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowScheduleTimeLine".Translate(), ref Settings.showScheduleTimeLine,
            "VUIP.ShowScheduleTimeLineTip".Translate(),
            Patch_PawnTable_PawnTableOnGUI.ChronosPointerActive ? "VUIP.ScheduleTimeLineChronosPointer".Translate().ToString() : null);
        SettingsWidgets.Checkbox(list, "VUIP.ShiftClickAssignAreaToAll".Translate(), ref Settings.shiftClickAssignAreaToAll, "VUIP.ShiftClickAssignAreaToAllTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.GroupAnimalsBySpecies".Translate(), ref Settings.groupAnimalsBySpecies, "VUIP.GroupAnimalsBySpeciesTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.GroupWildlifeBySpecies".Translate(), ref Settings.groupWildlifeBySpecies, "VUIP.GroupWildlifeBySpeciesTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowLeatherColumn".Translate(), ref Settings.showLeatherColumn, "VUIP.ShowLeatherColumnTip".Translate());

        SettingsWidgets.Subheader(list, "VUIP.DefaultPawnSettingsSection".Translate());
        DrawDefaultSetting(list, "VUIP.ApplyDefaultSchedule", ref Settings.applyDefaultSchedule,
            DefaultSchedule.IsSet(false), DefaultSchedule.IsSet(true), "VUIP.ClearDefaultSchedule", "VUIP.ClearDefaultScheduleSlave", "VUIP.DefaultScheduleNone", DefaultSchedule.Clear);
        DrawDefaultSetting(list, "VUIP.ApplyDefaultWorkPriorities", ref Settings.applyDefaultWorkPriorities,
            DefaultWorkPriorities.IsSet(false), DefaultWorkPriorities.IsSet(true), "VUIP.ClearDefaultWorkPriorities", "VUIP.ClearDefaultWorkPrioritiesSlave", "VUIP.DefaultWorkPrioritiesNone", DefaultWorkPriorities.Clear);
        DrawDefaultSetting(list, "VUIP.ApplyDefaultAssignments", ref Settings.applyDefaultAssignments,
            DefaultAssignments.IsSet(false), DefaultAssignments.IsSet(true), "VUIP.ClearDefaultAssignments", "VUIP.ClearDefaultAssignmentsSlave", "VUIP.DefaultAssignmentsNone", DefaultAssignments.Clear);

        SettingsWidgets.Subheader(list, "VUIP.ResourceReadoutSection".Translate());
        DrawResourceReadoutSection(list);

        SettingsWidgets.Subheader(list, "VUIP.StorageFilterSection".Translate());
        DrawStorageFilterSection(list);

        SettingsWidgets.Subheader(list, "VUIP.OverlaysSection".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.AutoGrowingZoneFertility".Translate(), ref Settings.autoGrowingZoneFertility,
            "VUIP.AutoGrowingZoneFertilityTip".Translate());
        if (!Settings.autoGrowingZoneFertility)
        {
            DesignatorOverlay.GrowingZoneFertility.Restore();
        }

        SettingsWidgets.Checkbox(list, "VUIP.AutoBuildTerrainAffordance".Translate(), ref Settings.autoBuildTerrainAffordance,
            "VUIP.AutoBuildTerrainAffordanceTip".Translate());
        if (!Settings.autoBuildTerrainAffordance)
        {
            DesignatorOverlay.BuildTerrainAffordance.Restore();
        }

        SettingsWidgets.Subheader(list, "VUIP.SearchSection".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.GroupSearchResults".Translate(), ref Settings.groupSearchResults, "VUIP.GroupSearchResultsTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.WorldSearchAllTiles".Translate(), ref Settings.worldSearchAllTiles, "VUIP.WorldSearchAllTilesTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.WorldSearchAdvanced".Translate(), ref Settings.worldSearchAdvanced, "VUIP.WorldSearchAdvancedTip".Translate(),
            Settings.worldSearchAllTiles ? null : SettingsWidgets.RequiresSetting("VUIP.WorldSearchAllTiles"));
        SettingsWidgets.Checkbox(list, "VUIP.WorldSearchHighlight".Translate(), ref Settings.worldSearchHighlight, "VUIP.WorldSearchHighlightTip".Translate());

        SettingsWidgets.Subheader(list, "VUIP.MainBarSection".Translate());
        DrawMainButtonSummary(list);
    }

    private static void DrawNewGamePageContents(Listing_Standard list)
    {
        SettingsWidgets.Subheader(list, "VUIP.TitleScreenSection".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.HideTutorialButton".Translate(), ref Settings.hideTutorialButton, "VUIP.HideTutorialButtonTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowContinueButton".Translate(), ref Settings.showContinueButton, "VUIP.ShowContinueButtonTip".Translate());

        SettingsWidgets.Subheader(list, "VUIP.ScenarioSection".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.SortScenarioListByTechLevel".Translate(), ref Settings.sortScenarioListByTechLevel, "VUIP.SortScenarioListByTechLevelTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ColorScenarioListByTechLevel".Translate(), ref Settings.colorScenarioListByTechLevel, "VUIP.ColorScenarioListByTechLevelTip".Translate());

        SettingsWidgets.Subheader(list, "VUIP.DefaultNewGameSettingsSection".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.EnableNewGameDefaults".Translate(), ref Settings.enableNewGameDefaults, "VUIP.EnableNewGameDefaultsTip".Translate());
        string? newGameLocked = Settings.enableNewGameDefaults ? null : SettingsWidgets.RequiresSetting("VUIP.EnableNewGameDefaults");
        if (Settings.storytellerDefaults != null && SettingsWidgets.Button(list, "VUIP.ClearStorytellerDefault".Translate(), newGameLocked))
        {
            Settings.storytellerDefaults = null;
            Instance.WriteSettings();
        }

        if (Settings.worldDefaults != null && SettingsWidgets.Button(list, "VUIP.ClearWorldDefault".Translate(), newGameLocked))
        {
            Settings.worldDefaults = null;
            Instance.WriteSettings();
        }

        SettingsWidgets.Subheader(list, "VUIP.DefaultQuestRewardSettingsSection".Translate());
        if (!QuestRewardDefaults.IsSet)
        {
            SettingsWidgets.StatusLabel(list, "VUIP.QuestRewardDefaultsNone".Translate());
        }
        else if (SettingsWidgets.Button(list, "VUIP.ClearQuestRewardDefaults".Translate()))
        {
            QuestRewardDefaults.Clear();
        }
    }

    private static void DrawControlsPageContents(Listing_Standard list)
    {
        SettingsWidgets.Subheader(list, "VUIP.KeybindsSection".Translate());
        DrawKeybindsSection(list);
        if (SettingsWidgets.Button(list, "KeyboardConfig".Translate()))
        {
            Find.WindowStack.Add(new Dialog_KeyBindings());
        }
    }

    private static void DrawPawnPaneSection(Listing_Standard list)
    {
        string? conflictLocked = PawnReadout.ConflictingMod is { } conflict ? "VUIP.PawnPaneOtherModActive".Translate(conflict).ToString() : null;
        SettingsWidgets.Checkbox(list, "VUIP.PawnPaneEnabled".Translate(), ref Settings.pawnPaneEnabled, "VUIP.PawnPaneEnabledTip".Translate(), conflictLocked);
        string? locked = conflictLocked ?? (Settings.pawnPaneEnabled ? null : SettingsWidgets.RequiresSetting("VUIP.PawnPaneEnabled"));
        SettingsWidgets.Checkbox(list, "VUIP.PawnPaneOtherPawns".Translate(), ref Settings.pawnPaneOtherPawns, "VUIP.PawnPaneOtherPawnsTip".Translate(), locked);
        string? armorLocked = locked ?? (PawnReadout.ArmorUnavailable ? "VUIP.PawnPaneArmorCombatExtended".Translate().ToString() : null);
        SettingsWidgets.Checkbox(list, "VUIP.PawnPaneShowArmor".Translate(), ref Settings.pawnPaneShowArmor, "VUIP.PawnPaneShowArmorTip".Translate(), armorLocked);
        SettingsWidgets.Checkbox(list, "VUIP.PawnPaneShowSpeed".Translate(), ref Settings.pawnPaneShowSpeed, "VUIP.PawnPaneShowSpeedTip".Translate(), locked);
        SettingsWidgets.Checkbox(list, "VUIP.PawnPaneShowDps".Translate(), ref Settings.pawnPaneShowDps, "VUIP.PawnPaneShowDpsTip".Translate(), locked);
        SettingsWidgets.Checkbox(list, "VUIP.PawnPaneShowSkills".Translate(), ref Settings.pawnPaneShowSkills, "VUIP.PawnPaneShowSkillsTip".Translate(), locked);
        string? skillsLocked = locked ?? (Settings.pawnPaneShowSkills ? null : SettingsWidgets.RequiresSetting("VUIP.PawnPaneShowSkills"));
        SettingsWidgets.Checkbox(list, "VUIP.PawnPaneSkillStats".Translate(), ref Settings.pawnPaneSkillStats, "VUIP.PawnPaneSkillStatsTip".Translate(), skillsLocked);
        string? expertiseLocked = skillsLocked ?? (SkillTooltip.SkillsExpandedActive ? null : "VUIP.PawnPaneSkillExpertiseMissing".Translate().ToString());
        SettingsWidgets.Checkbox(list, "VUIP.PawnPaneSkillExpertise".Translate(), ref Settings.pawnPaneSkillExpertise, "VUIP.PawnPaneSkillExpertiseTip".Translate(), expertiseLocked);
        SettingsWidgets.Checkbox(list, "VUIP.PawnPaneShowSelfTend".Translate(), ref Settings.pawnPaneShowSelfTend, "VUIP.PawnPaneShowSelfTendTip".Translate(), locked);
        SettingsWidgets.Checkbox(list, "VUIP.PawnPaneShowRename".Translate(), ref Settings.pawnPaneShowRename, "VUIP.PawnPaneShowRenameTip".Translate(), locked);
        SettingsWidgets.Checkbox(list, "VUIP.PawnPaneShowIdentity".Translate(), ref Settings.pawnPaneShowIdentity, "VUIP.PawnPaneShowIdentityTip".Translate(), locked);
        SettingsWidgets.Checkbox(list, "VUIP.PawnPaneColorHealthBar".Translate(), ref Settings.pawnPaneColorHealthBar, "VUIP.PawnPaneColorHealthBarTip".Translate(), locked);
        SettingsWidgets.Checkbox(list, "VUIP.PawnPaneColorMoodBar".Translate(), ref Settings.pawnPaneColorMoodBar, "VUIP.PawnPaneColorMoodBarTip".Translate(), locked);
        Settings.pawnPaneNeedThreshold = Mathf.Round(SettingsWidgets.Slider(list,
            "VUIP.PawnPaneNeedThreshold".Translate(Settings.pawnPaneNeedThreshold.ToString("0")),
            Settings.pawnPaneNeedThreshold, 0f, 100f, "VUIP.PawnPaneNeedThresholdTip".Translate(), locked));
        string? widthLocked = locked ?? (Settings.pawnPaneShowSkills ? null : SettingsWidgets.RequiresSetting("VUIP.PawnPaneShowSkills"));
        Settings.pawnPaneWidth = Mathf.Round(SettingsWidgets.Slider(list,
            "VUIP.PawnPaneWidth".Translate(Settings.pawnPaneWidth.ToString("0")),
            Settings.pawnPaneWidth, PawnReadout.MinPaneWidth, PawnReadout.MaxPaneWidth, "VUIP.PawnPaneWidthTip".Translate(), widthLocked));
    }

    private static void DrawStorageFilterSection(Listing_Standard list)
    {
        SettingsWidgets.Checkbox(list, "VUIP.CollapseFilterCategories".Translate(), ref Settings.collapseFilterCategoriesByDefault, "VUIP.CollapseFilterCategoriesTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.FocusStorageSearch".Translate(), ref Settings.focusStorageSearch, "VUIP.FocusStorageSearchTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ResizeFilterTab".Translate(), ref Settings.resizeFilterTab, "VUIP.ResizeFilterTabTip".Translate());
        string? locked = Settings.resizeFilterTab ? null : SettingsWidgets.RequiresSetting("VUIP.ResizeFilterTab");
        SettingsWidgets.AdvancedToggle(list, ref Instance.filterSizeAdvancedExpanded, locked);
        if (Instance.filterSizeAdvancedExpanded)
        {
            Rect row = list.GetRect(28f);
            Rect left = row.LeftHalf().ContractedBy(4f, 0f);
            Rect right = row.RightHalf().ContractedBy(4f, 0f);
            DrawSizeField(left, "VUIP.FilterTabWidth".Translate(), ref Settings.filterTabWidth, ref filterTabWidthBuffer, 300f, 1200f, locked);
            DrawSizeField(right, "VUIP.FilterTabHeight".Translate(), ref Settings.filterTabHeight, ref filterTabHeightBuffer, 300f, 1600f, locked);
            list.Gap(6f);
        }
    }

    // Clamps only on Enter or losing focus; Widgets.TextFieldNumeric clamps on every keystroke.
    private static void DrawSizeField(Rect rect, string label, ref float value, ref string? buffer, float min, float max, string? lockedReason)
    {
        Rect labelRect = rect.LeftHalf();
        Rect fieldRect = rect.RightHalf();
        Color old = GUI.color;
        if (lockedReason != null)
        {
            GUI.color = Widgets.InactiveColor;
        }

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, label);
        if (lockedReason != null)
        {
            Widgets.DrawBox(fieldRect);
            Widgets.Label(fieldRect.ContractedBy(4f, 0f), value.ToString("0"));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = old;
            TooltipHandler.TipRegion(rect, lockedReason);
            buffer = null;
            return;
        }

        Text.Anchor = TextAnchor.UpperLeft;
        buffer ??= value.ToString("0");
        string controlName = "VUIP.SizeField." + label;
        GUI.SetNextControlName(controlName);
        buffer = Widgets.TextField(fieldRect, buffer);

        bool focused = GUI.GetNameOfFocusedControl() == controlName;
        bool committed = focused && Event.current.type == EventType.KeyDown
            && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        if (!focused || committed)
        {
            if (float.TryParse(buffer, out float parsed))
            {
                value = Mathf.Clamp(parsed, min, max);
            }

            buffer = value.ToString("0");
        }
    }

    private static void DrawResourceReadoutSection(Listing_Standard list)
    {
        SettingsWidgets.Checkbox(list, "VUIP.EditResourceReadout".Translate(), ref Settings.editResourceReadout, "VUIP.EditResourceReadoutTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowZeroResources".Translate(), ref Settings.showZeroResources, "VUIP.ShowZeroResourcesTip".Translate());
        bool countHidden = Settings.countHiddenInTotals;
        SettingsWidgets.Checkbox(list, "VUIP.CountHiddenInTotals".Translate(), ref Settings.countHiddenInTotals, "VUIP.CountHiddenInTotalsTip".Translate());
        if (countHidden != Settings.countHiddenInTotals)
        {
            ResourceReadoutTweaks.NotifyChanged();
        }

        if (ResourceReadoutTweaks.HasCustomOrder && SettingsWidgets.Button(list, "VUIP.ResetResourceOrder".Translate()))
        {
            ResourceReadoutTweaks.ResetOrder();
        }

        if (Settings.resourceHidden.Count > 0
            && SettingsWidgets.Button(list, "VUIP.ShowHiddenResourcesCount".Translate(Settings.resourceHidden.Count)))
        {
            ResourceReadoutTweaks.ShowAllHidden();
        }

        if (Settings.resourceCountAll.Count > 0
            && SettingsWidgets.Button(list, "VUIP.ClearResourceCountAll".Translate(Settings.resourceCountAll.Count)))
        {
            ResourceReadoutTweaks.ClearCountAll();
        }
    }

    private static void DrawMainButtonSummary(Listing_Standard list)
    {
        MainButtonLayout.EnsureInitialized();
        int dropdown = 0;
        int hidden = 0;
        foreach (MainButtonLayoutEntry entry in Settings.mainButtons)
        {
            if (MainButtonLayout.IsMenu(entry))
            {
                continue;
            }

            if (entry.placement == MainButtonPlacement.Dropdown)
            {
                dropdown++;
            }
            else if (entry.placement == MainButtonPlacement.Hidden)
            {
                hidden++;
            }
        }

        SettingsWidgets.StatusLabel(list, "VUIP.MainBarSummary".Translate(dropdown, hidden));
        if (SettingsWidgets.Button(list, "VUIP.ConfigureMainBar".Translate()))
        {
            Find.WindowStack.Add(new Dialog_MainButtonSettings());
        }
    }

    private static void DrawPlayButtonSummary(Listing_Standard list, string? lockedReason)
    {
        int mapVisible = PlayButtonFilter.CountVisible(world: false);
        int worldVisible = PlayButtonFilter.CountVisible(world: true);
        int mapHidden = Mathf.Max(0, PlayButtonFilter.MapButtons.Count - mapVisible);
        int worldHidden = Mathf.Max(0, PlayButtonFilter.WorldButtons.Count - worldVisible);
        SettingsWidgets.StatusLabel(list, "VUIP.PlayButtonsSummary".Translate(mapHidden, worldHidden));
        if (SettingsWidgets.Button(list, "VUIP.ConfigurePlayButtons".Translate(), lockedReason))
        {
            Find.WindowStack.Add(new Dialog_PlayButtonSettings());
        }
    }

    // The label key's "Tip" variant is the checkbox tooltip.
    // The clear action takes true for the slave default.
    private static void DrawDefaultSetting(Listing_Standard list, string labelKey, ref bool enabled, bool isSet, bool isSlaveSet, string clearKey, string clearSlaveKey, string noneKey, Action<bool> clear)
    {
        SettingsWidgets.Checkbox(list, labelKey.Translate(), ref enabled, (labelKey + "Tip").Translate());
        string? locked = enabled ? null : SettingsWidgets.RequiresSetting(labelKey);
        if (!isSet && !isSlaveSet)
        {
            SettingsWidgets.StatusLabel(list, noneKey.Translate());
            return;
        }

        if (isSet && SettingsWidgets.Button(list, clearKey.Translate(), locked))
        {
            clear(false);
        }

        if (isSlaveSet && SettingsWidgets.Button(list, clearSlaveKey.Translate(), locked))
        {
            clear(true);
        }
    }

    // A hotkey another mod already provides is shown locked, naming that mod.
    private static void DrawKeybindsSection(Listing_Standard list)
    {
        string? unforbidLocked = null;
        if (UnforbidAllHotkey.HandledByOtherMod)
        {
            string handledByMod = UnforbidAllHotkey.KeyzAllowUtilitiesActive
                ? "VUIP.KeyzAllowUtilitiesName".Translate()
                : "VUIP.AllowToolName".Translate();
            unforbidLocked = "VUIP.UnforbidAllHandledByOtherMod".Translate(handledByMod);
        }

        SettingsWidgets.Checkbox(list, KeybindSettingLabel("VUIP.EnableUnforbidAllHotkey", VUIPDefOf.VUIP_UnforbidAll),
            ref Settings.enableUnforbidAllHotkey, "VUIP.EnableUnforbidAllHotkeyTip".Translate(), unforbidLocked);

        SettingsWidgets.Checkbox(list, KeybindSettingLabel("VUIP.EnableTemperatureOverlayHotkey", VUIPDefOf.VUIP_ToggleTemperatureOverlay),
            ref Settings.enableTemperatureOverlayHotkey, "VUIP.EnableTemperatureOverlayHotkeyTip".Translate());

        SettingsWidgets.Checkbox(list, KeybindSettingLabel("VUIP.EnableDevModeHotkey", VUIPDefOf.VUIP_ToggleDevMode),
            ref Settings.enableDevModeHotkey, "VUIP.EnableDevModeHotkeyTip".Translate());
    }

    private static string KeybindSettingLabel(string labelKey, KeyBindingDef def)
    {
        string key = KeyPrefs.KeyPrefsData.GetBoundKeyCode(def, KeyPrefs.BindingSlot.A).ToStringReadable();
        return "VUIP.KeybindWithAssignedKey".Translate(labelKey.Translate(), key);
    }

    // The label key's "Tip" variant is the slider tooltip.
    private static float DrawSpeedSlider(Listing_Standard list, string labelKey, float value, float min, float max, string? lockedReason)
    {
        value = SettingsWidgets.Slider(list, labelKey.Translate(value.ToString("0.##")), value, min, max, (labelKey + "Tip").Translate(), lockedReason);
        return (float)Math.Round(value, 2);
    }

    private static void ResetHudSettings()
    {
        Settings.enabled = true;
        Settings.barBackgroundOpacity = DefaultBarOpacity;
        Settings.hudWidth = AlertDrawer.DefaultBarWidth;
        Settings.wrapText = false;
        Settings.wrapLetterText = false;
        Settings.maxLetterSlots = 0;
        Settings.reverseNotificationOrder = false;
        Settings.colorTemperature = true;
        Settings.outdoorTemperature = true;
        Settings.colorDayNight = true;
        Settings.showPreciseTime = true;
        Settings.showColonyDay = true;
        Settings.showColonyWealth = true;
        Settings.hideSpeedButtons = false;
        Settings.disableUltrafast = false;
        Settings.eventSpeedMode = EventSpeedMode.Normal;
        Settings.speedNormal = TimeSpeedControls.DefaultSpeedNormal;
        Settings.speedFast = TimeSpeedControls.DefaultSpeedFast;
        Settings.speedSuperfast = TimeSpeedControls.DefaultSpeedSuperfast;
        Settings.speedUltrafast = TimeSpeedControls.DefaultSpeedUltrafast;
        Settings.showPlayButtons.Clear();
        PlayButtonFilter.NotifyChanged();
        Settings.shiftColonistBarInDevMode = true;
        Settings.colonistBarDevOffset = 12f;
        Instance.WriteSettings();
    }

    private static void ResetAlertSettings()
    {
        Settings.showBleedingOutAlert = true;
        Settings.showHostilesPresentAlert = true;
        Settings.showTraderPresentAlert = true;
        Settings.showBatteriesLowAlert = true;
        Settings.batteryLowHours = 6f;
        Settings.batteryLowPercent = 20f;
        Settings.hideLockedResearchBenchAlert = true;
        Settings.enableSnooze = true;
        Settings.snoozeDays = 3;
        Settings.raidLetterStrategy = true;
        Settings.threatPointsInText = true;
        Settings.threatPointsInLabel = false;
        Settings.threatPointsFriendly = false;
        Instance.WriteSettings();
    }

    private static void ResetInspectSettings()
    {
        Settings.pawnPaneEnabled = true;
        Settings.pawnPaneOtherPawns = true;
        Settings.pawnPaneShowArmor = true;
        Settings.pawnPaneShowSpeed = true;
        Settings.pawnPaneShowDps = true;
        Settings.pawnPaneShowSkills = true;
        Settings.pawnPaneSkillStats = true;
        Settings.pawnPaneSkillExpertise = true;
        Settings.pawnPaneShowSelfTend = true;
        Settings.pawnPaneShowRename = true;
        Settings.pawnPaneShowIdentity = true;
        Settings.pawnPaneColorHealthBar = true;
        Settings.pawnPaneColorMoodBar = true;
        Settings.pawnPaneNeedThreshold = 30f;
        Settings.pawnPaneNeedsView = false;
        Settings.pawnPaneWidth = PawnReadout.DefaultPaneWidth;
        Settings.gearTabEnabled = true;
        Settings.showWildAnimalYields = true;
        Settings.growingZonePaneEnabled = true;
        Settings.workbenchPaneEnabled = true;
        Settings.powerPaneEnabled = true;
        Settings.itemPaneEnabled = true;
        Settings.storagePaneEnabled = true;
        Settings.buildingPaneEnabled = true;
        Settings.plantPaneEnabled = true;
        Settings.worldPaneEnabled = true;
        Settings.showRecreationType = true;
        Instance.WriteSettings();
    }

    private static void ResetColonySettings()
    {
        Settings.showShiftScheduleArrows = true;
        Settings.showScheduleTimeLine = true;
        Settings.shiftClickAssignAreaToAll = true;
        Settings.groupAnimalsBySpecies = true;
        Settings.groupWildlifeBySpecies = true;
        Settings.showLeatherColumn = true;
        Settings.applyDefaultSchedule = true;
        Settings.defaultSchedule = null;
        Settings.defaultScheduleSlave = null;
        Settings.applyDefaultWorkPriorities = true;
        Settings.defaultWorkPriorities = null;
        Settings.defaultWorkPrioritiesSlave = null;
        Settings.applyDefaultAssignments = true;
        Settings.defaultAssignments = null;
        Settings.defaultAssignmentsSlave = null;
        Settings.editResourceReadout = true;
        Settings.showZeroResources = false;
        Settings.countHiddenInTotals = true;
        Settings.resourceOrderSimple.Clear();
        Settings.resourceOrderCategorized.Clear();
        Settings.resourceCountAll.Clear();
        Settings.resourceParents.Clear();
        Settings.resourceHidden.Clear();
        ResourceReadoutTweaks.NotifyChanged();
        Settings.collapseFilterCategoriesByDefault = true;
        Settings.focusStorageSearch = false;
        Settings.resizeFilterTab = true;
        Settings.filterTabWidth = 460f;
        Settings.filterTabHeight = 560f;
        filterTabWidthBuffer = null;
        filterTabHeightBuffer = null;
        Settings.autoGrowingZoneFertility = true;
        Settings.autoBuildTerrainAffordance = true;
        Settings.groupSearchResults = true;
        Settings.worldSearchAllTiles = true;
        Settings.worldSearchAdvanced = true;
        Settings.worldSearchHighlight = true;
        WorldSearchText.ResetSources();
        MainButtonLayout.ResetToDefaults();
        Instance.WriteSettings();
    }

    private static void ResetNewGameSettings()
    {
        Settings.hideTutorialButton = true;
        Settings.showContinueButton = true;
        Settings.sortScenarioListByTechLevel = true;
        Settings.colorScenarioListByTechLevel = true;
        Settings.enableNewGameDefaults = true;
        Settings.storytellerDefaults = null;
        Settings.worldDefaults = null;
        Settings.defaultGoodwillRewards.Clear();
        Settings.defaultRoyalFavorRewards.Clear();
        Instance.WriteSettings();
    }

    private static void ResetKeybindsSettings()
    {
        Settings.enableUnforbidAllHotkey = true;
        Settings.enableTemperatureOverlayHotkey = true;
        Settings.enableDevModeHotkey = true;
        Instance.WriteSettings();
    }
}
