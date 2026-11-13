using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Cards for the other kinds of building: beds, doors, turrets, plant growers, fermenting barrels, graves and caskets, transporters,
/// construction frames, deep drills, occupied DLC buildings and art.
/// </summary>
public static partial class ObjectReadout
{
    private static readonly Texture2D BedTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/bed");
    private static readonly Texture2D ComfortTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/tea");
    private static readonly Texture2D DoorTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/garage-variant");
    private static readonly Texture2D TargetTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/shield-sword");
    private static readonly Texture2D RangeTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/ruler");
    private static readonly Texture2D BuildTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/hammer");
    private static readonly Texture2D ArtTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/palette");
    private static readonly Texture2D GraveTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/coffin");
    private static readonly Texture2D QualityTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/shield-crown");
    private static readonly Texture2D MedicalTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/hospital-box");
    private static readonly Texture2D MassTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/scale-balance");

    // Whether a casket's contents are known; resolved once for the session, and treated as known if the field is missing.
    private static readonly AccessTools.FieldRef<Building_Casket, bool>? ContentsKnown =
        ReflectionGuard.FieldRef<Building_Casket, bool>(nameof(Building_Casket), "contentsKnown", AccessTools.Field(typeof(Building_Casket), "contentsKnown"));

    // Lines that start with the fixed start of a translated sentence (before its first argument) move to a card's tooltip.
    private static PrefixCover? CoverKeyStart(View current, string key)
    {
        string template = key.Translate().ToString();
        int argument = template.IndexOf('{');
        string prefix = argument >= 0 ? template.Substring(0, argument) : template;
        if (prefix.Trim().Length < 3)
        {
            return null;
        }

        PrefixCover cover = new PrefixCover { Prefix = prefix };
        current.Prefixes.Add(cover);
        return cover;
    }

    private static string Joined(PrefixCover? cover)
    {
        return cover?.Joined ?? string.Empty;
    }

    private static void AddOtherBuildingCards(View current, Thing thing, HashSet<string> covered)
    {
        switch (thing)
        {
            case Building_Bed bed:
                AddBedCards(current, bed, covered);
                break;
            case Building_Door door:
                AddDoorCards(current, door);
                break;
            case Building_TurretGun turret:
                AddTurretCards(current, turret, covered);
                break;
            case Building_PlantGrower grower:
                AddGrowerCards(current, grower, covered);
                break;
            case Building_FermentingBarrel barrel:
                AddBarrelCards(current, barrel, covered);
                break;
            case Building_Grave grave:
                AddGraveCards(current, grave, covered);
                break;
            case Building_Casket casket:
                AddCasketCard(current, casket, covered);
                break;
            case Frame frame:
                AddFrameCards(current, frame, covered);
                break;
            case Building_Enterable enterable:
                AddOccupantCard(current, enterable.SelectedPawn ?? enterable.innerContainer.FirstOrDefault() as Pawn);
                break;
        }

        if (thing is ThingWithComps withComps)
        {
            if (withComps.GetComp<CompTransporter>() is CompTransporter transporter)
            {
                AddTransporterCards(current, transporter, covered);
            }

            if (withComps.GetComp<CompDeepDrill>() is CompDeepDrill drill)
            {
                AddDrillCards(current, thing, drill, covered);
            }

            if (ModsConfig.IdeologyActive && withComps.GetComp<CompBiosculpterPod>() is CompBiosculpterPod pod)
            {
                AddOccupantCard(current, pod.Occupant);
            }
        }
    }

