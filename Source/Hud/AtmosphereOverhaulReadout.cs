using System;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// With Atmosphere Overhaul active: its hovered-cell readout as one strip of icon cells (pressure, wind, humidity, oxygen, soil water),
/// its feels-like and ground temperatures in the temperature bar's tooltip, and its rainfall rate beside the weather. Without it, nothing is drawn.
/// </summary>
public static class AtmosphereOverhaulReadout
{
    private const string PackageId = "acutus.AtmosphereOverhaul.release";
    private const string ControlsType = "AtmosphereOverhaul.Patches.GlobalControlsPatch";
    private const string WeatherType = "AtmosphereOverhaul.Patches.GlobalMapWeather";
    private const string NotificationsType = "AtmosphereOverhaul.Patches.ModernNotificationsCompat";
    private const string InterfaceType = "AtmosphereOverhaul.RimWorld.AtmosphereInterface";
    private const string Separator = " | ";
    private const string NoValue = "-";
    private const int Cells = 5;
    private const float IconSize = 16f;
    private const float IconGap = 3f;

    private delegate bool PrecipitationGetter(Map map, out float mmPerHour, out float celsius, out float rh01);
    private delegate bool WindGetter(Map map, IntVec3 cell, out Vector2 wind);

    private static bool resolved;
    private static Func<Map, string>? buildReadout;
    private static PrecipitationGetter? tryGetPrecipitation;
    private static WindGetter? tryGetWind;
    private static Func<bool>? panelActive;

    // The fixed text before each Atmosphere Overhaul line's value, used to tell its optional lines apart.
    private static string feelsPrefix = string.Empty;
    private static string humidPrefix = string.Empty;
    private static string soilPrefix = string.Empty;

    private static Texture2D? pressureTex;
    private static Texture2D? windTex;
    private static Texture2D? humidityTex;
    private static Texture2D? soilTex;

    private static readonly Regex Number = new Regex(@"-?\d+(?:[.,]\d+)?", RegexOptions.Compiled);
    private static readonly Regex Fraction = new Regex(@"(\d+)\s*/\s*(\d+)", RegexOptions.Compiled);

    private static int frame = -1;
    private static bool shown;
    private static string? lastSource;
    private static string temperatureTip = string.Empty;

    // One strip cell: icon (or a short label), value, whether it has a reading, and its tooltip.
    private sealed class Cell
    {
        public Texture2D? Icon;
        public string Label = string.Empty;
        public string Value = NoValue;
        public bool Known;
        public float Angle;
        public bool Rotate;
        public string Tip = string.Empty;
        public float ContentWidth;
    }

    private static readonly Cell[] Strip = new Cell[Cells];

    private static WeatherDef? weatherDef;
    private static int weatherRate = int.MinValue;
    private static string weatherLabel = string.Empty;

    private static bool Resolve()
    {
        if (resolved)
        {
            return buildReadout != null;
        }

        resolved = true;
        if (ModLister.GetActiveModWithIdentifier(PackageId, ignorePostfix: true) == null)
        {
            return false;
        }

        buildReadout = ReflectionGuard.Delegate<Func<Map, string>>(ControlsType, "BuildHoveredAtmosphereReadout",
            AccessTools.Method(ControlsType + ":BuildHoveredAtmosphereReadout", new[] { typeof(Map) }));
        tryGetPrecipitation = ReflectionGuard.Delegate<PrecipitationGetter>(WeatherType, "TryGetPrecipitation",
            AccessTools.Method(WeatherType + ":TryGetPrecipitation"));
        tryGetWind = ReflectionGuard.Delegate<WindGetter>(InterfaceType, "TryGetWind", AccessTools.Method(InterfaceType + ":TryGetWind"));
        MethodInfo? panel = AccessTools.PropertyGetter(NotificationsType + ":PanelActive");
        panelActive = panel == null ? null : ReflectionGuard.Delegate<Func<bool>>(NotificationsType, "PanelActive", panel);
        feelsPrefix = KeyPrefix("AO_FeelsLike");
        humidPrefix = KeyPrefix("AO_ReadoutHumid");
        soilPrefix = KeyPrefix("AO_ReadoutSoil");
        pressureTex = ContentFinder<Texture2D>.Get("UI/Icons/Hud/atmosphere-pressure");
        windTex = ContentFinder<Texture2D>.Get("UI/Icons/Hud/atmosphere-wind");
        humidityTex = ContentFinder<Texture2D>.Get("UI/Icons/Hud/atmosphere-humidity");
        soilTex = ContentFinder<Texture2D>.Get("UI/Icons/Hud/atmosphere-soil");
        for (int i = 0; i < Cells; i++)
        {
            Strip[i] = new Cell();
        }

        Strip[0].Icon = pressureTex;
        Strip[1].Icon = windTex;
        Strip[2].Icon = humidityTex;
        Strip[3].Label = "O2";
        Strip[4].Icon = soilTex;
        return buildReadout != null;
    }

