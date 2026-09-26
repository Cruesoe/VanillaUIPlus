using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Work priorities pinned from the Work tab, saved with the mod settings and applied when a pawn's work settings are first set up.
/// </summary>
public static class DefaultWorkPriorities
{
    // Keyed by defName; work types that no longer exist or the pinned pawn couldn't do are skipped.
    public static bool IsSet => UiPlusMod.Settings.defaultWorkPriorities is { Count: > 0 };

    public static bool CanReceiveDefaults(Pawn pawn)
    {
        return pawn.workSettings != null && pawn.workSettings.EverWork && NewColonistDefaults.IsColonyHumanlike(pawn);
    }

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
        if (!UiPlusMod.Settings.applyDefaultWorkPriorities || !IsSet || !CanReceiveDefaults(pawn))
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
}

// Runs once, when a pawn first becomes able to work for the colony.
[HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.EnableAndInitialize))]
public static class Patch_Pawn_WorkSettings_EnableAndInitialize
{
    public static void Postfix(Pawn ___pawn)
    {
        DefaultWorkPriorities.ApplyTo(___pawn);
    }
}

// The pin on the Work tab; see PawnColumnWorker_DefaultPin.
public class PawnColumnWorker_DefaultWorkPriorities : PawnColumnWorker_DefaultPin
{
    protected override bool FeatureEnabled => UiPlusMod.Settings.applyDefaultWorkPriorities;

    protected override string SetTipKey => "VUIP.SetDefaultWorkPrioritiesTip";

    protected override string ClearTipKey => "VUIP.ClearDefaultWorkPrioritiesTip";

    protected override string SetMessageKey => "VUIP.DefaultWorkPrioritiesSet";

    protected override bool CanPin(Pawn pawn) => DefaultWorkPriorities.CanReceiveDefaults(pawn);

    protected override bool IsDefault(Pawn pawn) => DefaultWorkPriorities.Matches(pawn);

    protected override void SetDefault(Pawn pawn) => DefaultWorkPriorities.SetFrom(pawn);

    protected override void ClearDefault() => DefaultWorkPriorities.Clear();
}
