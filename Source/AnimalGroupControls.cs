using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

/// <summary>
/// Draws the Animals tab's species header cells that set a column's value for every animal of that species, collapsed ones included.
/// </summary>
[StaticConstructorOnStartup]
public static class AnimalGroupControls
{
    private static readonly MethodInfo? hasCheckboxMethod = AccessTools.Method(typeof(PawnColumnWorker_Checkbox), "HasCheckbox");
    private static readonly MethodInfo? getValueMethod = AccessTools.Method(typeof(PawnColumnWorker_Checkbox), "GetValue");
    private static readonly MethodInfo? setValueMethod = AccessTools.Method(typeof(PawnColumnWorker_Checkbox), "SetValue");

    private static readonly Texture2D[] careTextures =
    {
        ContentFinder<Texture2D>.Get("UI/Icons/Medical/NoCare"),
        ContentFinder<Texture2D>.Get("UI/Icons/Medical/NoMeds"),
        ThingDefOf.MedicineHerbal.uiIcon,
        ThingDefOf.MedicineIndustrial.uiIcon,
        ThingDefOf.MedicineUltratech.uiIcon,
    };

    private static readonly List<Pawn> eligible = new List<Pawn>();

    public static bool HasControl(PawnColumnDef column)
    {
        return column.Worker switch
        {
            PawnColumnWorker_Checkbox => hasCheckboxMethod != null && getValueMethod != null && setValueMethod != null,
            PawnColumnWorker_Trainable => column.trainable != null,
            PawnColumnWorker_Trainable_Special or PawnColumnWorker_Master or PawnColumnWorker_MedicalCare or PawnColumnWorker_AllowedArea => true,
            _ => false,
        };
    }

    public static void Draw(Rect rect, PawnColumnDef column, List<Pawn> pawns, PawnTable table)
    {
        switch (column.Worker)
        {
            case PawnColumnWorker_Checkbox worker:
                DrawCheckbox(rect, column, pawns, table, worker);
                break;
            case PawnColumnWorker_Trainable:
                DrawTrainable(rect, column, pawns);
                break;
            case PawnColumnWorker_Trainable_Special:
                DrawSpecialTrainable(rect, column, pawns);
                break;
            case PawnColumnWorker_Master:
                DrawMaster(rect, pawns);
                break;
            case PawnColumnWorker_MedicalCare:
                DrawMedicalCare(rect, pawns);
                break;
            case PawnColumnWorker_AllowedArea:
                DrawAllowedArea(rect, pawns);
                break;
        }
    }

    private static void DrawCheckbox(Rect rect, PawnColumnDef column, List<Pawn> pawns, PawnTable table, PawnColumnWorker_Checkbox worker)
    {
        eligible.Clear();
        foreach (Pawn pawn in pawns)
        {
            // Already-sterile animals can't take the operation, so they don't count towards the group's state.
            if (hasCheckboxMethod!.Invoke(worker, new object[] { pawn }) is true
                && !(worker is PawnColumnWorker_Sterilize && pawn.health.hediffSet.HasHediff(HediffDefOf.Sterilized)))
            {
                eligible.Add(pawn);
            }
        }

        bool? value = GroupCheckbox(rect, column, eligible, pawn => getValueMethod!.Invoke(worker, new object[] { pawn }) is true);
        if (value == null)
        {
            return;
        }

        foreach (Pawn pawn in eligible.ToList())
        {
            if (getValueMethod!.Invoke(worker, new object[] { pawn }) is true != value.Value)
            {
                setValueMethod!.Invoke(worker, new object[] { pawn, value.Value, table });
            }
        }
    }

    private static void DrawTrainable(Rect rect, PawnColumnDef column, List<Pawn> pawns)
    {
        TrainableDef trainable = column.trainable;
        eligible.Clear();
        foreach (Pawn pawn in pawns)
        {
            if (pawn.training != null && !pawn.training.HasLearned(trainable)
                && pawn.training.CanAssignToTrain(trainable, out bool visible).Accepted && visible)
            {
                eligible.Add(pawn);
            }
        }

        bool? value = GroupCheckbox(rect, column, eligible, pawn => pawn.training.GetWanted(trainable));
        if (value == null)
        {
            return;
        }

        foreach (Pawn pawn in eligible)
        {
            pawn.training.SetWantedRecursive(trainable, value.Value);
        }
    }

