using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Work priorities the player has pinned from the Work tab. Like the default schedule, they
/// are saved with the mod settings so they carry over to every colony, and they are copied
/// onto each pawn whose work settings are set up for the first time.
/// </summary>
public static class DefaultWorkPriorities
{
    // Keyed by defName so a default saved alongside a since-removed mod's work type still
    // loads; entries whose work type no longer exists are skipped when applied. Work types
    // the pinned pawn could not do are left out, so a pawn who is incapable of, say, art
    // does not switch art off for everyone who joins later.
    public static bool IsSet => UiPlusMod.Settings.defaultWorkPriorities is { Count: > 0 };

    public static bool Matches(Pawn pawn)
    {
        Dictionary<string, int>? priorities = UiPlusMod.Settings.defaultWorkPriorities;
        if (!IsSet || pawn.workSettings == null || !pawn.workSettings.EverWork)
        {
            return false;
        }

        bool comparedAny = false;
        foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
        {
            if (pawn.WorkTypeIsDisabled(workType) || !priorities!.TryGetValue(workType.defName, out int priority))
            {
                continue;
            }

            if (pawn.workSettings.GetPriority(workType) != priority)
            {
                return false;
            }

            comparedAny = true;
        }

        return comparedAny;
    }

    public static void SetFrom(Pawn pawn)
    {
        Dictionary<string, int> priorities = new Dictionary<string, int>();
        foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
        {
            if (!pawn.WorkTypeIsDisabled(workType))
            {
                priorities[workType.defName] = pawn.workSettings.GetPriority(workType);
            }
        }

        UiPlusMod.Settings.defaultWorkPriorities = priorities;
        UiPlusMod.Instance.WriteSettings();
    }

    public static void Clear()
    {
        UiPlusMod.Settings.defaultWorkPriorities = null;
        UiPlusMod.Instance.WriteSettings();
    }

    public static void ApplyTo(Pawn pawn)
    {
        if (!UiPlusMod.Settings.applyDefaultWorkPriorities
            || !IsSet
            || pawn.workSettings == null
            || !pawn.workSettings.EverWork
            || !CanReceiveDefaults(pawn))
        {
            return;
        }

        foreach (KeyValuePair<string, int> entry in UiPlusMod.Settings.defaultWorkPriorities!)
        {
            WorkTypeDef? workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(entry.Key);
            if (workType == null || entry.Value < 0 || pawn.WorkTypeIsDisabled(workType))
            {
                continue;
            }

            pawn.workSettings.SetPriority(workType, entry.Value);
        }
    }

    // Colony mechs also have work settings, but their work types come from their race and
    // a colonist's priorities make no sense for them. Only human-like members of the player
    // faction (colonists and slaves) get the default.
    public static bool CanReceiveDefaults(Pawn pawn)
    {
        return pawn.RaceProps.Humanlike
            && !pawn.IsMutant
            && pawn.Faction != null
            && pawn.Faction.IsPlayer;
    }
}

// Vanilla sets up a pawn's work settings exactly once, when it first becomes able to work
// for the colony: recruits, joiners, enslaved pawns, and children born in the colony.
[HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.EnableAndInitialize))]
public static class Patch_Pawn_WorkSettings_EnableAndInitialize
{
    public static void Postfix(Pawn ___pawn)
    {
        DefaultWorkPriorities.ApplyTo(___pawn);
    }
}

// Starting colonists are the exception: once the map is being generated, vanilla switches
// all of their work off and hands each job to whichever starting colonist is best at it,
// which would overwrite the default applied when they were created.
[HarmonyPatch(typeof(GameInitData), nameof(GameInitData.PrepForMapGen))]
public static class Patch_GameInitData_PrepForMapGen
{
    public static void Postfix(GameInitData __instance)
    {
        foreach (Pawn pawn in __instance.startingAndOptionalPawns)
        {
            DefaultWorkPriorities.ApplyTo(pawn);
        }
    }
}

// The pin on the Work tab; see PawnColumnWorker_DefaultPin.
public class PawnColumnWorker_DefaultWorkPriorities : PawnColumnWorker_DefaultPin
{
    protected override bool FeatureEnabled => UiPlusMod.Settings.applyDefaultWorkPriorities;

    protected override string SetTipKey => "VUIP.SetDefaultWorkPrioritiesTip";

    protected override string ClearTipKey => "VUIP.ClearDefaultWorkPrioritiesTip";

    protected override string SetMessageKey => "VUIP.DefaultWorkPrioritiesSet";

    protected override bool CanPin(Pawn pawn) =>
        pawn.workSettings != null && pawn.workSettings.EverWork && DefaultWorkPriorities.CanReceiveDefaults(pawn);

    protected override bool IsDefault(Pawn pawn) => DefaultWorkPriorities.Matches(pawn);

    protected override void SetDefault(Pawn pawn) => DefaultWorkPriorities.SetFrom(pawn);

    protected override void ClearDefault() => DefaultWorkPriorities.Clear();
}
