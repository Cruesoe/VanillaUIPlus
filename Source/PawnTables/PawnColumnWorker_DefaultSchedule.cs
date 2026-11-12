using Verse;

namespace VanillaUIPlus;

// The default button on the Schedule tab; see PawnColumnWorker_DefaultButton.
public class PawnColumnWorker_DefaultSchedule : PawnColumnWorker_DefaultButton
{
    protected override bool FeatureEnabled => UiPlusMod.Settings.applyDefaultSchedule;

    protected override bool IsSet(bool slave) => DefaultSchedule.IsSet(slave);

    protected override string TipKey => "VUIP.DefaultScheduleTip";

    protected override string SetMessageKey => "VUIP.DefaultScheduleSet";

    protected override string LoadedMessageKey => "VUIP.DefaultScheduleLoaded";

    protected override bool CanUse(Pawn pawn) => pawn.timetable != null && NewColonistDefaults.IsColonyHumanlike(pawn);

    protected override bool IsDefault(Pawn pawn, bool slave) => DefaultSchedule.Matches(pawn.timetable, slave);

    protected override void SetDefault(Pawn pawn, bool slave) => DefaultSchedule.SetFrom(pawn.timetable!, slave);

    protected override void LoadDefault(Pawn pawn, bool slave) => DefaultSchedule.Load(pawn, slave);

    protected override void ClearDefault(bool slave) => DefaultSchedule.Clear(slave);
}
