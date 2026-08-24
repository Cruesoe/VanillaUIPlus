using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus.Alerts;

public static class ReadoutDrawer
{
    private const float IconPad = 3f;
    private const float LeftColumnFraction = 0.34f;
    private static float cachedPlaySettingsHeight;
    private static readonly List<GameCondition> VisibleConditions = new List<GameCondition>();

    public static void ResetPlaySettingsHeight()
    {
        cachedPlaySettingsHeight = 0f;
    }

    public static void DrawPlaySettings(WidgetRow rowVisibility, bool worldView, ref float curBaseY)
    {
        float bottom = curBaseY;
        float pad = IconPad;
        float maxWidth = AlertDrawer.BarWidth - pad * 2f;
        float height = Mathf.Max(EstimatePlaySettingsHeight(worldView, pad, maxWidth), cachedPlaySettingsHeight);
        float y = bottom - height;
        AlertDrawer.DrawBarBackground(new Rect(UI.screenWidth - AlertDrawer.BarWidth, y, AlertDrawer.BarWidth, height));

        rowVisibility.Init(UI.screenWidth - pad, bottom - pad - WidgetRow.IconSize, UIDirection.LeftThenUp, maxWidth);
        Find.PlaySettings.DoPlaySettingsGlobalControls(rowVisibility, worldView);

        cachedPlaySettingsHeight = bottom - rowVisibility.FinalY + pad;
        curBaseY = bottom - cachedPlaySettingsHeight;
    }

    public static void DrawTimespeed(ref float curBaseY)
    {
        TickManager tickManager = Find.TickManager;
        if (AlertsMod.Settings.hideSpeedButtons)
        {
            HandleTimespeedKeys(tickManager);
            return;
        }

        float pad = IconPad;
        Vector2 buttonSize = TimeControls.TimeButSize;
        const int buttonCount = 4;
        float buttonsWidth = buttonSize.x * buttonCount;
        float height = buttonSize.y + pad * 2f;
        float x = UI.screenWidth - AlertDrawer.BarWidth;
        float y = curBaseY - height;

        AlertDrawer.DrawBarBackground(new Rect(x, y, AlertDrawer.BarWidth, height));
        float startX = x + (AlertDrawer.BarWidth - buttonsWidth) / 2f;
        float startY = y + pad;
        DrawTimespeedButtons(new Rect(startX, startY, buttonsWidth, buttonSize.y), buttonSize);
        curBaseY -= height;
    }

    private static void DrawTimespeedButtons(Rect row, Vector2 buttonSize)
    {
        TickManager tickManager = Find.TickManager;
        int index = 0;
        foreach (TimeSpeed timeSpeed in (TimeSpeed[])Enum.GetValues(typeof(TimeSpeed)))
        {
            if (timeSpeed == TimeSpeed.Ultrafast)
            {
                continue;
            }

            Rect rect = new Rect(row.x + index * buttonSize.x, row.y, buttonSize.x, buttonSize.y);
            index++;
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
            Widgets.DrawLineHorizontal(row.x + buttonSize.x * 2f, row.y + buttonSize.y / 2f, buttonSize.x * 2f);
        }

        if (tickManager.ForcePaused)
        {
            Widgets.DrawLineHorizontal(row.x + buttonSize.x, row.y + buttonSize.y / 2f, buttonSize.x * 3f);
        }

        GenUI.AbsorbClicksInRect(row);
        UIHighlighter.HighlightOpportunity(row, "TimeControls");
        HandleTimespeedKeys(tickManager);
    }

    private static void HandleTimespeedKeys(TickManager tickManager)
    {
        if (Event.current.type != EventType.KeyDown)
        {
            return;
        }

        if (KeyBindingDefOf.TogglePause.KeyDownEvent)
        {
            Find.TickManager.TogglePaused();
            PlayTimeControlSound(Find.TickManager.CurTimeSpeed);
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Pause, KnowledgeAmount.SpecificInteraction);
            Event.current.Use();
        }

