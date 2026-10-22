using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

public static class NewGameDefaults
{
    public const float ButtonWidth = 160f;
    public const float ButtonHeight = 32f;

    // Every public bool/int/float field of Difficulty, saved by name; unknown names are skipped.
    private static readonly FieldInfo[] DifficultyFields = typeof(Difficulty)
        .GetFields(BindingFlags.Public | BindingFlags.Instance)
        .Where(field => !field.IsLiteral && !field.IsInitOnly && IsValueSetting(field.FieldType))
        .ToArray();

    public static bool Enabled => UiPlusMod.Settings.enableNewGameDefaults;

    private static bool IsValueSetting(System.Type type)
    {
        return type == typeof(bool) || type == typeof(int) || type == typeof(float) || type == typeof(float?);
    }

    public static Dictionary<string, string> CaptureDifficulty(Difficulty source)
    {
        Dictionary<string, string> values = new Dictionary<string, string>();
        foreach (FieldInfo field in DifficultyFields)
        {
            object? value = field.GetValue(source);
            values[field.Name] = value is float f
                ? f.ToString("R", CultureInfo.InvariantCulture)
                : value?.ToString() ?? string.Empty;
        }

        return values;
    }

    public static void RestoreDifficulty(Difficulty target, Dictionary<string, string> values)
    {
        foreach (FieldInfo field in DifficultyFields)
        {
            if (!values.TryGetValue(field.Name, out string? text))
            {
                continue;
            }

            try
            {
                if (field.FieldType == typeof(bool))
                {
                    field.SetValue(target, bool.Parse(text));
                }
                else if (field.FieldType == typeof(int))
                {
                    field.SetValue(target, int.Parse(text, CultureInfo.InvariantCulture));
                }
                else if (field.FieldType == typeof(float))
                {
                    field.SetValue(target, float.Parse(text, CultureInfo.InvariantCulture));
                }
                else if (field.FieldType == typeof(float?))
                {
                    field.SetValue(target, text.Length == 0 ? null : float.Parse(text, CultureInfo.InvariantCulture));
                }
            }
            catch (System.FormatException)
            {
                // A hand-edited or corrupted value; keep the difficulty preset's own value.
            }
        }
    }

    public static Rect ButtonRect(Rect pageRect)
    {
        return new Rect(pageRect.xMax - ButtonWidth, pageRect.y + 4f, ButtonWidth, ButtonHeight);
    }

    public static void Saved()
    {
        UiPlusMod.Instance.WriteSettings();
        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        Messages.Message("VUIP.NewGameDefaultsSaved".Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
    }

    public static void ApplyFactions(List<FactionDef> factions, List<FactionDef> initialFactions)
    {
        List<FactionDef> result = new List<FactionDef>();
        foreach (string defName in UiPlusMod.Settings.worldDefaults!.factions)
        {
            FactionDef? faction = DefDatabase<FactionDef>.GetNamedSilentFail(defName);
            if (faction == null || !faction.displayInFactionSelection)
            {
                continue;
            }

            int max = faction.maxConfigurableAtWorldCreation;
            if (max >= 0 && result.Count(f => f == faction) >= max)
            {
                continue;
            }

            result.Add(faction);
        }

        // Whatever the game added that the player never sees in the list stays as it was.
        result.AddRange(factions.Where(f => !f.displayInFactionSelection));

        // A scenario can insist on a faction; the page would refuse to continue without it.
        Scenario? scenario = Find.Scenario;
        if (scenario != null)
        {
            foreach (ScenPart part in scenario.AllParts)
            {
                FactionDef? required = part.def.preventRemovalOfFaction;
                if (required != null && !result.Contains(required))
                {
                    result.Add(required);
                }
            }
        }

        factions.Clear();
        factions.AddRange(result);

        // The page compares against initialFactions to detect changes.
        initialFactions.Clear();
        initialFactions.AddRange(result);
    }
}
