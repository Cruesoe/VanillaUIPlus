using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public class PawnColumnWorker_Leather : PawnColumnWorker_Text
{
    private const int LabelPadding = 8;

    public override bool VisibleCurrently => UiPlusMod.Settings.showLeatherColumn && base.VisibleCurrently;

    protected override string GetTextFor(Pawn pawn)
    {
        return pawn.def.race?.leatherDef?.LabelCap ?? "-";
    }

    // The cell doesn't wrap, so the tooltip shows a long leather name in full.
    protected override string? GetTip(Pawn pawn)
    {
        return pawn.def.race?.leatherDef?.LabelCap.ToString();
    }

    // Wide enough for the longest leather name in the table, collapsed species included; measured when the table recaches.
    public override int GetMinWidth(PawnTable table)
    {
        int width = base.GetMinWidth(table);
        GameFont font = Text.Font;
        Text.Font = GameFont.Small;
        if (AnimalGroups.Get(table) is { } groups)
        {
            foreach (AnimalGroups.AnimalGroup group in groups)
            {
                width = Mathf.Max(width, LabelWidth(group.Pawns));
            }
        }
        else
        {
            width = Mathf.Max(width, LabelWidth(table.PawnsListForReading));
        }

        Text.Font = font;
        return width;
    }

    private int LabelWidth(List<Pawn> pawns)
    {
        int width = 0;
        ThingDef? last = null;
        foreach (Pawn pawn in pawns)
        {
            if (pawn.def != last)
            {
                last = pawn.def;
                width = Mathf.Max(width, Mathf.CeilToInt(Text.CalcSize(GetTextFor(pawn)).x) + LabelPadding);
            }
        }

        return width;
    }
}
