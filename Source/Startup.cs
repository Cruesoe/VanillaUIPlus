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

        // Patches class by class, so one failing patch only disables its own feature.
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

        StorageTabSelection.CollapseAll();
        MainButtonLayout.EnsureInitialized();
    }
}
