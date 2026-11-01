using Verse;

namespace VanillaUIPlus;

// The pin on the Schedule tab; see PawnColumnWorker_DefaultPin.
public class PawnColumnWorker_DefaultSchedule : PawnColumnWorker_DefaultPin
{
    protected override bool FeatureEnabled => UiPlusMod.Settings.applyDefaultSchedule;

    protected override string SetTipKey => "VUIP.SetDefaultScheduleTip";

    protected override string ClearTipKey => "VUIP.ClearDefaultScheduleTip";

    protected override string SetMessageKey => "VUIP.DefaultScheduleSet";

    protected override bool CanPin(Pawn pawn) => pawn.timetable != null;

    protected override bool IsDefault(Pawn pawn) => DefaultSchedule.Matches(pawn.timetable);

    protected override void SetDefault(Pawn pawn) => DefaultSchedule.SetFrom(pawn.timetable!);

    protected override void ClearDefault() => DefaultSchedule.Clear();
}