    // Matches the special training cell: one checkbox for all of the species' special trainables.
    private static void DrawSpecialTrainable(Rect rect, PawnColumnDef column, List<Pawn> pawns)
    {
        eligible.Clear();
        foreach (Pawn pawn in pawns)
        {
            if (pawn.training != null && !pawn.RaceProps.specialTrainables.NullOrEmpty()
                && pawn.RaceProps.specialTrainables.All(t => pawn.training.CanAssignToTrain(t, out _).Accepted))
            {
                eligible.Add(pawn);
            }
        }

        bool? value = GroupCheckbox(rect, column, eligible, pawn => pawn.RaceProps.specialTrainables.All(t => pawn.training.GetWanted(t)));
        if (value == null)
        {
            return;
        }

        foreach (Pawn pawn in eligible)
        {
            foreach (TrainableDef trainable in pawn.RaceProps.specialTrainables)
            {
                pawn.training.SetWantedRecursive(trainable, value.Value);
            }
        }
    }

    // Draws an on, off or mixed checkbox for the eligible animals; returns the value to give them all when clicked.
    private static bool? GroupCheckbox(Rect rect, PawnColumnDef column, List<Pawn> pawns, System.Func<Pawn, bool> getValue)
    {
        if (pawns.Count == 0)
        {
            return null;
        }

        int on = pawns.Count(getValue);
        MultiCheckboxState state = on == pawns.Count ? MultiCheckboxState.On : on == 0 ? MultiCheckboxState.Off : MultiCheckboxState.Partial;
        Rect box = new Rect(rect.x + (int)((rect.width - 24f) / 2f), rect.y + (int)((rect.height - 24f) / 2f), 24f, 24f);
        TooltipHandler.TipRegion(box, ApplyTip(column, pawns.Count));
        if (Widgets.CheckboxMulti(box, state) == state)
        {
            return null;
        }

        return state != MultiCheckboxState.On;
    }

    private static string ApplyTip(PawnColumnDef column, int count)
    {
        string label = column.headerTip.NullOrEmpty() ? column.LabelCap.ToString() : column.headerTip;
        return label + "\n\n" + "VUIP.AnimalGroupApplyTip".Translate(count);
    }

