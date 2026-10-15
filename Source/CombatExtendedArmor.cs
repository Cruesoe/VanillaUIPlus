using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Combat Extended's overall armor, calculated as its Gear tab does (ITab_Inventory.RebuildArmorCache and TryDrawOverallArmor), through reflection.
/// </summary>
public static class CombatExtendedArmor
{
    private static bool resolved;
    private static Func<Apparel, StatDef, BodyPartRecord, float>? apparelStat;
    private static Func<Pawn, StatDef, BodyPartRecord, float>? pawnStat;
    private static Func<Pawn, bool>? isCrouching;
    private static Type? shieldType;
    private static Type? shieldExtensionType;
    private static Func<object, BodyPartRecord, bool, bool>? coveredByShield;

    /// <summary>Whether Combat Extended's armor methods were found.</summary>
    public static bool Available
    {
        get
        {
            Resolve();
            return apparelStat != null && pawnStat != null;
        }
    }

    // Coverage-weighted average over the outer parts CE lists; sharp is in mm RHA, blunt in MPa, heat a fraction.
    public static float Overall(Pawn pawn, StatDef stat)
    {
        if (!Available)
        {
            return 0f;
        }

        List<Apparel>? apparel = pawn.apparel?.WornApparel;
        Apparel? shield = null;
        if (apparel != null && shieldType != null)
        {
            for (int i = 0; i < apparel.Count; i++)
            {
                if (shieldType.IsInstanceOfType(apparel[i]))
                {
                    shield = apparel[i];
                    break;
                }
            }
        }

        bool crouching = shield != null && isCrouching != null && isCrouching(pawn);
        object? shieldExtension = shield != null && shieldExtensionType != null ? ModExtension(shield.def, shieldExtensionType) : null;
        float total = 0f;
        float coverage = 0f;
        List<BodyPartRecord> parts = pawn.RaceProps.body.AllParts;
        for (int i = 0; i < parts.Count; i++)
        {
            BodyPartRecord part = parts[i];
            if (part.depth != BodyPartDepth.Outside || (part.coverage < 0.1f
                && !part.def.tags.Contains(BodyPartTagDefOf.BreathingPathway) && !part.def.tags.Contains(BodyPartTagDefOf.SightSource)))
            {
                continue;
            }

            float value = pawnStat!(pawn, stat, part);
            if (apparel != null)
            {
                for (int j = 0; j < apparel.Count; j++)
                {
                    if (apparel[j] != shield)
                    {
                        value += apparelStat!(apparel[j], stat, part);
                    }
                }

                if (shield != null && !shield.def.apparel.CoversBodyPart(part) && shieldExtension != null
                    && coveredByShield != null && coveredByShield(shieldExtension, part, crouching))
                {
                    value += shield.GetStatValue(stat);
                }
            }

            total += value * part.coverage;
            coverage += part.coverage;
        }

        return coverage > 0f ? total / coverage : 0f;
    }

    private static object? ModExtension(Def def, Type type)
    {
        List<DefModExtension>? extensions = def.modExtensions;
        if (extensions == null)
        {
            return null;
        }

        for (int i = 0; i < extensions.Count; i++)
        {
            if (type.IsInstanceOfType(extensions[i]))
            {
                return extensions[i];
            }
        }

        return null;
    }

    private static void Resolve()
    {
        if (resolved)
        {
            return;
        }

        resolved = true;
        Type? utility = AccessTools.TypeByName("CombatExtended.ArmorUtilityCE");
        apparelStat = ReflectionGuard.Delegate<Func<Apparel, StatDef, BodyPartRecord, float>>("ArmorUtilityCE", "PartialStat(Apparel)",
            utility == null ? null : AccessTools.Method(utility, "PartialStat", new[] { typeof(Apparel), typeof(StatDef), typeof(BodyPartRecord) }));
        pawnStat = ReflectionGuard.Delegate<Func<Pawn, StatDef, BodyPartRecord, float>>("ArmorUtilityCE", "PartialStat(Pawn)",
            utility == null ? null : AccessTools.Method(utility, "PartialStat", new[] { typeof(Pawn), typeof(StatDef), typeof(BodyPartRecord) }));

        // Shields are optional: without them the armor still matches CE for pawns that carry none.
        shieldType = AccessTools.TypeByName("CombatExtended.Apparel_Shield");
        shieldExtensionType = AccessTools.TypeByName("CombatExtended.ShieldDefExtension");
        Type? ceUtility = AccessTools.TypeByName("CombatExtended.CE_Utility");
        if (ceUtility != null && AccessTools.Method(ceUtility, "IsCrouching", new[] { typeof(Pawn) }) is { } crouch)
        {
            isCrouching = ReflectionGuard.Delegate<Func<Pawn, bool>>("CE_Utility", "IsCrouching", crouch);
        }

        if (shieldExtensionType != null
            && AccessTools.Method(shieldExtensionType, "PartIsCoveredByShield", new[] { typeof(BodyPartRecord), typeof(bool) }) is { } covered)
        {
            coveredByShield = (extension, part, crouching) => (bool)covered.Invoke(extension, new object[] { part, crouching });
        }
    }
}