        if (Find.WindowStack.WindowsForcePause)
        {
            HandleDevTimespeedKeys(tickManager);
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

        if (KeyBindingDefOf.TimeSpeed_Faster.KeyDownEvent && (int)Find.TickManager.CurTimeSpeed < 3)
        {
            Find.TickManager.CurTimeSpeed++;
            PlayTimeControlSound(Find.TickManager.CurTimeSpeed);
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.TimeControls, KnowledgeAmount.SpecificInteraction);
            Event.current.Use();
        }

        HandleDevTimespeedKeys(tickManager);
    }

    private static void HandleDevTimespeedKeys(TickManager tickManager)
    {
        if (!Prefs.DevMode)
        {
            return;
        }

        if (KeyBindingDefOf.TimeSpeed_Ultrafast.KeyDownEvent)
        {
            Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
            PlayTimeControlSound(Find.TickManager.CurTimeSpeed);
            Event.current.Use();
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

    public static void DrawDate(ref float curBaseY)
    {
        Vector2 longLat = CurrentLongLat();
        int ticksAbs = Find.TickManager.TicksAbs;
        int hour = GenDate.HourInteger(ticksAbs, longLat.x);
        Season season = GenDate.Season(ticksAbs, longLat);
        string hourLabel = HourLabel(hour);
        string dateLabel = GenDate.DateReadoutStringAt(ticksAbs, longLat);
        string seasonLabel = SeasonLabelVisible ? season.LabelCap() : string.Empty;

        Text.Font = GameFont.Small;
        float lineHeight = Text.LineHeight;
        float totalHeight = lineHeight * 2f;
        float y = curBaseY - totalHeight;
        float x = UI.screenWidth - AlertDrawer.BarWidth;
        Rect block = new Rect(x, y, AlertDrawer.BarWidth, totalHeight);

        DrawSplitBar(new Rect(x, y, AlertDrawer.BarWidth, lineHeight), hourLabel, seasonLabel, AlertDrawer.BarWidth * LeftColumnFraction);
        DrawBar(new Rect(x, y + lineHeight, AlertDrawer.BarWidth, lineHeight), dateLabel);

        if (Mouse.IsOver(block))
        {
            StringBuilder tip = new StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                Quadrum quadrum = (Quadrum)i;
                tip.AppendLine(quadrum.Label() + " - " + quadrum.GetSeason(longLat.y).LabelCap());
            }

            TooltipHandler.TipRegion(block, new TipSignal("DateReadoutTip".Translate(GenDate.DaysPassed, 15, season.LabelCap(), 15, GenDate.Quadrum(GenTicks.TicksAbs, longLat.x).Label(), tip.ToString()), 86423));
        }

        curBaseY -= totalHeight;
    }

    public static void DrawTemperatureAndWeather(ref float curBaseY)
    {
        if (Find.CurrentMap == null)
        {
            return;
        }

        Text.Font = GameFont.Small;
        float lineHeight = Text.LineHeight;
        Rect bar = new Rect(UI.screenWidth - AlertDrawer.BarWidth, curBaseY - lineHeight, AlertDrawer.BarWidth, lineHeight);
        string temperature = CurrentTemperature().ToStringTemperature("F0");
        string weather = Find.CurrentMap.weatherManager.CurWeatherPerceived.LabelCap;
        DrawSplitBar(bar, temperature, weather, bar.width * LeftColumnFraction);

        string weatherTip = Find.CurrentMap.weatherManager.CurWeatherPerceived.description;
        if (!weatherTip.NullOrEmpty())
        {
            TooltipHandler.TipRegion(bar, weatherTip);
        }

        curBaseY -= lineHeight;
    }

    public static void DrawGameConditions(Map map, ref float curBaseY)
    {
        VisibleConditions.Clear();
        CollectVisibleConditions(map.gameConditionManager, VisibleConditions);
        if (VisibleConditions.Count == 0)
        {
            return;
        }

        Text.Font = GameFont.Small;
        float lineHeight = Text.LineHeight;
        for (int i = VisibleConditions.Count - 1; i >= 0; i--)
        {
            GameCondition condition = VisibleConditions[i];
            Rect bar = new Rect(UI.screenWidth - AlertDrawer.BarWidth, curBaseY - lineHeight, AlertDrawer.BarWidth, lineHeight);
            DrawBar(bar, condition.LabelCap, ConditionBarColor(condition));
            TooltipHandler.TipRegion(bar, new TipSignal(condition.TooltipString, 0x3A2DF42A ^ condition.uniqueID));
            if (Widgets.ButtonInvisible(bar))
            {
                if (condition.conditionCauser != null && !condition.hideSource && CameraJumper.CanJump(condition.conditionCauser))
                {
                    CameraJumper.TryJumpAndSelect(condition.conditionCauser);
                }
                else if (condition.quest != null)
                {
                    Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Quests);
                    ((MainTabWindow_Quests)MainButtonDefOf.Quests.TabWindow).Select(condition.quest);
                }
            }

            curBaseY -= lineHeight;
        }
    }

    private static void CollectVisibleConditions(GameConditionManager manager, List<GameCondition> into)
    {
        List<GameCondition> active = manager.ActiveConditions;
        for (int i = 0; i < active.Count; i++)
        {
            GameCondition condition = active[i];
            if (!condition.def.displayOnUI)
            {
                continue;
            }

            if (manager.ownerMap != null && (!condition.CanApplyOnMap(manager.ownerMap) || condition.HiddenByOtherCondition(manager.ownerMap)))
            {
                continue;
            }

            into.Add(condition);
        }

        if (manager.Parent != null)
        {
            CollectVisibleConditions(manager.Parent, into);
        }
    }

    internal static void DrawSplitBar(Rect rect, string leftText, string rightText, float leftWidth = -1f)
    {
        AlertDrawer.DrawBarBackground(rect);
        if (Mouse.IsOver(rect))
        {
            Widgets.DrawHighlight(rect);
        }

        if (leftWidth < 0f)
        {
            leftWidth = rect.width / 2f;
        }

        Rect left = new Rect(rect.x, rect.y, leftWidth, rect.height);
        Rect right = new Rect(rect.x + leftWidth, rect.y, rect.width - leftWidth, rect.height);

        Text.Font = GameFont.Small;
        bool oldWrap = Text.WordWrap;
        Text.WordWrap = false;
        Text.Anchor = TextAnchor.MiddleCenter;
        if (!rightText.NullOrEmpty())
        {
            Widgets.Label(left, leftText);
            Widgets.Label(right, rightText.Truncate(right.width));
        }
        else
        {
            Widgets.Label(rect, leftText);
        }

        Text.WordWrap = oldWrap;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private static Color ConditionBarColor(GameCondition condition)
    {
        LetterDef? letter = condition.def?.letterDef;
        if (letter == null && condition is GameCondition_ForceWeather)
        {
            letter = LetterDefOf.NegativeEvent;
        }

        if (letter == null)
        {
            return Color.clear;
        }

        Color color = AlertDrawer.LetterFillColor(letter.color);
        color.a = AlertDrawer.BarColor.a;
        return color;
    }

    private static void DrawBar(Rect rect, string text, Color fill = default)
    {
        if (fill.a > 0.001f)
        {
            if (AlertsMod.Settings.showBarBackgrounds)
            {
                Widgets.DrawBoxSolid(rect, fill);
            }
        }
        else
        {
            AlertDrawer.DrawBarBackground(rect);
        }

        if (Mouse.IsOver(rect))
        {
            Widgets.DrawHighlight(rect);
        }

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        bool oldWrap = Text.WordWrap;
        Text.WordWrap = false;
        Widgets.Label(rect, text.Truncate(rect.width));
        Text.WordWrap = oldWrap;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private static string HourLabel(int hour)
    {
        if (!Prefs.TwelveHourClockMode)
        {
            return hour.ToString() + "LetterHour".Translate();
        }

        TaggedString suffix = hour >= 12 ? "PM".Translate() : "AM".Translate();
        if (hour == 0)
        {
            return $"12 {suffix}";
        }

        if (hour > 12)
        {
            return $"{hour - 12} {suffix}";
        }

        return $"{hour} {suffix}";
    }

    private static float EstimatePlaySettingsHeight(bool worldView, float pad, float maxWidth)
    {
        int count = CountPlaySettingIcons(worldView);
        if (count <= 0)
        {
            return pad * 2f;
        }

        int perRow = Mathf.Max(1, Mathf.FloorToInt((maxWidth - WidgetRow.IconSize) / (WidgetRow.IconSize + WidgetRow.DefaultGap)) + 1);
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)perRow));
        return rows * WidgetRow.IconSize + (rows - 1) * WidgetRow.DefaultGap + pad * 2f;
    }

    private static int CountPlaySettingIcons(bool worldView)
    {
        int drawn = PlayButtonFilter.LastDrawn(worldView);
        if (drawn >= 0)
        {
            return drawn;
        }

        if (worldView)
        {
            int count = 6;
            if (Current.ProgramState == ProgramState.Playing)
            {
                count++;
            }

            if (ModsConfig.OdysseyActive)
            {
                count++;
            }

            return count;
        }

        int mapCount = 13;
        if (ModsConfig.BiotechActive)
        {
            mapCount++;
        }

        if (ModsConfig.OdysseyActive && Find.CurrentMap != null && Find.CurrentMap.Biome.inVacuum)
        {
            mapCount++;
        }

        if (ModsConfig.AnomalyActive && Find.Anomaly.AnomalyStudyEnabled)
        {
            mapCount++;
        }

        return mapCount;
    }

    private static bool SeasonLabelVisible => !WorldRendererUtility.WorldSelected && Find.CurrentMap != null;

    private static Vector2 CurrentLongLat()
    {
        if (WorldRendererUtility.WorldSelected && Find.WorldSelector.SelectedTile.Valid)
        {
            return Find.WorldGrid.LongLatOf(Find.WorldSelector.SelectedTile);
        }

        if (WorldRendererUtility.WorldSelected && Find.WorldSelector.NumSelectedObjects > 0)
        {
            return Find.WorldGrid.LongLatOf(Find.WorldSelector.FirstSelectedObject.Tile);
        }

        return Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile);
    }

    private static float CurrentTemperature()
    {
        IntVec3 cell = UI.MouseCell();
        IntVec3 usefulCell = cell;
        Room? room = cell.GetRoom(Find.CurrentMap);
        if (room == null)
        {
            for (int i = 0; i < 9; i++)
            {
                IntVec3 neighbor = cell + GenAdj.AdjacentCellsAndInside[i];
                if (!neighbor.InBounds(Find.CurrentMap))
                {
                    continue;
                }

                Room? neighborRoom = neighbor.GetRoom(Find.CurrentMap);
                if (neighborRoom != null && ((!neighborRoom.PsychologicallyOutdoors && !neighborRoom.UsesOutdoorTemperature) || (!neighborRoom.PsychologicallyOutdoors && (room == null || room.PsychologicallyOutdoors)) || (neighborRoom.PsychologicallyOutdoors && room == null)))
                {
                    usefulCell = neighbor;
                    room = neighborRoom;
                }
            }
        }

        if (room == null || usefulCell.Fogged(Find.CurrentMap))
        {
            return Find.CurrentMap.mapTemperature.OutdoorTemp;
        }

        return room.Temperature;
    }
}
