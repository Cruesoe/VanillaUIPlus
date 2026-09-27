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
    public const string BetterInspectPanePackageId = "assssssqwww.betterinspectpane";
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

    private static string? conflictingMod;
    private static bool conflictChecked;
    private static bool? combatExtendedActive;
    private static List<SkillDef>? skillsInOrder;
    private static Snapshot? snapshot;
    private static int selectedFrame = -1;
    private static Pawn? selectedPawn;

    public static bool CombatExtendedActive => combatExtendedActive ??= ModsConfig.IsActive(CombatExtendedPackageId);
    public static bool Enabled => UiPlusMod.Settings.pawnPaneEnabled && ConflictingMod == null;

    /// <summary>Name of an active mod that replaces the same pane (RimHUD, Better Inspect Pane), or null.</summary>
    public static string? ConflictingMod
    {
        get
        {
            if (!conflictChecked)
            {
                conflictChecked = true;
                ModMetaData? mod = ModLister.GetActiveModWithIdentifier(RimHudPackageId, ignorePostfix: true)
                    ?? ModLister.GetActiveModWithIdentifier(BetterInspectPanePackageId, ignorePostfix: true);
                conflictingMod = mod?.Name;
            }

            return conflictingMod;
        }
    }

    public static List<SkillDef> SkillsInOrder =>
        skillsInOrder ??= DefDatabase<SkillDef>.AllDefs.OrderByDescending(def => def.listOrder).ToList();

    public static int SkillRows => (SkillsInOrder.Count + 1) / 2;

    public static int BodyRows => UiPlusMod.Settings.pawnPaneShowSkills ? Mathf.Max(StatRows, SkillRows) : StatRows;

    public static float PaneHeight =>
        Mathf.Max(InspectPaneUtility.PaneHeight, TopBarHeight + SectionGap + BodyRows * RowHeight + SectionGap + FooterHeight + PaneChrome);

    /// <summary>The single selected pawn when it gets the readout, otherwise null; worked out once per frame, since the pane patches ask many times.</summary>
    public static Pawn? SelectedPawn()
    {
        int frame = Time.frameCount;
        if (frame != selectedFrame)
        {
            selectedFrame = frame;
            selectedPawn = FindSelectedPawn();
        }

        return selectedPawn;
    }

    private static Pawn? FindSelectedPawn()
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
        bool skills = UiPlusMod.Settings.pawnPaneShowSkills;
        float width = skills ? Mathf.Max(vanillaWidth, UiPlusMod.Settings.pawnPaneWidth) : Mathf.Max(vanillaWidth, MinPaneWidth);
        return Mathf.Max(width, PawnReadoutDrawer.MinPaneWidth(skills));
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
        public readonly float BaseMoveSpeed;
        public readonly float WorkSpeed;
        public readonly bool Ranged;
        public readonly float Dps;
        public readonly float HitChance;
        public readonly string? HitTip;
        public readonly string? RangedTip;
        public readonly float BleedRate;
        public readonly int TicksToBleedOut;
        public readonly List<Need> LowNeeds = new List<Need>();

        // Display text, formatted once per refresh rather than every frame.
        public readonly string ArmorSharpText;
        public readonly string ArmorBluntText;
        public readonly string ArmorHeatText;
        public readonly string RangeText;
        public readonly string MoveText;
        public readonly string WorkText;
        public readonly string DpsText;
        public readonly string BleedingText = string.Empty;
        public readonly string LowNeedsText = string.Empty;
        public readonly bool LowNeedsCritical;

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
            BaseMoveSpeed = pawn.def.GetStatValueAbstract(StatDefOf.MoveSpeed);
            WorkSpeed = pawn.GetStatValue(StatDefOf.WorkSpeedGlobal);

            Verb? verb = pawn.equipment?.PrimaryEq?.PrimaryVerb;
            Ranged = verb != null && !verb.IsMeleeAttack;
            if (Ranged)
            {
                // Medium range, or the weapon's range if shorter.
                float distance = Mathf.Min(verb!.verbProps.range, ShootTuning.DistMedium);
                float shooterHit = ShotReport.HitFactorFromShooter(pawn, distance);
                float weaponHit = verb.verbProps.GetHitChanceFactor(verb.EquipmentSource, distance);
                HitChance = Mathf.Clamp01(shooterHit * weaponHit);
                HitTip = "VUIP.PawnPaneHitTipTitle".Translate(distance.ToString("0")).Resolve().AsTipTitle() + "\n\n"
                    + "VUIP.PawnPaneRangedDpsShooterHit".Translate(shooterHit.ToStringPercent()) + "\n"
                    + "VUIP.PawnPaneRangedDpsWeaponHit".Translate(weaponHit.ToStringPercent()) + "\n\n"
                    + "VUIP.PawnPaneRangedDpsNote".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor);
                Dps = RangedDps(pawn, verb, distance, shooterHit, weaponHit, out RangedTip);
            }
            else
            {
                Dps = pawn.GetStatValue(StatDefOf.MeleeDPS);
                HitChance = pawn.GetStatValue(StatDefOf.MeleeHitChance);
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

            ArmorSharpText = ArmorSharp.ToStringPercent("F0");
            ArmorBluntText = ArmorBlunt.ToStringPercent("F0");
            ArmorHeatText = ArmorHeat.ToStringPercent("F0");
            RangeText = TemperatureRange(Comfortable.min, Comfortable.max);
            MoveText = MoveSpeed.ToString("0.00");
            WorkText = StatDefOf.WorkSpeedGlobal.ValueToString(WorkSpeed);
            DpsText = !Ranged ? StatDefOf.MeleeDPS.ValueToString(Dps) : Dps >= 0f ? Dps.ToString("0.0") : "-";
            if (BleedRate > 0f)
            {
                BleedingText = TicksToBleedOut > 0 && TicksToBleedOut < int.MaxValue
                    ? "VUIP.PawnPaneBleedingValue".Translate(TicksToBleedOut.ToStringTicksToPeriod())
                    : "VUIP.PawnPaneBleedingRate".Translate(BleedRate.ToStringPercent());
            }

            if (LowNeeds.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (Need need in LowNeeds)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(need.LabelCap).Append(' ').Append(need.CurLevelPercentage.ToStringPercent());
                    LowNeedsCritical |= need.CurLevelPercentage < threshold / 2f;
                }

                LowNeedsText = sb.ToString();
            }
        }
    }

    // "-14 ~ 39°C": both ends in the player's temperature unit, with the unit written once.
    public static string TemperatureRange(float minCelsius, float maxCelsius)
    {
        TemperatureDisplayMode mode = Prefs.TemperatureMode;
        string unit = mode switch
        {
            TemperatureDisplayMode.Fahrenheit => "°F",
            TemperatureDisplayMode.Kelvin => "K",
            _ => "°C",
        };
        return GenTemperature.CelsiusTo(minCelsius, mode).ToString("F0") + " ~ " + GenTemperature.CelsiusTo(maxCelsius, mode).ToString("F0") + unit;
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
    private static float RangedDps(Pawn pawn, Verb verb, float distance, float shooterHit, float weaponHit, out string? tip)
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
