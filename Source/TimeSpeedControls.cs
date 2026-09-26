using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

public enum EventSpeedMode
{
    Slow,
    Normal,
    Fast,
    Half,
    Ignore
}

public static class TimeSpeedControls
{
    public const float DefaultSpeedNormal = 1f;
    public const float DefaultSpeedFast = 3f;
    public const float DefaultSpeedSuperfast = 6f;
    public const float DefaultSpeedUltrafast = 15f;

    private static bool UltrafastButtonShown => !UiPlusMod.Settings.disableUltrafast;

    // With ultrafast disabled, its key still works in development mode, as in vanilla.
    private static bool UltrafastKeyAllowed => !UiPlusMod.Settings.disableUltrafast || Prefs.DevMode;

    private static readonly TimeSpeed[] Speeds = (TimeSpeed[])Enum.GetValues(typeof(TimeSpeed));
    private static readonly TimeSpeed[] DirectKeySpeeds = { TimeSpeed.Normal, TimeSpeed.Fast, TimeSpeed.Superfast };
    private static readonly AccessTools.FieldRef<bool>? UltraSpeedBoost =
        ReflectionGuard.StaticFieldRef<bool>(nameof(TickManager), "UltraSpeedBoost", AccessTools.Field(typeof(TickManager), "UltraSpeedBoost"));
    private static readonly Func<TickManager, bool>? NothingHappeningInGame =
        ReflectionGuard.Delegate<Func<TickManager, bool>>(nameof(TickManager), "NothingHappeningInGame",
            AccessTools.Method(typeof(TickManager), "NothingHappeningInGame"));

    private const string UltrafastTexPath = "UI/VanillaUIPlus/TimeSpeedButton_Ultrafast";
    private static Texture2D? ultrafastTex;
    private static int handledKeyFrame = -1;
    private static KeyCode handledKeyCode;
    private static readonly string[] SpeedTips = new string[5];
    private static bool speedTipsDirty = true;
    private static int rateFrame = -1;
    private static TimeSpeed rateSpeed;
    private static bool rateForced;
    private static bool ratePaused;
    private static float rateCached;

    public static bool DrewThisGui { get; private set; }

    public static void ResetDrewThisGui()
    {
        DrewThisGui = false;
    }

    public static void Draw(ref float curBaseY)
    {
        DrewThisGui = true;
        if (UiPlusMod.Settings.hideSpeedButtons)
        {
            HandleKeys(Find.TickManager);
            return;
        }

        float pad = 3f;
        Vector2 buttonSize = TimeControls.TimeButSize;
        float height = buttonSize.y + pad * 2f;
        float x = UI.screenWidth - AlertDrawer.BarWidth;
        float y = curBaseY - height;
        AlertDrawer.DrawBarBackground(new Rect(x, y, AlertDrawer.BarWidth, height));
        DrawButtons(new Rect(x + pad, y + pad, AlertDrawer.BarWidth - pad * 2f, buttonSize.y));
        curBaseY -= height;
    }

    public static void DrawButtons(Rect row)
    {
        DrewThisGui = true;
        TickManager tickManager = Find.TickManager;
        int buttonCount = UltrafastButtonShown ? Speeds.Length : Speeds.Length - 1;
        float buttonWidth = row.width / buttonCount;
        float buttonHeight = row.height;
        for (int i = 0; i < buttonCount; i++)
        {
            TimeSpeed timeSpeed = Speeds[i];
            Rect rect = new Rect(row.x + i * buttonWidth, row.y, buttonWidth, buttonHeight);
            if (Widgets.ButtonImage(rect, SpeedButtonTexture(timeSpeed), doMouseoverSound: true, SpeedTip(timeSpeed)) && !tickManager.ForcePaused)
            {
                if (timeSpeed == TimeSpeed.Paused)
                {
                    tickManager.TogglePaused();
                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Pause, KnowledgeAmount.SpecificInteraction);
                }
                else
                {
                    tickManager.CurTimeSpeed = timeSpeed;
                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.TimeControls, KnowledgeAmount.SpecificInteraction);
                }

                PlayTimeControlSound(tickManager.CurTimeSpeed);
            }

