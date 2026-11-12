using Verse;

namespace VanillaUIPlus;

// The default button on the Work tab; see PawnColumnWorker_DefaultButton.
public class PawnColumnWorker_DefaultWorkPriorities : PawnColumnWorker_DefaultButton
{
    protected override bool FeatureEnabled => UiPlusMod.Settings.applyDefaultWorkPriorities;

    protected override bool IsSet(bool slave) => DefaultWorkPriorities.IsSet(slave);

    protected override string TipKey => "VUIP.DefaultWorkPrioritiesTip";

    protected override string SetMessageKey => "VUIP.DefaultWorkPrioritiesSet";

    protected override string LoadedMessageKey => "VUIP.DefaultWorkPrioritiesLoaded";

    protected override bool CanUse(Pawn pawn) => DefaultWorkPriorities.CanReceiveDefaults(pawn);

    protected override bool IsDefault(Pawn pawn, bool slave) => DefaultWorkPriorities.Matches(pawn, slave);

    protected override void SetDefault(Pawn pawn, bool slave) => DefaultWorkPriorities.SetFrom(pawn, slave);

    protected override void LoadDefault(Pawn pawn, bool slave) => DefaultWorkPriorities.Load(pawn, slave);

    protected override void ClearDefault(bool slave) => DefaultWorkPriorities.Clear(slave);
}
