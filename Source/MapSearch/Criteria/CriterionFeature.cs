using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>A tile feature; an animal habitat can also be narrowed to one animal.</summary>
public class CriterionFeature : CriterionDefSet<TileMutatorDef>
{
    private static List<PawnKindDef>? habitatAnimals;

    public string? animal;

    public CriterionFeature()
    {
    }

    public CriterionFeature(IEnumerable<TileMutatorDef> defs) : base(defs)
    {
    }

    protected override string Title => "VUIP.WorldQueryFeature".Translate();

    private bool IsHabitat => Defs.Any(d => d.Worker is TileMutatorWorker_AnimalHabitat);

    public override float ControlsHeight => IsHabitat ? ControlRowHeight : 0f;

    private static List<PawnKindDef> HabitatAnimals => habitatAnimals ??= DefDatabase<PawnKindDef>.AllDefs
        .Where(k => k.RaceProps is { Animal: true } && DefDatabase<BiomeDef>.AllDefs.Any(b => b.CommonalityOfAnimal(k) > 0f))
        .OrderBy(k => k.label)
        .ToList();

    public override void DrawControls(Rect rect, ref bool changed)
    {
        PawnKindDef? current = animal == null ? null : DefDatabase<PawnKindDef>.GetNamedSilentFail(animal);
        Rect button = new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 4f);
        string label = current?.LabelCap ?? "VUIP.WorldQueryAnyAnimal".Translate();
        if (!Widgets.ButtonText(button, label.Truncate(button.width - 12f)))
        {
            return;
        }

        List<FloatMenuOption> options = new List<FloatMenuOption>
        {
            new FloatMenuOption("VUIP.WorldQueryAnyAnimal".Translate(), () => SetAnimal(null))
        };
        foreach (PawnKindDef kind in HabitatAnimals)
        {
            string defName = kind.defName;
            options.Add(new FloatMenuOption(kind.LabelCap, () => SetAnimal(defName)));
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    private void SetAnimal(string? defName)
    {
        animal = defName;
        WorldQueryPanel.NotifyChanged();
    }

    public override bool Matches(Tile tile)
    {
        foreach (TileMutatorDef mutator in tile.Mutators)
        {
            if (!Defs.Contains(mutator))
            {
                continue;
            }

            if (animal == null || mutator.Worker is not TileMutatorWorker_AnimalHabitat habitat)
            {
                return true;
            }

            if (habitat.GetAnimalKind(tile.tile)?.defName == animal)
            {
                return true;
            }
        }

        return false;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref animal, "animal");
    }
}
