using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// The Gear tab's summary, slots and row warnings for one pawn. Rebuilt when the pawn or its gear changes, and once a second.
/// </summary>
public sealed class GearSnapshot
{
    private const int RefreshTicks = 60;
    private const float RefreshSeconds = 1f;

    // Lifetime: the selected pawn; cleared when the game is unloaded.
    private static GearSnapshot? current;
    private static readonly List<Thought> TmpThoughts = new List<Thought>();

    public readonly Pawn Pawn;
    private readonly int tick;
    private readonly float realTime;
    private readonly int fingerprint;

    public readonly bool ShowLoad;
    public readonly float LoadFraction;
    public readonly string LoadText = string.Empty;
    public readonly string LoadTip = string.Empty;

    public readonly bool ShowTemperature;
    public readonly bool TemperatureOutside;
    public readonly string TemperatureText = string.Empty;
    public readonly string TemperatureTip = string.Empty;

    public readonly bool ShowArmor;
    public readonly string[] ArmorLabels = new string[3];
    public readonly string[] ArmorTexts = new string[3];
    public readonly string[] ArmorTips = new string[3];

    public readonly bool ShowWeapon;
    public readonly string WeaponText = string.Empty;
    public readonly string? WeaponTip;

    public readonly List<GearSlotState> Slots = new List<GearSlotState>();
    private readonly Dictionary<Thing, string> warnings = new Dictionary<Thing, string>();
    private readonly Dictionary<Thing, GearRowAction> actions = new Dictionary<Thing, GearRowAction>();

    // Whether the player can give this pawn orders from the tab (the base game's CanControlColonist).
    public readonly bool CanAct;

    public bool ShowEquipment { get; private set; }
    public bool ShowApparel { get; private set; }
    public bool ShowInventory { get; private set; }
    public readonly List<Thing> EquipmentRows = new List<Thing>();
    public readonly List<Thing> UtilityRows = new List<Thing>();
    public readonly List<Thing> ApparelRows = new List<Thing>();
    public readonly List<Thing> InventoryRows = new List<Thing>();

    public readonly string EquipmentHeader = "Equipment".Translate();
    public readonly string UtilityHeader = "VUIP.GearUtility".Translate();
    public readonly string ApparelHeader = "Apparel".Translate();
    public readonly string InventoryHeader = "Inventory".Translate();
    public readonly string ForceTip = "VUIP.GearForceTip".Translate();

    private GearSnapshot(ITab_Pawn_Gear tab, Pawn pawn, int tick, int fingerprint)
    {
        Pawn = pawn;
        this.tick = tick;
        this.fingerprint = fingerprint;
        realTime = Time.realtimeSinceStartup;
        CanAct = !pawn.Dead && GearTabAccess.CanControlColonist(tab);
        BuildRows(tab, pawn);

        if (!pawn.Dead && GearTabAccess.ShouldShowInventory(tab, pawn))
        {
            ShowLoad = true;
            float mass = MassUtility.GearAndInventoryMass(pawn);
            StringBuilder capacityExplanation = new StringBuilder();
            float capacity = MassUtility.Capacity(pawn, capacityExplanation);
            LoadFraction = capacity > 0f ? mass / capacity : 0f;
            LoadText = "VUIP.GearLoadValue".Translate(mass.ToString("0.#"), capacity.ToString("0.#"));
            LoadTip = "MassCarried".Translate(mass.ToString("0.##"), capacity.ToString("0.##")).Resolve().AsTipTitle() + "\n\n" + capacityExplanation.ToString().TrimEnd();
        }

        if (!pawn.Dead)
        {
            ShowTemperature = true;
            float min = pawn.GetStatValue(StatDefOf.ComfyTemperatureMin);
            float max = pawn.GetStatValue(StatDefOf.ComfyTemperatureMax);
            TemperatureText = min.ToStringTemperature("F0") + " ~ " + max.ToStringTemperature("F0");
            TemperatureTip = ("ComfyTemperatureRange".Translate() + ": " + TemperatureText).AsTipTitle();
            Map? map = pawn.MapHeld;
            if (map != null)
            {
                float outdoor = map.mapTemperature.OutdoorTemp;
                TemperatureOutside = outdoor < min || outdoor > max;
                TemperatureTip += "\n\n" + "VUIP.GearOutdoorTemperature".Translate(outdoor.ToStringTemperature("F0"));
            }
        }

        if (GearTabAccess.ShouldShowOverallArmor(tab, pawn))
        {
            ShowArmor = true;
            SetArmor(pawn, 0, StatDefOf.ArmorRating_Sharp, "ArmorSharp");
            SetArmor(pawn, 1, StatDefOf.ArmorRating_Blunt, "ArmorBlunt");
            SetArmor(pawn, 2, StatDefOf.ArmorRating_Heat, "ArmorHeat");
        }

        if (!pawn.Dead && pawn.equipment != null && GearTabAccess.ShouldShowEquipment(tab, pawn))
        {
            ShowWeapon = true;
            WeaponText = WeaponSummary(pawn, out WeaponTip);
        }

        if (!pawn.Dead && pawn.RaceProps.Humanlike && pawn.apparel != null)
        {
            BuildSlots(pawn);
        }

        BuildWarnings(pawn);
    }

