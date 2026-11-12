using Verse;

namespace VanillaUIPlus;

// The default button on the Schedule tab; see PawnColumnWorker_DefaultButton.
public class PawnColumnWorker_DefaultSchedule : PawnColumnWorker_DefaultButton
{
    protected override bool FeatureEnabled => UiPlusMod.Settings.applyDefaultSchedule;

    protected override bool IsSet => DefaultSchedule.IsSet;

    protected override string TipKey => "VUIP.DefaultScheduleTip";

    protected override string SetMessageKey => "VUIP.DefaultScheduleSet";

    protected override string LoadedMessageKey => "VUIP.DefaultScheduleLoaded";

    protected override bool CanUse(Pawn pawn) => pawn.timetable != null && NewColonistDefaults.IsColonyHumanlike(pawn);

    protected override bool IsDefault(Pawn pawn) => DefaultSchedule.Matches(pawn.timetable);

    protected override void SetDefault(Pawn pawn) => DefaultSchedule.SetFrom(pawn.timetable!);

    protected override void LoadDefault(Pawn pawn) => DefaultSchedule.ApplyTo(pawn);

    protected override void ClearDefault() => DefaultSchedule.Clear();
}