    private static void DrawMaster(Rect rect, List<Pawn> pawns)
    {
        eligible.Clear();
        eligible.AddRange(pawns.Where(p => p.RaceProps.Animal && p.Faction == Faction.OfPlayer && p.training.HasLearned(TrainableDefOf.Obedience)));
        if (eligible.Count == 0)
        {
            return;
        }

        Pawn? master = eligible[0].playerSettings.Master;
        string label = eligible.All(p => p.playerSettings.Master == master)
            ? master == null ? "(" + "NoneLower".Translate() + ")" : master.LabelShort
            : "VUIP.AnimalGroupMixed".Translate().ToString();
        Rect button = rect.ContractedBy(2f);
        TooltipHandler.TipRegion(button, "Master".Translate() + "\n\n" + "VUIP.AnimalGroupApplyTip".Translate(eligible.Count));
        if (!Widgets.ButtonText(button, label.Truncate(button.width)))
        {
            return;
        }

        List<Pawn> animals = eligible.ToList();
        List<FloatMenuOption> options = new List<FloatMenuOption>
        {
            new FloatMenuOption("(" + "NoneLower".Translate() + ")", () => animals.ForEach(p => p.playerSettings.Master = null)),
        };
        foreach (Pawn colonist in PawnsFinder.AllMaps_FreeColonistsSpawned)
        {
            List<Pawn> handled = animals.Where(p => TrainableUtility.CanBeMaster(colonist, p)).ToList();
            string text = colonist.LabelShort;
            if (handled.Count == 0)
            {
                int level = colonist.skills.GetSkill(SkillDefOf.Animals).Level;
                int needed = animals.Min(TrainableUtility.MinimumHandlingSkill);
                options.Add(new FloatMenuOption(text + " (" + "SkillTooLow".Translate(SkillDefOf.Animals.LabelCap, level, needed) + ")", null));
                continue;
            }

            if (handled.Count < animals.Count)
            {
                text += " (" + "VUIP.AnimalGroupMasterSome".Translate(handled.Count, animals.Count) + ")";
            }

            options.Add(new FloatMenuOption(text, () => handled.ForEach(p => p.playerSettings.Master = colonist)));
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void DrawMedicalCare(Rect rect, List<Pawn> pawns)
    {
        eligible.Clear();
        eligible.AddRange(pawns.Where(p => p.playerSettings != null));
        if (eligible.Count == 0)
        {
            return;
        }

        MedicalCareCategory care = eligible[0].playerSettings.medCare;
        bool mixed = eligible.Any(p => p.playerSettings.medCare != care);
        string tip = "VUIP.AnimalGroupApplyTip".Translate(eligible.Count);
        bool clicked = mixed
            ? Widgets.ButtonText(rect, "VUIP.AnimalGroupMixedShort".Translate())
            : Widgets.ButtonImage(rect, careTextures[(int)care]);
        TooltipHandler.TipRegion(rect, (mixed ? "VUIP.AnimalGroupMixed".Translate().ToString() : care.GetLabel().CapitalizeFirst()) + "\n\n" + tip);
        if (!clicked)
        {
            return;
        }

        List<Pawn> animals = eligible.ToList();
        List<FloatMenuOption> options = new List<FloatMenuOption>();
        for (int i = 0; i < careTextures.Length; i++)
        {
            MedicalCareCategory category = (MedicalCareCategory)i;
            options.Add(new FloatMenuOption(category.GetLabel().CapitalizeFirst(), () => animals.ForEach(p => p.playerSettings.medCare = category),
                careTextures[i], Color.white));
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    // Matches the area selectors in the cells below; clicking an area assigns it to every animal that can take one.
    private static void DrawAllowedArea(Rect rect, List<Pawn> pawns)
    {
        Map? map = Find.CurrentMap;
        eligible.Clear();
        eligible.AddRange(pawns.Where(p => p.Faction == Faction.OfPlayer && p.playerSettings != null && p.playerSettings.SupportsAllowedAreas));
        if (map == null || eligible.Count == 0)
        {
            return;
        }

        List<Area?> areas = new List<Area?> { null };
        areas.AddRange(map.areaManager.AllAreas.Where(a => a.AssignableAsAllowed()));
        float width = rect.width / areas.Count;
        Text.WordWrap = false;
        for (int i = 0; i < areas.Count; i++)
        {
            Area? area = areas[i];
            Rect selector = new Rect(rect.x + i * width, rect.y, width, rect.height).ContractedBy(1f);
            MouseoverSounds.DoRegion(selector);
            GUI.DrawTexture(selector, area != null ? area.ColorTexture : BaseContent.GreyTex);
            string label = AreaUtility.AreaAllowedLabel_Area(area);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(selector.x + 3f, selector.y, selector.width - 3f, selector.height), label);
            Text.Anchor = TextAnchor.UpperLeft;
            if (eligible.All(p => p.playerSettings.AreaRestrictionInPawnCurrentMap == area))
            {
                Widgets.DrawBox(selector, 2);
            }

            if (Mouse.IsOver(selector))
            {
                area?.MarkForDraw();
            }

            TooltipHandler.TipRegion(selector, label + "\n\n" + "VUIP.AnimalGroupApplyTip".Translate(eligible.Count));
            if (Widgets.ButtonInvisible(selector, doMouseoverSound: false))
            {
                foreach (Pawn pawn in eligible)
                {
                    pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;
                }

                SoundDefOf.Designate_DragStandard_Changed_NoCam.PlayOneShotOnCamera();
            }
        }

        Text.WordWrap = true;
    }
}
