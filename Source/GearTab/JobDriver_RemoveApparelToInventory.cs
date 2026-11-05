using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaUIPlus;

/// <summary>
/// Takes worn apparel off into the pawn's inventory, taking as long as the base game's remove apparel job.
/// </summary>
public class JobDriver_RemoveApparelToInventory : JobDriver
{
    private int duration;

    private Apparel Apparel => (Apparel)job.GetTarget(TargetIndex.A).Thing;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref duration, "duration");
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return true;
    }

    public override void Notify_Starting()
    {
        base.Notify_Starting();
        duration = (int)(Apparel.GetStatValue(StatDefOf.EquipDelay) * 60f);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TargetIndex.A);
        this.FailOn(() => !pawn.apparel.WornApparel.Contains(Apparel) || GearActions.CannotRemoveToInventory(pawn, Apparel) != null);
        yield return Toils_General.Wait(duration).PlaySustainerOrSound(Apparel.def.apparel.soundRemove).WithProgressBarToilDelay(TargetIndex.A);
        yield return Toils_General.Do(() =>
        {
            if (!GearActions.MoveWornToInventory(pawn, Apparel))
            {
                EndJobWith(JobCondition.Incompletable);
            }
        });
    }
}
