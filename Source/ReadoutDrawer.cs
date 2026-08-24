using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public static class ReadoutDrawer
{
    private const float IconPad = 3f;
    private const float LeftColumnFraction = 0.28f;
    private static float cachedPlaySettingsHeight;
    private static readonly List<GameCondition> VisibleConditions = new List<GameCondition>();
    private static int dateHour = int.MinValue;
    private static int dateDay = int.MinValue;
    private static float dateLongLatX = float.NaN;
    private static bool dateShowDay;
    private static bool dateTwelveHour;
    private static Season dateSeason;
    private static string dateHourLabel = string.Empty;
    private static string dateDateLabel = string.Empty;
    private static string dateSeasonLabel = string.Empty;
    private static string dateDayLabel = string.Empty;
    private static IntVec3 tempCell = IntVec3.Invalid;
    private static int tempTick = -1;
    private static bool tempOutdoor;
    private static float tempCached;

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

    public static void DrawDate(ref float curBaseY)
    {
        Vector2 longLat = CurrentLongLat();
        int ticksAbs = Find.TickManager.TicksAbs;
        int hour = GenDate.HourInteger(ticksAbs, longLat.x);
        Season season = GenDate.Season(ticksAbs, longLat);
        bool showDay = UiPlusMod.Settings.showColonyDay;
        int colonyDay = GenDate.DaysPassed + 1;
        bool twelveHour = Prefs.TwelveHourClockMode;
        if (hour != dateHour
            || colonyDay != dateDay
            || season != dateSeason
            || showDay != dateShowDay
            || twelveHour != dateTwelveHour
            || longLat.x != dateLongLatX)
        {
            dateHour = hour;
            dateDay = colonyDay;
            dateSeason = season;
            dateShowDay = showDay;
            dateTwelveHour = twelveHour;
            dateLongLatX = longLat.x;
            dateHourLabel = HourLabel(hour);
            dateDateLabel = GenDate.DateReadoutStringAt(ticksAbs, longLat);
            dateSeasonLabel = SeasonLabelVisible ? season.LabelCap() : string.Empty;
            dateDayLabel = showDay ? "VUIP.ColonyDay".Translate(colonyDay).ToString() : string.Empty;
        }

        Text.Font = GameFont.Small;
        float lineHeight = Text.LineHeight;
        float totalHeight = lineHeight * (showDay ? 3f : 2f);
        float y = curBaseY - totalHeight;
        float x = UI.screenWidth - AlertDrawer.BarWidth;
        Rect block = new Rect(x, y, AlertDrawer.BarWidth, totalHeight);

        Color hourFill = UiPlusMod.Settings.colorDayNight ? DayNightFill() : default;
        DrawSplitBar(new Rect(x, y, AlertDrawer.BarWidth, lineHeight), dateHourLabel, dateSeasonLabel, AlertDrawer.BarWidth * LeftColumnFraction, leftFill: hourFill);
        DrawBar(new Rect(x, y + lineHeight, AlertDrawer.BarWidth, lineHeight), dateDateLabel);
        if (showDay)
        {
            DrawBar(new Rect(x, y + lineHeight * 2f, AlertDrawer.BarWidth, lineHeight), dateDayLabel);
        }

        if (Mouse.IsOver(block))
        {
            StringBuilder tip = new StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                Quadrum quadrum = (Quadrum)i;
                tip.AppendLine(quadrum.Label() + " - " + quadrum.GetSeason(longLat.y).LabelCap());
            }

            TooltipHandler.TipRegion(block, new TipSignal("DateReadoutTip".Translate(colonyDay, 15, season.LabelCap(), 15, GenDate.Quadrum(GenTicks.TicksAbs, longLat.x).Label(), tip.ToString()), 86423));
        }

        curBaseY -= totalHeight;
    }

    public static void DrawTemperatureAndWeather(ref float curBaseY, bool showWeather = true)
    {
        if (Find.CurrentMap == null)
        {
            return;
        }

        Text.Font = GameFont.Small;
        float lineHeight = Text.LineHeight;
        Rect bar = new Rect(UI.screenWidth - AlertDrawer.BarWidth, curBaseY - lineHeight, AlertDrawer.BarWidth, lineHeight);
        float celsius = CurrentTemperature();
        string temperature = celsius.ToStringTemperature("F0");
        string weather = showWeather ? Find.CurrentMap.weatherManager.CurWeatherPerceived.LabelCap : string.Empty;
        Color tempFill = UiPlusMod.Settings.colorTemperature ? TemperatureFill(celsius) : default;
        DrawSplitBar(bar, temperature, weather, bar.width * LeftColumnFraction, leftFill: tempFill);

        if (showWeather)
        {
            string weatherTip = Find.CurrentMap.weatherManager.CurWeatherPerceived.description;
            if (!weatherTip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(bar, weatherTip);
            }
        }

        curBaseY -= lineHeight;
    }

    public static void DrawGameConditions(GameConditionManager manager, ref float curBaseY)
    {
        VisibleConditions.Clear();
        CollectVisibleConditions(manager, VisibleConditions);
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

    internal static void DrawSplitBar(Rect rect, string leftText, string rightText, float leftWidth = -1f, Color fill = default, Color leftFill = default)
    {
        if (leftWidth < 0f)
        {
            leftWidth = rect.width / 2f;
        }

        Rect left = new Rect(rect.x, rect.y, leftWidth, rect.height);
        Rect right = new Rect(rect.x + leftWidth, rect.y, rect.width - leftWidth, rect.height);

        if (fill.a > 0.001f)
        {
            Widgets.DrawBoxSolid(rect, fill);
        }
        else
        {
            AlertDrawer.DrawBarBackground(rect);
        }

        if (leftFill.a > 0.001f)
        {
            Widgets.DrawBoxSolid(left, leftFill);
        }

        if (Mouse.IsOver(rect))
        {
            Widgets.DrawHighlight(rect);
        }

        Text.Font = GameFont.Small;
        bool oldWrap = Text.WordWrap;
        Text.WordWrap = false;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(left, leftText);
        if (!rightText.NullOrEmpty())
        {
            Widgets.Label(right, rightText.Truncate(right.width, AlertDrawer.SharedTruncateCache));
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
        color.a = InfoFillAlpha;
        return color;
    }

    private static float InfoFillAlpha => Mathf.Clamp(Mathf.Max(UiPlusMod.Settings.barBackgroundOpacity, 0.55f), 0.55f, 0.9f);

    private static readonly Color TempCold = new Color(0.16f, 0.34f, 0.78f);
    private static readonly Color TempComfort = new Color(0.12f, 0.50f, 0.18f);
    private static readonly Color TempHot = new Color(0.78f, 0.14f, 0.12f);

    private static Color TemperatureFill(float celsius)
    {
        Color rgb;
        if (celsius < 16f)
        {
            rgb = Color.Lerp(TempCold, TempComfort, Mathf.InverseLerp(0f, 16f, celsius));
        }
        else if (celsius <= 26f)
        {
            rgb = TempComfort;
        }
        else
        {
            rgb = Color.Lerp(TempComfort, TempHot, Mathf.InverseLerp(26f, 40f, celsius));
        }

        rgb.a = InfoFillAlpha;
        return rgb;
    }

    private static Color DayNightFill()
    {
        Color night = Color.HSVToRGB(0.63f, 0.62f, 0.36f);
        Color day = Color.HSVToRGB(0.11f, 0.70f, 0.42f);
        Color color = Color.Lerp(night, day, Mathf.Clamp01(CurrentSunGlow()));
        color.a = InfoFillAlpha;
        return color;
    }

    private static float CurrentSunGlow()
    {
        if (!WorldRendererUtility.WorldSelected && Find.CurrentMap != null)
        {
            return Find.CurrentMap.skyManager.CurSkyGlow;
        }

        PlanetTile tile = Find.WorldSelector.SelectedTile;
        if (!tile.Valid && Find.WorldSelector.NumSelectedObjects > 0)
        {
            tile = Find.WorldSelector.FirstSelectedObject.Tile;
        }

        if (!tile.Valid && Find.CurrentMap != null)
        {
            tile = Find.CurrentMap.Tile;
        }

        if (!tile.Valid)
        {
            return 1f;
        }

        return GenCelestial.CelestialSunGlow(tile, Find.TickManager.TicksAbs);
    }

    private static void DrawBar(Rect rect, string text, Color fill = default)
    {
        if (fill.a > 0.001f)
        {
            Widgets.DrawBoxSolid(rect, fill);
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
        Widgets.Label(rect, text.Truncate(rect.width, AlertDrawer.SharedTruncateCache));
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

        return PlayButtonFilter.CountVisible(worldView);
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

        if (Find.CurrentMap != null)
        {
            return Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile);
        }

        return default;
    }

    private static float CurrentTemperature()
    {
        Map map = Find.CurrentMap;
        bool outdoor = UiPlusMod.Settings.outdoorTemperature;
        if (outdoor)
        {
            return map.mapTemperature.OutdoorTemp;
        }

        IntVec3 cell = UI.MouseCell();
        int tick = Find.TickManager.TicksGame;
        if (tempTick == tick && tempCell == cell && tempOutdoor == outdoor)
        {
            return tempCached;
        }

        tempTick = tick;
        tempCell = cell;
        tempOutdoor = outdoor;
        IntVec3 usefulCell = cell;
        Room? room = cell.GetRoom(map);
        if (room == null)
        {
            for (int i = 0; i < 9; i++)
            {
                IntVec3 neighbor = cell + GenAdj.AdjacentCellsAndInside[i];
                if (!neighbor.InBounds(map))
                {
                    continue;
                }

                Room? neighborRoom = neighbor.GetRoom(map);
                if (neighborRoom != null && ((!neighborRoom.PsychologicallyOutdoors && !neighborRoom.UsesOutdoorTemperature) || (!neighborRoom.PsychologicallyOutdoors && (room == null || room.PsychologicallyOutdoors)) || (neighborRoom.PsychologicallyOutdoors && room == null)))
                {
                    usefulCell = neighbor;
                    room = neighborRoom;
                }
            }
        }

        if (room == null || usefulCell.Fogged(map))
        {
            tempCached = map.mapTemperature.OutdoorTemp;
            return tempCached;
        }

        tempCached = room.Temperature;
        return tempCached;
    }
}
