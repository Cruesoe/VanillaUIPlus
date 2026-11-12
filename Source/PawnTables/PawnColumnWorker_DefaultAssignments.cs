using Verse;

namespace VanillaUIPlus;

// The default button on the Assign tab; see PawnColumnWorker_DefaultButton.
public class PawnColumnWorker_DefaultAssignments : PawnColumnWorker_DefaultButton
{
    protected override bool FeatureEnabled => UiPlusMod.Settings.applyDefaultAssignments;

    protected override bool IsSet => DefaultAssignments.IsSet;

    protected override string TipKey => "VUIP.DefaultAssignmentsTip";

    protected override string SetMessageKey => "VUIP.DefaultAssignmentsSet";

    protected override string LoadedMessageKey => "VUIP.DefaultAssignmentsLoaded";

    protected override bool CanUse(Pawn pawn) => DefaultAssignments.CanReceiveDefaults(pawn);

    protected override bool IsDefault(Pawn pawn) => DefaultAssignments.Matches(pawn);

    protected override void SetDefault(Pawn pawn) => DefaultAssignments.SetFrom(pawn);

    protected override void LoadDefault(Pawn pawn) => DefaultAssignments.ApplyTo(pawn);

    protected override void ClearDefault() => DefaultAssignments.Clear();
}
