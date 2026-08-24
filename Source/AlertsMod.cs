using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus.Alerts;

public class AlertsMod : Mod
{
    public static AlertsSettings Settings = null!;
    public static AlertsMod Instance = null!;

    public static bool Enabled => Settings.enabled;

    public AlertsMod(ModContentPack content) : base(content)
    {
        Instance = this;
        Settings = GetSettings<AlertsSettings>();
    }

    public override string SettingsCategory()
    {
        return "VUIA.SettingsCategory".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Settings.DoWindowContents(inRect);
    }
}

public class AlertsSettings : ModSettings
{
    public bool enabled = true;
    public int snoozeDays = 3;
    public bool wrapText;
    public bool showBarBackgrounds = true;
    public bool hideSpeedButtons;
    public Dictionary<string, bool> showPlayButtons = new Dictionary<string, bool>();
    private Vector2 settingsScroll;
    private float settingsHeight;

    public bool IsPlayButtonShown(string id)
    {
        return !showPlayButtons.TryGetValue(id, out bool shown) || shown;
    }

    public void SetPlayButtonShown(string id, bool shown)
    {
        showPlayButtons[id] = shown;
        PlayButtonFilter.NotifyChanged();
        AlertsMod.Instance?.WriteSettings();
    }

    public void DoWindowContents(Rect inRect)
    {
        Rect view = new Rect(0f, 0f, inRect.width - 20f, Mathf.Max(inRect.height, settingsHeight));
        Widgets.BeginScrollView(inRect, ref settingsScroll, view);
        Listing_Standard list = new Listing_Standard();
        list.Begin(view);

        list.CheckboxLabeled("VUIA.Enabled".Translate(), ref enabled, "VUIA.EnabledTip".Translate());
        list.GapLine();

        snoozeDays = Mathf.Clamp(snoozeDays, 1, 15);
        string daysLabel = "VUIA.SnoozeDays".Translate(snoozeDays);
        snoozeDays = Mathf.RoundToInt(list.SliderLabeled(daysLabel, snoozeDays, 1f, 15f, tooltip: "VUIA.SnoozeDaysTip".Translate()));
        snoozeDays = Mathf.Clamp(snoozeDays, 1, 15);

        list.Gap(6f);
        list.CheckboxLabeled("VUIA.WrapText".Translate(), ref wrapText, "VUIA.WrapTextTip".Translate());
        list.CheckboxLabeled("VUIA.ShowBarBackgrounds".Translate(), ref showBarBackgrounds, "VUIA.ShowBarBackgroundsTip".Translate());
        list.CheckboxLabeled("VUIA.HideSpeedButtons".Translate(), ref hideSpeedButtons, "VUIA.HideSpeedButtonsTip".Translate());
        list.Gap(12f);

        if (list.ButtonText("VUIA.ClearSnoozes".Translate()))
        {
            if (Current.ProgramState == ProgramState.Playing && SnoozeTracker.Current() is SnoozeTracker tracker)
            {
                int count = tracker.ClearAll();
                Messages.Message("VUIA.ClearSnoozesDone".Translate(count), MessageTypeDefOf.PositiveEvent, historical: false);
            }
            else
            {
                Messages.Message("VUIA.ClearSnoozesNeedGame".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            }
        }

        list.GapLine();
        PlayButtonFilter.DrawSettings(list, view.width);

        if (Event.current.type == EventType.Layout)
        {
            settingsHeight = list.CurHeight + 24f;
        }

        list.End();
        Widgets.EndScrollView();
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref enabled, "enabled", true);
        Scribe_Values.Look(ref snoozeDays, "snoozeDays", 3);
        Scribe_Values.Look(ref wrapText, "wrapText", false);
        Scribe_Values.Look(ref showBarBackgrounds, "showBarBackgrounds", true);
        Scribe_Values.Look(ref hideSpeedButtons, "hideSpeedButtons", false);
        Scribe_Collections.Look(ref showPlayButtons, "showPlayButtons", LookMode.Value, LookMode.Value);
        if (showPlayButtons == null)
        {
            showPlayButtons = new Dictionary<string, bool>();
        }

        snoozeDays = Mathf.Clamp(snoozeDays, 1, 15);
    }
}
