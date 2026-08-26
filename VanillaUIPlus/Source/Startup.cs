using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

[StaticConstructorOnStartup]
public static class Startup
{
    static Startup()
    {
        new Harmony("cruesoe.vanillauiplus").PatchAll();
    }
}
