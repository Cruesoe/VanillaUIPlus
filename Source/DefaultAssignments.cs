using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>Everything on the Assign tab as pinned from one pawn, saved with the mod settings.</summary>
public class AssignDefaults : IExposable
{
    public HostilityResponseMode? hostilityResponse;
    public MedicalCareCategory? medicalCare;

    // Matched by name; a colony without a policy of that name keeps its default.
    public string? apparelPolicy;
    public string? foodPolicy;
    public string? drugPolicy;
    public string? readingPolicy;

    // Every inventory stock group by defName, including groups other mods add.
    public Dictionary<string, string> stockThings = new Dictionary<string, string>();
    public Dictionary<string, int> stockCounts = new Dictionary<string, int>();

    public void ExposeData()
    {
        Scribe_Values.Look(ref hostilityResponse, "hostilityResponse");
        Scribe_Values.Look(ref medicalCare, "medicalCare");
        Scribe_Values.Look(ref apparelPolicy, "apparelPolicy");
        Scribe_Values.Look(ref foodPolicy, "foodPolicy");
        Scribe_Values.Look(ref drugPolicy, "drugPolicy");
        Scribe_Values.Look(ref readingPolicy, "readingPolicy");
        Scribe_Collections.Look(ref stockThings, "stockThings", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref stockCounts, "stockCounts", LookMode.Value, LookMode.Value);
        stockThings ??= new Dictionary<string, string>();
        stockCounts ??= new Dictionary<string, int>();
    }
}

public static class DefaultAssignments
{
    public static bool IsSet => UiPlusMod.Settings.defaultAssignments != null;

    // Saved stock things resolved to defs, for the defaults instance they were read from.
    private static readonly Dictionary<string, ThingDef?> ResolvedStockThings = new Dictionary<string, ThingDef?>();
    private static AssignDefaults? resolvedFor;

    public static bool CanReceiveDefaults(Pawn pawn)
    {
        return NewColonistDefaults.IsColonyHumanlike(pawn) && pawn.outfits != null && Current.Game != null;
    }

