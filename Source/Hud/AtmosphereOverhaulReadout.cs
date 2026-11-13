using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Atmosphere Overhaul's hovered-cell readout and rainfall rate, drawn in the HUD stack that replaces the one Atmosphere Overhaul draws itself.
/// </summary>
public static class AtmosphereOverhaulReadout
{
    private const string PackageId = "acutus.AtmosphereOverhaul.release";
    private const string ControlsType = "AtmosphereOverhaul.Patches.GlobalControlsPatch";
    private const string WeatherType = "AtmosphereOverhaul.Patches.GlobalMapWeather";
    private const string NotificationsType = "AtmosphereOverhaul.Patches.ModernNotificationsCompat";

    private delegate bool PrecipitationGetter(Map map, out float mmPerHour, out float celsius, out float rh01);

    private static bool resolved;
    private static Func<Map, string>? buildReadout;
    private static PrecipitationGetter? tryGetPrecipitation;
    private static Func<bool>? panelActive;

    private static string? lastSource;
    private static bool lastCompact;
    private static readonly List<string> Lines = new List<string>();

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
        return buildReadout != null;
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

    /// <summary>Draws the readout's rows above curBaseY, without the indoors status and air temperature the temperature bar already shows.</summary>
    public static void Draw(Map map, ref float curBaseY)
    {
        if (!Resolve() || OwnPanel())
        {
            return;
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
            return;
        }

        // Atmosphere Overhaul returns the same string while the hovered cell and simulation step are unchanged.
        bool compact = AtmosphereOverhaulCompact();
        if (!ReferenceEquals(source, lastSource) || compact != lastCompact)
        {
            lastSource = source;
            lastCompact = compact;
            BuildLines(source, compact);
        }

        for (int i = Lines.Count - 1; i >= 0; i--)
        {
            ReadoutDrawer.DrawExternalBar(Lines[i], ref curBaseY);
        }
    }

    private static void BuildLines(string source, bool compact)
    {
        Lines.Clear();
        if (source.Length == 0)
        {
            return;
        }

        string[] lines = source.Split('\n');
        if (compact)
        {
            lines = lines.Length > 2 ? new[] { lines[0], lines[1] } : lines;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (i == 0)
            {
                // "<Indoors|Outdoors> <pressure>hPa": keeps the pressure.
                int space = line.LastIndexOf(' ');
                line = line.EndsWith("hPa", StringComparison.Ordinal) && space >= 0 ? line.Substring(space + 1) : string.Empty;
            }
            else if (i == 1)
            {
                // "<air temperature> | Ground <temperature>": keeps the ground temperature.
                int bar = line.IndexOf(" | ", StringComparison.Ordinal);
                line = bar >= 0 ? line.Substring(bar + 3) : string.Empty;
            }

            if (line.Length > 0)
            {
                Lines.Add(line);
            }
        }
    }

    private static Func<bool>? compactGetter;
    private static bool compactResolved;

    // Atmosphere Overhaul's compact setting keeps only the first two lines.
    private static bool AtmosphereOverhaulCompact()
    {
        if (!compactResolved)
        {
            compactResolved = true;
            const string settingsType = "AtmosphereOverhaul.AtmosphereOverhaulSettings";
            compactGetter = ReflectionGuard.Delegate<Func<bool>>(settingsType, "CompactReadout", AccessTools.PropertyGetter(settingsType + ":CompactReadout"));
        }

        try
        {
            return compactGetter != null && compactGetter();
        }
        catch (Exception)
        {
            compactGetter = null;
            return false;
        }
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

        bool shown = (weather.rainRate > 0f || weather.snowRate > 0f) && mmPerHour >= 0.05f;
        // Tenths below 1 mm/h, whole numbers above, as Atmosphere Overhaul formats it.
        int rate = !shown ? -1 : mmPerHour >= 1f ? Mathf.RoundToInt(mmPerHour) * 10 : Mathf.RoundToInt(mmPerHour * 10f);
        if (weather != weatherDef || rate != weatherRate)
        {
            weatherDef = weather;
            weatherRate = rate;
            weatherLabel = rate < 0 ? weather.LabelCap.ToString() : weather.LabelCap + " (" + mmPerHour.ToString(mmPerHour >= 1f ? "0" : "0.0") + "mm/h)";
        }

        return weatherLabel;
    }
}
