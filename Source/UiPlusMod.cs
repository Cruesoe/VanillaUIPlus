using System;
using System.Collections.Generic;
using System.IO;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public class UiPlusMod : Mod
{
    public const float DefaultBarOpacity = 0.78f;

    public static UiPlusSettings Settings = null!;
    public static UiPlusMod Instance = null!;

    public static bool Enabled => Settings.enabled;

    private Vector2 settingsScroll;
    private float settingsHeight;
    private bool hudSectionExpanded;
    private bool mainMenuSectionExpanded;
    private int lastSettingsFrame = -100;

    public UiPlusMod(ModContentPack content) : base(content)
    {
        Instance = this;
        Settings = GetSettings<UiPlusSettings>();
        TryMigrateOldSettings();
    }

    public override string SettingsCategory()
    {
        return "VUIP.SettingsCategory".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        if (Time.frameCount > lastSettingsFrame + 1)
        {
            hudSectionExpanded = false;
            mainMenuSectionExpanded = false;
        }

        lastSettingsFrame = Time.frameCount;
        float viewWidth = inRect.width - 16f;
        Rect view = new Rect(0f, 0f, viewWidth, Mathf.Max(settingsHeight, inRect.height));
        Widgets.BeginScrollView(inRect, ref settingsScroll, view);
        Listing_Standard list = new Listing_Standard
        {
            maxOneColumn = true
        };
        list.Begin(view);

        hudSectionExpanded = DrawSectionHeader(
            list,
            "VUIP.HudSection".Translate(),
            "VUIP.HudSectionTip".Translate(),
            "VUIP.HudResetTip".Translate(),
            hudSectionExpanded,
            ResetHudSettings);
        if (hudSectionExpanded)
        {
            DrawHudSection(list, viewWidth);
        }

        list.Gap();
        mainMenuSectionExpanded = DrawSectionHeader(
            list,
            "VUIP.MainMenuSection".Translate(),
            "VUIP.MainMenuSectionTip".Translate(),
            "VUIP.MainMenuResetTip".Translate(),
            mainMenuSectionExpanded,
            ResetMainMenuSettings);
        if (mainMenuSectionExpanded)
        {
            MainButtonLayout.DrawSettings(list);
        }

        list.End();
        settingsHeight = list.CurHeight + 12f;
        Widgets.EndScrollView();
    }

    private void DrawHudSection(Listing_Standard list, float width)
    {
        list.CheckboxLabeled("VUIP.Enabled".Translate(), ref Settings.enabled, "VUIP.EnabledTip".Translate());

        int opacityPercent = Mathf.RoundToInt(Settings.barBackgroundOpacity * 100f);
        string opacityLabel = "VUIP.BarOpacity".Translate(opacityPercent);
        Settings.barBackgroundOpacity = list.SliderLabeled(opacityLabel, Settings.barBackgroundOpacity, 0f, 1f, tooltip: "VUIP.BarOpacityTip".Translate());
        Settings.barBackgroundOpacity = Mathf.Clamp01(Mathf.Round(Settings.barBackgroundOpacity * 100f) / 100f);

        DrawSubheader(list, "VUIP.HudAlerts");
        Settings.snoozeDays = Mathf.Clamp(Settings.snoozeDays, 1, 15);
        string daysLabel = "VUIP.SnoozeDays".Translate(Settings.snoozeDays);
        Settings.snoozeDays = Mathf.RoundToInt(list.SliderLabeled(daysLabel, Settings.snoozeDays, 1f, 15f, tooltip: "VUIP.SnoozeDaysTip".Translate()));
        Settings.snoozeDays = Mathf.Clamp(Settings.snoozeDays, 1, 15);
        list.CheckboxLabeled("VUIP.WrapText".Translate(), ref Settings.wrapText, "VUIP.WrapTextTip".Translate());
        list.CheckboxLabeled("VUIP.ReverseOrder".Translate(), ref Settings.reverseNotificationOrder, "VUIP.ReverseOrderTip".Translate());
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

        DrawSubheader(list, "VUIP.HudDateTemp");
        list.CheckboxLabeled("VUIP.ColorTemperature".Translate(), ref Settings.colorTemperature, "VUIP.ColorTemperatureTip".Translate());
        list.CheckboxLabeled("VUIP.OutdoorTemperature".Translate(), ref Settings.outdoorTemperature, "VUIP.OutdoorTemperatureTip".Translate());
        list.CheckboxLabeled("VUIP.ColorDayNight".Translate(), ref Settings.colorDayNight, "VUIP.ColorDayNightTip".Translate());
        list.CheckboxLabeled("VUIP.ShowColonyDay".Translate(), ref Settings.showColonyDay, "VUIP.ShowColonyDayTip".Translate());

        DrawSubheader(list, "VUIP.HudTimeSpeed");
        list.CheckboxLabeled("VUIP.HideSpeedButtons".Translate(), ref Settings.hideSpeedButtons, "VUIP.HideSpeedButtonsTip".Translate());
        list.Gap(6f);
        Rect eventRect = list.GetRect(30f);
        if (Widgets.ButtonText(eventRect, "VUIP.EventSpeedSetting".Translate(TimeSpeedControls.EventSpeedLabel(Settings.eventSpeedMode))))
        {
            TimeSpeedControls.ShowEventSpeedMenu();
        }

        TooltipHandler.TipRegion(eventRect, "VUIP.EventSpeedTip".Translate());
        Settings.speedNormal = DrawSpeedSlider(list, "VUIP.SpeedNormal", Settings.speedNormal, 0.1f, 3f);
        Settings.speedFast = DrawSpeedSlider(list, "VUIP.SpeedFast", Settings.speedFast, 0.1f, 6f);
        Settings.speedSuperfast = DrawSpeedSlider(list, "VUIP.SpeedSuperfast", Settings.speedSuperfast, 0.1f, 15f);
        Settings.speedUltrafast = DrawSpeedSlider(list, "VUIP.SpeedUltrafast", Settings.speedUltrafast, 0.1f, 150f);

        DrawSubheader(list, "VUIP.HudPlayButtons");
        PlayButtonFilter.DrawSettings(list, width);
    }

    private void TryMigrateOldSettings()
    {
        string folder = Content.FolderName;
        string config = GenFilePaths.ConfigFolderPath;
        string newFile = Path.Combine(config, GenText.SanitizeFilename($"Mod_{folder}_{nameof(UiPlusMod)}.xml"));
        string oldFile = Path.Combine(config, GenText.SanitizeFilename($"Mod_{folder}_AlertsMod.xml"));
        if (File.Exists(newFile) || !File.Exists(oldFile))
        {
            return;
        }

        UiPlusSettings loaded = LoadedModManager.ReadModSettings<global::VanillaUIPlus.Alerts.AlertsSettings>(folder, "AlertsMod");
        Settings.CopyFrom(loaded);
        PlayButtonFilter.NotifyChanged();
        WriteSettings();
    }

    private static void ResetHudSettings()
    {
        Settings.enabled = true;
        Settings.snoozeDays = 3;
        Settings.wrapText = false;
        Settings.reverseNotificationOrder = false;
        Settings.barBackgroundOpacity = DefaultBarOpacity;
        Settings.colorTemperature = true;
        Settings.outdoorTemperature = true;
        Settings.colorDayNight = true;
        Settings.showColonyDay = true;
        Settings.hideSpeedButtons = false;
        Settings.eventSpeedMode = EventSpeedMode.Normal;
        Settings.speedNormal = TimeSpeedControls.DefaultSpeedNormal;
        Settings.speedFast = TimeSpeedControls.DefaultSpeedFast;
        Settings.speedSuperfast = TimeSpeedControls.DefaultSpeedSuperfast;
        Settings.speedUltrafast = TimeSpeedControls.DefaultSpeedUltrafast;
        Settings.showPlayButtons.Clear();
        PlayButtonFilter.NotifyChanged();
        Instance.WriteSettings();
    }

    private static void ResetMainMenuSettings()
    {
        MainButtonLayout.ResetToVanilla();
        Instance.WriteSettings();
    }

    private static void DrawSubheader(Listing_Standard list, string key)
    {
        list.Gap(10f);
        Color old = GUI.color;
        GUI.color = new Color(0.72f, 0.72f, 0.72f);
        list.Label(key.Translate());
        GUI.color = old;
        list.GapLine();
    }

    private static bool DrawSectionHeader(Listing_Standard listing, string label, string headerTip, string resetTip, bool expanded, Action onReset)
    {
        Text.Font = GameFont.Medium;
        float height = Text.LineHeight + 8f;
        Rect row = listing.GetRect(height);
        Rect resetRect = new Rect(row.xMax - 110f, row.y + (row.height - 30f) / 2f, 110f, 30f);
        Rect toggleRect = new Rect(row.x, row.y, resetRect.x - row.x - 8f, row.height);

        Widgets.DrawHighlightIfMouseover(toggleRect);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(toggleRect, (expanded ? "▼  " : "▶  ") + label);
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(toggleRect, headerTip);
        if (Widgets.ButtonInvisible(toggleRect))
        {
            expanded = !expanded;
        }

        Text.Font = GameFont.Small;
        TooltipHandler.TipRegion(resetRect, resetTip);
        if (Widgets.ButtonText(resetRect, "Reset".Translate()))
        {
            onReset();
        }

        listing.GapLine();
        return expanded;
    }

    private static float DrawSpeedSlider(Listing_Standard list, string labelKey, float value, float min, float max)
    {
        string label = labelKey.Translate(value.ToString("0.##"));
        value = list.SliderLabeled(label, value, min, max, tooltip: (labelKey + "Tip").Translate());
        return (float)Math.Round(value, 2);
    }
}

