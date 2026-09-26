using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Pawn inspect pane readout for colonists, slaves and prisoners: which pawns get it, the pane size, and a cached stat snapshot.
/// </summary>
public static class PawnReadout
{
    public const string RimHudPackageId = "jaxe.rimhud";
    public const string CombatExtendedPackageId = "ceteam.combatextended";
    public const float MinPaneWidth = 432f;
    public const float MaxPaneWidth = 720f;
    public const float DefaultPaneWidth = 560f;
    public const float TopBarHeight = 21f;
    public const float RowHeight = 20f;
    public const float SectionGap = 4f;
    public const float FooterHeight = 22f;
    public const int StatRows = 6;

    // Space the base game's pane leaves above and below its contents.
    private const float PaneChrome = 40f;
    private const int RefreshTicks = 60;
    private const float RefreshSeconds = 1f;

    private static bool? rimHudActive;
    private static bool? combatExtendedActive;
    private static List<SkillDef>? skillsInOrder;
    private static Snapshot? snapshot;

    public static bool RimHudActive => rimHudActive ??= ModsConfig.IsActive(RimHudPackageId);
    public static bool CombatExtendedActive => combatExtendedActive ??= ModsConfig.IsActive(CombatExtendedPackageId);
    public static bool Enabled => UiPlusMod.Settings.pawnPaneEnabled && !RimHudActive;

    public static List<SkillDef> SkillsInOrder =>
        skillsInOrder ??= DefDatabase<SkillDef>.AllDefs.OrderByDescending(def => def.listOrder).ToList();

    public static int SkillRows => (SkillsInOrder.Count + 1) / 2;

    public static int BodyRows => UiPlusMod.Settings.pawnPaneShowSkills ? Mathf.Max(StatRows, SkillRows) : StatRows;

    public static float PaneHeight =>
        Mathf.Max(InspectPaneUtility.PaneHeight, TopBarHeight + SectionGap + BodyRows * RowHeight + SectionGap + FooterHeight + PaneChrome);

    /// <summary>The single selected pawn when it gets the readout, otherwise null.</summary>
    public static Pawn? SelectedPawn()
    {
        if (!Enabled || Find.Selector == null || Find.Selector.NumSelected != 1)
        {
            return null;
        }

        return Find.Selector.SingleSelectedThing is Pawn pawn && Qualifies(pawn) ? pawn : null;
    }

    public static bool Qualifies(Pawn pawn)
    {
        if (pawn.Dead || pawn.def.hideInspect || !pawn.RaceProps.Humanlike || pawn.IsMutant || pawn.skills == null)
        {
            return false;
        }

        return pawn.Faction == Faction.OfPlayer || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony;
    }

    public static float PaneWidth(float vanillaWidth)
    {
        return UiPlusMod.Settings.pawnPaneShowSkills
            ? Mathf.Max(vanillaWidth, UiPlusMod.Settings.pawnPaneWidth)
            : Mathf.Max(vanillaWidth, MinPaneWidth);
    }

    /// <summary>Height of the base game's pane, or of ours while a readout pawn is selected.</summary>
    public static float CurrentPaneHeight()
    {
        return SelectedPawn() != null ? PaneHeight : InspectPaneUtility.PaneHeight;
    }

    public static Snapshot For(Pawn pawn)
    {
        Snapshot? current = snapshot;
        int ticks = Find.TickManager?.TicksGame ?? 0;
        if (current == null || current.Pawn != pawn || ticks - current.Tick >= RefreshTicks || ticks < current.Tick
            || Time.realtimeSinceStartup - current.RealTime >= RefreshSeconds)
        {
            current = new Snapshot(pawn, ticks);
            snapshot = current;
        }

        return current;
    }

    public class Snapshot
    {
        public readonly Pawn Pawn;
        public readonly int Tick;
        public readonly float RealTime;
        public readonly float ArmorSharp;
        public readonly float ArmorBlunt;
        public readonly float ArmorHeat;
        public readonly float Temperature;
        public readonly FloatRange Comfortable;
        public readonly FloatRange Safe;
        public readonly float MoveSpeed;
        public readonly bool Ranged;
        public readonly float Dps;
        public readonly string? RangedTip;
        public readonly float BleedRate;
        public readonly int TicksToBleedOut;
        public readonly List<Need> LowNeeds = new List<Need>();

