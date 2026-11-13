using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Atmosphere Overhaul's hovered-cell readout as two HUD rows (pressure and wind; oxygen, humidity and soil water), its feels-like and ground temperatures
/// in the temperature bar's tooltip, and its rainfall rate beside the weather.
/// </summary>
public static class AtmosphereOverhaulReadout
{
    private const string PackageId = "acutus.AtmosphereOverhaul.release";
    private const string ControlsType = "AtmosphereOverhaul.Patches.GlobalControlsPatch";
    private const string WeatherType = "AtmosphereOverhaul.Patches.GlobalMapWeather";
    private const string NotificationsType = "AtmosphereOverhaul.Patches.ModernNotificationsCompat";
    private const string Separator = " | ";

    private delegate bool PrecipitationGetter(Map map, out float mmPerHour, out float celsius, out float rh01);

    private static bool resolved;
    private static Func<Map, string>? buildReadout;
    private static PrecipitationGetter? tryGetPrecipitation;
    private static Func<bool>? panelActive;

    // The fixed text before each Atmosphere Overhaul line's value, used to tell its optional lines apart.
    private static string feelsPrefix = string.Empty;
    private static string humidPrefix = string.Empty;
    private static string soilPrefix = string.Empty;

    private static int frame = -1;
    private static bool shown;
    private static string? lastSource;
    private static string pressure = string.Empty;
    private static string wind = string.Empty;
    private static string oxygen = string.Empty;
    private static string humidity = string.Empty;
    private static string tooltip = string.Empty;
    private static string temperatureTip = string.Empty;

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
        MethodInfo? panel = AccessTools.PropertyGetter(NotificationsType + ":PanelActive");
        panelActive = panel == null ? null : ReflectionGuard.Delegate<Func<bool>>(NotificationsType, "PanelActive", panel);
        feelsPrefix = KeyPrefix("AO_FeelsLike");
        humidPrefix = KeyPrefix("AO_ReadoutHumid");
        soilPrefix = KeyPrefix("AO_ReadoutSoil");
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

    // Reads Atmosphere Overhaul's readout once per frame; false when there is nothing to show.
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
            Parse(source);
        }

        return true;
    }

    // Lines: "<status> <pressure>hPa", "<air> | Ground <t>", optional "Feels like <t>", wind, "Humid <h>% | O2 <o>%", optional "Soil <held>/<cap>mm".
    private static void Parse(string source)
    {
        pressure = wind = oxygen = humidity = temperatureTip = string.Empty;
        string ground = string.Empty;
        string feels = string.Empty;
        string soil = string.Empty;
        string[] lines = source.Length == 0 ? Array.Empty<string>() : source.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (i == 0)
            {
                int space = line.LastIndexOf(' ');
                pressure = line.EndsWith("hPa", StringComparison.Ordinal) && space >= 0 ? line.Substring(space + 1) : string.Empty;
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
                humidity = bar >= 0 ? line.Substring(0, bar) : line;
                oxygen = bar >= 0 ? line.Substring(bar + Separator.Length) : string.Empty;
            }
            else if (line.StartsWith(soilPrefix, StringComparison.Ordinal))
            {
                soil = line;
            }
            else
            {
                wind = line;
            }
        }

        if (soil.Length > 0)
        {
            humidity = humidity.Length > 0 ? humidity + "  " + soil : soil;
        }

        temperatureTip = feels.Length > 0 && ground.Length > 0 ? feels + "\n" + ground : feels + ground;
        tooltip = source;
    }

    /// <summary>Draws two rows above curBaseY: pressure and wind, then oxygen with humidity and soil water. Their height doesn't change with the hovered cell.</summary>
    public static void Draw(Map map, ref float curBaseY)
    {
        if (!Update(map))
        {
            return;
        }

        ReadoutDrawer.DrawExternalSplitRow(oxygen, humidity, tooltip, ref curBaseY);
        ReadoutDrawer.DrawExternalSplitRow(pressure, wind, tooltip, ref curBaseY);
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
