using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

/// <summary>
/// Storyteller page choices saved with "Set as default". Defs are kept as defNames so a
/// default that names a since-removed storyteller still loads; it is then just skipped.
/// </summary>
public class StorytellerDefaults : IExposable
{
    public string? storyteller;
    public string? difficulty;

    // Custom difficulty as field name -> text value, plus the Anomaly playstyle by defName.
    // Mod settings load before defs exist, so a Difficulty object (which saves a def
    // reference) cannot be stored here directly.
    public Dictionary<string, string>? difficultySettings;
    public string? anomalyPlaystyle;

    // Null when the player had not picked reload-anytime or commitment mode yet, so the
    // page keeps asking instead of choosing for them.
    public bool? permadeath;

    public void ExposeData()
    {
        Scribe_Values.Look(ref storyteller, "storyteller");
        Scribe_Values.Look(ref difficulty, "difficulty");
        Scribe_Collections.Look(ref difficultySettings, "difficultySettings", LookMode.Value, LookMode.Value);
        Scribe_Values.Look(ref anomalyPlaystyle, "anomalyPlaystyle");
        Scribe_Values.Look(ref permadeath, "permadeath");
    }
}

/// <summary>
/// World generation page choices saved with "Set as default", including map size and
/// starting season from its advanced settings dialog. The seed is left out on purpose.
/// </summary>
public class WorldDefaults : IExposable
{
    public float planetCoverage = 0.3f;
    public OverallRainfall rainfall = OverallRainfall.Normal;
    public OverallTemperature temperature = OverallTemperature.Normal;
    public OverallPopulation population = OverallPopulation.Normal;
    public LandmarkDensity landmarkDensity = LandmarkDensity.Normal;
    public float pollution = 0.05f;
    public int mapSize = 250;
    public Season startingSeason = Season.Undefined;

    // Selectable factions only, one entry per copy. Hidden factions are the game's to add.
    public List<string> factions = new List<string>();

    public void ExposeData()
    {
        Scribe_Values.Look(ref planetCoverage, "planetCoverage", 0.3f);
        Scribe_Values.Look(ref rainfall, "rainfall", OverallRainfall.Normal);
        Scribe_Values.Look(ref temperature, "temperature", OverallTemperature.Normal);
        Scribe_Values.Look(ref population, "population", OverallPopulation.Normal);
        Scribe_Values.Look(ref landmarkDensity, "landmarkDensity", LandmarkDensity.Normal);
        Scribe_Values.Look(ref pollution, "pollution", 0.05f);
        Scribe_Values.Look(ref mapSize, "mapSize", 250);
        Scribe_Values.Look(ref startingSeason, "startingSeason", Season.Undefined);
        Scribe_Collections.Look(ref factions, "factions", LookMode.Value);
        if (factions == null)
        {
            factions = new List<string>();
        }
    }
}

public static class NewGameDefaults
{
    public const float ButtonWidth = 160f;
    public const float ButtonHeight = 32f;

    // Difficulty has dozens of plain settings and no way to list them, so save every public
    // bool/int/float field by name. Unknown names from an older game version are skipped.
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
            if (!values.TryGetValue(field.Name, out string text))
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

        // The page compares against this to know what "changed" means, so the saved
        // default becomes the new starting point.
        initialFactions.Clear();
        initialFactions.AddRange(result);
    }
}

// Vanilla only picks a storyteller the first time this page opens and leaves the difficulty
// empty until the player chooses one, so an empty difficulty means a fresh page. Checking
// that keeps Back-then-Next from throwing away changes the player already made here.
[HarmonyPatch(typeof(Page_SelectStoryteller), nameof(Page_SelectStoryteller.PreOpen))]
public static class Patch_Page_SelectStoryteller_PreOpen
{
    public static void Postfix(ref StorytellerDef ___storyteller, ref DifficultyDef ___difficulty, ref Difficulty ___difficultyValues)
    {
        StorytellerDefaults? defaults = UiPlusMod.Settings.storytellerDefaults;
        if (!NewGameDefaults.Enabled || defaults == null || ___difficulty != null)
        {
            return;
        }

        StorytellerDef? storyteller = defaults.storyteller != null ? DefDatabase<StorytellerDef>.GetNamedSilentFail(defaults.storyteller) : null;
        if (storyteller != null && storyteller.listVisible)
        {
            ___storyteller = storyteller;
        }

        DifficultyDef? difficulty = defaults.difficulty != null ? DefDatabase<DifficultyDef>.GetNamedSilentFail(defaults.difficulty) : null;
        if (difficulty != null)
        {
            ___difficulty = difficulty;
            ___difficultyValues = new Difficulty(difficulty);
            if (defaults.difficultySettings != null)
            {
                NewGameDefaults.RestoreDifficulty(___difficultyValues, defaults.difficultySettings);
            }

            AnomalyPlaystyleDef? playstyle = defaults.anomalyPlaystyle != null
                ? DefDatabase<AnomalyPlaystyleDef>.GetNamedSilentFail(defaults.anomalyPlaystyle)
                : null;
            if (playstyle != null)
            {
                ___difficultyValues.AnomalyPlaystyleDef = playstyle;
            }

            if (ModsConfig.AnomalyActive && Find.Scenario != null && Find.Scenario.standardAnomalyPlaystyleOnly)
            {
                ___difficultyValues.AnomalyPlaystyleDef = AnomalyPlaystyleDefOf.Standard;
            }
        }

        if (defaults.permadeath.HasValue && Find.GameInitData != null)
        {
            Find.GameInitData.permadeathChosen = true;
            Find.GameInitData.permadeath = defaults.permadeath.Value;
        }
    }
}