        public Snapshot(Pawn pawn, int tick)
        {
            Pawn = pawn;
            Tick = tick;
            RealTime = Time.realtimeSinceStartup;

            if (!CombatExtendedActive)
            {
                ArmorSharp = OverallArmor(pawn, StatDefOf.ArmorRating_Sharp);
                ArmorBlunt = OverallArmor(pawn, StatDefOf.ArmorRating_Blunt);
                ArmorHeat = OverallArmor(pawn, StatDefOf.ArmorRating_Heat);
            }

            Temperature = pawn.AmbientTemperature;
            Comfortable = GenTemperature.ComfortableTemperatureRange(pawn);
            Safe = GenTemperature.SafeTemperatureRange(pawn);
            MoveSpeed = pawn.GetStatValue(StatDefOf.MoveSpeed);

            Verb? verb = pawn.equipment?.PrimaryEq?.PrimaryVerb;
            Ranged = verb != null && !verb.IsMeleeAttack;
            if (Ranged)
            {
                Dps = RangedDps(pawn, verb!, out RangedTip);
            }
            else
            {
                Dps = pawn.GetStatValue(StatDefOf.MeleeDPS);
            }

            BleedRate = pawn.health.hediffSet.BleedRateTotal;
            TicksToBleedOut = BleedRate > 0f ? HealthUtility.TicksUntilDeathDueToBloodLoss(pawn) : int.MaxValue;

            float threshold = UiPlusMod.Settings.pawnPaneNeedThreshold / 100f;
            if (pawn.needs != null)
            {
                foreach (Need need in pawn.needs.AllNeeds)
                {
                    if (need != pawn.needs.mood && need.ShowOnNeedList && need.CurLevelPercentage < threshold)
                    {
                        LowNeeds.Add(need);
                    }
                }
            }
        }
    }

    // Same calculation as the Gear tab's overall armor (ITab_Pawn_Gear.TryDrawOverallArmor).
    private static float OverallArmor(Pawn pawn, StatDef stat)
    {
        float total = 0f;
        float natural = Mathf.Clamp01(pawn.GetStatValue(stat) / 2f);
        List<BodyPartRecord> parts = pawn.RaceProps.body.AllParts;
        List<Apparel>? apparel = pawn.apparel?.WornApparel;
        for (int i = 0; i < parts.Count; i++)
        {
            float unblocked = 1f - natural;
            if (apparel != null)
            {
                for (int j = 0; j < apparel.Count; j++)
                {
                    if (apparel[j].def.apparel.CoversBodyPart(parts[i]))
                    {
                        unblocked *= 1f - Mathf.Clamp01(apparel[j].GetStatValue(stat) / 2f);
                    }
                }
            }

            total += parts[i].coverageAbs * (1f - unblocked);
        }

        return Mathf.Clamp(total * 2f, 0f, 2f);
    }

    // Damage per full shooting cycle at medium range (or the weapon's range if shorter), with the pawn's and weapon's hit chance; ignores cover, weather and target size.
    private static float RangedDps(Pawn pawn, Verb verb, out string? tip)
    {
        tip = null;
        if (verb is not Verb_LaunchProjectile launcher || launcher.Projectile?.projectile == null)
        {
            return -1f;
        }

        Thing? weapon = verb.EquipmentSource;
        int damage = launcher.Projectile.projectile.GetDamageAmount(weapon);
        int shots = verb.BurstShotCount;
        float warmup = verb.WarmupTime * pawn.GetStatValue(StatDefOf.AimingDelayFactor);
        float cooldown = verb.verbProps.AdjustedCooldown(verb, pawn);
        float burst = ((shots - 1) * verb.TicksBetweenBurstShots).TicksToSeconds();
        float cycle = warmup + cooldown + burst;
        if (cycle <= 0f || damage <= 0)
        {
            return -1f;
        }

        float distance = Mathf.Min(verb.verbProps.range, ShootTuning.DistMedium);
        float shooterHit = ShotReport.HitFactorFromShooter(pawn, distance);
        float weaponHit = verb.verbProps.GetHitChanceFactor(weapon, distance);
        float raw = damage * shots / cycle;
        float dps = raw * shooterHit * weaponHit;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("VUIP.PawnPaneRangedDpsTipTitle".Translate(distance.ToString("0")).Resolve().AsTipTitle());
        sb.AppendLine();
        sb.AppendLine("VUIP.PawnPaneRangedDpsDamage".Translate(damage, shots));
        sb.AppendLine("VUIP.PawnPaneRangedDpsCycle".Translate(cycle.ToString("0.00"), warmup.ToString("0.00"), cooldown.ToString("0.00"), burst.ToString("0.00")));
        sb.AppendLine("VUIP.PawnPaneRangedDpsRaw".Translate(raw.ToString("0.0")));
        sb.AppendLine("VUIP.PawnPaneRangedDpsShooterHit".Translate(shooterHit.ToStringPercent()));
        sb.AppendLine("VUIP.PawnPaneRangedDpsWeaponHit".Translate(weaponHit.ToStringPercent()));
        sb.AppendLine();
        sb.Append("VUIP.PawnPaneRangedDpsNote".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor));
        tip = sb.ToString();
        return dps;
    }
}
