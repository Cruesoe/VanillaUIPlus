using System.Collections.Generic;
using RimWorld;
using UnityEngine;

namespace VanillaUIPlus;

/// <summary>Moves every hour one slot later; 23:00's assignment wraps around to midnight.</summary>
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
