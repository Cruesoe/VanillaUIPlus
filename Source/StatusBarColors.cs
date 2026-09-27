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
    private const int Extreme = 0;
    private const int Major = 1;
    private const int Minor = 2;
    private const int Neutral = 3;
    private const int Content = 4;
    private const int Happy = 5;

    // Color Coded Mood Bar's settings are re-read this often, so changes made there show without reading them every frame.
    private const float RefreshSeconds = 1f;

    private static readonly string[] FieldNames = { "Extreme", "Major", "Minor", "Neutral", "Content", "Happy" };

    private static readonly Color[] Defaults =
    {
        new Color(1f, 0f, 0f, 0.44f),
        new Color(1f, 0.5f, 0.31f, 0.44f),
        new Color(1f, 0.96f, 0.016f, 0.44f),
        new Color(0.87f, 0.96f, 0.79f, 0.44f),
        new Color(0f, 1f, 1f, 0.44f),
        new Color(0.1f, 0.75f, 0.2f, 0.44f),
    };

    private static readonly Dictionary<Color, Texture2D> TexturesByColor = new Dictionary<Color, Texture2D>();
    private static readonly Texture2D?[] Textures = new Texture2D?[FieldNames.Length];
    private static bool resolved;
    private static FieldInfo? settingsField;
    private static FieldInfo?[]? colorFields;
    private static float refreshedAt = float.MinValue;

    public static Texture2D MoodTexture(Pawn pawn) => TextureFor(MoodLevel(pawn));

    public static Texture2D HealthTexture(Pawn pawn) => TextureFor(HealthLevel(pawn));

    // Same bands as Color Coded Mood Bar: extreme, major and minor break thresholds, then 65% and 90%.
    private static int MoodLevel(Pawn pawn)
    {
        float mood = pawn.needs.mood.CurLevel;
        Verse.AI.MentalBreaker breaker = pawn.mindState.mentalBreaker;
        if (mood <= breaker.BreakThresholdExtreme)
        {
            return Extreme;
        }

        if (mood <= breaker.BreakThresholdMajor)
        {
            return Major;
        }

        if (mood <= breaker.BreakThresholdMinor)
        {
            return Minor;
        }

        if (mood <= 0.65f)
        {
            return Neutral;
        }

        return mood <= 0.9f ? Content : Happy;
    }

    // Follows the checks behind the base game's health label (HealthUtility.GetGeneralConditionLabel).
    private static int HealthLevel(Pawn pawn)
    {
        Pawn_HealthTracker health = pawn.health;
        if (health.Dead || !health.capacities.CanBeAwake || health.InPainShock || (pawn.Downed && !LifeStageUtility.AlwaysDowned(pawn)))
        {
            return Extreme;
        }

        if (pawn.Deathresting)
        {
            return Neutral;
        }

        List<Hediff> hediffs = health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            if (hediffs[i] is Hediff_Injury injury && !injury.IsPermanent())
            {
                return Major;
            }
        }

        return health.hediffSet.PainTotal > 0.3f ? Minor : Happy;
    }

    private static Texture2D TextureFor(int level)
    {
        float now = Time.realtimeSinceStartup;
        if (now - refreshedAt >= RefreshSeconds || now < refreshedAt)
        {
            refreshedAt = now;
            Refresh();
        }

        return Textures[level]!;
    }

    // Reads the six colours and points each level at a texture of that colour, making a texture only for a colour not seen before.
    private static void Refresh()
    {
        object? settings = ModSettings();
        for (int i = 0; i < FieldNames.Length; i++)
        {
            Color color = Read(settings, i);
            if (!TexturesByColor.TryGetValue(color, out Texture2D tex))
            {
                tex = SolidColorMaterials.NewSolidColorTexture(color);
                TexturesByColor[color] = tex;
            }

            Textures[i] = tex;
        }
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

    private static Color Read(object? settings, int level)
    {
        if (settings == null)
        {
            return Defaults[level];
        }

        try
        {
            if (colorFields == null)
            {
                colorFields = new FieldInfo?[FieldNames.Length];
                for (int i = 0; i < FieldNames.Length; i++)
                {
                    colorFields[i] = AccessTools.Field(settings.GetType(), FieldNames[i]);
                }
            }

            return colorFields[level]?.GetValue(settings) is Color color ? color : Defaults[level];
        }
        catch (Exception)
        {
            return Defaults[level];
        }
    }
}
