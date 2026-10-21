using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public class UiPlusSettings : ModSettings
{
    public bool enabled = true;
    public int snoozeDays = 3;
    public bool enableSnooze = true;
    public bool wrapText;
    public bool wrapLetterText;
    public bool reverseNotificationOrder;
    public float barBackgroundOpacity = UiPlusMod.DefaultBarOpacity;
    public float hudWidth = AlertDrawer.DefaultBarWidth;
    public bool colorTemperature = true;
    public bool outdoorTemperature = true;
    public bool colorDayNight = true;
    public bool showPreciseTime = true;
    public bool showColonyDay = true;
    public bool showColonyWealth = true;
    public bool showBleedingOutAlert = true;
    public bool showHostilesPresentAlert = true;
    public bool showTraderPresentAlert = true;
    public bool showBatteriesLowAlert = true;
    public bool hideLockedResearchBenchAlert = true;
    public bool shiftColonistBarInDevMode = true;
    public float colonistBarDevOffset = 12f;
    public bool showLeatherColumn = true;
    public bool showWildAnimalYields = true;
    public bool pawnPaneEnabled = true;
    public bool pawnPaneShowArmor = true;
    public bool pawnPaneShowTemperature = true;
    public bool pawnPaneShowSpeed = true;
    public bool pawnPaneShowDps = true;
    public bool pawnPaneShowSkills = true;
    public bool pawnPaneShowSelfTend = true;
    public bool pawnPaneShowRename = true;
    public bool pawnPaneShowIdentity = true;
    public bool pawnPaneColorHealthBar = true;
    public bool pawnPaneColorMoodBar = true;
    public float pawnPaneNeedThreshold = 30f;
    public bool pawnPaneNeedsView;
    public float pawnPaneWidth = PawnReadout.DefaultPaneWidth;
    public bool collapseFilterCategoriesByDefault = true;
    public bool focusStorageSearch;
    public bool resizeFilterTab = true;
    public float filterTabWidth = 460f;
    public float filterTabHeight = 560f;
    public float batteryLowHours = 6f;
    public float batteryLowPercent = 20f;
    public bool dragToReorderResources = true;
    public bool resourceRightClickMenu = true;
    public bool showZeroResources;
    public bool countHiddenInTotals = true;
    public List<string> resourceOrderSimple = new List<string>();
    public List<string> resourceOrderCategorized = new List<string>();
    public List<string> resourceCountAll = new List<string>();
    public List<string> resourceHidden = new List<string>();
    public Dictionary<string, string> resourceParents = new Dictionary<string, string>();
    public List<string> resourceOpenCategories = new List<string>();
    public bool shiftClickAssignAreaToAll = true;
    public bool showShiftScheduleArrows = true;
    public bool showScheduleTimeLine = true;
    public bool applyDefaultSchedule = true;
    public List<string>? defaultSchedule;
    public bool applyDefaultWorkPriorities = true;
    public Dictionary<string, int>? defaultWorkPriorities;
    public bool applyDefaultAssignments = true;
    public AssignDefaults? defaultAssignments;
    public bool colorScenarioListByTechLevel = true;
    public bool sortScenarioListByTechLevel = true;
    public bool enableNewGameDefaults = true;
    public StorytellerDefaults? storytellerDefaults;
    public WorldDefaults? worldDefaults;
    public Dictionary<string, bool> defaultGoodwillRewards = new Dictionary<string, bool>();
    public Dictionary<string, bool> defaultRoyalFavorRewards = new Dictionary<string, bool>();
    public bool enableUnforbidAllHotkey = true;
    public bool enableTemperatureOverlayHotkey = true;
    public bool autoGrowingZoneFertility = true;
    public bool enableDevModeHotkey = true;
    public bool hideSpeedButtons;
    public bool disableUltrafast;
    public EventSpeedMode eventSpeedMode = EventSpeedMode.Normal;
    public float speedNormal = TimeSpeedControls.DefaultSpeedNormal;
    public float speedFast = TimeSpeedControls.DefaultSpeedFast;
    public float speedSuperfast = TimeSpeedControls.DefaultSpeedSuperfast;
    public float speedUltrafast = TimeSpeedControls.DefaultSpeedUltrafast;
    public Dictionary<string, bool> showPlayButtons = new Dictionary<string, bool>();
    public bool hideTutorialButton = true;
    public bool showContinueButton = true;
    public List<MainButtonLayoutEntry> mainButtons = new List<MainButtonLayoutEntry>();
    public bool groupSearchResults = true;
    public bool worldSearchAllTiles = true;
    public bool worldSearchAdvanced = true;
    public bool worldSearchHighlight = true;
    public bool worldSearchPlaces = true;
    public bool worldSearchLandmarks = true;
    public bool worldSearchFeatures = true;
    public bool worldSearchBiomes = true;
    public bool worldSearchTerrain;
    public bool worldSearchRoadsRivers = true;
    public bool worldSearchStone = true;
    public List<SavedWorldSearch> savedWorldSearches = new List<SavedWorldSearch>();

    public bool IsPlayButtonShown(string id)
    {
        return !showPlayButtons.TryGetValue(id, out bool shown) || shown;
    }

    public void SetPlayButtonShown(string id, bool shown)
    {
        showPlayButtons[id] = shown;
        PlayButtonFilter.NotifyChanged();
        UiPlusMod.Instance?.WriteSettings();
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref enabled, "enabled", true);
        Scribe_Values.Look(ref snoozeDays, "snoozeDays", 3);
        Scribe_Values.Look(ref enableSnooze, "enableSnooze", true);
        Scribe_Values.Look(ref wrapText, "wrapText", false);
        Scribe_Values.Look(ref wrapLetterText, "wrapLetterText", false);
        Scribe_Values.Look(ref reverseNotificationOrder, "reverseNotificationOrder", false);
        Scribe_Values.Look(ref colorTemperature, "colorTemperature", true);
        Scribe_Values.Look(ref outdoorTemperature, "outdoorTemperature", true);
        Scribe_Values.Look(ref colorDayNight, "colorDayNight", true);
        Scribe_Values.Look(ref showPreciseTime, "showPreciseTime", true);
        Scribe_Values.Look(ref showColonyDay, "showColonyDay", true);
        Scribe_Values.Look(ref showColonyWealth, "showColonyWealth", true);
        Scribe_Values.Look(ref showBleedingOutAlert, "showBleedingOutAlert", true);
        Scribe_Values.Look(ref showHostilesPresentAlert, "showHostilesPresentAlert", true);
        Scribe_Values.Look(ref showTraderPresentAlert, "showTraderPresentAlert", true);
        Scribe_Values.Look(ref showBatteriesLowAlert, "showBatteriesLowAlert", true);
        Scribe_Values.Look(ref hideLockedResearchBenchAlert, "hideLockedResearchBenchAlert", true);
        Scribe_Values.Look(ref shiftColonistBarInDevMode, "shiftColonistBarInDevMode", true);
        Scribe_Values.Look(ref colonistBarDevOffset, "colonistBarDevOffset", 12f);
        Scribe_Values.Look(ref showLeatherColumn, "showLeatherColumn", true);
        Scribe_Values.Look(ref showWildAnimalYields, "showWildAnimalYields", true);
        Scribe_Values.Look(ref pawnPaneEnabled, "pawnPaneEnabled", true);
        Scribe_Values.Look(ref pawnPaneShowArmor, "pawnPaneShowArmor", true);
        Scribe_Values.Look(ref pawnPaneShowTemperature, "pawnPaneShowTemperature", true);
        Scribe_Values.Look(ref pawnPaneShowSpeed, "pawnPaneShowSpeed", true);
        Scribe_Values.Look(ref pawnPaneShowDps, "pawnPaneShowDps", true);
        Scribe_Values.Look(ref pawnPaneShowSkills, "pawnPaneShowSkills", true);
        Scribe_Values.Look(ref pawnPaneShowSelfTend, "pawnPaneShowSelfTend", true);
        Scribe_Values.Look(ref pawnPaneShowRename, "pawnPaneShowRename", true);
        Scribe_Values.Look(ref pawnPaneShowIdentity, "pawnPaneShowIdentity", true);
        Scribe_Values.Look(ref pawnPaneColorHealthBar, "pawnPaneColorHealthBar", true);
        Scribe_Values.Look(ref pawnPaneColorMoodBar, "pawnPaneColorMoodBar", true);
        Scribe_Values.Look(ref pawnPaneNeedThreshold, "pawnPaneNeedThreshold", 30f);
        Scribe_Values.Look(ref pawnPaneNeedsView, "pawnPaneNeedsView");
        Scribe_Values.Look(ref pawnPaneWidth, "pawnPaneWidth", PawnReadout.DefaultPaneWidth);
        Scribe_Values.Look(ref collapseFilterCategoriesByDefault, "collapseFilterCategoriesByDefault", true);
        Scribe_Values.Look(ref focusStorageSearch, "focusStorageSearch", false);
        Scribe_Values.Look(ref resizeFilterTab, "resizeFilterTab", true);
        Scribe_Values.Look(ref filterTabWidth, "filterTabWidth", 460f);
        Scribe_Values.Look(ref filterTabHeight, "filterTabHeight", 560f);
        Scribe_Values.Look(ref batteryLowHours, "batteryLowHours", 6f);
        Scribe_Values.Look(ref batteryLowPercent, "batteryLowPercent", 20f);
        Scribe_Values.Look(ref dragToReorderResources, "dragToReorderResources", true);
        Scribe_Values.Look(ref resourceRightClickMenu, "resourceRightClickMenu", true);
        Scribe_Values.Look(ref showZeroResources, "showZeroResources", false);
        Scribe_Values.Look(ref countHiddenInTotals, "countHiddenInTotals", true);
        Scribe_Collections.Look(ref resourceOrderSimple, "resourceOrderSimple", LookMode.Value);
        Scribe_Collections.Look(ref resourceOrderCategorized, "resourceOrderCategorized", LookMode.Value);
        Scribe_Collections.Look(ref resourceCountAll, "resourceCountAll", LookMode.Value);
        resourceOrderSimple ??= new List<string>();
        resourceOrderCategorized ??= new List<string>();
        resourceCountAll ??= new List<string>();
        Scribe_Collections.Look(ref resourceHidden, "resourceHidden", LookMode.Value);
        resourceHidden ??= new List<string>();
        Scribe_Collections.Look(ref resourceParents, "resourceParents", LookMode.Value, LookMode.Value);
        resourceParents ??= new Dictionary<string, string>();
        Scribe_Collections.Look(ref resourceOpenCategories, "resourceOpenCategories", LookMode.Value);
        resourceOpenCategories ??= new List<string>();
        Scribe_Values.Look(ref shiftClickAssignAreaToAll, "shiftClickAssignAreaToAll", true);
        Scribe_Values.Look(ref showShiftScheduleArrows, "showShiftScheduleArrows", true);
        Scribe_Values.Look(ref showScheduleTimeLine, "showScheduleTimeLine", true);
        Scribe_Values.Look(ref applyDefaultSchedule, "applyDefaultSchedule", true);
        Scribe_Collections.Look(ref defaultSchedule, "defaultSchedule", LookMode.Value);
        Scribe_Values.Look(ref applyDefaultWorkPriorities, "applyDefaultWorkPriorities", true);
        Scribe_Collections.Look(ref defaultWorkPriorities, "defaultWorkPriorities", LookMode.Value, LookMode.Value);
        Scribe_Values.Look(ref applyDefaultAssignments, "applyDefaultAssignments", true);
        Scribe_Deep.Look(ref defaultAssignments, "defaultAssignments");
        Scribe_Values.Look(ref colorScenarioListByTechLevel, "colorScenarioListByTechLevel", true);
        Scribe_Values.Look(ref sortScenarioListByTechLevel, "sortScenarioListByTechLevel", true);
        Scribe_Values.Look(ref enableNewGameDefaults, "enableNewGameDefaults", true);
        Scribe_Deep.Look(ref storytellerDefaults, "storytellerDefaults");
        Scribe_Deep.Look(ref worldDefaults, "worldDefaults");
        Scribe_Collections.Look(ref defaultGoodwillRewards, "defaultGoodwillRewards", LookMode.Value, LookMode.Value);
        defaultGoodwillRewards ??= new Dictionary<string, bool>();
        Scribe_Collections.Look(ref defaultRoyalFavorRewards, "defaultRoyalFavorRewards", LookMode.Value, LookMode.Value);
        defaultRoyalFavorRewards ??= new Dictionary<string, bool>();
        Scribe_Values.Look(ref enableUnforbidAllHotkey, "enableUnforbidAllHotkey", true);
        Scribe_Values.Look(ref enableTemperatureOverlayHotkey, "enableTemperatureOverlayHotkey", true);
        Scribe_Values.Look(ref autoGrowingZoneFertility, "autoGrowingZoneFertility", true);
        Scribe_Values.Look(ref enableDevModeHotkey, "enableDevModeHotkey", true);
        Scribe_Values.Look(ref hideSpeedButtons, "hideSpeedButtons", false);
        Scribe_Values.Look(ref disableUltrafast, "disableUltrafast", false);
        Scribe_Values.Look(ref eventSpeedMode, "eventSpeedMode", EventSpeedMode.Normal);
        Scribe_Values.Look(ref speedNormal, "speedNormal", TimeSpeedControls.DefaultSpeedNormal);
        Scribe_Values.Look(ref speedFast, "speedFast", TimeSpeedControls.DefaultSpeedFast);
        Scribe_Values.Look(ref speedSuperfast, "speedSuperfast", TimeSpeedControls.DefaultSpeedSuperfast);
        Scribe_Values.Look(ref speedUltrafast, "speedUltrafast", TimeSpeedControls.DefaultSpeedUltrafast);
        Scribe_Collections.Look(ref showPlayButtons, "showPlayButtons", LookMode.Value, LookMode.Value);
        showPlayButtons ??= new Dictionary<string, bool>();
        Scribe_Values.Look(ref hideTutorialButton, "hideTutorialButton", true);
        Scribe_Values.Look(ref showContinueButton, "showContinueButton", true);
        Scribe_Collections.Look(ref mainButtons, "mainButtons", LookMode.Deep);
        mainButtons ??= new List<MainButtonLayoutEntry>();
        Scribe_Values.Look(ref hudWidth, "hudWidth", AlertDrawer.DefaultBarWidth);
        Scribe_Values.Look(ref groupSearchResults, "groupSearchResults", true);
        Scribe_Values.Look(ref worldSearchAllTiles, "worldSearchAllTiles", true);
        Scribe_Values.Look(ref worldSearchAdvanced, "worldSearchAdvanced", true);
        Scribe_Values.Look(ref worldSearchHighlight, "worldSearchHighlight", true);
        Scribe_Values.Look(ref worldSearchPlaces, "worldSearchPlaces", true);
        Scribe_Values.Look(ref worldSearchLandmarks, "worldSearchLandmarks", true);
        Scribe_Values.Look(ref worldSearchFeatures, "worldSearchFeatures", true);
        Scribe_Values.Look(ref worldSearchBiomes, "worldSearchBiomes", true);
        Scribe_Values.Look(ref worldSearchTerrain, "worldSearchTerrain", false);
        Scribe_Values.Look(ref worldSearchRoadsRivers, "worldSearchRoadsRivers", true);
        Scribe_Values.Look(ref worldSearchStone, "worldSearchStone", true);
        Scribe_Collections.Look(ref savedWorldSearches, "savedWorldSearches", LookMode.Deep);
        savedWorldSearches ??= new List<SavedWorldSearch>();
        savedWorldSearches.RemoveAll(s => s == null);

        // Older settings stored an on/off "showBarBackgrounds" instead of an opacity.
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            Scribe_Values.Look(ref barBackgroundOpacity, "barBackgroundOpacity", UiPlusMod.DefaultBarOpacity);
        }
        else
        {
            bool showBarBackgrounds = true;
            Scribe_Values.Look(ref showBarBackgrounds, "showBarBackgrounds", true);
            float opacity = -1f;
            Scribe_Values.Look(ref opacity, "barBackgroundOpacity", -1f);
            barBackgroundOpacity = opacity >= 0f
                ? Mathf.Clamp01(opacity)
                : (showBarBackgrounds ? UiPlusMod.DefaultBarOpacity : 0f);
        }

        snoozeDays = Mathf.Clamp(snoozeDays, 1, 15);
        barBackgroundOpacity = Mathf.Clamp01(barBackgroundOpacity);
        speedNormal = Mathf.Clamp(speedNormal, 0.1f, 3f);
        speedFast = Mathf.Clamp(speedFast, 0.1f, 6f);
        speedSuperfast = Mathf.Clamp(speedSuperfast, 0.1f, 15f);
        speedUltrafast = Mathf.Clamp(speedUltrafast, 0.1f, 150f);
        filterTabWidth = Mathf.Clamp(filterTabWidth, 300f, 1200f);
        filterTabHeight = Mathf.Clamp(filterTabHeight, 300f, 1600f);
        hudWidth = Mathf.Clamp(hudWidth, UiPlusMod.MinHudWidth, UiPlusMod.MaxHudWidth);
        pawnPaneNeedThreshold = Mathf.Clamp(pawnPaneNeedThreshold, 0f, 100f);
        pawnPaneWidth = Mathf.Clamp(pawnPaneWidth, PawnReadout.MinPaneWidth, PawnReadout.MaxPaneWidth);
    }
}
