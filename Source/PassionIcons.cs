using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// The icon for a skill's passion: Vanilla Skills Expanded's PassionDef icon when that mod is active (covering passions it and other mods add), otherwise the Bio tab's.
/// </summary>
public static class PassionIcons
{
    private static bool resolved;
    private static IList? passionDefs;
    private static Func<object, Texture2D>? defIcon;

    public static Texture2D? For(Passion passion)
    {
        Resolve();
        if (passionDefs != null && defIcon != null)
        {
            // Vanilla Skills Expanded stores each passion as its PassionDef's index
            int index = (int)passion;
            if (index >= 0 && index < passionDefs.Count && passionDefs[index] is object def)
            {
                return defIcon(def);
            }
        }

        return passion switch
        {
            Passion.Minor => SkillUI.PassionMinorIcon,
            Passion.Major => SkillUI.PassionMajorIcon,
            _ => null,
        };
    }

    private static void Resolve()
    {
        if (resolved)
        {
            return;
        }

        resolved = true;
        Type? manager = GenTypes.GetTypeInAnyAssembly("VSE.Passions.PassionManager");
        Type? def = GenTypes.GetTypeInAnyAssembly("VSE.Passions.PassionDef");
        if (manager == null || def == null)
        {
            return;
        }

        FieldInfo? passions = AccessTools.Field(manager, "Passions");
        if (ReflectionGuard.Found("PassionManager", "Passions", passions))
        {
            passionDefs = passions!.GetValue(null) as IList;
        }

        defIcon = ReflectionGuard.UntypedDelegate<Texture2D>("PassionDef", "Icon", AccessTools.PropertyGetter(def, "Icon"));
    }
}
