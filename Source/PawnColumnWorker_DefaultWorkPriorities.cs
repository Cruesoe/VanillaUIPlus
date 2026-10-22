using Verse;

namespace VanillaUIPlus;

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
