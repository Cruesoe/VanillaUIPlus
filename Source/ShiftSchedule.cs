using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

/// <summary>
/// Arrows beside the Schedule tab's timetable that rotate a pawn's day one hour; the header moves everyone. Based on Orion's Shift Schedule (MIT).
/// </summary>
public abstract class PawnColumnWorker_ShiftSchedule : PawnColumnWorker
{
    private const float IconSize = 22f;

    // Loaded on first draw, on the main thread, like the default pin's textures.
    private static Texture2D? arrowLeftTex;
    private static Texture2D? arrowRightTex;

    private string? cellTip;

    protected static Texture2D ArrowLeftTex => arrowLeftTex ??= ContentFinder<Texture2D>.Get("UI/Widgets/ArrowLeft");
    protected static Texture2D ArrowRightTex => arrowRightTex ??= ContentFinder<Texture2D>.Get("UI/Widgets/ArrowRight");

    protected abstract Texture2D Icon { get; }

    protected abstract string CellTipKey { get; }

    protected abstract string HeaderTipKey { get; }

    protected abstract void Rotate(List<TimeAssignmentDef> times);

    public override bool VisibleCurrently =>
        UiPlusMod.Settings.showShiftScheduleArrows && base.VisibleCurrently;

    public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
    {
        if (pawn.timetable == null)
        {
            return;
        }

        Rect button = new Rect(
            rect.x + (rect.width - IconSize) / 2f,
            rect.y + (rect.height - IconSize) / 2f,
            IconSize,
            IconSize);
        if (Widgets.ButtonImage(button, Icon, tooltip: cellTip ??= CellTipKey.Translate()))
        {
            Shift(pawn);
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }
    }

    protected override void HeaderClicked(Rect headerRect, PawnTable table)
    {
        if (Event.current.button != 0)
        {
            return;
        }

        foreach (Pawn pawn in table.PawnsListForReading)
        {
            Shift(pawn);
        }

        SoundDefOf.Tick_High.PlayOneShotOnCamera();
    }

    protected override string GetHeaderTip(PawnTable table)
    {
        return HeaderTipKey.Translate();
    }

    public override int GetMinWidth(PawnTable table)
    {
        return Mathf.Max(base.GetMinWidth(table), Mathf.CeilToInt(IconSize) + 4);
    }

    public override int GetMaxWidth(PawnTable table)
    {
        return Mathf.Min(base.GetMaxWidth(table), GetMinWidth(table));
    }

    private void Shift(Pawn pawn)
    {
        List<TimeAssignmentDef>? times = pawn.timetable?.times;
        if (times != null && times.Count > 1)
        {
            Rotate(times);
        }
    }
}

/// <summary>Moves every hour one slot earlier; midnight's assignment wraps round to 23:00.</summary>
public class PawnColumnWorker_ShiftScheduleEarlier : PawnColumnWorker_ShiftSchedule
{
    protected override Texture2D Icon => ArrowLeftTex;

    protected override string CellTipKey => "VUIP.ShiftScheduleEarlierTip";

    protected override string HeaderTipKey => "VUIP.ShiftAllSchedulesEarlierTip";

    protected override void Rotate(List<TimeAssignmentDef> times)
    {
        TimeAssignmentDef first = times[0];
        times.RemoveAt(0);
        times.Add(first);
    }
}

/// <summary>Moves every hour one slot later; 23:00's assignment wraps round to midnight.</summary>
public class PawnColumnWorker_ShiftScheduleLater : PawnColumnWorker_ShiftSchedule
{
    protected override Texture2D Icon => ArrowRightTex;

    protected override string CellTipKey => "VUIP.ShiftScheduleLaterTip";

    protected override string HeaderTipKey => "VUIP.ShiftAllSchedulesLaterTip";

    protected override void Rotate(List<TimeAssignmentDef> times)
    {
        int last = times.Count - 1;
        TimeAssignmentDef final = times[last];
        times.RemoveAt(last);
        times.Insert(0, final);
    }
}