    private static string KeyPrefix(string key)
    {
        string text = key.Translate("\u0001", "\u0001").Resolve();
        int cut = text.IndexOf('\u0001');
        return cut > 0 ? text.Substring(0, cut) : "\u0002";
    }

    // Atmosphere Overhaul draws the readout itself (in its postfix) while Modern Notifications' panel is on.
    private static bool OwnPanel()
    {
        try
        {
            return panelActive != null && panelActive();
        }
        catch (Exception)
        {
            panelActive = null;
            return false;
        }
    }

    // Reads Atmosphere Overhaul's readout once per frame; false when Atmosphere Overhaul isn't active or draws it itself.
    private static bool Update(Map map)
    {
        if (frame == Time.frameCount)
        {
            return shown;
        }

        frame = Time.frameCount;
        shown = false;
        if (!Resolve() || OwnPanel())
        {
            return false;
        }

        string source;
        try
        {
            source = buildReadout!(map) ?? string.Empty;
        }
        catch (Exception exception)
        {
            Log.ErrorOnce($"[Vanilla UI+] Atmosphere Overhaul readout failed: {exception}", 0x5C1D01);
            buildReadout = null;
            return false;
        }

        shown = true;
        // Atmosphere Overhaul returns the same string while the hovered cell and simulation step are unchanged.
        if (!ReferenceEquals(source, lastSource))
        {
            lastSource = source;
            Parse(map, source);
        }

        return true;
    }

    // Lines: "<status> <pressure>hPa", "<air> | Ground <t>", optional "Feels like <t>", wind, "Humid <h>% | O2 <o>%", optional "Soil <held>/<cap>mm".
    private static void Parse(Map map, string source)
    {
        for (int i = 0; i < Cells; i++)
        {
            Strip[i].Value = NoValue;
            Strip[i].Known = false;
            Strip[i].Rotate = false;
            Strip[i].Tip = string.Empty;
        }

        string ground = string.Empty;
        string feels = string.Empty;
        string[] lines = source.Length == 0 ? Array.Empty<string>() : source.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (i == 0)
            {
                int space = line.LastIndexOf(' ');
                if (line.EndsWith("hPa", StringComparison.Ordinal) && space >= 0)
                {
                    string hpa = line.Substring(space + 1, line.Length - space - 4);
                    SetCell(Strip[0], hpa, "VUIP.AtmospherePressureTip".Translate(hpa));
                }
            }
            else if (i == 1)
            {
                int bar = line.IndexOf(Separator, StringComparison.Ordinal);
                ground = bar >= 0 ? line.Substring(bar + Separator.Length) : string.Empty;
            }
            else if (line.StartsWith(feelsPrefix, StringComparison.Ordinal))
            {
                feels = line;
            }
            else if (line.StartsWith(humidPrefix, StringComparison.Ordinal))
            {
                int bar = line.IndexOf(Separator, StringComparison.Ordinal);
                string humid = bar >= 0 ? line.Substring(0, bar) : line;
                Match h = Number.Match(humid);
                if (h.Success)
                {
                    SetCell(Strip[2], h.Value + "%", "VUIP.AtmosphereHumidityTip".Translate(h.Value));
                }

                // "O2 <o>%": the last number, since the label has a digit of its own.
                MatchCollection? o = bar >= 0 ? Number.Matches(line.Substring(bar + Separator.Length)) : null;
                if (o is { Count: > 0 })
                {
                    string oxygen = o[o.Count - 1].Value;
                    SetCell(Strip[3], oxygen + "%", "VUIP.AtmosphereOxygenTip".Translate(oxygen));
                }
            }
            else if (line.StartsWith(soilPrefix, StringComparison.Ordinal))
            {
                Match s = Fraction.Match(line);
                if (s.Success)
                {
                    SetCell(Strip[4], s.Groups[1].Value + "/" + s.Groups[2].Value, "VUIP.AtmosphereSoilTip".Translate(s.Groups[1].Value, s.Groups[2].Value));
                }
            }
            else
            {
                ParseWind(map, line);
            }
        }

