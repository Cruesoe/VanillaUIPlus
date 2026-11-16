using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// The Gear tab's stats, apparel slots, equipment and inventory for one pawn. Rebuilt when the pawn or its gear changes, and once a second.
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

    // Rows of the Combat and Protection boxes; empty when the box isn't shown.
    public readonly List<GearStat> Combat = new List<GearStat>();
    public readonly List<GearStat> Protection = new List<GearStat>();
    public readonly string CombatHeader = "VUIP.GearCombat".Translate();
    public readonly string ProtectionHeader = "VUIP.GearProtection".Translate();

    // Apparel slots in grid order, and worn apparel no slot takes (mod layers, or all of it for pawns without slots).
    public readonly List<GearSlotState> Slots = new List<GearSlotState>();
    public readonly List<GearItem> OtherApparel = new List<GearItem>();
    // Grid rows shown, and each slot row's place among them.
    public int SlotRows { get; private set; }
    public int[] RowMap { get; private set; } = Array.Empty<int>();
    // Equipped weapons, then weapons carried in the inventory; the rest of the inventory.
    public readonly List<GearItem> Equipment = new List<GearItem>();
    public readonly List<GearItem> Inventory = new List<GearItem>();
    private readonly Dictionary<Thing, string> warnings = new Dictionary<Thing, string>();

    // Whether the player can give this pawn orders from the tab (the base game's CanControlColonist), and the wider CanControl drop uses.
    public readonly bool CanAct;
    public readonly bool CanControl;

    public bool ShowEquipment { get; private set; }
    public bool ShowApparel { get; private set; }
    public bool ShowInventory { get; private set; }

    public readonly string EquipmentHeader = "Equipment".Translate();
    public readonly string ApparelHeader = "Apparel".Translate();
    public readonly string InventoryHeader = "Inventory".Translate();
    public readonly string NoWeaponText = "VUIP.GearNoWeapon".Translate();
    public readonly string EmptyText = "VUIP.GearInventoryEmpty".Translate();
    public readonly string ForceTip = "VUIP.GearForceTip".Translate();

    private GearSnapshot(ITab_Pawn_Gear tab, Pawn pawn, int tick, int fingerprint)
    {
        Pawn = pawn;
        this.tick = tick;
        this.fingerprint = fingerprint;
        realTime = Time.realtimeSinceStartup;
        CanControl = !pawn.Dead && GearTabAccess.CanControl(tab);
        CanAct = CanControl && GearTabAccess.CanControlColonist(tab);
        BuildWarnings(pawn);
        BuildItems(tab, pawn);

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

        if (!pawn.Dead && pawn.equipment != null && GearTabAccess.ShouldShowEquipment(tab, pawn))
        {
            BuildCombat(pawn);
        }

        if (GearTabAccess.ShouldShowOverallArmor(tab, pawn))
        {
            AddArmor(pawn, 0, StatDefOf.ArmorRating_Sharp, "ArmorSharp");
            AddArmor(pawn, 1, StatDefOf.ArmorRating_Blunt, "ArmorBlunt");
            AddArmor(pawn, 2, StatDefOf.ArmorRating_Heat, "ArmorHeat");
        }

        if (!pawn.Dead)
        {
            AddComfort(pawn);
        }
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

    // Every piece of gear the base game's tab would list, sorted into the boxes: worn apparel by slot, weapons, and the rest of the inventory.
    private void BuildItems(ITab_Pawn_Gear tab, Pawn pawn)
    {
        ShowApparel = GearTabAccess.ShouldShowApparel(tab, pawn);
        ShowEquipment = GearTabAccess.ShouldShowEquipment(tab, pawn);
        ShowInventory = GearTabAccess.ShouldShowInventory(tab, pawn);

        if (ShowApparel)
        {
            List<GearItem> worn = new List<GearItem>();
            foreach (Apparel apparel in pawn.apparel!.WornApparel.OrderByDescending(ap => ap.def.apparel.bodyPartGroups[0].listOrder))
            {
                worn.Add(MakeItem(pawn, apparel, false));
            }

            if (pawn.RaceProps.Humanlike)
            {
                BuildSlots(pawn, worn);
            }

            for (int i = 0; i < worn.Count; i++)
            {
                if (!InAnySlot(worn[i]))
                {
                    OtherApparel.Add(worn[i]);
                }
            }
        }

        if (ShowEquipment)
        {
            foreach (ThingWithComps equipped in pawn.equipment!.AllEquipmentListForReading)
            {
                Equipment.Add(MakeItem(pawn, equipped, false));
            }
        }

        if (ShowInventory)
        {
            ThingOwner<Thing> held = pawn.inventory.innerContainer;
            for (int i = 0; i < held.Count; i++)
            {
                GearItem item = MakeItem(pawn, held[i], true);
                if (ShowEquipment && held[i].def.IsWeapon && held[i] is ThingWithComps)
                {
                    item.Muted = true;
                    Equipment.Add(item);
                }
                else
                {
                    Inventory.Add(item);
                }
            }
        }
    }

    private bool InAnySlot(GearItem item)
    {
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].Items.Contains(item))
            {
                return true;
            }
        }

        return false;
    }

    // Labels, health, warnings and the actions the base game's row would offer (ITab_Pawn_Gear.DrawThingRow), plus moving to and from the inventory.
    private GearItem MakeItem(Pawn pawn, Thing thing, bool inventory)
    {
        GearItem item = new GearItem(thing, inventory)
        {
            Label = thing.LabelCap,
            Warning = warnings.TryGetValue(thing, out string? warning) ? warning : null,
            CountText = inventory && thing.stackCount > 1 ? thing.stackCount.ToStringCached() : string.Empty,
        };

        if (thing.def.useHitPoints && thing.MaxHitPoints > 0)
        {
            item.HitPoints = Mathf.Clamp01((float)thing.HitPoints / thing.MaxHitPoints);
        }

        Apparel? worn = !inventory ? thing as Apparel : null;
        if (worn != null && pawn.outfits != null)
        {
            item.Forced = pawn.outfits.forcedHandler.IsForced(worn);
            item.CanForce = CanAct;
        }

        if (thing.def.IsWeapon)
        {
            item.Detail = WeaponDetail(thing);
        }

        SetDrop(pawn, item, worn);
        item.CanEat = inventory && CanAct && FoodUtility.WillIngestFromInventoryNow(pawn, thing);
        item.Action = ActionFor(pawn, thing, inventory);
        item.Tip = ItemDetails(item) + "\n\n" + "VUIP.GearItemClickTip".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor);
        return item;
    }

    // The base game's drop button rules: shown when the pawn can be controlled, greyed out for quest lodgers, locked apparel and gear destroyed on drop.
    private void SetDrop(Pawn pawn, GearItem item, Apparel? worn)
    {
        bool inventory = item.Inventory;
        item.DropShown = CanControl && (inventory || CanAct || (pawn.Spawned && !pawn.Map.IsPlayerHome));
        if (!item.DropShown)
        {
            return;
        }

        if (worn != null && pawn.apparel != null && pawn.apparel.IsLocked(worn))
        {
            item.DropBlocked = "DropThingLocked".Translate();
        }
        else if (pawn.IsQuestLodger() && (inventory || !EquipmentUtility.QuestLodgerCanUnequip(item.Thing, pawn)))
        {
            item.DropBlocked = "DropThingLodger".Translate();
        }
        else if (!inventory && pawn.kindDef.destroyGearOnDrop)
        {
            item.DropBlocked = "VUIP.GearDropDestroyed".Translate();
        }

        item.DropTip = item.DropBlocked ?? "DropThing".Translate();
    }

    private GearRowAction? ActionFor(Pawn pawn, Thing thing, bool inventory)
    {
        if (!CanAct)
        {
            return null;
        }

        if (!inventory)
        {
            if (thing is Apparel worn)
            {
                return new GearRowAction(GearRowAction.ActionKind.RemoveToInventory, GearActions.CannotRemoveToInventory(pawn, worn),
                    "VUIP.GearRemoveTip".Translate(thing.LabelShort));
            }

            return thing is ThingWithComps weapon
                ? new GearRowAction(GearRowAction.ActionKind.UnequipToInventory, GearActions.CannotUnequipToInventory(pawn, weapon), "VUIP.GearUnequipTip".Translate(thing.LabelShort))
                : null;
        }

        if (thing is Apparel held)
        {
            return pawn.apparel != null
                ? new GearRowAction(GearRowAction.ActionKind.WearFromInventory, GearActions.CannotWearFromInventory(pawn, held), "VUIP.GearWearTip".Translate(thing.LabelShort))
                : null;
        }

        return thing.def.IsWeapon && thing is ThingWithComps && pawn.equipment != null
            ? new GearRowAction(GearRowAction.ActionKind.EquipFromInventory, GearActions.CannotEquipFromInventory(pawn, thing), "VUIP.GearEquipTip".Translate(thing.LabelShort))
            : null;
    }

    // The weapon's own damage per second and range, without a wielder, as the item readout shows them.
    private static string WeaponDetail(Thing weapon)
    {
        if (weapon.def.IsMeleeWeapon)
        {
            StatDef melee = StatDefOf.MeleeWeapon_AverageDPS;
            return "VUIP.GearWeaponMelee".Translate(melee.ValueToString(weapon.GetStatValue(melee)));
        }

        Verb? verb = weapon.TryGetComp<CompEquippable>()?.PrimaryVerb;
        if (verb == null || verb.IsMeleeAttack)
        {
            return string.Empty;
        }

        float distance = Mathf.Min(verb.verbProps.range, ShootTuning.DistMedium);
        float dps = PawnReadout.RangedDps(null, verb, distance, 1f, verb.verbProps.GetHitChanceFactor(weapon, distance), out _);
        return "VUIP.GearWeaponRanged".Translate(dps >= 0f ? dps.ToString("0.00") : "-", verb.verbProps.range.ToString("0.#"));
    }

    // Label, health, mass, weapon stats, forced and warnings; no click hint.
    private string ItemDetails(GearItem item)
    {
        Thing thing = item.Thing;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(thing.LabelCap.AsTipTitle());
        if (item.HitPoints >= 0f)
        {
            sb.AppendLine("VUIP.GearHitPoints".Translate(thing.HitPoints, thing.MaxHitPoints, item.HitPoints.ToStringPercent()));
        }

        sb.AppendLine(StatDefOf.Mass.LabelCap + ": " + (thing.GetStatValue(StatDefOf.Mass) * thing.stackCount).ToStringMass());
        if (!item.Detail.NullOrEmpty())
        {
            sb.AppendLine(item.Detail);
        }

        if (item.Forced)
        {
            sb.AppendLine();
            sb.AppendLine(ForceTip);
        }

        if (item.Warning != null)
        {
            sb.AppendLine();
            sb.AppendLine(item.Warning.Colorize(ColorLibrary.RedReadable));
        }

        return sb.ToString().TrimEnd();
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

    private void AddArmor(Pawn pawn, int icon, StatDef stat, string labelKey)
    {
        float value = PawnReadout.OverallArmor(pawn, stat);
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
        Protection.Add(new GearStat { Icon = icon, Label = labelKey.Translate(), Value = value.ToStringPercent("F0"), Tip = sb.ToString() });
    }

    // Comfortable temperature range, red when the map's outdoor temperature is outside it.
    private void AddComfort(Pawn pawn)
    {
        float min = pawn.GetStatValue(StatDefOf.ComfyTemperatureMin);
        float max = pawn.GetStatValue(StatDefOf.ComfyTemperatureMax);
        string range = min.ToStringTemperature("F0") + " ~ " + max.ToStringTemperature("F0");
        string tip = ("ComfyTemperatureRange".Translate() + ": " + range).AsTipTitle();
        bool outside = false;
        Map? map = pawn.MapHeld;
        if (map != null)
        {
            float outdoor = map.mapTemperature.OutdoorTemp;
            outside = outdoor < min || outdoor > max;
            tip += "\n\n" + "VUIP.GearOutdoorTemperature".Translate(outdoor.ToStringTemperature("F0"));
        }

        Protection.Add(new GearStat
        {
            Icon = 3, Label = "VUIP.GearComfort".Translate(), Value = range, Color = outside ? ColorLibrary.RedReadable : Color.white, Tip = tip
        });
    }

    // Melee damage, the primary ranged weapon's damage and range, and move speed.
    private void BuildCombat(Pawn pawn)
    {
        StatDef meleeStat = StatDefOf.MeleeDPS;
        float melee = pawn.GetStatValue(meleeStat);
        Combat.Add(new GearStat
        {
            Texture = GearTabDrawer.MeleeTex, Label = "VUIP.GearMeleeDps".Translate(), Value = meleeStat.ValueToString(melee), Tip = StatTip(pawn, meleeStat, melee)
        });

        Verb? verb = pawn.equipment?.PrimaryEq?.PrimaryVerb;
        if (verb != null && !verb.IsMeleeAttack)
        {
            float distance = Mathf.Min(verb.verbProps.range, ShootTuning.DistMedium);
            float shooterHit = ShotReport.HitFactorFromShooter(pawn, distance);
            float weaponHit = verb.verbProps.GetHitChanceFactor(verb.EquipmentSource, distance);
            float dps = PawnReadout.RangedDps(pawn, verb, distance, shooterHit, weaponHit, out string? dpsTip);
            Combat.Add(new GearStat
            {
                Icon = 6, Label = "VUIP.GearRangedDps".Translate(), Value = dps >= 0f ? dps.ToString("0.00") : "-",
                Color = dps >= 0f ? Color.white : PawnReadoutDrawer.MutedColor, Tip = dpsTip ?? "VUIP.PawnPaneRangedDpsUnknown".Translate()
            });

            string range = verb.verbProps.range.ToString("0.#");
            string rangeLabel = "VUIP.GearRange".Translate();
            Combat.Add(new GearStat
            {
                Texture = GearTabDrawer.RangeTex, Label = rangeLabel, Value = range,
                Tip = (rangeLabel + ": " + range).AsTipTitle() + "\n\n" + "VUIP.GearRangeTip".Translate(verb.EquipmentSource?.LabelCap ?? string.Empty)
            });
        }

        StatDef moveStat = StatDefOf.MoveSpeed;
        float move = pawn.GetStatValue(moveStat);
        Combat.Add(new GearStat { Icon = 4, Label = moveStat.LabelCap, Value = moveStat.ValueToString(move), Tip = StatTip(pawn, moveStat, move) });
    }

    private static string StatTip(Pawn pawn, StatDef stat, float value)
    {
        return stat.LabelCap.Resolve().AsTipTitle() + "\n\n" + stat.Worker.GetExplanationFull(StatRequest.For(pawn), stat.toStringNumberSense, value);
    }

    // Every slot the pawn has the body parts for, with the worn apparel that fills it and, for living pawns, why an empty one matters.
    private void BuildSlots(Pawn pawn, List<GearItem> wornItems)
    {
        List<Apparel> worn = pawn.apparel.WornApparel;
        Dictionary<BodyPartGroupDef, string> nudity = pawn.Dead ? new Dictionary<BodyPartGroupDef, string>() : NudityReasons(pawn);
        List<GearSlot> slots = GearSlot.All;
        for (int s = 0; s < slots.Count; s++)
        {
            GearSlot slot = slots[s];
            if (!slot.PawnHasParts(pawn))
            {
                continue;
            }

            List<GearItem> items = new List<GearItem>();
            for (int i = 0; i < wornItems.Count; i++)
            {
                if (slot.Fits(wornItems[i].Thing.def))
                {
                    items.Add(wornItems[i]);
                }
            }

            List<string> problems = new List<string>();
            if (slot.NudityGroup != null && nudity.TryGetValue(slot.NudityGroup, out string? reason) && !Covered(worn, slot.NudityGroup))
            {
                problems.Add(reason!);
            }

            if (!pawn.Dead)
            {
                AddRequirementProblems(pawn, slot, problems);
            }

            string? problem = problems.Count > 0 ? string.Join("\n", problems) : null;
            StringBuilder tip = new StringBuilder();
            tip.AppendLine(slot.Label.AsTipTitle());
            tip.AppendLine(slot.Tip.Colorize(ColoredText.SubtleGrayColor));
            tip.AppendLine();
            if (items.Count == 0)
            {
                tip.AppendLine("VUIP.GearSlotEmpty".Translate().Resolve());
            }

            for (int i = 0; i < items.Count; i++)
            {
                tip.AppendLine(ItemDetails(items[i]));
                tip.AppendLine();
            }

            if (problem != null)
            {
                tip.AppendLine();
                tip.AppendLine(problem.Colorize(ColorLibrary.RedReadable));
            }

            string plainTip = tip.ToString().TrimEnd();
            string hint = (items.Count > 0 ? "VUIP.GearSlotFilledClickTip" : "VUIP.GearSlotClickTip").Translate().Resolve();
            string clickTip = plainTip + "\n\n" + hint.Colorize(ColoredText.SubtleGrayColor);
            Slots.Add(new GearSlotState(slot, items, problem, plainTip, clickTip));
        }

        // Grid rows with no slot for this pawn are closed up.
        int maxRow = 0;
        for (int i = 0; i < Slots.Count; i++)
        {
            maxRow = Mathf.Max(maxRow, Slots[i].Slot.Row + 1);
        }

        RowMap = new int[maxRow];
        for (int row = 0; row < maxRow; row++)
        {
            RowMap[row] = Slots.Any(state => state.Slot.Row == row) ? SlotRows++ : -1;
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

                if (apparel.def.IsShieldThatBlocksRanged && rangedPrimary)
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
