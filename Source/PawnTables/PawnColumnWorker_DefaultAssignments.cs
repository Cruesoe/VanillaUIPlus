using Verse;

namespace VanillaUIPlus;

// The default button on the Assign tab; see PawnColumnWorker_DefaultButton.
public class PawnColumnWorker_DefaultAssignments : PawnColumnWorker_DefaultButton
{
    protected override bool FeatureEnabled => UiPlusMod.Settings.applyDefaultAssignments;

    protected override bool IsSet(bool slave) => DefaultAssignments.IsSet(slave);

    protected override string TipKey => "VUIP.DefaultAssignmentsTip";

    protected override string SetMessageKey => "VUIP.DefaultAssignmentsSet";

    protected override string LoadedMessageKey => "VUIP.DefaultAssignmentsLoaded";

    protected override bool CanUse(Pawn pawn) => DefaultAssignments.CanReceiveDefaults(pawn);

    protected override bool IsDefault(Pawn pawn, bool slave) => DefaultAssignments.Matches(pawn, slave);

    protected override void SetDefault(Pawn pawn, bool slave) => DefaultAssignments.SetFrom(pawn, slave);

    protected override void LoadDefault(Pawn pawn, bool slave) => DefaultAssignments.Load(pawn, slave);

    protected override void ClearDefault(bool slave) => DefaultAssignments.Clear(slave);
}