    /// <summary>The cached snapshot for this pawn, rebuilt when it's stale.</summary>
    public static GearSnapshot For(ITab_Pawn_Gear tab, Pawn pawn)
    {
        int ticks = Find.TickManager?.TicksGame ?? 0;
        int print = Fingerprint(pawn);
        GearSnapshot? snapshot = current;
        if (snapshot == null || snapshot.Pawn != pawn || snapshot.fingerprint != print || ticks - snapshot.tick >= RefreshTicks
            || ticks < snapshot.tick || Time.realtimeSinceStartup - snapshot.realTime >= RefreshSeconds)
        {
            snapshot = new GearSnapshot(tab, pawn, ticks, print);
            current = snapshot;
        }

        return snapshot;
    }

    /// <summary>Rebuilds on the next draw, after an action changed the pawn's gear.</summary>
    public static void Invalidate()
    {
        current = null;
    }

    // Drops the cached pawn so an unloaded game isn't kept in memory.
    public static void Forget()
    {
        current = null;
    }

    public string? WarningFor(Thing thing)
    {
        return warnings.TryGetValue(thing, out string? warning) ? warning : null;
    }

    public GearRowAction? ActionFor(Thing thing)
    {
        return actions.TryGetValue(thing, out GearRowAction? action) ? action : null;
    }

    // The base game's sections and order, with belts moved from Equipment to their own Utility section.
    private void BuildRows(ITab_Pawn_Gear tab, Pawn pawn)
    {
        if (GearTabAccess.ShouldShowEquipment(tab, pawn))
        {
            ShowEquipment = true;
            EquipmentRows.AddRange(pawn.equipment!.AllEquipmentListForReading);
            if (pawn.apparel != null)
            {
                foreach (Apparel worn in pawn.apparel.WornApparel)
                {
                    if (worn.def.apparel.layers.Contains(ApparelLayerDefOf.Belt))
                    {
                        UtilityRows.Add(worn);
                    }
                }
            }
        }

        if (GearTabAccess.ShouldShowApparel(tab, pawn))
        {
            ShowApparel = true;
            foreach (Apparel worn in pawn.apparel!.WornApparel.OrderByDescending(ap => ap.def.apparel.bodyPartGroups[0].listOrder))
            {
                if (!worn.def.apparel.layers.Contains(ApparelLayerDefOf.Belt))
                {
                    ApparelRows.Add(worn);
                }
            }
        }

        if (GearTabAccess.ShouldShowInventory(tab, pawn))
        {
            ShowInventory = true;
            InventoryRows.AddRange(pawn.inventory.innerContainer);
        }

        if (!CanAct)
        {
            return;
        }

        foreach (Thing thing in EquipmentRows)
        {
            if (thing is ThingWithComps weapon)
            {
                actions[thing] = new GearRowAction(GearRowAction.ActionKind.UnequipToInventory, GearActions.CannotUnequipToInventory(pawn, weapon),
                    "VUIP.GearUnequipTip".Translate(thing.LabelShort));
            }
        }

        foreach (Thing thing in UtilityRows.Concat(ApparelRows))
        {
            Apparel worn = (Apparel)thing;
            actions[thing] = new GearRowAction(GearRowAction.ActionKind.RemoveToInventory, GearActions.CannotRemoveToInventory(pawn, worn),
                "VUIP.GearRemoveTip".Translate(thing.LabelShort));
        }

        foreach (Thing thing in InventoryRows)
        {
            if (thing is Apparel held)
            {
                if (pawn.apparel != null)
                {
                    actions[thing] = new GearRowAction(GearRowAction.ActionKind.WearFromInventory, GearActions.CannotWearFromInventory(pawn, held),
                        "VUIP.GearWearTip".Translate(thing.LabelShort));
                }
            }
            else if (thing.def.IsWeapon && thing is ThingWithComps && pawn.equipment != null)
            {
                actions[thing] = new GearRowAction(GearRowAction.ActionKind.EquipFromInventory, GearActions.CannotEquipFromInventory(pawn, thing),
                    "VUIP.GearEquipTip".Translate(thing.LabelShort));
            }
        }
    }

