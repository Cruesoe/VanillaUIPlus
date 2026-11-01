using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

// Reopening the tab counts as a new stockpile for search focus.
[HarmonyPatch(typeof(ITab_Storage), nameof(ITab_Storage.OnOpen))]
public static class Patch_ITab_Storage_OnOpen
{
    public static void Postfix()
    {
        StorageTabSelection.Forget();
    }
}
