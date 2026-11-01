using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(Dialog_RewardPrefsConfig), nameof(Dialog_RewardPrefsConfig.DoWindowContents))]
public static class Patch_Dialog_RewardPrefsConfig_DoWindowContents
{
    public static void Postfix(Rect inRect)
    {
        QuestRewardDefaults.DrawSaveButton(inRect);
    }
}
