using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Combat Extended's projectile for a ranged verb (the loaded ammo's when there is one) and its pellet count, through reflection.
/// </summary>
public static class CombatExtendedRanged
{
    private static bool resolved;
    private static Type? verbType;
    private static MethodInfo? projectileGetter;
    private static Type? propertiesType;
    private static FieldInfo? pelletCount;

    // The projectile the verb would fire now, or null if it isn't a CE launcher.
    public static ThingDef? Projectile(Verb verb)
    {
        Resolve();
        if (verbType == null || projectileGetter == null || !verbType.IsInstanceOfType(verb))
        {
            return null;
        }

        try
        {
            return projectileGetter.Invoke(verb, null) as ThingDef;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Pellets per shot (shotguns fire several), at least 1.
    public static int PelletCount(ProjectileProperties projectile)
    {
        Resolve();
        if (pelletCount == null || propertiesType == null || !propertiesType.IsInstanceOfType(projectile))
        {
            return 1;
        }

        return Math.Max(1, (int)pelletCount.GetValue(projectile));
    }

    private static void Resolve()
    {
        if (resolved)
        {
            return;
        }

        resolved = true;
        verbType = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
        projectileGetter = verbType == null ? null : AccessTools.PropertyGetter(verbType, "Projectile");
        propertiesType = AccessTools.TypeByName("CombatExtended.ProjectilePropertiesCE");
        pelletCount = propertiesType == null ? null : AccessTools.Field(propertiesType, "pelletCount");
        if (verbType != null && projectileGetter == null)
        {
            Log.Warning("[Vanilla UI+] Combat Extended's Verb_LaunchProjectileCE.Projectile was not found; ranged DPS is unavailable for its weapons.");
        }
    }
}