    // Changes whenever gear is added, removed or forced, so the tab reacts at once.
    private static int Fingerprint(Pawn pawn)
    {
        unchecked
        {
            int hash = 17;
            if (pawn.equipment != null)
            {
                List<ThingWithComps> equipment = pawn.equipment.AllEquipmentListForReading;
                for (int i = 0; i < equipment.Count; i++)
                {
                    hash = hash * 31 + equipment[i].thingIDNumber;
                }
            }

            if (pawn.apparel != null)
            {
                List<Apparel> worn = pawn.apparel.WornApparel;
                for (int i = 0; i < worn.Count; i++)
                {
                    hash = hash * 31 + worn[i].thingIDNumber;
                    if (pawn.outfits != null && pawn.outfits.forcedHandler.IsForced(worn[i]))
                    {
                        hash = hash * 31 + 1;
                    }
                }
            }

            if (pawn.inventory != null)
            {
                ThingOwner<Thing> inventory = pawn.inventory.innerContainer;
                for (int i = 0; i < inventory.Count; i++)
                {
                    hash = hash * 31 + inventory[i].thingIDNumber;
                    hash = hash * 31 + inventory[i].stackCount;
                }
            }

            return hash;
        }
    }

    private void SetArmor(Pawn pawn, int index, StatDef stat, string labelKey)
    {
        float value = PawnReadout.OverallArmor(pawn, stat);
        ArmorLabels[index] = labelKey.Translate();
        ArmorTexts[index] = value.ToStringPercent("F0");

        StringBuilder sb = new StringBuilder();
        sb.AppendLine((labelKey.Translate() + ": " + value.ToStringPercent("F0")).AsTipTitle());
        sb.AppendLine();
        float natural = pawn.GetStatValue(stat);
        if (natural > 0f)
        {
            sb.AppendLine("VUIP.GearArmorNatural".Translate(natural.ToStringPercent("F0")));
        }

        List<Apparel>? worn = pawn.apparel?.WornApparel;
        if (worn != null)
        {
            for (int i = 0; i < worn.Count; i++)
            {
                float apparelValue = worn[i].GetStatValue(stat);
                if (apparelValue > 0f)
                {
                    sb.AppendLine("  " + worn[i].LabelCapNoCount + ": " + apparelValue.ToStringPercent("F0"));
                }
            }
        }

        sb.AppendLine();
        sb.Append("VUIP.GearArmorTip".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor));
        ArmorTips[index] = sb.ToString();
    }

    private static string WeaponSummary(Pawn pawn, out string? tip)
    {
        Verb? verb = pawn.equipment?.PrimaryEq?.PrimaryVerb;
        if (verb != null && !verb.IsMeleeAttack)
        {
            float distance = Mathf.Min(verb.verbProps.range, ShootTuning.DistMedium);
            float shooterHit = ShotReport.HitFactorFromShooter(pawn, distance);
            float weaponHit = verb.verbProps.GetHitChanceFactor(verb.EquipmentSource, distance);
            float dps = PawnReadout.RangedDps(pawn, verb, distance, shooterHit, weaponHit, out tip);
            tip ??= "VUIP.PawnPaneRangedDpsUnknown".Translate();
            return "VUIP.GearRangedValue".Translate(dps >= 0f ? dps.ToString("0.00") : "-", verb.verbProps.range.ToString("0"));
        }

        float melee = pawn.GetStatValue(StatDefOf.MeleeDPS);
        tip = StatDefOf.MeleeDPS.LabelCap.Resolve().AsTipTitle() + "\n\n" + StatDefOf.MeleeDPS.description;
        return "VUIP.GearMeleeValue".Translate(StatDefOf.MeleeDPS.ValueToString(melee));
    }

    private void BuildSlots(Pawn pawn)
    {
        List<Apparel> worn = pawn.apparel.WornApparel;
        Dictionary<BodyPartGroupDef, string> nudity = NudityReasons(pawn);
        List<GearSlot> slots = GearSlot.All;
        for (int s = 0; s < slots.Count; s++)
        {
            GearSlot slot = slots[s];
            if (!slot.PawnHasParts(pawn))
            {
                continue;
            }

            Apparel? item = null;
            for (int i = 0; i < worn.Count; i++)
            {
                if (slot.Fits(worn[i].def))
                {
                    item = worn[i];
                    break;
                }
            }

            List<string> problems = new List<string>();
            if (slot.NudityGroup != null && nudity.TryGetValue(slot.NudityGroup, out string? reason) && !Covered(worn, slot.NudityGroup))
            {
                problems.Add(reason!);
            }

            AddRequirementProblems(pawn, slot, problems);

            string? problem = problems.Count > 0 ? string.Join("\n", problems) : null;
            StringBuilder tip = new StringBuilder();
            tip.AppendLine(slot.Label.AsTipTitle());
            tip.AppendLine(slot.Tip.Colorize(ColoredText.SubtleGrayColor));
            tip.AppendLine();
            tip.AppendLine(item != null ? item.LabelCap : "VUIP.GearSlotEmpty".Translate().Resolve());
            if (problem != null)
            {
                tip.AppendLine();
                tip.AppendLine(problem.Colorize(ColorLibrary.RedReadable));
            }

            string plainTip = tip.ToString().TrimEnd();
            string clickTip = plainTip + "\n\n" + "VUIP.GearSlotClickTip".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor);
            Slots.Add(new GearSlotState(slot, item, problem, plainTip, clickTip));
        }
    }

    // Body part groups the pawn has a bad mood thought about leaving uncovered, from the base game and Ideology nudity thoughts.
    private static Dictionary<BodyPartGroupDef, string> NudityReasons(Pawn pawn)
    {
        Dictionary<BodyPartGroupDef, string> result = new Dictionary<BodyPartGroupDef, string>();
        if (pawn.needs?.mood?.thoughts == null)
        {
            return result;
        }

        TmpThoughts.Clear();
        pawn.needs.mood.thoughts.GetAllMoodThoughts(TmpThoughts);
        for (int i = 0; i < TmpThoughts.Count; i++)
        {
            Thought thought = TmpThoughts[i];
            Type? worker = thought.def.workerClass;
            if (worker == null)
            {
                continue;
            }

            float mood = thought.MoodOffset();
            if (mood >= 0f)
            {
                continue;
            }

            bool legs = false;
            bool torso = false;
            bool head = false;
            if (worker == typeof(ThoughtWorker_PsychologicallyNude))
            {
                legs = true;
                torso = pawn.gender == Gender.Female;
            }
            else if (worker == typeof(ThoughtWorker_Precept_GroinUncovered))
            {
                legs = true;
            }
            else if (worker == typeof(ThoughtWorker_Precept_GroinOrChestUncovered))
            {
                legs = torso = true;
            }
            else if (worker == typeof(ThoughtWorker_Precept_GroinChestOrHairUncovered) || worker == typeof(ThoughtWorker_Precept_GroinChestHairOrFaceUncovered))
            {
                legs = torso = head = true;
            }
            else
            {
                continue;
            }

            string reason = "VUIP.GearSlotNudity".Translate(thought.LabelCap, mood.ToStringWithSign("0.#"));
            if (legs)
            {
                result[BodyPartGroupDefOf.Legs] = reason;
            }

            if (torso)
            {
                result[BodyPartGroupDefOf.Torso] = reason;
            }

            if (head)
            {
                result[BodyPartGroupDefOf.UpperHead] = reason;
            }
        }

        TmpThoughts.Clear();
        return result;
    }

    // Any worn apparel on the group; full-head apparel counts for the upper head, as in the base game's checks.
    private static bool Covered(List<Apparel> worn, BodyPartGroupDef group)
    {
        for (int i = 0; i < worn.Count; i++)
        {
            List<BodyPartGroupDef> groups = worn[i].def.apparel.bodyPartGroups;
            if (groups.Contains(group) || (group == BodyPartGroupDefOf.UpperHead && groups.Contains(BodyPartGroupDefOf.FullHead)))
            {
                return true;
            }
        }

        return false;
    }

    // Royal title and role apparel the pawn must wear but doesn't, on slots the required apparel would go in.
    private static void AddRequirementProblems(Pawn pawn, GearSlot slot, List<string> problems)
    {
        List<ApparelRequirement> active = pawn.apparel.ActiveRequirementsForReading;
        if (active.Count == 0)
        {
            return;
        }

        foreach (ApparelRequirementWithSource source in pawn.apparel.AllRequirements)
        {
            ApparelRequirement requirement = source.requirement;
            if (!active.Contains(requirement) || requirement.IsMet(pawn))
            {
                continue;
            }

            ThingDef? fitting = requirement.AllRequiredApparel(pawn.gender).FirstOrDefault(slot.Fits);
            if (fitting == null)
            {
                continue;
            }

            string what = requirement.groupLabel.NullOrEmpty() ? fitting.label : requirement.groupLabel;
            problems.Add("VUIP.GearSlotRequired".Translate(source.SourceLabelCap, what));
        }
    }

    private void BuildWarnings(Pawn pawn)
    {
        Thing? primary = pawn.equipment?.Primary;
        bool rangedPrimary = primary != null && primary.def.IsWeaponUsingProjectiles;
        if (pawn.apparel != null)
        {
            List<Apparel> worn = pawn.apparel.WornApparel;
            ApparelPolicy? policy = pawn.outfits?.CurrentApparelPolicy;
            for (int i = 0; i < worn.Count; i++)
            {
                Apparel apparel = worn[i];
                if (apparel.def.useHitPoints && (float)apparel.HitPoints / apparel.MaxHitPoints < ThoughtWorker_ApparelDamaged.MinForFrayed)
                {
                    AddWarning(apparel, "VUIP.GearWarnTorn".Translate());
                }

                if (policy != null && !policy.filter.Allows(apparel) && !pawn.outfits!.forcedHandler.IsForced(apparel))
                {
                    AddWarning(apparel, "VUIP.GearWarnPolicy".Translate(policy.label));
                }

                if (apparel.def == ThingDefOf.Apparel_ShieldBelt && rangedPrimary)
                {
                    AddWarning(apparel, "VUIP.GearWarnShield".Translate());
                    AddWarning(primary!, "VUIP.GearWarnShield".Translate());
                }
            }
        }

        if (pawn.inventory != null)
        {
            ThingOwner<Thing> inventory = pawn.inventory.innerContainer;
            for (int i = 0; i < inventory.Count; i++)
            {
                Thing thing = inventory[i];
                if (CompBiocodable.IsBiocoded(thing) && !CompBiocodable.IsBiocodedFor(thing, pawn))
                {
                    AddWarning(thing, "VUIP.GearWarnBiocoded".Translate());
                }
            }
        }
    }

    private void AddWarning(Thing thing, string text)
    {
        warnings[thing] = warnings.TryGetValue(thing, out string? existing) ? existing + "\n" + text : text;
    }
}