            if (((!tickManager.ForcePaused) ? tickManager.CurTimeSpeed : TimeSpeed.Paused) == timeSpeed)
            {
                GUI.DrawTexture(rect, TexUI.HighlightTex);
            }
        }

        if (tickManager.slower.ForcedNormalSpeed)
        {
            Widgets.DrawLineHorizontal(row.x + buttonWidth * 2f, row.y + buttonHeight / 2f, buttonWidth * (buttonCount - 2));
        }

        if (tickManager.ForcePaused)
        {
            Widgets.DrawLineHorizontal(row.x + buttonWidth, row.y + buttonHeight / 2f, buttonWidth * (buttonCount - 1));
        }

        TryOpenEventSpeedMenu(row);
        GenUI.AbsorbClicksInRect(row);
        UIHighlighter.HighlightOpportunity(row, "TimeControls");
        HandleKeys(tickManager);
    }

    public static void HandleKeys(TickManager tickManager)
    {
        if (Event.current.type != EventType.KeyDown)
        {
            return;
        }

        if (handledKeyFrame == Time.frameCount && handledKeyCode == Event.current.keyCode)
        {
            return;
        }

        handledKeyFrame = Time.frameCount;
        handledKeyCode = Event.current.keyCode;

        if (KeyBindingDefOf.TogglePause.KeyDownEvent)
        {
            tickManager.TogglePaused();
            PlayTimeControlSound(tickManager.CurTimeSpeed);
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Pause, KnowledgeAmount.SpecificInteraction);
            Event.current.Use();
        }

        if (Find.WindowStack.WindowsForcePause)
        {
            HandleUltrafastAndDevKeys(tickManager);
            return;
        }

        for (int i = 0; i < DirectKeySpeeds.Length; i++)
        {
            if (KeyBindingFor(DirectKeySpeeds[i]).KeyDownEvent)
            {
                SetSpeedFromKey(tickManager, DirectKeySpeeds[i]);
            }
        }

        if (KeyBindingDefOf.TimeSpeed_Slower.KeyDownEvent && tickManager.CurTimeSpeed != TimeSpeed.Paused)
        {
            SetSpeedFromKey(tickManager, tickManager.CurTimeSpeed - 1);
        }

        TimeSpeed fastest = UltrafastKeyAllowed ? TimeSpeed.Ultrafast : TimeSpeed.Superfast;
        if (KeyBindingDefOf.TimeSpeed_Faster.KeyDownEvent && tickManager.CurTimeSpeed < fastest)
        {
            SetSpeedFromKey(tickManager, tickManager.CurTimeSpeed + 1);
        }

        HandleUltrafastAndDevKeys(tickManager);
    }

    private static void SetSpeedFromKey(TickManager tickManager, TimeSpeed speed, bool teach = true)
    {
        tickManager.CurTimeSpeed = speed;
        PlayTimeControlSound(speed);
        if (teach)
        {
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.TimeControls, KnowledgeAmount.SpecificInteraction);
        }

        Event.current.Use();
    }

    /// <summary>Drops a running game from ultrafast to superfast once ultrafast is disabled, unless in development mode.</summary>
    public static void DropUltrafastIfDisabled()
    {
        TickManager? tickManager = Current.Game?.tickManager;
        if (tickManager != null && !UltrafastKeyAllowed && tickManager.CurTimeSpeed == TimeSpeed.Ultrafast)
        {
            tickManager.CurTimeSpeed = TimeSpeed.Superfast;
        }
    }

    public static void ShowEventSpeedMenu()
    {
        List<FloatMenuOption> options = new List<FloatMenuOption>();
        AddEventSpeedOption(options, EventSpeedMode.Slow, "VUIP.EventSpeedSlow");
        AddEventSpeedOption(options, EventSpeedMode.Normal, "VUIP.EventSpeedNormal");
        AddEventSpeedOption(options, EventSpeedMode.Fast, "VUIP.EventSpeedFast");
        AddEventSpeedOption(options, EventSpeedMode.Half, "VUIP.EventSpeedHalf");
        AddEventSpeedOption(options, EventSpeedMode.Ignore, "VUIP.EventSpeedIgnore");
        Find.WindowStack.Add(new FloatMenu(options, "VUIP.EventSpeedMenu".Translate()));
    }

    public static string EventSpeedLabel(EventSpeedMode mode)
    {
        return mode switch
        {
            EventSpeedMode.Slow => "VUIP.EventSpeedSlow".Translate(),
            EventSpeedMode.Fast => "VUIP.EventSpeedFast".Translate(),
            EventSpeedMode.Half => "VUIP.EventSpeedHalf".Translate(),
            EventSpeedMode.Ignore => "VUIP.EventSpeedIgnore".Translate(),
            _ => "VUIP.EventSpeedNormal".Translate()
        };
    }

    public static float TickRate(TickManager manager)
    {
        TimeSpeed speed = manager.CurTimeSpeed;
        bool forced = manager.slower.ForcedNormalSpeed;
        bool paused = manager.Paused;
        if (rateFrame == Time.frameCount && rateSpeed == speed && rateForced == forced && ratePaused == paused)
        {
            return rateCached;
        }

        rateFrame = Time.frameCount;
        rateSpeed = speed;
        rateForced = forced;
        ratePaused = paused;
        UiPlusSettings settings = UiPlusMod.Settings;
        if (forced)
        {
            if (speed == TimeSpeed.Paused)
            {
                rateCached = 0f;
                return rateCached;
            }

            rateCached = settings.eventSpeedMode switch
            {
                EventSpeedMode.Slow => settings.speedNormal * 0.5f,
                EventSpeedMode.Fast => settings.speedNormal * 2f,
                EventSpeedMode.Half => SpeedFor(manager, speed) * 0.5f,
                EventSpeedMode.Ignore => SpeedFor(manager, speed),
                _ => settings.speedNormal
            };
            return rateCached;
        }

        rateCached = SpeedFor(manager, speed);
        return rateCached;
    }

    private static float SpeedFor(TickManager manager, TimeSpeed speed)
    {
        UiPlusSettings settings = UiPlusMod.Settings;
        switch (speed)
        {
            case TimeSpeed.Paused:
                return 0f;
            case TimeSpeed.Normal:
                return settings.speedNormal;
            case TimeSpeed.Fast:
                return settings.speedFast;
            case TimeSpeed.Superfast:
                if (Find.Maps.Count == 0)
                {
                    return settings.speedSuperfast * 3f;
                }

                if (NothingHappeningInGame != null && NothingHappeningInGame(manager))
                {
                    return settings.speedSuperfast * 2f;
                }

                return settings.speedSuperfast;
            case TimeSpeed.Ultrafast:
                bool boost = UltraSpeedBoost != null && UltraSpeedBoost();
                if (Find.Maps.Count == 0 || boost)
                {
                    return settings.speedUltrafast * 10f;
                }

                return settings.speedUltrafast;
            default:
                return 1f;
        }
    }

    // Vanilla reuses the superfast texture for ultrafast; this mod ships a four-arrow one.
    private static Texture2D SpeedButtonTexture(TimeSpeed speed)
    {
        if (speed == TimeSpeed.Ultrafast)
        {
            ultrafastTex ??= ContentFinder<Texture2D>.Get(UltrafastTexPath, reportFailure: false) ?? TexButton.SpeedButtonTextures[(uint)speed];
            return ultrafastTex;
        }

        return TexButton.SpeedButtonTextures[(uint)speed];
    }

    private static string SpeedTip(TimeSpeed speed)
    {
        if (speedTipsDirty)
        {
            speedTipsDirty = false;
            for (int i = 0; i < Speeds.Length && i < SpeedTips.Length; i++)
            {
                SpeedTips[i] = string.Format(
                    "{0}: {1}",
                    "HotKeyTip".Translate(),
                    KeyPrefs.KeyPrefsData.GetBoundKeyCode(KeyBindingFor(Speeds[i]), KeyPrefs.BindingSlot.A).ToStringReadable());
            }
        }

        int index = (int)speed;
        if (index < 0 || index >= SpeedTips.Length)
        {
            return string.Empty;
        }

        return SpeedTips[index];
    }

    public static void InvalidateSpeedTips()
    {
        speedTipsDirty = true;
    }

    private static void TryOpenEventSpeedMenu(Rect row)
    {
        if (!Mouse.IsOver(row) || Event.current.type != EventType.MouseDown || Event.current.button != 1)
        {
            return;
        }

        ShowEventSpeedMenu();
        Event.current.Use();
    }

    private static void AddEventSpeedOption(List<FloatMenuOption> options, EventSpeedMode mode, string labelKey)
    {
        string label = labelKey.Translate();
        if (UiPlusMod.Settings.eventSpeedMode == mode)
        {
            label += " ✓";
        }

        options.Add(new FloatMenuOption(label, delegate
        {
            UiPlusMod.Settings.eventSpeedMode = mode;
            UiPlusMod.Instance?.WriteSettings();
        }));
    }

    private static void HandleUltrafastAndDevKeys(TickManager tickManager)
    {
        if (UltrafastKeyAllowed && KeyBindingDefOf.TimeSpeed_Ultrafast.KeyDownEvent)
        {
            SetSpeedFromKey(tickManager, TimeSpeed.Ultrafast, teach: false);
        }

        if (!Prefs.DevMode)
        {
            return;
        }

        if (KeyBindingDefOf.Dev_TickOnce.KeyDownEvent && tickManager.CurTimeSpeed == TimeSpeed.Paused)
        {
            tickManager.DoSingleTick();
            SoundDefOf.Clock_Stop.PlayOneShotOnCamera();
        }
    }

    private static KeyBindingDef KeyBindingFor(TimeSpeed speed)
    {
        return speed switch
        {
            TimeSpeed.Paused => KeyBindingDefOf.TogglePause,
            TimeSpeed.Normal => KeyBindingDefOf.TimeSpeed_Normal,
            TimeSpeed.Fast => KeyBindingDefOf.TimeSpeed_Fast,
            TimeSpeed.Superfast => KeyBindingDefOf.TimeSpeed_Superfast,
            TimeSpeed.Ultrafast => KeyBindingDefOf.TimeSpeed_Ultrafast,
            _ => KeyBindingDefOf.TimeSpeed_Normal
        };
    }

    private static void PlayTimeControlSound(TimeSpeed speed)
    {
        SoundDef? sound = speed switch
        {
            TimeSpeed.Paused => SoundDefOf.Clock_Stop,
            TimeSpeed.Normal => SoundDefOf.Clock_Normal,
            TimeSpeed.Fast => SoundDefOf.Clock_Fast,
            TimeSpeed.Superfast => SoundDefOf.Clock_Superfast,
            TimeSpeed.Ultrafast => SoundDefOf.Clock_Superfast,
            _ => null
        };
        sound?.PlayOneShotOnCamera();
    }
}