        if (!Strip[4].Known && source.Length > 0)
        {
            Strip[4].Tip = "VUIP.AtmosphereNoSoilTip".Translate();
        }

        temperatureTip = feels.Length > 0 && ground.Length > 0 ? feels + "\n" + ground : feels + ground;
        MeasureCells();
    }

    // "<from> <speed>m/s", or Atmosphere Overhaul's calm wording; the arrow follows the hovered cell's wind vector.
    private static void ParseWind(Map map, string line)
    {
        Cell cell = Strip[1];
        MatchCollection numbers = Number.Matches(line);
        if (numbers.Count == 0)
        {
            return;
        }

        Match speed = numbers[numbers.Count - 1];
        Vector2 wind = Vector2.zero;
        bool vector = false;
        try
        {
            vector = tryGetWind != null && tryGetWind(map, UI.MouseCell(), out wind);
        }
        catch (Exception)
        {
            tryGetWind = null;
        }

        if (!vector || wind.magnitude < 0.05f)
        {
            cell.Value = NoValue;
            cell.Known = true;
            cell.Tip = "VUIP.AtmosphereCalmTip".Translate();
            return;
        }

        string from = line.Substring(0, speed.Index).Trim();
        cell.Value = speed.Value;
        cell.Known = true;
        cell.Rotate = true;
        // The wind vector's x is east and its y is north; the arrow points the way the air moves, clockwise from up.
        cell.Angle = Mathf.Atan2(wind.x, wind.y) * Mathf.Rad2Deg;
        cell.Tip = "VUIP.AtmosphereWindTip".Translate(from, speed.Value);
    }

    private static void SetCell(Cell cell, string value, string tip)
    {
        cell.Value = value;
        cell.Known = true;
        cell.Tip = tip;
    }

    private static void MeasureCells()
    {
        GameFont font = Text.Font;
        Text.Font = GameFont.Small;
        for (int i = 0; i < Cells; i++)
        {
            Cell cell = Strip[i];
            float head = cell.Icon != null ? IconSize : Text.CalcSize(cell.Label).x;
            cell.ContentWidth = head + IconGap + Text.CalcSize(cell.Value).x;
        }

        Text.Font = font;
    }

    /// <summary>Draws the strip above curBaseY: one bar of five cells that keeps its height whatever the hovered cell.</summary>
    public static void Draw(Map map, ref float curBaseY)
    {
        if (!Update(map))
        {
            return;
        }

        // A fogged or off-map cell has no readout, so every cell shows a dimmed dash.
        Text.Font = GameFont.Small;
        float lineHeight = Text.LineHeight;
        Rect bar = new Rect(UI.screenWidth - AlertDrawer.BarWidth, curBaseY - lineHeight, AlertDrawer.BarWidth, lineHeight);
        AlertDrawer.DrawBarBackground(bar);
        float width = bar.width / Cells;
        bool wrap = Text.WordWrap;
        Text.WordWrap = false;
        Text.Anchor = TextAnchor.MiddleLeft;
        for (int i = 0; i < Cells; i++)
        {
            DrawCell(new Rect(bar.x + width * i, bar.y, width, bar.height), Strip[i]);
        }

        Text.Anchor = TextAnchor.UpperLeft;
        Text.WordWrap = wrap;
        GUI.color = Color.white;
        curBaseY -= lineHeight;
    }

    private static void DrawCell(Rect rect, Cell cell)
    {
        if (Mouse.IsOver(rect))
        {
            Widgets.DrawHighlight(rect);
            if (cell.Tip.Length > 0)
            {
                TooltipHandler.TipRegion(rect, cell.Tip);
            }
        }

        Color tint = cell.Known ? Color.white : PawnReadoutDrawer.MutedColor;
        float x = rect.x + Mathf.Max(2f, (rect.width - cell.ContentWidth) / 2f);
        if (cell.Icon != null)
        {
            Rect icon = new Rect(x, rect.center.y - IconSize / 2f, IconSize, IconSize);
            GUI.color = cell.Known ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            if (cell.Rotate)
            {
                Widgets.DrawTextureRotated(icon, cell.Icon, cell.Angle);
            }
            else
            {
                GUI.DrawTexture(icon, cell.Icon, ScaleMode.ScaleToFit);
            }

            x += IconSize + IconGap;
        }
        else
        {
            GUI.color = PawnReadoutDrawer.MutedColor;
            float labelWidth = Text.CalcSize(cell.Label).x;
            Widgets.Label(new Rect(x, rect.y, labelWidth, rect.height), cell.Label);
            x += labelWidth + IconGap;
        }

        GUI.color = tint;
        Widgets.Label(new Rect(x, rect.y, rect.xMax - x, rect.height), cell.Value);
        GUI.color = Color.white;
    }

    /// <summary>Atmosphere Overhaul's feels-like and ground temperatures for the hovered cell, for the temperature bar's tooltip; empty without it.</summary>
    public static string TemperatureTip(Map map)
    {
        return Update(map) ? temperatureTip : string.Empty;
    }

    /// <summary>The weather label with Atmosphere Overhaul's rainfall rate when it is raining or snowing, as its own weather row shows it.</summary>
    public static string WeatherLabel(Map map, WeatherDef weather)
    {
        if (!Resolve())
        {
            return weather.LabelCap;
        }

        float mmPerHour;
        try
        {
            if (tryGetPrecipitation == null || !tryGetPrecipitation(map, out mmPerHour, out _, out _))
            {
                mmPerHour = (map.weatherManager.RainRate + map.weatherManager.SnowRate) * 6f;
            }
        }
        catch (Exception)
        {
            tryGetPrecipitation = null;
            mmPerHour = (map.weatherManager.RainRate + map.weatherManager.SnowRate) * 6f;
        }

        bool raining = (weather.rainRate > 0f || weather.snowRate > 0f) && mmPerHour >= 0.05f;
        // Tenths below 1 mm/h, whole numbers above, as Atmosphere Overhaul formats it.
        int rate = !raining ? -1 : mmPerHour >= 1f ? Mathf.RoundToInt(mmPerHour) * 10 : Mathf.RoundToInt(mmPerHour * 10f);
        if (weather != weatherDef || rate != weatherRate)
        {
            weatherDef = weather;
            weatherRate = rate;
            weatherLabel = rate < 0 ? weather.LabelCap.ToString() : weather.LabelCap + " (" + mmPerHour.ToString(mmPerHour >= 1f ? "0" : "0.0") + "mm/h)";
        }

        return weatherLabel;
    }
}
