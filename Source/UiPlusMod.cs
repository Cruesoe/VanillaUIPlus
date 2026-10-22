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

    public static UiPlusSettings Settings = null!;
    public static UiPlusMod Instance = null!;

    public static bool Enabled => Settings.enabled;

    private SettingsPage settingsPage;
    private readonly Vector2[] pageScroll = new Vector2[5];
    private readonly float[] pageHeight = new float[5];
    private bool speedAdvancedExpanded;
    private bool colonistBarAdvancedExpanded;
    private bool filterSizeAdvancedExpanded;
    private static string? filterTabWidthBuffer;
    private static string? filterTabHeightBuffer;

    private enum SettingsPage
    {
        Hud,
        Notifications,
        Interface,
        Defaults,
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
            new TabRecord("VUIP.HudTab".Translate(), () => settingsPage = SettingsPage.Hud, settingsPage == SettingsPage.Hud),
            new TabRecord("VUIP.NotificationsTab".Translate(), () => settingsPage = SettingsPage.Notifications, settingsPage == SettingsPage.Notifications),
            new TabRecord("VUIP.InterfaceTab".Translate(), () => settingsPage = SettingsPage.Interface, settingsPage == SettingsPage.Interface),
            new TabRecord("VUIP.DefaultsTab".Translate(), () => settingsPage = SettingsPage.Defaults, settingsPage == SettingsPage.Defaults),
            new TabRecord("VUIP.ControlsTab".Translate(), () => settingsPage = SettingsPage.Controls, settingsPage == SettingsPage.Controls)
        };
        TabDrawer.DrawTabs(pageRect, tabs);

        switch (settingsPage)
        {
            case SettingsPage.Hud:
                DrawPage(pageRect, "VUIP.HudTab", "VUIP.HudResetTip", ResetHudSettings, DrawHudPageContents);
                break;
            case SettingsPage.Notifications:
                DrawPage(pageRect, "VUIP.NotificationsTab", "VUIP.CustomNotificationsResetTip", ResetCustomNotificationSettings, DrawNotificationsPageContents);
                break;
            case SettingsPage.Interface:
                DrawPage(pageRect, "VUIP.InterfaceTab", "VUIP.InterfaceResetTip", ResetInterfaceSettings, DrawInterfacePageContents);
                break;
            case SettingsPage.Defaults:
                DrawPage(pageRect, "VUIP.DefaultsTab", "VUIP.DefaultsResetTip", ResetDefaultSettings, DrawDefaultsPageContents);
                break;
            case SettingsPage.Controls:
                DrawPage(pageRect, "VUIP.ControlsTab", "VUIP.KeybindsResetTip", ResetKeybindsSettings, DrawControlsPageContents);
                break;
        }
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

        SettingsWidgets.Header(list.GetRect(36f), titleKey.Translate(), "VUIP.ResetPage".Translate(), resetTipKey.Translate(), reset);
        list.GapLine();
        drawContents(list);

        list.End();
        pageHeight[pageIndex] = list.CurHeight + 12f;
        pageScroll[pageIndex] = scroll;
        Widgets.EndScrollView();
    }

    private static void DrawDefaultsPageContents(Listing_Standard list)
    {
        SettingsWidgets.Subheader(list, "VUIP.DefaultPawnSettingsSection".Translate());
        DrawDefaultPinSetting(list, "VUIP.ApplyDefaultSchedule", ref Settings.applyDefaultSchedule,
            DefaultSchedule.IsSet, "VUIP.ClearDefaultSchedule", "VUIP.DefaultScheduleNone", DefaultSchedule.Clear);
        DrawDefaultPinSetting(list, "VUIP.ApplyDefaultWorkPriorities", ref Settings.applyDefaultWorkPriorities,
            DefaultWorkPriorities.IsSet, "VUIP.ClearDefaultWorkPriorities", "VUIP.DefaultWorkPrioritiesNone", DefaultWorkPriorities.Clear);
        DrawDefaultPinSetting(list, "VUIP.ApplyDefaultAssignments", ref Settings.applyDefaultAssignments,
            DefaultAssignments.IsSet, "VUIP.ClearDefaultAssignments", "VUIP.DefaultAssignmentsNone", DefaultAssignments.Clear);

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
        else if (list.ButtonText("VUIP.ClearQuestRewardDefaults".Translate()))
        {
            QuestRewardDefaults.Clear();
        }
    }

    private void DrawHudPageContents(Listing_Standard list)
    {
        SettingsWidgets.Subheader(list, "VUIP.HudGeneral".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.Enabled".Translate(), ref Settings.enabled, "VUIP.EnabledTip".Translate());

        int opacityPercent = Mathf.RoundToInt(Settings.barBackgroundOpacity * 100f);
        string opacityLabel = "VUIP.BarOpacity".Translate(opacityPercent);
        Settings.barBackgroundOpacity = list.SliderLabeled(opacityLabel, Settings.barBackgroundOpacity, 0f, 1f, tooltip: "VUIP.BarOpacityTip".Translate());
        Settings.barBackgroundOpacity = Mathf.Clamp01(Mathf.Round(Settings.barBackgroundOpacity * 100f) / 100f);

        string widthLabel = "VUIP.HudWidth".Translate(Settings.hudWidth.ToString("0"));
        Settings.hudWidth = Mathf.Round(list.SliderLabeled(widthLabel, Settings.hudWidth, MinHudWidth, MaxHudWidth, tooltip: "VUIP.HudWidthTip".Translate()));

        // Snooze options are on the Notifications page.
        SettingsWidgets.Subheader(list, "VUIP.HudAlerts".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.WrapText".Translate(), ref Settings.wrapText, "VUIP.WrapTextTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.WrapLetterText".Translate(), ref Settings.wrapLetterText, "VUIP.WrapLetterTextTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.RaidLetterStrategy".Translate(), ref Settings.raidLetterStrategy, "VUIP.RaidLetterStrategyTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ReverseOrder".Translate(), ref Settings.reverseNotificationOrder, "VUIP.ReverseOrderTip".Translate());

        SettingsWidgets.Subheader(list, "VUIP.HudDateTemp".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ColorTemperature".Translate(), ref Settings.colorTemperature, "VUIP.ColorTemperatureTip".Translate(ReadoutDrawer.ComfortMinCelsius.ToStringTemperature("F0"), ReadoutDrawer.ComfortMaxCelsius.ToStringTemperature("F0")));
        SettingsWidgets.Checkbox(list, "VUIP.OutdoorTemperature".Translate(), ref Settings.outdoorTemperature, "VUIP.OutdoorTemperatureTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ColorDayNight".Translate(), ref Settings.colorDayNight, "VUIP.ColorDayNightTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowPreciseTime".Translate(), ref Settings.showPreciseTime, "VUIP.ShowPreciseTimeTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowColonyDay".Translate(), ref Settings.showColonyDay, "VUIP.ShowColonyDayTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowColonyWealth".Translate(), ref Settings.showColonyWealth, "VUIP.ShowColonyWealthTip".Translate());

        SettingsWidgets.Subheader(list, "VUIP.HudTimeSpeed".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.HideSpeedButtons".Translate(), ref Settings.hideSpeedButtons, "VUIP.HideSpeedButtonsTip".Translate());
        bool ultrafastWasDisabled = Settings.disableUltrafast;
        SettingsWidgets.Checkbox(list, "VUIP.DisableUltrafast".Translate(), ref Settings.disableUltrafast, "VUIP.DisableUltrafastTip".Translate());
        if (Settings.disableUltrafast && !ultrafastWasDisabled)
        {
            TimeSpeedControls.DropUltrafastIfDisabled();
        }

        list.Gap(6f);
        Rect eventRect = list.GetRect(30f);
        if (Widgets.ButtonText(eventRect, "VUIP.EventSpeedSetting".Translate(TimeSpeedControls.EventSpeedLabel(Settings.eventSpeedMode))))
        {
            TimeSpeedControls.ShowEventSpeedMenu();
        }

        TooltipHandler.TipRegion(eventRect, "VUIP.EventSpeedTip".Translate());
        SettingsWidgets.AdvancedToggle(list, ref speedAdvancedExpanded);
        if (speedAdvancedExpanded)
        {
            Settings.speedNormal = DrawSpeedSlider(list, "VUIP.SpeedNormal", Settings.speedNormal, 0.1f, 3f);
            Settings.speedFast = DrawSpeedSlider(list, "VUIP.SpeedFast", Settings.speedFast, 0.1f, 6f);
            Settings.speedSuperfast = DrawSpeedSlider(list, "VUIP.SpeedSuperfast", Settings.speedSuperfast, 0.1f, 15f);
            Settings.speedUltrafast = DrawSpeedSlider(list, "VUIP.SpeedUltrafast", Settings.speedUltrafast, 0.1f, 150f);
        }

        SettingsWidgets.Subheader(list, "VUIP.HudPlayButtons".Translate());
        DrawPlayButtonSummary(list);
    }

    private void DrawNotificationsPageContents(Listing_Standard list)
    {
        SettingsWidgets.Subheader(list, "VUIP.NotificationsAdded".Translate());
        DrawCustomNotificationsSection(list);
    }

    private void DrawInterfacePageContents(Listing_Standard list)
    {
        SettingsWidgets.Subheader(list, "VUIP.TitleScreenSection".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.HideTutorialButton".Translate(), ref Settings.hideTutorialButton, "VUIP.HideTutorialButtonTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowContinueButton".Translate(), ref Settings.showContinueButton, "VUIP.ShowContinueButtonTip".Translate());

        SettingsWidgets.Subheader(list, "VUIP.MainBarSection".Translate());
        DrawMainButtonSummary(list);

        SettingsWidgets.Subheader(list, "VUIP.ResourceReadoutSection".Translate());
        DrawResourceReadoutSection(list);

        SettingsWidgets.Subheader(list, "VUIP.ColonistBarSection".Translate());
        DrawColonistBarSection(list);

        SettingsWidgets.Subheader(list, "VUIP.PawnPaneSection".Translate());
        DrawPawnPaneSection(list);

        SettingsWidgets.Subheader(list, "VUIP.WildlifeSection".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowLeatherColumn".Translate(), ref Settings.showLeatherColumn, "VUIP.ShowLeatherColumnTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowWildAnimalYields".Translate(), ref Settings.showWildAnimalYields, "VUIP.ShowWildAnimalYieldsTip".Translate());

        SettingsWidgets.Subheader(list, "VUIP.StorageFilterSection".Translate());
        DrawStorageFilterSection(list);

        SettingsWidgets.Subheader(list, "VUIP.SearchSection".Translate());
        DrawSearchSection(list);

        SettingsWidgets.Subheader(list, "VUIP.OverlaysSection".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.AutoGrowingZoneFertility".Translate(), ref Settings.autoGrowingZoneFertility,
            "VUIP.AutoGrowingZoneFertilityTip".Translate());
        if (!Settings.autoGrowingZoneFertility)
        {
            GrowingZoneFertility.Restore();
        }

        SettingsWidgets.Subheader(list, "VUIP.PawnTableSection".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShiftClickAssignAreaToAll".Translate(), ref Settings.shiftClickAssignAreaToAll, "VUIP.ShiftClickAssignAreaToAllTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowShiftScheduleArrows".Translate(), ref Settings.showShiftScheduleArrows, "VUIP.ShowShiftScheduleArrowsTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowScheduleTimeLine".Translate(), ref Settings.showScheduleTimeLine,
            "VUIP.ShowScheduleTimeLineTip".Translate(),
            Patch_PawnTable_PawnTableOnGUI.ChronosPointerActive ? "VUIP.ScheduleTimeLineChronosPointer".Translate().ToString() : null);

        SettingsWidgets.Subheader(list, "VUIP.ScenarioSection".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.SortScenarioListByTechLevel".Translate(), ref Settings.sortScenarioListByTechLevel, "VUIP.SortScenarioListByTechLevelTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ColorScenarioListByTechLevel".Translate(), ref Settings.colorScenarioListByTechLevel, "VUIP.ColorScenarioListByTechLevelTip".Translate());
    }

    private static void DrawControlsPageContents(Listing_Standard list)
    {
        SettingsWidgets.Subheader(list, "VUIP.KeybindsSection".Translate());
        DrawKeybindsSection(list);
        list.Gap(6f);
        if (list.ButtonText("KeyboardConfig".Translate()))
        {
            Find.WindowStack.Add(new Dialog_KeyBindings());
        }

        if (RelatedModSettings.Any)
        {
            SettingsWidgets.Subheader(list, "VUIP.IntegrationsSection".Translate());
            RelatedModSettings.Draw(list);
        }
    }

    private static void ResetDefaultSettings()
    {
        Settings.applyDefaultSchedule = true;
        Settings.defaultSchedule = null;
        Settings.applyDefaultWorkPriorities = true;
        Settings.defaultWorkPriorities = null;
        Settings.applyDefaultAssignments = true;
        Settings.defaultAssignments = null;
        Settings.enableNewGameDefaults = true;
        Settings.storytellerDefaults = null;
        Settings.worldDefaults = null;
        Settings.defaultGoodwillRewards.Clear();
        Settings.defaultRoyalFavorRewards.Clear();
        Instance.WriteSettings();
    }

    private static void DrawColonistBarSection(Listing_Standard list)
    {
        SettingsWidgets.Checkbox(list, "VUIP.ShiftColonistBarInDevMode".Translate(), ref Settings.shiftColonistBarInDevMode, "VUIP.ShiftColonistBarInDevModeTip".Translate());
        string? locked = Settings.shiftColonistBarInDevMode ? null : SettingsWidgets.RequiresSetting("VUIP.ShiftColonistBarInDevMode");
        SettingsWidgets.AdvancedToggle(list, ref Instance.colonistBarAdvancedExpanded, locked);
        if (Instance.colonistBarAdvancedExpanded)
        {
            Settings.colonistBarDevOffset = Mathf.Round(SettingsWidgets.Slider(list,
                "VUIP.ColonistBarDevOffset".Translate(Settings.colonistBarDevOffset.ToString("0")),
                Settings.colonistBarDevOffset, 0f, 48f, "VUIP.ColonistBarDevOffsetTip".Translate(), locked));
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
        list.Gap(6f);
        SettingsWidgets.Checkbox(list, "VUIP.FocusStorageSearch".Translate(), ref Settings.focusStorageSearch, "VUIP.FocusStorageSearchTip".Translate());
        list.Gap(6f);
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

    private static void DrawSearchSection(Listing_Standard list)
    {
        SettingsWidgets.Checkbox(list, "VUIP.GroupSearchResults".Translate(), ref Settings.groupSearchResults, "VUIP.GroupSearchResultsTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.WorldSearchAllTiles".Translate(), ref Settings.worldSearchAllTiles, "VUIP.WorldSearchAllTilesTip".Translate());
        string? locked = Settings.worldSearchAllTiles ? null : SettingsWidgets.RequiresSetting("VUIP.WorldSearchAllTiles");
        SettingsWidgets.Checkbox(list, "VUIP.WorldSearchAdvanced".Translate(), ref Settings.worldSearchAdvanced, "VUIP.WorldSearchAdvancedTip".Translate(), locked);
        SettingsWidgets.Checkbox(list, "VUIP.WorldSearchHighlight".Translate(), ref Settings.worldSearchHighlight, "VUIP.WorldSearchHighlightTip".Translate());
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

    private static void ResetInterfaceSettings()
    {
        Settings.autoGrowingZoneFertility = true;
        Settings.hideTutorialButton = true;
        Settings.showContinueButton = true;
        MainButtonLayout.ResetToDefaults();
        Settings.dragToReorderResources = true;
        Settings.resourceRightClickMenu = true;
        Settings.showZeroResources = false;
        Settings.countHiddenInTotals = true;
        Settings.resourceOrderSimple.Clear();
        Settings.resourceOrderCategorized.Clear();
        Settings.resourceCountAll.Clear();
        Settings.resourceParents.Clear();
        Settings.resourceHidden.Clear();
        ResourceReadoutTweaks.NotifyChanged();
        Settings.shiftColonistBarInDevMode = true;
        Settings.colonistBarDevOffset = 12f;
        Settings.pawnPaneEnabled = true;
        Settings.pawnPaneShowArmor = true;
        Settings.pawnPaneOtherPawns = true;
        Settings.pawnPaneShowSpeed = true;
        Settings.pawnPaneShowDps = true;
        Settings.pawnPaneShowSkills = true;
        Settings.pawnPaneShowSelfTend = true;
        Settings.pawnPaneShowRename = true;
        Settings.pawnPaneShowIdentity = true;
        Settings.pawnPaneColorHealthBar = true;
        Settings.pawnPaneColorMoodBar = true;
        Settings.pawnPaneNeedThreshold = 30f;
        Settings.pawnPaneNeedsView = false;
        Settings.pawnPaneWidth = PawnReadout.DefaultPaneWidth;
        Settings.showLeatherColumn = true;
        Settings.showWildAnimalYields = true;
        Settings.collapseFilterCategoriesByDefault = true;
        Settings.focusStorageSearch = false;
        Settings.resizeFilterTab = true;
        Settings.filterTabWidth = 460f;
        Settings.filterTabHeight = 560f;
        filterTabWidthBuffer = null;
        filterTabHeightBuffer = null;
        Settings.groupSearchResults = true;
        Settings.worldSearchAllTiles = true;
        Settings.worldSearchAdvanced = true;
        Settings.worldSearchHighlight = true;
        WorldSearchText.ResetSources();
        Settings.shiftClickAssignAreaToAll = true;
        Settings.showShiftScheduleArrows = true;
        Settings.showScheduleTimeLine = true;
        Settings.sortScenarioListByTechLevel = true;
        Settings.colorScenarioListByTechLevel = true;
        Instance.WriteSettings();
    }

    private static void DrawResourceReadoutSection(Listing_Standard list)
    {
        SettingsWidgets.Checkbox(list, "VUIP.DragToReorderResources".Translate(), ref Settings.dragToReorderResources, "VUIP.DragToReorderResourcesTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ResourceRightClickMenu".Translate(), ref Settings.resourceRightClickMenu, "VUIP.ResourceRightClickMenuTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowZeroResources".Translate(), ref Settings.showZeroResources, "VUIP.ShowZeroResourcesTip".Translate());
        bool countHidden = Settings.countHiddenInTotals;
        SettingsWidgets.Checkbox(list, "VUIP.CountHiddenInTotals".Translate(), ref Settings.countHiddenInTotals, "VUIP.CountHiddenInTotalsTip".Translate());
        if (countHidden != Settings.countHiddenInTotals)
        {
            ResourceReadoutTweaks.NotifyChanged();
        }

        list.Gap(6f);

        if (ResourceReadoutTweaks.HasCustomOrder && list.ButtonText("VUIP.ResetResourceOrder".Translate()))
        {
            ResourceReadoutTweaks.ResetOrder();
        }

        if (Settings.resourceHidden.Count > 0
            && list.ButtonText("VUIP.ShowHiddenResourcesCount".Translate(Settings.resourceHidden.Count)))
        {
            ResourceReadoutTweaks.ShowAllHidden();
        }

        if (Settings.resourceCountAll.Count > 0
            && list.ButtonText("VUIP.ClearResourceCountAll".Translate(Settings.resourceCountAll.Count)))
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
        if (list.ButtonText("VUIP.ConfigureMainBar".Translate()))
        {
            Find.WindowStack.Add(new Dialog_MainButtonSettings());
        }
    }

    private static void DrawPlayButtonSummary(Listing_Standard list)
    {
        int mapVisible = PlayButtonFilter.CountVisible(world: false);
        int worldVisible = PlayButtonFilter.CountVisible(world: true);
        int mapHidden = Mathf.Max(0, PlayButtonFilter.MapButtons.Count - mapVisible);
        int worldHidden = Mathf.Max(0, PlayButtonFilter.WorldButtons.Count - worldVisible);
        SettingsWidgets.StatusLabel(list, "VUIP.PlayButtonsSummary".Translate(mapHidden, worldHidden));
        if (list.ButtonText("VUIP.ConfigurePlayButtons".Translate()))
        {
            Find.WindowStack.Add(new Dialog_PlayButtonSettings());
        }
    }

    // The label key's "Tip" variant is the checkbox tooltip.
    private static void DrawDefaultPinSetting(Listing_Standard list, string labelKey, ref bool enabled, bool isSet, string clearKey, string noneKey, Action clear)
    {
        SettingsWidgets.Checkbox(list, labelKey.Translate(), ref enabled, (labelKey + "Tip").Translate());
        if (!isSet)
        {
            SettingsWidgets.StatusLabel(list, noneKey.Translate());
        }
        else if (SettingsWidgets.Button(list, clearKey.Translate(), enabled ? null : SettingsWidgets.RequiresSetting(labelKey)))
        {
            clear();
        }
    }

    private static void DrawCustomNotificationsSection(Listing_Standard list)
    {
        SettingsWidgets.Checkbox(list, "VUIP.ShowBleedingOutAlert".Translate(), ref Settings.showBleedingOutAlert, "VUIP.ShowBleedingOutAlertTip".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.ShowHostilesPresentAlert".Translate(), ref Settings.showHostilesPresentAlert, "VUIP.ShowHostilesPresentAlertTip".Translate());
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

        SettingsWidgets.Subheader(list, "VUIP.NotificationsSnooze".Translate());
        SettingsWidgets.Checkbox(list, "VUIP.EnableSnooze".Translate(), ref Settings.enableSnooze, "VUIP.EnableSnoozeTip".Translate());
        Settings.snoozeDays = Mathf.Clamp(Settings.snoozeDays, 1, 15);
        Settings.snoozeDays = Mathf.Clamp(Mathf.RoundToInt(SettingsWidgets.Slider(list,
            "VUIP.SnoozeDays".Translate(Settings.snoozeDays), Settings.snoozeDays, 1f, 15f, "VUIP.SnoozeDaysTip".Translate(),
            Settings.enableSnooze ? null : SettingsWidgets.RequiresSetting("VUIP.EnableSnooze"))), 1, 15);

        // Offered with snoozing off too: turning it off leaves existing snoozes saved.
        if (list.ButtonText("VUIP.ClearSnoozes".Translate()))
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
    }

    private static void ResetCustomNotificationSettings()
    {
        Settings.showBleedingOutAlert = true;
        Settings.showHostilesPresentAlert = true;
        Settings.showTraderPresentAlert = true;
        Settings.showBatteriesLowAlert = true;
        Settings.hideLockedResearchBenchAlert = true;
        Settings.batteryLowHours = 6f;
        Settings.batteryLowPercent = 20f;
        Settings.enableSnooze = true;
        Settings.snoozeDays = 3;
        Instance.WriteSettings();
    }

    private static void ResetHudSettings()
    {
        Settings.enabled = true;
        Settings.wrapText = false;
        Settings.wrapLetterText = false;
        Settings.raidLetterStrategy = true;
        Settings.reverseNotificationOrder = false;
        Settings.barBackgroundOpacity = DefaultBarOpacity;
        Settings.hudWidth = AlertDrawer.DefaultBarWidth;
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
        Instance.WriteSettings();
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

    private static void ResetKeybindsSettings()
    {
        Settings.enableUnforbidAllHotkey = true;
        Settings.enableTemperatureOverlayHotkey = true;
        Settings.enableDevModeHotkey = true;
        Instance.WriteSettings();
    }

    // The label key's "Tip" variant is the slider tooltip.
    private static float DrawSpeedSlider(Listing_Standard list, string labelKey, float value, float min, float max)
    {
        string label = labelKey.Translate(value.ToString("0.##"));
        value = list.SliderLabeled(label, value, min, max, tooltip: (labelKey + "Tip").Translate());
        return (float)Math.Round(value, 2);
    }
}
