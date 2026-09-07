using RimWorld;
using Verse;

namespace VanillaUIPlus;

public class PawnColumnWorker_Leather : PawnColumnWorker_Text
{
    public override bool VisibleCurrently => UiPlusMod.Settings.showLeatherColumn && base.VisibleCurrently;

    protected override string GetTextFor(Pawn pawn)
    {
        return pawn.def.race?.leatherDef?.LabelCap ?? "-";
    }

    protected override string GetTip(Pawn pawn)
    {
        return pawn.def.race?.leatherDef?.LabelCap ?? base.GetTip(pawn);
    }
}