public class UiPlusSettings : ModSettings
{
    public bool enabled = true;
    public int snoozeDays = 3;
    public bool wrapText;
    public bool reverseNotificationOrder;
    public float barBackgroundOpacity = UiPlusMod.DefaultBarOpacity;
    public bool colorTemperature = true;
    public bool outdoorTemperature = true;
    public bool colorDayNight = true;
    public bool showColonyDay = true;
    public bool hideSpeedButtons;
    public EventSpeedMode eventSpeedMode = EventSpeedMode.Normal;
    public float speedNormal = TimeSpeedControls.DefaultSpeedNormal;
    public float speedFast = TimeSpeedControls.DefaultSpeedFast;
    public float speedSuperfast = TimeSpeedControls.DefaultSpeedSuperfast;
    public float speedUltrafast = TimeSpeedControls.DefaultSpeedUltrafast;
    public Dictionary<string, bool> showPlayButtons = new Dictionary<string, bool>();
    public List<MainButtonLayoutEntry> mainButtons = new List<MainButtonLayoutEntry>();

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

    public void CopyFrom(UiPlusSettings other)
    {
        enabled = other.enabled;
        snoozeDays = other.snoozeDays;
        wrapText = other.wrapText;
        reverseNotificationOrder = other.reverseNotificationOrder;
        barBackgroundOpacity = other.barBackgroundOpacity;
        colorTemperature = other.colorTemperature;
        outdoorTemperature = other.outdoorTemperature;
        colorDayNight = other.colorDayNight;
        showColonyDay = other.showColonyDay;
        hideSpeedButtons = other.hideSpeedButtons;
        eventSpeedMode = other.eventSpeedMode;
        speedNormal = other.speedNormal;
        speedFast = other.speedFast;
        speedSuperfast = other.speedSuperfast;
        speedUltrafast = other.speedUltrafast;
        showPlayButtons = other.showPlayButtons == null
            ? new Dictionary<string, bool>()
            : new Dictionary<string, bool>(other.showPlayButtons);
        mainButtons = other.mainButtons == null
            ? new List<MainButtonLayoutEntry>()
            : new List<MainButtonLayoutEntry>(other.mainButtons);
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref enabled, "enabled", true);
        Scribe_Values.Look(ref snoozeDays, "snoozeDays", 3);
        Scribe_Values.Look(ref wrapText, "wrapText", false);
        Scribe_Values.Look(ref reverseNotificationOrder, "reverseNotificationOrder", false);
        Scribe_Values.Look(ref colorTemperature, "colorTemperature", true);
        Scribe_Values.Look(ref outdoorTemperature, "outdoorTemperature", true);
        Scribe_Values.Look(ref colorDayNight, "colorDayNight", true);
        Scribe_Values.Look(ref showColonyDay, "showColonyDay", true);
        Scribe_Values.Look(ref hideSpeedButtons, "hideSpeedButtons", false);
        Scribe_Values.Look(ref eventSpeedMode, "eventSpeedMode", EventSpeedMode.Normal);
        Scribe_Values.Look(ref speedNormal, "speedNormal", TimeSpeedControls.DefaultSpeedNormal);
        Scribe_Values.Look(ref speedFast, "speedFast", TimeSpeedControls.DefaultSpeedFast);
        Scribe_Values.Look(ref speedSuperfast, "speedSuperfast", TimeSpeedControls.DefaultSpeedSuperfast);
        Scribe_Values.Look(ref speedUltrafast, "speedUltrafast", TimeSpeedControls.DefaultSpeedUltrafast);
        Scribe_Collections.Look(ref showPlayButtons, "showPlayButtons", LookMode.Value, LookMode.Value);
        if (showPlayButtons == null)
        {
            showPlayButtons = new Dictionary<string, bool>();
        }

        Scribe_Collections.Look(ref mainButtons, "mainButtons", LookMode.Deep);
        if (mainButtons == null)
        {
            mainButtons = new List<MainButtonLayoutEntry>();
        }

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
    }
}
