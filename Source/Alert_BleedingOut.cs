using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

public class Alert_BleedingOut : Alert_Critical
{
    private const int UrgentBleedTicks = 45000;

    private readonly List<Pawn> bleedingPawns = new List<Pawn>();
    private readonly StringBuilder explanation = new StringBuilder();

    public Alert_BleedingOut()
    {
        defaultLabel = "VUIP.BleedingOut".Translate();
    }

    protected override bool DoMessage => false;

    private List<Pawn> BleedingPawns
    {
        get
        {
            bleedingPawns.Clear();
            foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_AliveSpawned_FreeColonistsAndPrisoners_NoCryptosleep)
            {
                if (IsBleedingOut(pawn))
                {
                    bleedingPawns.Add(pawn);
                }
            }

            return bleedingPawns;
        }
    }

    public override TaggedString GetExplanation()
    {
        explanation.Length = 0;
        foreach (Pawn pawn in bleedingPawns)
        {
            int ticks = HealthUtility.TicksUntilDeathDueToBloodLoss(pawn);
            explanation.Append("  - ");
            explanation.Append(pawn.NameShortColored.Resolve());
            if (ticks > 0 && ticks < int.MaxValue)
            {
                explanation.Append(" (");
                explanation.Append(ticks.ToStringTicksToPeriod());
                explanation.Append(")");
            }

            explanation.AppendLine();
        }

        return "VUIP.BleedingOutDesc".Translate(explanation.ToString().TrimEndNewlines());
    }

    public override AlertReport GetReport()
    {
        if (!UiPlusMod.Enabled || !UiPlusMod.Settings.showBleedingOutAlert)
        {
            return false;
        }

        return AlertReport.CulpritsAre(BleedingPawns);
    }

    private static bool IsBleedingOut(Pawn pawn)
    {
        if (pawn.Dead || pawn.health?.hediffSet == null)
        {
            return false;
        }

        if (pawn.health.hediffSet.BleedRateTotal < 0.001f)
        {
            return false;
        }

        return HealthUtility.TicksUntilDeathDueToBloodLoss(pawn) < UrgentBleedTicks;
    }
}
