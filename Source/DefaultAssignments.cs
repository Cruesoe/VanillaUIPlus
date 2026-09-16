using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Everything on the Assign tab, as pinned from one pawn. Saved with the mod settings like the
/// default schedule, so it carries over to every colony.
/// </summary>
public class AssignDefaults : IExposable
{
    public HostilityResponseMode? hostilityResponse;
    public MedicalCareCategory? medicalCare;

    // Policies belong to a save, so they are matched by name. A colony without a policy of
    // that name keeps the game's default policy for that slot.
    public string? apparelPolicy;
    public string? foodPolicy;
    public string? drugPolicy;
    public string? readingPolicy;

    // Every inventory stock group, keyed by defName: vanilla's medicine (the Carry column)
    // and any mod that adds its own group, such as Progression: Ammunition's ammo column.
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

    // Same rule as work priorities: colonists and slaves, not colony mechs or ghouls, whose
    // policies the game disables.
    public static bool CanReceiveDefaults(Pawn pawn)
    {
        return pawn.RaceProps.Humanlike
            && !pawn.IsMutant
            && pawn.Faction != null
            && pawn.Faction.IsPlayer
            && pawn.outfits != null
            && Current.Game != null;
    }

    // Runs for every row of the Assign tab each frame, so it reads the pawn directly
    // instead of building a snapshot, and never creates inventory stock entries.
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
                    && UsableStockThing(group, savedThing) != null)
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

    // Slaves keep the game's separate slave medical care default. A pawn being enslaved is
    // not a slave yet when it joins the faction, so the caller says so instead.
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
            // Only groups that differ get an entry, so a pawn is not given stored entries
            // that just repeat the game's own defaults.
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
            // A pawn who cannot fight only offers ignore/flee, so their choice says nothing
            // about what fighters should do; leave it out rather than force flee on all.
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

    // What the game would report for a group, without GetCurrentEntryFor's side effect of
    // storing a default entry on the pawn.
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

    private static bool CanUse(Pawn pawn, HostilityResponseMode mode)
    {
        return mode != HostilityResponseMode.Attack || !pawn.WorkTagIsDisabled(WorkTags.Violent);
    }

    // A saved name that this colony has no policy for cannot be applied, so it should not
    // leave the pin permanently empty either.
    private static bool SamePolicy<T>(string? saved, Policy? current, List<T> policies) where T : Policy
    {
        return saved == null || current?.label == saved || FindPolicy(policies, saved) == null;
    }

    private static T? FindPolicy<T>(List<T> policies, string? label) where T : Policy
    {
        return label == null ? null : policies.FirstOrDefault(policy => policy.label == label);
    }
}

// The game adds a pawn's apparel, food, drug, reading and inventory trackers the first time
// it belongs to the player: starting colonists, colony births, and pawns generated straight
// into the colony. Trackers read back from a save are not new, so loading is unaffected.
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
