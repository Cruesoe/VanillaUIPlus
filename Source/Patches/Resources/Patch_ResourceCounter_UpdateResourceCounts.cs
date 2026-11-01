using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(ResourceCounter), nameof(ResourceCounter.UpdateResourceCounts))]
public static class Patch_ResourceCounter_UpdateResourceCounts
{
    public static void Postfix()
    {
        ResourceReadoutTweaks.NotifyCountsUpdated();
    }
}
