using Verse;

namespace VanillaUIPlus;

// The default button on the Work tab; see PawnColumnWorker_DefaultButton.
public class PawnColumnWorker_DefaultWorkPriorities : PawnColumnWorker_DefaultButton
{
    protected override bool FeatureEnabled => UiPlusMod.Settings.applyDefaultWorkPriorities;

    protected override bool IsSet => DefaultWorkPriorities.IsSet;

    protected override string TipKey => "VUIP.DefaultWorkPrioritiesTip";

    protected override string SetMessageKey => "VUIP.DefaultWorkPrioritiesSet";

    protected override string LoadedMessageKey => "VUIP.DefaultWorkPrioritiesLoaded";

    protected override bool CanUse(Pawn pawn) => DefaultWorkPriorities.CanReceiveDefaults(pawn);

    protected override bool IsDefault(Pawn pawn) => DefaultWorkPriorities.Matches(pawn);

    protected override void SetDefault(Pawn pawn) => DefaultWorkPriorities.SetFrom(pawn);

    protected override void LoadDefault(Pawn pawn) => DefaultWorkPriorities.ApplyTo(pawn);

    protected override void ClearDefault() => DefaultWorkPriorities.Clear();
}