    // Runs for every Assign tab row each frame: reads the pawn directly and never creates stock entries.
    public static bool Matches(Pawn pawn)
    {
        AssignDefaults? defaults = UiPlusMod.Settings.defaultAssignments;
        if (defaults == null || !CanReceiveDefaults(pawn))
        {
            return false;
        }

        Pawn_PlayerSettings? settings = pawn.playerSettings;
        if (settings != null)
        {
            if (defaults.hostilityResponse is { } response
                && CanUse(pawn, response)
                && settings.hostilityResponse != response)
            {
                return false;
            }

            if (defaults.medicalCare is { } care && !pawn.IsSlave && settings.medCare != care)
            {
                return false;
            }
        }

        Game game = Current.Game;
        if (!SamePolicy(defaults.apparelPolicy, pawn.outfits.CurrentApparelPolicy, game.outfitDatabase.AllOutfits)
            || !SamePolicy(defaults.foodPolicy, pawn.foodRestriction?.CurrentFoodPolicy, game.foodRestrictionDatabase.AllFoodRestrictions)
            || !SamePolicy(defaults.drugPolicy, pawn.drugs?.CurrentPolicy, game.drugPolicyDatabase.AllPolicies)
            || !SamePolicy(defaults.readingPolicy, pawn.reading?.CurrentPolicy, game.readingPolicyDatabase.AllReadingPolicies))
        {
            return false;
        }

        if (pawn.inventoryStock != null)
        {
            foreach (InventoryStockGroupDef group in DefDatabase<InventoryStockGroupDef>.AllDefsListForReading)
            {
                ReadStock(pawn.inventoryStock, group, out ThingDef? thing, out int count);
                if (defaults.stockCounts.TryGetValue(group.defName, out int savedCount)
                    && ClampCount(group, savedCount) != count)
                {
                    return false;
                }

                if (defaults.stockThings.TryGetValue(group.defName, out string savedThing)
                    && thing?.defName != savedThing
                    && CachedStockThing(defaults, group, savedThing) != null)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static void SetFrom(Pawn pawn)
    {
        UiPlusMod.Settings.defaultAssignments = Capture(pawn);
        UiPlusMod.Instance.WriteSettings();
    }

    public static void Clear()
    {
        UiPlusMod.Settings.defaultAssignments = null;
        UiPlusMod.Instance.WriteSettings();
    }

    // Slaves keep the slave medical default; a pawn mid-enslavement isn't a slave yet, so the caller passes includeMedicalCare.
    public static void ApplyTo(Pawn pawn, bool includeMedicalCare = true)
    {
        AssignDefaults? defaults = UiPlusMod.Settings.defaultAssignments;
        if (!UiPlusMod.Settings.applyDefaultAssignments || defaults == null || !CanReceiveDefaults(pawn))
        {
            return;
        }

        if (pawn.playerSettings != null)
        {
            if (defaults.hostilityResponse is { } response && CanUse(pawn, response))
            {
                pawn.playerSettings.hostilityResponse = response;
            }

            if (defaults.medicalCare is { } care && includeMedicalCare && !pawn.IsSlave)
            {
                pawn.playerSettings.medCare = care;
            }
        }

        Game game = Current.Game;
        ApparelPolicy? apparel = FindPolicy(game.outfitDatabase.AllOutfits, defaults.apparelPolicy);
        if (apparel != null)
        {
            pawn.outfits.CurrentApparelPolicy = apparel;
        }

        FoodPolicy? food = FindPolicy(game.foodRestrictionDatabase.AllFoodRestrictions, defaults.foodPolicy);
        if (food != null && pawn.foodRestriction != null)
        {
            pawn.foodRestriction.CurrentFoodPolicy = food;
        }

        DrugPolicy? drugs = FindPolicy(game.drugPolicyDatabase.AllPolicies, defaults.drugPolicy);
        if (drugs != null && pawn.drugs != null)
        {
            pawn.drugs.CurrentPolicy = drugs;
        }

        ReadingPolicy? reading = FindPolicy(game.readingPolicyDatabase.AllReadingPolicies, defaults.readingPolicy);
        if (reading != null && pawn.reading != null)
        {
            pawn.reading.CurrentPolicy = reading;
        }

        if (pawn.inventoryStock != null)
        {
            // Only groups that differ get an entry.
            foreach (InventoryStockGroupDef group in DefDatabase<InventoryStockGroupDef>.AllDefsListForReading)
            {
                ReadStock(pawn.inventoryStock, group, out ThingDef? currentThing, out int currentCount);
                if (defaults.stockThings.TryGetValue(group.defName, out string thingName))
                {
                    ThingDef? thing = UsableStockThing(group, thingName);
                    if (thing != null && thing != currentThing)
                    {
                        pawn.inventoryStock.SetThingForGroup(group, thing);
                    }
                }

                if (defaults.stockCounts.TryGetValue(group.defName, out int count)
                    && ClampCount(group, count) != currentCount)
                {
                    pawn.inventoryStock.SetCountForGroup(group, count);
                }
            }
        }
    }

    private static AssignDefaults Capture(Pawn pawn)
    {
        Pawn_PlayerSettings? settings = pawn.playerSettings;
        AssignDefaults captured = new AssignDefaults
        {
            // Left out for a pawn who can't fight, whose only choices are ignore or flee.
            hostilityResponse = pawn.WorkTagIsDisabled(WorkTags.Violent) ? null : settings?.hostilityResponse,

            // A slave's medical care follows the game's slave default, not a colonist's.
            medicalCare = pawn.IsSlave ? null : settings?.medCare,

            apparelPolicy = pawn.outfits?.CurrentApparelPolicy?.label,
            foodPolicy = pawn.foodRestriction?.CurrentFoodPolicy?.label,
            drugPolicy = pawn.drugs?.CurrentPolicy?.label,
            readingPolicy = pawn.reading?.CurrentPolicy?.label
        };

        if (pawn.inventoryStock != null)
        {
            foreach (InventoryStockGroupDef group in DefDatabase<InventoryStockGroupDef>.AllDefsListForReading)
            {
                ReadStock(pawn.inventoryStock, group, out ThingDef? thing, out int count);
                if (thing != null)
                {
                    captured.stockThings[group.defName] = thing.defName;
                }

                captured.stockCounts[group.defName] = count;
            }
        }

        return captured;
    }

    // What the game reports for a group, without GetCurrentEntryFor storing a default entry on the pawn.
    private static void ReadStock(Pawn_InventoryStockTracker stock, InventoryStockGroupDef group, out ThingDef? thing, out int count)
    {
        if (stock.stockEntries.TryGetValue(group, out InventoryStockEntry entry))
        {
            thing = entry.thingDef;
            count = entry.count;
        }
        else
        {
            thing = group.DefaultThingDef;
            count = group.min;
        }
    }

    private static int ClampCount(InventoryStockGroupDef group, int count)
    {
        return count < group.min ? group.min : count > group.max ? group.max : count;
    }

    private static ThingDef? UsableStockThing(InventoryStockGroupDef group, string defName)
    {
        ThingDef? thing = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        return thing != null && group.thingDefs.Contains(thing) ? thing : null;
    }

    private static ThingDef? CachedStockThing(AssignDefaults defaults, InventoryStockGroupDef group, string defName)
    {
        if (!ReferenceEquals(resolvedFor, defaults))
        {
            resolvedFor = defaults;
            ResolvedStockThings.Clear();
        }

        if (!ResolvedStockThings.TryGetValue(group.defName, out ThingDef? thing))
        {
            thing = UsableStockThing(group, defName);
            ResolvedStockThings[group.defName] = thing;
        }

        return thing;
    }

    private static bool CanUse(Pawn pawn, HostilityResponseMode mode)
    {
        return mode != HostilityResponseMode.Attack || !pawn.WorkTagIsDisabled(WorkTags.Violent);
    }

    // A saved name this colony has no policy for can't apply, so it doesn't count as a mismatch.
    private static bool SamePolicy<T>(string? saved, Policy? current, List<T> policies) where T : Policy
    {
        return saved == null || current?.label == saved || FindPolicy(policies, saved) == null;
    }

    private static T? FindPolicy<T>(List<T> policies, string? label) where T : Policy
    {
        if (label == null)
        {
            return null;
        }

        for (int i = 0; i < policies.Count; i++)
        {
            if (policies[i].label == label)
            {
                return policies[i];
            }
        }

        return null;
    }
}

// Applies when a pawn's policy trackers are first created for the player; loading a save is unaffected.
[HarmonyPatch(typeof(PawnComponentsUtility), nameof(PawnComponentsUtility.AddAndRemoveDynamicComponents))]
public static class Patch_PawnComponentsUtility_AddAndRemoveDynamicComponents
{
    public static void Prefix(Pawn pawn, ref bool __state)
    {
        __state = pawn.outfits == null;
    }

    public static void Postfix(Pawn pawn, bool __state)
    {
        if (__state && pawn.outfits != null && Scribe.mode == LoadSaveMode.Inactive)
        {
            DefaultAssignments.ApplyTo(pawn);
        }
    }
}

// The pin on the Assign tab; see PawnColumnWorker_DefaultPin.
public class PawnColumnWorker_DefaultAssignments : PawnColumnWorker_DefaultPin
{
    protected override bool FeatureEnabled => UiPlusMod.Settings.applyDefaultAssignments;

    protected override string SetTipKey => "VUIP.SetDefaultAssignmentsTip";

    protected override string ClearTipKey => "VUIP.ClearDefaultAssignmentsTip";

    protected override string SetMessageKey => "VUIP.DefaultAssignmentsSet";

    protected override bool CanPin(Pawn pawn) => DefaultAssignments.CanReceiveDefaults(pawn);

    protected override bool IsDefault(Pawn pawn) => DefaultAssignments.Matches(pawn);

    protected override void SetDefault(Pawn pawn) => DefaultAssignments.SetFrom(pawn);

    protected override void ClearDefault() => DefaultAssignments.Clear();
}
