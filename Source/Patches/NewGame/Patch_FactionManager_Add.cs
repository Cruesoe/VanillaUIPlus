using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(FactionManager), nameof(FactionManager.Add))]
public static class Patch_FactionManager_Add
{
    public static void Prefix(Faction faction)
    {
        QuestRewardDefaults.Apply(faction);
    }
}
