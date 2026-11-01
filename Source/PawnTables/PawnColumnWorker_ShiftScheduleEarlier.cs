using System.Collections.Generic;
using RimWorld;
using UnityEngine;

namespace VanillaUIPlus;

/// <summary>Moves every hour one slot earlier; midnight's assignment wraps around to 23:00.</summary>
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
