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

    private static readonly TimeSpeed[] Speeds = (TimeSpeed[])Enum.GetValues(typeof(TimeSpeed));
    private static readonly FieldInfo UltraSpeedBoostField = AccessTools.Field(typeof(TickManager), "UltraSpeedBoost");
    private static readonly Func<TickManager, bool>? NothingHappeningInGame =
        AccessTools.Method(typeof(TickManager), "NothingHappeningInGame") is MethodInfo method
            ? AccessTools.MethodDelegate<Func<TickManager, bool>>(method)
            : null;

    private static int handledKeyFrame = -1;
    private static KeyCode handledKeyCode;

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
        int buttonCount = Speeds.Length;
        float buttonWidth = row.width / buttonCount;
        float buttonHeight = row.height;
        for (int i = 0; i < Speeds.Length; i++)
        {
            TimeSpeed timeSpeed = Speeds[i];
            Rect rect = new Rect(row.x + i * buttonWidth, row.y, buttonWidth, buttonHeight);
            string tooltip = string.Format("{0}: {1}", "HotKeyTip".Translate(), KeyPrefs.KeyPrefsData.GetBoundKeyCode(KeyBindingFor(timeSpeed), KeyPrefs.BindingSlot.A).ToStringReadable());
            if (Widgets.ButtonImage(rect, TexButton.SpeedButtonTextures[(uint)timeSpeed], doMouseoverSound: true, tooltip) && !tickManager.ForcePaused)
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
            Widgets.DrawLineHorizontal(row.x + buttonWidth * 2f, row.y + buttonHeight / 2f, buttonWidth * 3f);
        }

        if (tickManager.ForcePaused)
        {
            Widgets.DrawLineHorizontal(row.x + buttonWidth, row.y + buttonHeight / 2f, buttonWidth * 4f);
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
            Find.TickManager.TogglePaused();
            PlayTimeControlSound(Find.TickManager.CurTimeSpeed);
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Pause, KnowledgeAmount.SpecificInteraction);
            Event.current.Use();
        }

        if (Find.WindowStack.WindowsForcePause)
        {
            HandleUltrafastAndDevKeys(tickManager);
            return;
        }

        if (KeyBindingDefOf.TimeSpeed_Normal.KeyDownEvent)
        {
            Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
            PlayTimeControlSound(Find.TickManager.CurTimeSpeed);
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.TimeControls, KnowledgeAmount.SpecificInteraction);
            Event.current.Use();
        }

        if (KeyBindingDefOf.TimeSpeed_Fast.KeyDownEvent)
        {
            Find.TickManager.CurTimeSpeed = TimeSpeed.Fast;
            PlayTimeControlSound(Find.TickManager.CurTimeSpeed);
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.TimeControls, KnowledgeAmount.SpecificInteraction);
            Event.current.Use();
        }

        if (KeyBindingDefOf.TimeSpeed_Superfast.KeyDownEvent)
        {
            Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
            PlayTimeControlSound(Find.TickManager.CurTimeSpeed);
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.TimeControls, KnowledgeAmount.SpecificInteraction);
            Event.current.Use();
        }

        if (KeyBindingDefOf.TimeSpeed_Slower.KeyDownEvent && Find.TickManager.CurTimeSpeed != TimeSpeed.Paused)
        {
            Find.TickManager.CurTimeSpeed--;
            PlayTimeControlSound(Find.TickManager.CurTimeSpeed);
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.TimeControls, KnowledgeAmount.SpecificInteraction);
            Event.current.Use();
        }

        if (KeyBindingDefOf.TimeSpeed_Faster.KeyDownEvent && (int)Find.TickManager.CurTimeSpeed < 4)
        {
            Find.TickManager.CurTimeSpeed++;
            PlayTimeControlSound(Find.TickManager.CurTimeSpeed);
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.TimeControls, KnowledgeAmount.SpecificInteraction);
            Event.current.Use();
        }

        HandleUltrafastAndDevKeys(tickManager);
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
        UiPlusSettings settings = UiPlusMod.Settings;
        if (manager.slower.ForcedNormalSpeed)
        {
            if (manager.CurTimeSpeed == TimeSpeed.Paused)
            {
                return 0f;
            }

            return settings.eventSpeedMode switch
            {
                EventSpeedMode.Slow => settings.speedNormal * 0.5f,
                EventSpeedMode.Fast => settings.speedNormal * 2f,
                EventSpeedMode.Half => SpeedFor(manager, manager.CurTimeSpeed) * 0.5f,
                EventSpeedMode.Ignore => SpeedFor(manager, manager.CurTimeSpeed),
                _ => settings.speedNormal
            };
        }

        return SpeedFor(manager, manager.CurTimeSpeed);
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
                bool boost = UltraSpeedBoostField != null && (bool)UltraSpeedBoostField.GetValue(null);
                if (Find.Maps.Count == 0 || boost)
                {
                    return settings.speedUltrafast * 10f;
                }

                return settings.speedUltrafast;
            default:
                return 1f;
        }
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
        if (KeyBindingDefOf.TimeSpeed_Ultrafast.KeyDownEvent)
        {
            Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
            PlayTimeControlSound(Find.TickManager.CurTimeSpeed);
            Event.current.Use();
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
