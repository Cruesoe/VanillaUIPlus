using Verse;

namespace VanillaUIPlus;

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
