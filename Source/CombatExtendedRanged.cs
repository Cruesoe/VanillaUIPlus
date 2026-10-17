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

    // True for a Combat Extended launcher verb.
    public static bool IsLauncher(Verb verb)
    {
        Resolve();
        return verbType != null && verbType.IsInstanceOfType(verb);
    }

    // The projectile the verb would fire now, or null if it isn't a CE launcher.
    public static ThingDef? Projectile(Verb verb)
    {
        if (!IsLauncher(verb) || projectileGetter == null)
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

        return pelletCount.GetValue(projectile) is int count ? Math.Max(1, count) : 1;
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
