using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Mood and health bar fills in the style of Color Coded Mood Bar: its own colour settings when it's active, its defaults otherwise.
/// </summary>
public static class StatusBarColors
{
    private static readonly Color DefaultExtreme = new Color(1f, 0f, 0f, 0.44f);
    private static readonly Color DefaultMajor = new Color(1f, 0.5f, 0.31f, 0.44f);
    private static readonly Color DefaultMinor = new Color(1f, 0.96f, 0.016f, 0.44f);
    private static readonly Color DefaultNeutral = new Color(0.87f, 0.96f, 0.79f, 0.44f);
    private static readonly Color DefaultContent = new Color(0f, 1f, 1f, 0.44f);
    private static readonly Color DefaultHappy = new Color(0.1f, 0.75f, 0.2f, 0.44f);

    private static readonly Dictionary<Color, Texture2D> Textures = new Dictionary<Color, Texture2D>();
    private static bool resolved;
    private static FieldInfo? settingsField;

    public static Texture2D MoodTexture(Pawn pawn) => TextureOf(MoodColor(pawn));

    public static Texture2D HealthTexture(Pawn pawn) => TextureOf(HealthColor(pawn));

    // Same bands as Color Coded Mood Bar: extreme, major and minor break thresholds, then 65% and 90%.
    private static Color MoodColor(Pawn pawn)
    {
        float mood = pawn.needs.mood.CurLevel;
        Verse.AI.MentalBreaker breaker = pawn.mindState.mentalBreaker;
        object? settings = ModSettings();
        if (mood <= breaker.BreakThresholdExtreme)
        {
            return Read(settings, "Extreme", DefaultExtreme);
        }

        if (mood <= breaker.BreakThresholdMajor)
        {
            return Read(settings, "Major", DefaultMajor);
        }

        if (mood <= breaker.BreakThresholdMinor)
        {
            return Read(settings, "Minor", DefaultMinor);
        }

        if (mood <= 0.65f)
        {
            return Read(settings, "Neutral", DefaultNeutral);
        }

        return mood <= 0.9f ? Read(settings, "Content", DefaultContent) : Read(settings, "Happy", DefaultHappy);
    }

    // Follows the checks behind the base game's health label (HealthUtility.GetGeneralConditionLabel).
    private static Color HealthColor(Pawn pawn)
    {
        object? settings = ModSettings();
        Pawn_HealthTracker health = pawn.health;
        if (health.Dead || !health.capacities.CanBeAwake || health.InPainShock || (pawn.Downed && !LifeStageUtility.AlwaysDowned(pawn)))
        {
            return Read(settings, "Extreme", DefaultExtreme);
        }

        if (pawn.Deathresting)
        {
            return Read(settings, "Neutral", DefaultNeutral);
        }

        foreach (Hediff hediff in health.hediffSet.hediffs)
        {
            if (hediff is Hediff_Injury injury && !injury.IsPermanent())
            {
                return Read(settings, "Major", DefaultMajor);
            }
        }

        return health.hediffSet.PainTotal > 0.3f ? Read(settings, "Minor", DefaultMinor) : Read(settings, "Happy", DefaultHappy);
    }

    private static Texture2D TextureOf(Color color)
    {
        if (!Textures.TryGetValue(color, out Texture2D tex))
        {
            tex = SolidColorMaterials.NewSolidColorTexture(color);
            Textures[color] = tex;
        }

        return tex;
    }

    private static object? ModSettings()
    {
        if (!resolved)
        {
            resolved = true;
            settingsField = AccessTools.TypeByName("ColoredMoodBar13.Main") is { } main ? AccessTools.Field(main, "Settings") : null;
        }

        return settingsField?.GetValue(null);
    }

    private static Color Read(object? settings, string field, Color fallback)
    {
        if (settings == null)
        {
            return fallback;
        }

        try
        {
            return Traverse.Create(settings).Field(field).GetValue() is Color color ? color : fallback;
        }
        catch (Exception)
        {
            return fallback;
        }
    }
}
