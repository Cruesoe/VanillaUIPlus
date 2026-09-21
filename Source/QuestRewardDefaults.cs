using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

public static class QuestRewardDefaults
{
    private const float ButtonWidth = 160f;
    private const float ButtonHeight = 32f;

    public static bool IsSet => UiPlusMod.Settings.defaultGoodwillRewards.Count > 0
        || UiPlusMod.Settings.defaultRoyalFavorRewards.Count > 0;

    public static void SaveCurrent()
    {
        Dictionary<string, bool> goodwill = new Dictionary<string, bool>(UiPlusMod.Settings.defaultGoodwillRewards);
        Dictionary<string, bool> royalFavor = new Dictionary<string, bool>(UiPlusMod.Settings.defaultRoyalFavorRewards);
        foreach (Faction faction in Find.FactionManager.AllFactionsVisibleInViewOrder)
        {
            if (faction.IsPlayer)
            {
                continue;
            }

            // Faction names and instances change between worlds, while the def is
            // stable. Multiple factions of the same type therefore share defaults.
            string defName = faction.def.defName;
            if (faction.CanEverGiveGoodwillRewards)
            {
                goodwill[defName] = faction.allowGoodwillRewards;
            }

            if (faction.def.HasRoyalTitles)
            {
                royalFavor[defName] = faction.allowRoyalFavorRewards;
            }
        }

        UiPlusMod.Settings.defaultGoodwillRewards = goodwill;
        UiPlusMod.Settings.defaultRoyalFavorRewards = royalFavor;
        UiPlusMod.Instance.WriteSettings();
        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        Messages.Message("VUIP.QuestRewardDefaultsSaved".Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
    }

    public static void Apply(Faction faction)
    {
        if (faction.def == null)
        {
            return;
        }

        string defName = faction.def.defName;
        if (UiPlusMod.Settings.defaultGoodwillRewards.TryGetValue(defName, out bool allowGoodwill))
        {
            faction.allowGoodwillRewards = allowGoodwill;
        }

        if (UiPlusMod.Settings.defaultRoyalFavorRewards.TryGetValue(defName, out bool allowRoyalFavor))
        {
            faction.allowRoyalFavorRewards = allowRoyalFavor;
        }
    }

    public static void Clear()
    {
        UiPlusMod.Settings.defaultGoodwillRewards.Clear();
        UiPlusMod.Settings.defaultRoyalFavorRewards.Clear();
        UiPlusMod.Instance.WriteSettings();
    }

    public static void DrawSaveButton(Rect inRect)
    {
        Rect button = new Rect(inRect.xMax - ButtonWidth, inRect.y + 4f, ButtonWidth, ButtonHeight);
        TooltipHandler.TipRegion(button, "VUIP.SetQuestRewardDefaultsTip".Translate());
        if (Widgets.ButtonText(button, "VUIP.SetAsDefault".Translate()))
        {
            SaveCurrent();
        }
    }
}

[HarmonyPatch(typeof(Dialog_RewardPrefsConfig), nameof(Dialog_RewardPrefsConfig.DoWindowContents))]
public static class Patch_Dialog_RewardPrefsConfig_DoWindowContents
{
    public static void Postfix(Rect inRect)
    {
        QuestRewardDefaults.DrawSaveButton(inRect);
    }
}

[HarmonyPatch(typeof(FactionManager), nameof(FactionManager.Add))]
public static class Patch_FactionManager_Add
{
    public static void Prefix(Faction faction)
    {
        QuestRewardDefaults.Apply(faction);
    }
}
