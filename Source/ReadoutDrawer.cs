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
    private static int dateMinute = int.MinValue;
    private static int dateDay = int.MinValue;
    private static float dateLongLatX = float.NaN;
    private static bool dateShowDay;
    private static bool dateTwelveHour;
    private static bool dateShowPreciseTime;
    private static Season dateSeason;
    private static string dateHourLabel = string.Empty;
    private static string dateDateLabel = string.Empty;
    private static string dateSeasonLabel = string.Empty;
    private static string dateDayLabel = string.Empty;
    private static float dateLongLatY = float.NaN;
    private static string dateTooltip = string.Empty;
    private static int wealthTick = -1;
    private static int wealthMapId = -1;
    private static int wealthRounded = int.MinValue;
    private static string wealthLabel = string.Empty;
    private static string wealthTip = string.Empty;
    private static IntVec3 tempCell = IntVec3.Invalid;
    private static int tempTick = -1;
    private static float tempCached;
    private static float tempLabelCelsius = float.NaN;
    private static TemperatureDisplayMode tempLabelMode;
    private static string tempLabel = string.Empty;
    private static int clockMinute = -1;
    private static int clockHour = -1;
    private static bool clockTwelveHour;
    private static string clockLabel = string.Empty;
    private static float valueColumnWidth = -1f;
    private static float valueColumnBarWidth;
    private static float valueColumnScale;
    private static bool valueColumnTwelveHour;
    private static bool valueColumnPrecise;
    private static LoadedLanguage? valueColumnLanguage;
    private static readonly Color NightFill = Color.HSVToRGB(0.63f, 0.62f, 0.36f);
    private static readonly Color DayFill = Color.HSVToRGB(0.11f, 0.70f, 0.42f);

    public static void ResetPlaySettingsHeight()
    {
        cachedPlaySettingsHeight = 0f;
    }

    public static void DrawPlaySettings(WidgetRow rowVisibility, bool worldView, ref float curBaseY)
    {
        float bottom = curBaseY;
        float pad = IconPad;
        float icon = WidgetRow.IconSize;
        float gap = WidgetRow.DefaultGap;

        // Sized to a whole number of icon columns and centred, so both margins match.
        float available = AlertDrawer.BarWidth - pad * 2f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((available - icon) / (icon + gap)) + 1);
        float gridWidth = columns * icon + (columns - 1) * gap;
        float sidePad = Mathf.Floor((AlertDrawer.BarWidth - gridWidth) / 2f);

        float height = Mathf.Max(EstimatePlaySettingsHeight(worldView, pad, gridWidth), cachedPlaySettingsHeight);
        float y = bottom - height;
        AlertDrawer.DrawBarBackground(new Rect(UI.screenWidth - AlertDrawer.BarWidth, y, AlertDrawer.BarWidth, height));

        // The epsilon stops a full row wrapping early on float rounding.
        rowVisibility.Init(UI.screenWidth - sidePad, bottom - pad - icon, UIDirection.LeftThenUp, gridWidth + 0.1f);
        Find.PlaySettings.DoPlaySettingsGlobalControls(rowVisibility, worldView);

        cachedPlaySettingsHeight = bottom - rowVisibility.FinalY + pad;
        curBaseY = bottom - cachedPlaySettingsHeight;
    }

    public static void DrawDate(ref float curBaseY)
    {
        Vector2 longLat = CurrentLongLat();
        int ticksAbs = Find.TickManager.TicksAbs;
        int hour = GenDate.HourInteger(ticksAbs, longLat.x);
        int minute = MinuteOfHour(ticksAbs, longLat.x);
        Season season = GenDate.Season(ticksAbs, longLat);
        bool showDay = UiPlusMod.Settings.showColonyDay;
        bool showWealth = UiPlusMod.Settings.showColonyWealth && CurrentWealthMap() != null;
        int colonyDay = GenDate.DaysPassed + 1;
        bool twelveHour = Prefs.TwelveHourClockMode;
        bool showPreciseTime = UiPlusMod.Settings.showPreciseTime;
        if (hour != dateHour
            || (showPreciseTime && minute != dateMinute)
            || colonyDay != dateDay
            || season != dateSeason
            || showDay != dateShowDay
            || twelveHour != dateTwelveHour
            || showPreciseTime != dateShowPreciseTime
            || longLat.x != dateLongLatX
            || longLat.y != dateLongLatY)
        {
            dateHour = hour;
            dateMinute = minute;
            dateDay = colonyDay;
            dateSeason = season;
            dateShowDay = showDay;
            dateTwelveHour = twelveHour;
            dateShowPreciseTime = showPreciseTime;
            dateLongLatX = longLat.x;
            dateLongLatY = longLat.y;
            dateHourLabel = HourLabel(hour, minute, showPreciseTime);
            dateDateLabel = GenDate.DateReadoutStringAt(ticksAbs, longLat);
            dateSeasonLabel = SeasonLabelVisible ? season.LabelCap() : string.Empty;
            dateDayLabel = showDay ? "VUIP.ColonyDay".Translate(colonyDay).ToString() : string.Empty;

            // The tooltip only changes with the date, so it is rebuilt here rather than on hover.
            StringBuilder tip = new StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                Quadrum quadrum = (Quadrum)i;
                tip.AppendLine(quadrum.Label() + " - " + quadrum.GetSeason(longLat.y).LabelCap());
            }

            dateTooltip = "DateReadoutTip".Translate(
                colonyDay,
                15,
                season.LabelCap(),
                15,
                GenDate.Quadrum(ticksAbs, longLat.x).Label(),
                tip.ToString());
        }

        Text.Font = GameFont.Small;
        float lineHeight = Text.LineHeight;
        int lines = 2 + (showDay ? 1 : 0) + (showWealth ? 1 : 0);
        float totalHeight = lineHeight * lines;
        float y = curBaseY - totalHeight;
        float x = UI.screenWidth - AlertDrawer.BarWidth;
        Rect dateBlock = new Rect(x, y, AlertDrawer.BarWidth, lineHeight * (2 + (showDay ? 1 : 0)));

        Color hourFill = UiPlusMod.Settings.colorDayNight ? DayNightFill() : default;
        DrawSplitBar(new Rect(x, y, AlertDrawer.BarWidth, lineHeight), dateHourLabel, dateSeasonLabel, ValueColumnWidth(AlertDrawer.BarWidth), leftFill: hourFill);
        DrawBar(new Rect(x, y + lineHeight, AlertDrawer.BarWidth, lineHeight), dateDateLabel);
        float nextY = y + lineHeight * 2f;
        if (showDay)
        {
            DrawBar(new Rect(x, nextY, AlertDrawer.BarWidth, lineHeight), dateDayLabel);
            nextY += lineHeight;
        }

        if (showWealth)
        {
            DrawWealthBar(new Rect(x, nextY, AlertDrawer.BarWidth, lineHeight));
        }

        if (Mouse.IsOver(dateBlock))
        {
            TooltipHandler.TipRegion(dateBlock, new TipSignal(dateTooltip, 86423));
        }

        curBaseY -= totalHeight;
    }

    private static void DrawWealthBar(Rect rect)
    {
        Map? map = CurrentWealthMap();
        if (map?.wealthWatcher == null)
        {
            return;
        }

        WealthWatcher watcher = map.wealthWatcher;
        float total = watcher.WealthTotal;
        int rounded = Mathf.RoundToInt(total);
        int tick = Find.TickManager.TicksGame;
        if (wealthMapId != map.uniqueID || wealthRounded != rounded || tick - wealthTick >= 60)
        {
            wealthTick = tick;
            wealthMapId = map.uniqueID;
            wealthRounded = rounded;
            wealthLabel = "VUIP.ColonyWealth".Translate(total.ToStringMoney());
            wealthTip = "VUIP.ColonyWealthTip".Translate(
                total.ToStringMoney(),
                watcher.WealthItems.ToStringMoney(),
                watcher.WealthBuildings.ToStringMoney(),
                watcher.WealthPawns.ToStringMoney());
        }

        DrawBar(rect, wealthLabel);
        TooltipHandler.TipRegion(rect, new TipSignal(wealthTip, 0x57A1C4E3 ^ wealthMapId));
    }

    private static Map? CurrentWealthMap()
    {
        if (Find.CurrentMap != null)
        {
            return Find.CurrentMap;
        }

        return Find.AnyPlayerHomeMap;
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
        // The label is rebuilt only when the temperature or display mode changes.
        float celsius = CurrentTemperature();
        if (celsius != tempLabelCelsius || Prefs.TemperatureMode != tempLabelMode)
        {
            tempLabelCelsius = celsius;
            tempLabelMode = Prefs.TemperatureMode;
            tempLabel = celsius.ToStringTemperature("F0");
        }

        string temperature = tempLabel;
        string weather = showWeather ? Find.CurrentMap.weatherManager.CurWeatherPerceived.LabelCap : string.Empty;
        Color tempFill = UiPlusMod.Settings.colorTemperature ? TemperatureFill(celsius) : default;
        DrawSplitBar(bar, temperature, weather, ValueColumnWidth(bar.width), leftFill: tempFill);

        if (showWeather && Mouse.IsOver(bar))
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

            // TooltipString allocates, so it is only built while hovered.
            if (Mouse.IsOver(bar))
            {
                TooltipHandler.TipRegion(bar, new TipSignal(condition.TooltipString, 0x3A2DF42A ^ condition.uniqueID));
            }

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
        Widgets.Label(left, TextCache.Truncate(leftText, left.width));
        if (!rightText.NullOrEmpty())
        {
            Widgets.Label(right, TextCache.Truncate(rightText, right.width));
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
        Color color = Color.Lerp(NightFill, DayFill, Mathf.Clamp01(CurrentSunGlow()));
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

    // Draws the real-time clock as one of the bars.
    internal static void DrawRealtimeClock(ref float curBaseY)
    {
        DateTime now = DateTime.Now;
        bool twelveHour = Prefs.TwelveHourClockMode;
        if (now.Minute != clockMinute || now.Hour != clockHour || twelveHour != clockTwelveHour)
        {
            clockMinute = now.Minute;
            clockHour = now.Hour;
            clockTwelveHour = twelveHour;
            clockLabel = now.ToString(twelveHour ? "h:mm tt" : "HH:mm");
        }

        Text.Font = GameFont.Small;
        float lineHeight = Text.LineHeight;
        Rect bar = new Rect(UI.screenWidth - AlertDrawer.BarWidth, curBaseY - lineHeight, AlertDrawer.BarWidth, lineHeight);
        DrawBar(bar, clockLabel);
        curBaseY -= lineHeight;
    }

    // Draws another mod's row as a split bar aligned with the date and temperature rows.
    internal static void DrawExternalSplitRow(string leftText, string rightText, string? tooltip, ref float curBaseY)
    {
        Text.Font = GameFont.Small;
        float lineHeight = Text.LineHeight;
        Rect bar = new Rect(UI.screenWidth - AlertDrawer.BarWidth, curBaseY - lineHeight, AlertDrawer.BarWidth, lineHeight);
        DrawSplitBar(bar, leftText, rightText, ValueColumnWidth(AlertDrawer.BarWidth));
        if (!tooltip.NullOrEmpty())
        {
            TooltipHandler.TipRegion(bar, tooltip);
        }

        curBaseY -= lineHeight;
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
        Widgets.Label(rect, TextCache.Truncate(text, rect.width));
        Text.WordWrap = oldWrap;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private static int MinuteOfHour(long ticksAbs, float longitude)
    {
        long localTicks = ticksAbs + GenDate.LocalTicksOffsetFromLongitude(longitude);
        int ticksIntoHour = (int)GenMath.PositiveMod(localTicks, GenDate.TicksPerHour);
        return ticksIntoHour * 60 / GenDate.TicksPerHour;
    }

    // Fits the widest clock value, leaving at least 60px on the right; cached until the clock mode, language, scale or width changes.
    private static float ValueColumnWidth(float barWidth)
    {
        bool twelveHour = Prefs.TwelveHourClockMode;
        bool precise = UiPlusMod.Settings.showPreciseTime;
        float scale = Prefs.UIScale;
        LoadedLanguage language = LanguageDatabase.activeLanguage;
        if (valueColumnWidth >= 0f
            && barWidth == valueColumnBarWidth
            && scale == valueColumnScale
            && twelveHour == valueColumnTwelveHour
            && precise == valueColumnPrecise
            && language == valueColumnLanguage)
        {
            return valueColumnWidth;
        }

        valueColumnBarWidth = barWidth;
        valueColumnScale = scale;
        valueColumnTwelveHour = twelveHour;
        valueColumnPrecise = precise;
        valueColumnLanguage = language;
        Text.Font = GameFont.Small;
        string widestClock;
        if (twelveHour)
        {
            string am = "AM".Translate();
            string pm = "PM".Translate();
            string suffix = Text.CalcSize(am).x >= Text.CalcSize(pm).x ? am : pm;
            widestClock = precise ? $"12:59 {suffix}" : $"12 {suffix}";
        }
        else
        {
            widestClock = precise ? "23:59" : "23" + "LetterHour".Translate();
        }

        float needed = Text.CalcSize(widestClock).x + 10f;
        valueColumnWidth = Mathf.Min(Mathf.Max(barWidth * LeftColumnFraction, needed), Mathf.Max(0f, barWidth - 60f));
        return valueColumnWidth;
    }

    private static string HourLabel(int hour, int minute, bool precise)
    {
        if (!Prefs.TwelveHourClockMode)
        {
            return precise ? $"{hour:00}:{minute:00}" : hour.ToString() + "LetterHour".Translate();
        }

        TaggedString suffix = hour >= 12 ? "PM".Translate() : "AM".Translate();
        int displayHour = hour == 0 ? 12 : (hour > 12 ? hour - 12 : hour);
        return precise ? $"{displayHour}:{minute:00} {suffix}" : $"{displayHour} {suffix}";
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
        if (UiPlusMod.Settings.outdoorTemperature)
        {
            return map.mapTemperature.OutdoorTemp;
        }

        IntVec3 cell = UI.MouseCell();
        int tick = Find.TickManager.TicksGame;
        if (tempTick == tick && tempCell == cell)
        {
            return tempCached;
        }

        tempTick = tick;
        tempCell = cell;
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