[HarmonyPatch(typeof(Page_SelectStoryteller), nameof(Page_SelectStoryteller.DoWindowContents))]
public static class Patch_Page_SelectStoryteller_DoWindowContents
{
    public static void Postfix(Rect rect, StorytellerDef ___storyteller, DifficultyDef ___difficulty, Difficulty ___difficultyValues)
    {
        if (!NewGameDefaults.Enabled)
        {
            return;
        }

        Rect button = NewGameDefaults.ButtonRect(rect);
        TooltipHandler.TipRegion(button, "VUIP.SetStorytellerDefaultTip".Translate());
        if (!Widgets.ButtonText(button, "VUIP.SetAsDefault".Translate()))
        {
            return;
        }

        if (___storyteller == null || ___difficulty == null)
        {
            Messages.Message("VUIP.ChooseDifficultyFirst".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        GameInitData? init = Find.GameInitData;
        UiPlusMod.Settings.storytellerDefaults = new StorytellerDefaults
        {
            storyteller = ___storyteller.defName,
            difficulty = ___difficulty.defName,
            difficultySettings = ___difficultyValues != null ? NewGameDefaults.CaptureDifficulty(___difficultyValues) : null,
            anomalyPlaystyle = ___difficultyValues?.AnomalyPlaystyleDef?.defName,
            permadeath = init != null && init.permadeathChosen ? init.permadeath : (bool?)null
        };
        NewGameDefaults.Saved();
    }
}

// Applied only the first time the page opens, when vanilla resets it; Back-then-Next keeps
// the player's edits. Hooking Reset itself would also catch the page's "Reset all" button,
// which should still go back to the game's own values. The seed stays random.
[HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.PreOpen))]
public static class Patch_Page_CreateWorldParams_PreOpen
{
    public static void Prefix(bool ___initialized, ref bool __state)
    {
        __state = !___initialized;
    }

    public static void Postfix(
        bool __state,
        ref float ___planetCoverage,
        ref OverallRainfall ___rainfall,
        ref OverallTemperature ___temperature,
        ref OverallPopulation ___population,
        ref LandmarkDensity ___landmarkDensity,
        ref float ___pollution,
        List<FactionDef> ___factions,
        List<FactionDef> ___initialFactions)
    {
        WorldDefaults? defaults = UiPlusMod.Settings.worldDefaults;
        if (!__state || !NewGameDefaults.Enabled || defaults == null)
        {
            return;
        }

        ___planetCoverage = defaults.planetCoverage;
        ___rainfall = defaults.rainfall;
        ___temperature = defaults.temperature;
        ___population = defaults.population;
        if (ModsConfig.OdysseyActive)
        {
            ___landmarkDensity = defaults.landmarkDensity;
        }

        if (ModsConfig.BiotechActive)
        {
            ___pollution = defaults.pollution;
        }

        if (Find.GameInitData != null)
        {
            Find.GameInitData.mapSize = defaults.mapSize;
            Find.GameInitData.startingSeason = defaults.startingSeason;
        }

        if (___factions != null && ___initialFactions != null)
        {
            NewGameDefaults.ApplyFactions(___factions, ___initialFactions);
        }
    }
}

[HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.DoWindowContents))]
public static class Patch_Page_CreateWorldParams_DoWindowContents
{
    public static void Postfix(
        Rect rect,
        float ___planetCoverage,
        OverallRainfall ___rainfall,
        OverallTemperature ___temperature,
        OverallPopulation ___population,
        LandmarkDensity ___landmarkDensity,
        float ___pollution,
        List<FactionDef> ___factions)
    {
        if (!NewGameDefaults.Enabled)
        {
            return;
        }

        Rect button = NewGameDefaults.ButtonRect(rect);
        TooltipHandler.TipRegion(button, "VUIP.SetWorldDefaultTip".Translate());
        if (!Widgets.ButtonText(button, "VUIP.SetAsDefault".Translate()))
        {
            return;
        }

        GameInitData? init = Find.GameInitData;
        UiPlusMod.Settings.worldDefaults = new WorldDefaults
        {
            planetCoverage = ___planetCoverage,
            rainfall = ___rainfall,
            temperature = ___temperature,
            population = ___population,
            landmarkDensity = ___landmarkDensity,
            pollution = ___pollution,
            mapSize = init?.mapSize ?? 250,
            startingSeason = init?.startingSeason ?? Season.Undefined,
            factions = (___factions ?? new List<FactionDef>())
                .Where(f => f.displayInFactionSelection)
                .Select(f => f.defName)
                .ToList()
        };
        NewGameDefaults.Saved();
    }
}
