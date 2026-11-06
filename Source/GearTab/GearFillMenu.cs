using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VanillaUIPlus;

/// <summary>
/// The menu an empty slot opens: apparel that fits it, from the pawn's inventory and the map, best first by the pawn's own apparel score.
/// </summary>
public static class GearFillMenu
{
    private const int MaxMapOptions = 30;

    private struct Candidate
    {
        public Apparel Apparel;
        public bool FromInventory;
        public string? Reason;
        public float Score;
    }

    public static void Open(Pawn pawn, GearSlot slot)
    {
        List<float> wornScores = new List<float>();
        List<Apparel> worn = pawn.apparel.WornApparel;
        for (int i = 0; i < worn.Count; i++)
        {
            wornScores.Add(JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, worn[i]));
        }

        List<Candidate> inventory = new List<Candidate>();
        ThingOwner<Thing> held = pawn.inventory.innerContainer;
        for (int i = 0; i < held.Count; i++)
        {
            if (held[i] is Apparel apparel && slot.Fits(apparel.def))
            {
                inventory.Add(new Candidate
                {
                    Apparel = apparel,
                    FromInventory = true,
                    Reason = GearActions.CannotWearFromInventory(pawn, apparel),
                    Score = JobGiver_OptimizeApparel.ApparelScoreGain(pawn, apparel, wornScores)
                });
            }
        }

        List<Candidate> map = new List<Candidate>();
        if (pawn.Spawned)
        {
            List<Thing> things = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Apparel apparel && apparel.Spawned && slot.Fits(apparel.def) && !apparel.IsForbidden(pawn))
                {
                    map.Add(new Candidate
                    {
                        Apparel = apparel,
                        FromInventory = false,
                        Reason = CannotWearFromMap(pawn, apparel),
                        Score = JobGiver_OptimizeApparel.ApparelScoreGain(pawn, apparel, wornScores)
                    });
                }
            }
        }

        inventory.Sort(ByScore);
        map.Sort(ByScore);
        List<FloatMenuOption> options = new List<FloatMenuOption>();
        foreach (Candidate candidate in inventory)
        {
            options.Add(Option(pawn, candidate));
        }

        for (int i = 0; i < map.Count && i < MaxMapOptions; i++)
        {
            options.Add(Option(pawn, map[i]));
        }

        if (options.Count == 0)
        {
            options.Add(new FloatMenuOption("VUIP.GearFillNothing".Translate(slot.Label), null));
        }

        Find.WindowStack.Add(new FloatMenu(options, "VUIP.GearFillTitle".Translate(slot.Label)));
    }

    // Wearable items first, then by score.
    private static int ByScore(Candidate a, Candidate b)
    {
        bool aOk = a.Reason == null;
        bool bOk = b.Reason == null;
        if (aOk != bOk)
        {
            return aOk ? -1 : 1;
        }

        return b.Score.CompareTo(a.Score);
    }

    // The checks the base game's right-click Wear option makes (FloatMenuOptionProvider_Wear).
    private static string? CannotWearFromMap(Pawn pawn, Apparel apparel)
    {
        if (!pawn.CanReach(apparel, PathEndMode.ClosestTouch, Danger.Deadly))
        {
            return "NoPath".Translate().CapitalizeFirst();
        }

        if (apparel.IsBurning())
        {
            return "Burning".Translate();
        }

        if (pawn.apparel.WouldReplaceLockedApparel(apparel))
        {
            return "WouldReplaceLockedApparel".Translate().CapitalizeFirst();
        }

        if (pawn.IsMutant && pawn.mutant.Def.disableApparel)
        {
            return pawn.mutant.Def.LabelCap;
        }

        if (!ApparelUtility.HasPartsToWear(pawn, apparel.def))
        {
            return "CannotWearBecauseOfMissingBodyParts".Translate().CapitalizeFirst();
        }

        return EquipmentUtility.CanEquip(apparel, pawn, out string reason) ? null : reason.CapitalizeFirst();
    }

    private static FloatMenuOption Option(Pawn pawn, Candidate candidate)
    {
        Apparel apparel = candidate.Apparel;
        string label = apparel.LabelCap;
        if (candidate.FromInventory)
        {
            label = "VUIP.GearFillFromInventory".Translate(label);
        }

        Apparel? replaced = ApparelUtility.GetApparelReplacedByNewApparel(pawn, apparel);
        if (replaced != null)
        {
            label += " " + "VUIP.GearFillReplaces".Translate(replaced.LabelShort);
        }

        if (candidate.Reason != null)
        {
            return new FloatMenuOption(label + ": " + candidate.Reason, null, apparel, Color.white);
        }

        return new FloatMenuOption(label, () =>
        {
            if (candidate.FromInventory)
            {
                GearActions.OrderWearFromInventory(pawn, apparel);
            }
            else
            {
                GearActions.OrderWearFromMap(pawn, apparel);
            }
        }, apparel, Color.white);
    }
}
