using System;
using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

[StaticConstructorOnStartup]
public static class Startup
{
    static Startup()
    {
        Harmony harmony = new Harmony("cruesoe.vanillauiplus");

        // Deliberately not Harmony.PatchAll: it stops at the first patch class that
        // throws, so one bad target takes every later patch down with it, and the failure
        // surfaces as this type initializer dying rather than as the feature at fault.
        // Patching class by class keeps one broken feature from disabling the mod.
        foreach (Type type in typeof(Startup).Assembly.GetTypes())
        {
            try
            {
                harmony.CreateClassProcessor(type).Patch();
            }
            catch (Exception exception)
            {
                Log.Error($"[Vanilla UI+] Could not apply {type.Name}. That feature is off; the rest of the mod is unaffected.\n{exception}");
            }
        }

        MainButtonLayout.EnsureInitialized();
    }
}
