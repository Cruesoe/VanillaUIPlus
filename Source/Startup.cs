using HarmonyLib;
using Verse;

namespace VanillaUIPlus.Alerts;

[StaticConstructorOnStartup]
public static class Startup
{
    static Startup()
    {
        new Harmony("cruesoe.vanillauiplus.alerts").PatchAll();
    }
}