    // Who the bed is for, its owners, rest effectiveness, comfort and quality.
    private static void AddBedCards(View current, Building_Bed bed, HashSet<string> covered)
    {
        string forTitle = TextCache.Key("VUIP.ObjectPaneBedForShort");
        string forText = string.Empty;
        string forValue = TextCache.Key(bed.def.building.bed_humanlike ? "VUIP.ObjectPaneBedPeople" : "VUIP.ObjectPaneBedAnimals");
        if (bed.def.building.bed_humanlike && bed.def.building.bed_DisplayOwnerType && bed.Faction == Faction.OfPlayer)
        {
            forText = (bed.ForOwnerType switch
            {
                BedOwnerType.Prisoner => "ForPrisonerUse",
                BedOwnerType.Slave => "ForSlaveUse",
                _ => "ForColonistUse",
            }).Translate().Resolve();
            covered.Add(forText);
            forValue = TextCache.Key(bed.ForOwnerType switch
            {
                BedOwnerType.Prisoner => "VUIP.ObjectPaneBedPrisoners",
                BedOwnerType.Slave => "VUIP.ObjectPaneBedSlaves",
                _ => "VUIP.ObjectPaneBedColonists",
            });
        }

        PrefixCover? infection = null;
        if (bed.Medical)
        {
            string medical = "MedicalBed".Translate().Resolve();
            covered.Add(medical);
            infection = CoverLabel(current, "RoomInfectionChanceFactor".Translate());
            forText = forText.Length > 0 ? forText + "\n" + medical : medical;
            forValue += " (" + TextCache.Key("VUIP.ObjectPaneBedMedical") + ")";
        }

        string forBody = forText;
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = bed.Medical ? MedicalTex : BedTex, Caption = forTitle, Value = forValue,
            Tip = () => Tip(forTitle, (forBody.Length > 0 ? forBody : forValue) + (infection?.Lines.Count > 0 ? "\n" + string.Join("\n", infection.Lines) : string.Empty))
        });

        if (!bed.Medical && bed.CompAssignableToPawn != null && bed.def.building.bed_DisplayOwnersInInspectString)
        {
            CoverLabel(current, "Owner".Translate());
            CoverLabel(current, "Owners".Translate());
            List<Pawn> owners = bed.OwnersForReading;
            string ownerTitle = TextCache.Key("VUIP.ObjectPaneOwnerShort");
            string ownerBody = owners.Count == 0 ? "Nobody".Translate().Resolve() : string.Join("\n", owners.Select(p => p.LabelCap));
            Pawn? first = owners.Count > 0 ? owners[0] : null;
            current.Cards.Add(new ReadoutCard
            {
                IconInstance = first, IconTexture = first == null ? WorkerTex : null, Caption = ownerTitle,
                Value = first == null ? "Nobody".Translate().Resolve().CapitalizeFirst() : first.LabelShortCap + (owners.Count > 1 ? " +" + (owners.Count - 1) : string.Empty),
                ValueColor = first == null ? PawnReadoutDrawer.MutedColor : Color.white, Tip = () => Tip(ownerTitle, ownerBody),
                Click = first == null ? null : () => CameraJumper.TryJumpAndSelect(first)
            });
        }

        AddStatCard(current, bed, StatDefOf.BedRestEffectiveness, BedTex, v => v.ToStringPercent(), caption: TextCache.Key("VUIP.ObjectPaneRestShort"), perStack: false);
        AddStatCard(current, bed, StatDefOf.Comfort, ComfortTex, v => v.ToStringPercent(), perStack: false);
        AddQualityCard(current, bed);
    }

    private static void AddQualityCard(View current, Thing thing)
    {
        if (!thing.TryGetQuality(out QualityCategory quality))
        {
            return;
        }

        string title = "Quality".Translate().Resolve();
        CoverLabel(current, title);
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = QualityTex, Caption = title, Value = quality.GetLabel().CapitalizeFirst(),
            Tip = () => Tip(thing.LabelCap, title + ": " + quality.GetLabel()) + "\n\n" + "ClickForMoreInfo".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor),
            Click = () => Find.WindowStack.Add(new Dialog_InfoCard(thing))
        });
    }

    // Open, held open or closed, and how long it takes to open.
    private static void AddDoorCards(View current, Building_Door door)
    {
        string stateTitle = TextCache.Key("VUIP.ObjectPaneDoorShort");
        string state = TextCache.Key(door.HoldOpen ? "VUIP.ObjectPaneDoorHeldOpen" : door.Open ? "VUIP.ObjectPaneDoorOpen" : "VUIP.ObjectPaneDoorClosed");
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = DoorTex, Caption = stateTitle, Value = state, ValueColor = door.HoldOpen ? PawnReadoutDrawer.WarningTextColor : Color.white,
            Tip = () => Tip(stateTitle, TextCache.Key("VUIP.ObjectPaneDoorTip"))
        });

        string speedTitle = TextCache.Key("VUIP.ObjectPaneOpenTimeShort");
        float seconds = door.TicksToOpenNow.TicksToSeconds();
        current.Cards.Add(new ReadoutCard
        {
            Icon = 4, Caption = speedTitle, Value = seconds.ToString("0.0") + " s",
            Tip = () => Tip(speedTitle, "VUIP.ObjectPaneOpenTimeTip".Translate(seconds.ToString("0.0")).Resolve())
        });
    }

    // Damage per second, range, what it is shooting at and a mortar's shell.
    private static void AddTurretCards(View current, Building_TurretGun turret, HashSet<string> covered)
    {
        Verb? verb = turret.gun?.TryGetComp<CompEquippable>()?.PrimaryVerb;
        if (verb == null)
        {
            return;
        }

        float dps = TurretDps(turret, verb, out string tip);
        string dpsTitle = TextCache.Key("VUIP.PawnPaneRangedDpsShort");
        current.Cards.Add(new ReadoutCard
        {
            Icon = 6, Caption = dpsTitle, Value = dps >= 0f ? dps.ToString("0.00") : "-", ValueColor = dps >= 0f ? Color.white : PawnReadoutDrawer.MutedColor,
            Tip = () => tip
        });

        string rangeTitle = TextCache.Key("VUIP.ObjectPaneRangeShort");
        float range = verb.verbProps.range;
        float minRange = verb.verbProps.minRange;
        PrefixCover minLines = CoverLabel(current, "MinimumRange".Translate());
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = RangeTex, Caption = rangeTitle, Value = minRange > 0f ? minRange.ToString("F0") + " - " + range.ToString("F0") : range.ToString("F0"),
            Tip = () => Tip(rangeTitle, minLines.Joined + "VUIP.ObjectPaneRangeTip".Translate(range.ToString("F0")).Resolve())
        });

        string targetTitle = TextCache.Key("VUIP.ObjectPaneTargetShort");
        Thing? target = turret.CurrentTarget.Thing;
        current.Cards.Add(new ReadoutCard
        {
            IconInstance = target, IconTexture = target == null ? TargetTex : null, Caption = targetTitle,
            Value = target != null ? target.LabelShortCap : TextCache.Key(turret.Active ? "VUIP.ObjectPaneTurretIdle" : "VUIP.ObjectPaneTurretInactive"),
            ValueColor = target != null ? ColorLibrary.RedReadable : turret.Active ? PawnReadoutDrawer.MutedColor : PawnReadoutDrawer.WarningTextColor,
            Tip = () => Tip(targetTitle, target != null ? target.LabelCap.ToString() : TextCache.Key(turret.Active ? "VUIP.ObjectPaneTurretIdleTip" : "VUIP.ObjectPaneTurretInactiveTip"))
        });

        if (turret.gun!.TryGetComp<CompChangeableProjectile>() is CompChangeableProjectile shell)
        {
            covered.Add("ShellNotLoaded".Translate().Resolve());
            CoverKeyStart(current, "ShellLoaded");
            string shellTitle = TextCache.Key("VUIP.ObjectPaneShellShort");
            ThingDef? loaded = shell.LoadedShell;
            current.Cards.Add(new ReadoutCard
            {
                IconThing = loaded, IconTexture = loaded == null ? TargetTex : null, Caption = shellTitle,
                Value = loaded != null ? loaded.LabelCap.ToString() : TextCache.Key("VUIP.ObjectPaneEmpty"),
                ValueColor = loaded != null ? Color.white : PawnReadoutDrawer.WarningTextColor,
                Tip = () => Tip(shellTitle, loaded != null ? loaded.LabelCap.ToString() : "ShellNotLoaded".Translate().Resolve())
            });
        }
    }

    // Every shot hitting, with the turret's own warmup and burst cooldown (Building_TurretGun.BurstCooldownTime).
    private static float TurretDps(Building_TurretGun turret, Verb verb, out string tip)
    {
        string title = TextCache.Key("VUIP.ObjectPaneTurretDpsTitle").AsTipTitle() + "\n\n";
        bool ceVerb = CombatExtendedRanged.IsLauncher(verb);
        ProjectileProperties? projectile = ceVerb ? CombatExtendedRanged.Projectile(verb)?.projectile : (verb as Verb_LaunchProjectile)?.Projectile?.projectile;
        if (projectile == null)
        {
            tip = title + TextCache.Key("VUIP.PawnPaneRangedDpsUnknown");
            return -1f;
        }

        int damage = projectile.GetDamageAmount(turret.gun) * (ceVerb ? CombatExtendedRanged.PelletCount(projectile) : 1);
        int shots = verb.BurstShotCount;
        float warmup = turret.def.building.turretBurstWarmupTime.Average;
        float cooldown = turret.def.building.turretBurstCooldownTime >= 0f ? turret.def.building.turretBurstCooldownTime : verb.verbProps.defaultCooldownTime;
        float burst = ((shots - 1) * verb.TicksBetweenBurstShots).TicksToSeconds();
        float cycle = warmup + cooldown + burst;
        if (cycle <= 0f || damage <= 0)
        {
            tip = title + TextCache.Key("VUIP.PawnPaneRangedDpsUnknown");
            return -1f;
        }

        tip = title + "VUIP.PawnPaneRangedDpsDamage".Translate(damage, shots) + "\n"
            + "VUIP.PawnPaneRangedDpsCycle".Translate(cycle.ToString("0.00"), warmup.ToString("0.00"), cooldown.ToString("0.00"), burst.ToString("0.00")) + "\n\n"
            + TextCache.Key("VUIP.ObjectPaneTurretDpsNote").Colorize(ColoredText.SubtleGrayColor);
        return damage * shots / cycle;
    }

    // Crop, growth, plants ready, growing season and fertility, as for a growing zone.
    private static void AddGrowerCards(View current, Building_PlantGrower grower, HashSet<string> covered)
    {
        ThingDef crop = grower.GetPlantDefToGrow();
        if (crop == null)
        {
            return;
        }

        int cells = grower.def.Size.Area;
        int plants = 0;
        int ready = 0;
        float growth = 0f;
        foreach (Plant plant in grower.PlantsOnMe)
        {
            if (plant.def != crop)
            {
                continue;
            }

            plants++;
            growth += plant.Growth;
            if (plant.HarvestableNow)
            {
                ready++;
            }
        }

        current.Cards.Add(new ReadoutCard
        {
            IconThing = crop, Caption = crop.LabelCap, Value = plants + " / " + cells,
            Tip = () => Tip(crop.LabelCap, "VUIP.ZonePanePlantedTip".Translate(plants, Find.ActiveLanguageWorker.Pluralize(crop.label, plants), cells).Resolve())
                + "\n\n" + "ClickForMoreInfo".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor),
            Click = () => Find.WindowStack.Add(new Dialog_InfoCard(crop))
        });

        string growthTitle = TextCache.Key("VUIP.ZonePaneGrowthShort");
        float average = plants > 0 ? growth / plants : 0f;
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = PawnReadoutDrawer.GrowthTex, Caption = growthTitle, Value = plants > 0 ? average.ToStringPercent() : "-",
            ValueColor = plants > 0 ? Color.white : PawnReadoutDrawer.MutedColor, Tip = () => Tip(growthTitle, "VUIP.ObjectPaneAverageGrowth".Translate(average.ToStringPercent()).Resolve())
        });

        string readyTitle = TextCache.Key("VUIP.ZonePaneReadyShort");
        bool harvestable = crop.plant.Harvestable;
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = PawnReadoutDrawer.HarvestTex, Caption = readyTitle, Value = harvestable ? ready.ToString() : "-",
            ValueColor = !harvestable ? PawnReadoutDrawer.MutedColor : ready > 0 ? PawnReadoutDrawer.GoodColor : Color.white,
            Tip = () => Tip(readyTitle, harvestable
                ? "VUIP.ZonePaneReadyTip".Translate(ready, crop.label, crop.plant.harvestMinGrowth.ToStringPercent()).Resolve()
                : "VUIP.ZonePaneNotHarvestable".Translate(crop.label).Resolve())
        });

        if (grower.Spawned)
        {
            bool season = PlantUtility.GrowthSeasonNow(grower.Position, grower.Map, crop);
            string seasonText = (season ? "GrowSeasonHereNow" : "CannotGrowBadSeasonTemperature").Translate().Resolve();
            covered.Add(seasonText);
            string seasonTitle = TextCache.Key("VUIP.ZonePaneSeasonShort");
            current.Cards.Add(new ReadoutCard
            {
                IconTexture = PawnReadoutDrawer.SeasonTex, Caption = seasonTitle, Value = TextCache.Key(season ? "VUIP.ObjectPaneSeasonNow" : "VUIP.ObjectPaneSeasonNotNow"),
                ValueColor = season ? PawnReadoutDrawer.GoodColor : ColorLibrary.RedReadable, Tip = () => Tip(seasonTitle, seasonText)
            });
        }

        string fertilityTitle = "Fertility".Translate().Resolve();
        float fertility = grower.def.fertility;
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = PawnReadoutDrawer.FertilityTex, Caption = fertilityTitle, Value = fertility.ToStringPercent(),
            Tip = () => Tip(fertilityTitle, "VUIP.ObjectPaneGrowerFertilityTip".Translate(fertility.ToStringPercent()).Resolve())
        });
    }

    // Wort or beer held, fermentation progress and temperature against the ideal range.
    private static void AddBarrelCards(View current, Building_FermentingBarrel barrel, HashSet<string> covered)
    {
        const int capacity = Building_FermentingBarrel.MaxCapacity;
        int held = capacity - barrel.SpaceLeftForWort;
        bool fermented = barrel.Fermented;
        covered.Add((fermented ? "ContainsBeer" : "ContainsWort").Translate(held, capacity).Resolve());
        covered.Add("Fermented".Translate().Resolve());
        PrefixCover? progressLines = CoverKeyStart(current, "FermentationProgress");
        PrefixCover? slowLines = CoverKeyStart(current, "FermentationBarrelOutOfIdealTemperature");
        PrefixCover temperatureLines = CoverLabel(current, "Temperature".Translate());
        PrefixCover idealLines = CoverLabel(current, "IdealFermentingTemperature".Translate());

        string contentsTitle = "Contents".Translate().Resolve();
        ThingDef product = fermented ? ThingDefOf.Beer : ThingDefOf.Wort;
        current.Cards.Add(new ReadoutCard
        {
            IconThing = product, Caption = contentsTitle, Value = held > 0 ? held + " / " + capacity : TextCache.Key("VUIP.ObjectPaneEmpty"),
            ValueColor = held > 0 ? Color.white : PawnReadoutDrawer.MutedColor,
            Tip = () => Tip(contentsTitle, held > 0 ? (fermented ? "ContainsBeer" : "ContainsWort").Translate(held, capacity).Resolve() : TextCache.Key("VUIP.ObjectPaneEmptyTip"))
        });

        if (held > 0)
        {
            string progressTitle = TextCache.Key("VUIP.ObjectPaneProgressShort");
            current.Cards.Add(new ReadoutCard
            {
                IconTexture = ProgressTex, Caption = progressTitle, Value = fermented ? "Fermented".Translate().Resolve() : barrel.Progress.ToStringPercent(),
                ValueColor = fermented ? PawnReadoutDrawer.GoodColor : Color.white,
                Tip = () => Tip(progressTitle, (Joined(progressLines) + Joined(slowLines)).TrimEndNewlines().NullOrEmpty()
                    ? (fermented ? "Fermented".Translate().Resolve() : barrel.Progress.ToStringPercent())
                    : (Joined(progressLines) + Joined(slowLines)).TrimEndNewlines())
            });
        }

        float temperature = barrel.AmbientTemperature;
        float max = barrel.GetComp<CompTemperatureRuinable>()?.Props.maxSafeTemperature ?? 100f;
        bool ideal = temperature >= Building_FermentingBarrel.MinIdealTemperature && temperature <= max;
        string temperatureTitle = "Temperature".Translate().Resolve();
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = TemperatureTex, Caption = temperatureTitle, Value = temperature.ToStringTemperature("F0"),
            ValueColor = ideal ? Color.white : PawnReadoutDrawer.WarningTextColor,
            Tip = () => Tip(temperatureTitle, (temperatureLines.Joined + idealLines.Joined).TrimEndNewlines())
        });
    }

    // Who is buried here, or who it is assigned to.
    private static void AddGraveCards(View current, Building_Grave grave, HashSet<string> covered)
    {
        CoverLabel(current, "CasketContains".Translate());
        CoverLabel(current, "AssignedColonist".Translate());
        PrefixCover? died = CoverKeyStart(current, "DiedOn");
        string title = TextCache.Key("VUIP.ObjectPaneBuriedShort");
        Pawn? buried = grave.HasCorpse ? grave.Corpse.InnerPawn : null;
        Pawn? assigned = grave.AssignedPawn;
        current.Cards.Add(new ReadoutCard
        {
            IconInstance = buried ?? assigned, IconTexture = buried == null && assigned == null ? GraveTex : null,
            Caption = buried == null && assigned != null ? TextCache.Key("VUIP.ObjectPaneReservedShort") : title,
            Value = (buried ?? assigned)?.LabelShortCap ?? TextCache.Key("VUIP.ObjectPaneEmpty"),
            ValueColor = buried != null ? Color.white : PawnReadoutDrawer.MutedColor,
            Tip = () => Tip(title, buried != null ? buried.LabelCap + "\n" + Joined(died).TrimEndNewlines()
                : assigned != null ? "AssignedColonist".Translate() + ": " + assigned.LabelCap : TextCache.Key("VUIP.ObjectPaneEmptyTip"))
        });
    }

    // What a casket (cryptosleep casket, ancient casket) holds, if known.
    private static void AddCasketCard(View current, Building_Casket casket, HashSet<string> covered)
    {
        CoverLabel(current, "CasketContains".Translate());
        string title = "Contents".Translate().Resolve();
        bool known = ContentsKnown == null || ContentsKnown(casket);
        Thing? held = casket.ContainedThing;
        string body = known ? casket.SearchableContents.ContentsString.CapitalizeFirst() : "UnknownLower".Translate().Resolve().CapitalizeFirst();
        current.Cards.Add(new ReadoutCard
        {
            IconInstance = known ? held : null, IconTexture = known && held != null ? null : GraveTex, Caption = title,
            Value = !known ? "UnknownLower".Translate().Resolve().CapitalizeFirst() : held != null ? held.LabelShortCap : TextCache.Key("VUIP.ObjectPaneEmpty"),
            ValueColor = known && held != null ? Color.white : PawnReadoutDrawer.MutedColor, Tip = () => Tip(title, body)
        });
    }

    // Pawn or thing held by an enterable building (Biotech and Ideology machines).
    private static void AddOccupantCard(View current, Pawn? occupant)
    {
        string title = TextCache.Key("VUIP.ObjectPaneOccupantShort");
        current.Cards.Add(new ReadoutCard
        {
            IconInstance = occupant, IconTexture = occupant == null ? WorkerTex : null, Caption = title,
            Value = occupant?.LabelShortCap ?? TextCache.Key("VUIP.ObjectPaneEmpty"), ValueColor = occupant != null ? Color.white : PawnReadoutDrawer.MutedColor,
            Tip = () => Tip(title, occupant?.LabelCap ?? TextCache.Key("VUIP.ObjectPaneEmptyTip")),
            Click = occupant == null || !occupant.Spawned ? null : () => CameraJumper.TryJumpAndSelect(occupant)
        });
    }

    // Transport pods and shuttles: contents and mass.
    private static void AddTransporterCards(View current, CompTransporter transporter, HashSet<string> covered)
    {
        Cover(covered, transporter.CompInspectStringExtra());
        string contentsTitle = "Contents".Translate().Resolve();
        int count = transporter.innerContainer.TotalStackCount;
        string contents = transporter.innerContainer.ContentsString.CapitalizeFirst();
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = ContentsTex, Caption = contentsTitle, Value = count > 0 ? count.ToString() : TextCache.Key("VUIP.ObjectPaneEmpty"),
            ValueColor = count > 0 ? Color.white : PawnReadoutDrawer.MutedColor, Tip = () => Tip(contentsTitle, contents)
        });

        if (transporter.Props.showMassInInspectString)
        {
            string massTitle = "Mass".Translate().Resolve();
            float used = transporter.MassUsage;
            float capacity = transporter.MassCapacity;
            current.Cards.Add(new ReadoutCard
            {
                IconTexture = MassTex, Caption = massTitle, Value = used.ToString("F0") + " / " + capacity.ToString("F0") + " kg",
                ValueColor = used > capacity ? ColorLibrary.RedReadable : Color.white, Tip = () => Tip(massTitle, "VUIP.ObjectPaneTransporterMassTip".Translate(used.ToString("F0"), capacity.ToString("F0")).Resolve())
            });
        }
    }

    // What is being built, work done and materials delivered.
    private static void AddFrameCards(View current, Frame frame, HashSet<string> covered)
    {
        string buildsTitle = TextCache.Key("VUIP.ObjectPaneBuildsShort");
        ThingDef? builds = frame.BuildDef;
        string buildLabel = frame.LabelEntityToBuild.CapitalizeFirst();
        current.Cards.Add(new ReadoutCard
        {
            IconThing = builds, IconTexture = builds == null ? BuildTex : null, Caption = buildsTitle, Value = buildLabel, Tip = () => Tip(buildsTitle, buildLabel),
            Click = builds == null ? null : () => Find.WindowStack.Add(new Dialog_InfoCard(builds, frame.Stuff))
        });

        PrefixCover workLines = CoverLabel(current, "WorkLeft".Translate());
        string workTitle = TextCache.Key("VUIP.ObjectPaneWorkDoneShort");
        float done = frame.PercentComplete;
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = BuildTex, Caption = workTitle, Value = done.ToStringPercent(), Tip = () => Tip(workTitle, workLines.Joined.TrimEndNewlines())
        });

        covered.Add("ContainedResources".Translate() + ":");
        List<ThingDefCountClass> costs = frame.def.entityDefToBuild.CostListAdjusted(frame.Stuff);
        int delivered = 0;
        int needed = 0;
        StringBuilder lines = new StringBuilder();
        for (int i = 0; i < costs.Count; i++)
        {
            ThingDefCountClass cost = costs[i];
            int have = cost.count - frame.ThingCountNeeded(cost.thingDef);
            delivered += have;
            needed += cost.count;
            // Same line as Frame.GetInspectString.
            string line = $"{cost.thingDef.LabelCap}: {have} / {cost.count}";
            covered.Add(line);
            lines.AppendLine(line);
        }

        if (needed > 0)
        {
            string materialsTitle = "ContainedResources".Translate().Resolve();
            string body = lines.ToString().TrimEndNewlines();
            current.Cards.Add(new ReadoutCard
            {
                IconThing = costs[0].thingDef, Caption = TextCache.Key("VUIP.ObjectPaneMaterialsShort"), Value = delivered + " / " + needed,
                ValueColor = delivered >= needed ? PawnReadoutDrawer.GoodColor : PawnReadoutDrawer.WarningTextColor, Tip = () => Tip(materialsTitle, body)
            });
        }
    }

    // The resource under a deep drill and progress to the next portion.
    private static void AddDrillCards(View current, Thing drillThing, CompDeepDrill drill, HashSet<string> covered)
    {
        string text = drill.CompInspectStringExtra() ?? string.Empty;
        Cover(covered, text);
        if (!drillThing.Spawned)
        {
            return;
        }

        DeepDrillUtility.GetNextResource(drillThing.Position, drillThing.Map, out ThingDef resource, out int _, out IntVec3 _);
        string resourceTitle = "ResourceBelow".Translate().Resolve();
        current.Cards.Add(new ReadoutCard
        {
            IconThing = resource, IconTexture = resource == null ? BuildTex : null, Caption = resourceTitle,
            Value = resource?.LabelCap.ToString() ?? "None".Translate().Resolve(), ValueColor = resource != null ? Color.white : PawnReadoutDrawer.MutedColor,
            Tip = () => Tip(resourceTitle, text)
        });

        if (resource != null)
        {
            string progressTitle = TextCache.Key("VUIP.ObjectPaneProgressShort");
            current.Cards.Add(new ReadoutCard
            {
                IconTexture = ProgressTex, Caption = progressTitle, Value = drill.ProgressToNextPortionPercent.ToStringPercent("F0"), Tip = () => Tip(progressTitle, text)
            });
        }
    }

    // A building's art: its title, with the author in the tooltip.
    private static void AddArtCard(View current, Thing thing, HashSet<string> covered)
    {
        if (thing.TryGetComp<CompArt>() is not CompArt art || !art.Active)
        {
            return;
        }

        string text = art.CompInspectStringExtra() ?? string.Empty;
        Cover(covered, text);
        string title = TextCache.Key("VUIP.ObjectPaneArtShort");
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = ArtTex, Caption = title, Value = art.Title.CapitalizeFirst(), Tip = () => Tip(title, text) + Hint("VUIP.ObjectPaneArtClick"),
            Click = () => Find.WindowStack.Add(new Dialog_InfoCard(thing))
        });
    }
}
