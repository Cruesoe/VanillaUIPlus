using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

public static class GoodwillRewardDefaults
{
    private const float ButtonWidth = 160f;
    private const float ButtonHeight = 32f;

    public static bool IsSet => UiPlusMod.Settings.defaultGoodwillRewards.Count > 0;

    public static void SaveCurrent()
    {
        Dictionary<string, bool> defaults = new Dictionary<string, bool>(UiPlusMod.Settings.defaultGoodwillRewards);
        foreach (Faction faction in Find.FactionManager.AllFactionsVisibleInViewOrder)
        {
            if (!faction.IsPlayer && faction.CanEverGiveGoodwillRewards)
            {
                // Faction names and instances change between worlds, while the def is
                // stable. Multiple factions of the same type therefore share a default.
                defaults[faction.def.defName] = faction.allowGoodwillRewards;
            }
        }

        UiPlusMod.Settings.defaultGoodwillRewards = defaults;
        UiPlusMod.Instance.WriteSettings();
        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        Messages.Message("VUIP.GoodwillRewardDefaultsSaved".Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
    }

    public static void Apply(Faction faction)
    {
        if (faction.def != null
            && UiPlusMod.Settings.defaultGoodwillRewards.TryGetValue(faction.def.defName, out bool allow))
        {
            faction.allowGoodwillRewards = allow;
        }
    }

    public static void Clear()
    {
        UiPlusMod.Settings.defaultGoodwillRewards.Clear();
        UiPlusMod.Instance.WriteSettings();
    }

    public static void DrawSaveButton(Rect inRect)
    {
        Rect button = new Rect(inRect.xMax - ButtonWidth, inRect.y + 4f, ButtonWidth, ButtonHeight);
        TooltipHandler.TipRegion(button, "VUIP.SetGoodwillRewardDefaultsTip".Translate());
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
        GoodwillRewardDefaults.DrawSaveButton(inRect);
    }
}

[HarmonyPatch(typeof(FactionManager), nameof(FactionManager.Add))]
public static class Patch_FactionManager_Add
{
    public static void Prefix(Faction faction)
    {
        GoodwillRewardDefaults.Apply(faction);
    }
}
