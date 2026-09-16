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

    public static bool Matches(Pawn pawn)
    {
        AssignDefaults? defaults = UiPlusMod.Settings.defaultAssignments;
        if (defaults == null || !CanReceiveDefaults(pawn))
        {
            return false;
        }

        AssignDefaults current = Capture(pawn);
        if (defaults.hostilityResponse != null
            && defaults.hostilityResponse != current.hostilityResponse
            && CanUse(pawn, defaults.hostilityResponse.Value))
        {
            return false;
        }

        if (defaults.medicalCare != current.medicalCare
            || !SamePolicy(defaults.apparelPolicy, current.apparelPolicy, Current.Game.outfitDatabase.AllOutfits)
            || !SamePolicy(defaults.foodPolicy, current.foodPolicy, Current.Game.foodRestrictionDatabase.AllFoodRestrictions)
            || !SamePolicy(defaults.drugPolicy, current.drugPolicy, Current.Game.drugPolicyDatabase.AllPolicies)
            || !SamePolicy(defaults.readingPolicy, current.readingPolicy, Current.Game.readingPolicyDatabase.AllReadingPolicies))
        {
            return false;
        }

        foreach (KeyValuePair<string, int> entry in current.stockCounts)
        {
            if (defaults.stockCounts.TryGetValue(entry.Key, out int count) && count != entry.Value)
            {
                return false;
            }

            if (defaults.stockThings.TryGetValue(entry.Key, out string thing)
                && current.stockThings.TryGetValue(entry.Key, out string currentThing)
                && thing != currentThing)
            {
                return false;
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

    public static void ApplyTo(Pawn pawn)
    {
        AssignDefaults? defaults = UiPlusMod.Settings.defaultAssignments;
        if (!UiPlusMod.Settings.applyDefaultAssignments || defaults == null || !CanReceiveDefaults(pawn))
        {
            return;
        }

        if (pawn.playerSettings != null)
        {
            if (defaults.hostilityResponse != null && CanUse(pawn, defaults.hostilityResponse.Value))
            {
                pawn.playerSettings.hostilityResponse = defaults.hostilityResponse.Value;
            }

            if (defaults.medicalCare != null)
            {
                pawn.playerSettings.medCare = defaults.medicalCare.Value;
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
            foreach (InventoryStockGroupDef group in DefDatabase<InventoryStockGroupDef>.AllDefsListForReading)
            {
                if (defaults.stockThings.TryGetValue(group.defName, out string thingName))
                {
                    ThingDef? thing = DefDatabase<ThingDef>.GetNamedSilentFail(thingName);
                    if (thing != null && group.thingDefs.Contains(thing))
                    {
                        pawn.inventoryStock.SetThingForGroup(group, thing);
                    }
                }

                if (defaults.stockCounts.TryGetValue(group.defName, out int count))
                {
                    pawn.inventoryStock.SetCountForGroup(group, count);
                }
            }
        }
    }

    private static AssignDefaults Capture(Pawn pawn)
    {
        AssignDefaults captured = new AssignDefaults
        {
            hostilityResponse = pawn.playerSettings?.hostilityResponse,
            medicalCare = pawn.playerSettings?.medCare,
            apparelPolicy = pawn.outfits?.CurrentApparelPolicy?.label,
            foodPolicy = pawn.foodRestriction?.CurrentFoodPolicy?.label,
            drugPolicy = pawn.drugs?.CurrentPolicy?.label,
            readingPolicy = pawn.reading?.CurrentPolicy?.label
        };

        // A pawn who cannot fight only offers ignore/flee, so their choice says nothing about
        // what fighters should do; leave hostility response out rather than force flee on all.
        if (pawn.WorkTagIsDisabled(WorkTags.Violent))
        {
            captured.hostilityResponse = null;
        }

        if (pawn.inventoryStock != null)
        {
            foreach (InventoryStockGroupDef group in DefDatabase<InventoryStockGroupDef>.AllDefsListForReading)
            {
                ThingDef? thing = pawn.inventoryStock.GetDesiredThingForGroup(group);
                if (thing != null)
                {
                    captured.stockThings[group.defName] = thing.defName;
                }

                captured.stockCounts[group.defName] = pawn.inventoryStock.GetDesiredCountForGroup(group);
            }
        }

        return captured;
    }

    private static bool CanUse(Pawn pawn, HostilityResponseMode mode)
    {
        return mode != HostilityResponseMode.Attack || !pawn.WorkTagIsDisabled(WorkTags.Violent);
    }

    // A saved name that this colony has no policy for cannot be applied, so it should not
    // leave the pin permanently empty either.
    private static bool SamePolicy<T>(string? saved, string? current, List<T> policies) where T : Policy
    {
        return saved == null || saved == current || FindPolicy(policies, saved) == null;
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

// Recruits, slaves and joiners. Changing faction resets medical care after the trackers are
// added, so the default is applied again once the switch has finished.
[HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
public static class Patch_Pawn_SetFaction_DefaultAssignments
{
    public static void Prefix(Pawn __instance, ref bool __state)
    {
        __state = DefaultSchedule.IsPlayerPawn(__instance);
    }

    public static void Postfix(Pawn __instance, bool __state)
    {
        if (!__state && DefaultSchedule.IsPlayerPawn(__instance))
        {
            DefaultAssignments.ApplyTo(__instance);
        }
    }
}

// Starting colonists only join the player faction directly, without SetFaction, once the
// map is being generated.
[HarmonyPatch(typeof(GameInitData), nameof(GameInitData.PrepForMapGen))]
public static class Patch_GameInitData_PrepForMapGen_DefaultAssignments
{
    public static void Postfix(GameInitData __instance)
    {
        foreach (Pawn pawn in __instance.startingAndOptionalPawns)
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
