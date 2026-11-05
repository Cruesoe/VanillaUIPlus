using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VanillaUIPlus;

/// <summary>
/// Puts on apparel from the pawn's inventory, taking as long as the base game's wear job including what it replaces.
/// </summary>
public class JobDriver_WearFromInventory : JobDriver
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
        Apparel apparel = Apparel;
        duration = (int)(apparel.GetStatValue(StatDefOf.EquipDelay) * 60f);
        List<Apparel> worn = pawn.apparel.WornApparel;
        for (int i = worn.Count - 1; i >= 0; i--)
        {
            if (!ApparelUtility.CanWearTogether(apparel.def, worn[i].def, pawn.RaceProps.body))
            {
                duration += (int)(worn[i].GetStatValue(StatDefOf.EquipDelay) * 60f);
            }
        }
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TargetIndex.A);
        this.FailOn(() => !pawn.inventory.innerContainer.Contains(Apparel) || GearActions.CannotWearFromInventory(pawn, Apparel) != null);
        yield return Toils_General.Wait(duration).PlaySustainerOrSound(Apparel.def.apparel.soundWear).WithProgressBarToilDelay(TargetIndex.A);
        yield return Toils_General.Do(() =>
        {
            if (!GearActions.WearFromInventory(pawn, Apparel, job.playerForced))
            {
                EndJobWith(JobCondition.Incompletable);
            }
        });
    }
}
