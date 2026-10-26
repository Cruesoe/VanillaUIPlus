using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Auto Hunt and Auto Tame controls on the Wildlife tab: a settings cog beside Auto Hunt's toggle, and both mods' controls redrawn on Numbers' wild animals table, where Numbers' tab buttons cover them.
/// </summary>
public static class AutoHuntControls
{
    private const string AutoHuntPackageId = "Snues.AutoHunt";
    private const string AutoTamePackageId = "ceramic.autotame";
    private const string NumbersPackageId = "Mehni.Numbers";
    private const string NumbersTableDefName = "Numbers_WildAnimals";

    // Auto Hunt's own toggle on its Wildlife tab (AutoHunt.WildlifeTabWithToggle.DoWindowContents).
    private const float NativeCheckboxX = 5f;
    private const float NativeCheckboxY = 5f;
    private const float NativeCheckboxWidth = 140f;
    private const float CheckboxHeight = 30f;

    private const float IconSize = 28f;
    private const float Pad = 6f;
    private const float Gap = 6f;

    // Auto Tame's own Wildlife-tab button row: 200x32, 40px up from the bottom.
    private const float RowOffsetY = 40f;
    private const float RowHeight = 32f;
    private const float AutoTameButtonWidth = 200f;

    private static Type? autoHuntTabType;
    private static FieldInfo? autoHuntSettingsField;
    private static FieldInfo? autoHuntEnabledField;
    private static Mod? autoHuntMod;
    private static Type? autoTameDialogType;
    private static Type? numbersWindowType;
    private static FieldInfo? numbersTableDefField;
    private static Texture2D? settingsIcon;

    public static bool AutoHuntAvailable => autoHuntTabType != null && autoHuntSettingsField != null && autoHuntEnabledField != null;

    public static bool AutoTameOnNumbersAvailable => autoTameDialogType != null && numbersTableDefField != null;

    public static bool Init()
    {
        if (ModLister.GetActiveModWithIdentifier(AutoHuntPackageId, ignorePostfix: true) != null)
        {
            Type? modType = AccessTools.TypeByName("AutoHunt.AutoHuntMod");
            Type? settingsType = AccessTools.TypeByName("AutoHunt.Settings");
            Type? tabType = AccessTools.TypeByName("AutoHunt.WildlifeTabWithToggle");
            FieldInfo? settingsField = modType == null ? null : AccessTools.Field(modType, "settings");
            FieldInfo? enabledField = settingsType == null ? null : AccessTools.Field(settingsType, "enabled");
            if (ReflectionGuard.Found("AutoHunt", "WildlifeTabWithToggle", tabType)
                && ReflectionGuard.Found("AutoHunt.AutoHuntMod", "settings", settingsField)
                && ReflectionGuard.Found("AutoHunt.Settings", "enabled", enabledField))
            {
                autoHuntTabType = tabType;
                autoHuntSettingsField = settingsField;
                autoHuntEnabledField = enabledField;
            }
        }

        if (ModLister.GetActiveModWithIdentifier(NumbersPackageId, ignorePostfix: true) != null)
        {
            Type? windowType = AccessTools.TypeByName("Numbers.MainTabWindow_Numbers");
            FieldInfo? tableDefField = windowType == null ? null : AccessTools.Field(windowType, "pawnTableDef");
            if (ReflectionGuard.Found("Numbers.MainTabWindow_Numbers", "pawnTableDef", tableDefField))
            {
                numbersWindowType = windowType;
                numbersTableDefField = tableDefField;
            }

            if (ModLister.GetActiveModWithIdentifier(AutoTamePackageId, ignorePostfix: true) != null)
            {
                Type? dialogType = AccessTools.TypeByName("AutoTame.Dialog_AutoTameSettings");
                if (ReflectionGuard.Found("AutoTame", "Dialog_AutoTameSettings", dialogType))
                {
                    autoTameDialogType = dialogType;
                }
            }
        }

        return AutoHuntAvailable || AutoTameOnNumbersAvailable;
    }

    public static void Draw(MainTabWindow_PawnTable window, Rect rect)
    {
        if (IsNumbersWildlife(window))
        {
            if (AutoHuntAvailable)
            {
                DrawAutoHuntRow(rect);
            }

            if (autoTameDialogType != null)
            {
                DrawAutoTameButton(rect);
            }
        }
        else if (autoHuntTabType != null && autoHuntTabType.IsInstanceOfType(window))
        {
            DrawSettingsButton(new Rect(NativeCheckboxX + NativeCheckboxWidth + Gap, NativeCheckboxY + (CheckboxHeight - IconSize) / 2f, IconSize, IconSize));
        }
    }

    private static bool IsNumbersWildlife(MainTabWindow_PawnTable window)
    {
        return numbersWindowType != null && numbersWindowType.IsInstanceOfType(window)
            && numbersTableDefField!.GetValue(window) is Def { defName: NumbersTableDefName };
    }

    // Auto Hunt's toggle and cog, right-aligned on Auto Tame's button row.
    private static void DrawAutoHuntRow(Rect rect)
    {
        object? settings = autoHuntSettingsField!.GetValue(null);
        if (settings == null)
        {
            return;
        }

        bool enabled = autoHuntEnabledField!.GetValue(settings) is true;
        bool before = enabled;

        GameFont font = Text.Font;
        TextAnchor anchor = Text.Anchor;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;

        string label = "AutoHunt_Toggle".Translate().Resolve();
        float checkboxWidth = Text.CalcSize(label).x + 28f;
        float rowTop = rect.yMax - RowOffsetY;
        float cogX = rect.xMax - IconSize - Pad;
        Widgets.CheckboxLabeled(
            new Rect(cogX - Gap - checkboxWidth, rowTop + (RowHeight - CheckboxHeight) / 2f, checkboxWidth, CheckboxHeight),
            label, ref enabled, placeCheckboxNearText: true);

        Text.Anchor = anchor;
        Text.Font = font;

        DrawSettingsButton(new Rect(cogX, rowTop + (RowHeight - IconSize) / 2f, IconSize, IconSize));

        if (enabled == before)
        {
            return;
        }

        autoHuntEnabledField.SetValue(settings, enabled);
        (settings as ModSettings)?.Write();
    }

    private static void DrawAutoTameButton(Rect rect)
    {
        Rect buttonRect = new Rect(rect.x, rect.yMax - RowOffsetY, AutoTameButtonWidth, RowHeight);
        TooltipHandler.TipRegion(buttonRect, "VUIP.AutoTameSettingsTip".Translate());
        if (Widgets.ButtonText(buttonRect, "VUIP.AutoTameSettings".Translate()) && Activator.CreateInstance(autoTameDialogType!) is Window dialog)
        {
            Find.WindowStack.Add(dialog);
        }
    }

    private static void DrawSettingsButton(Rect rect)
    {
        settingsIcon ??= ContentFinder<Texture2D>.Get("UI/VanillaUIPlus/AutoHuntSettings");
        if (Widgets.ButtonImage(rect, settingsIcon) && AutoHuntMod() is { } mod)
        {
            Find.WindowStack.Add(new Dialog_ModSettings(mod));
        }

        TooltipHandler.TipRegion(rect, "VUIP.AutoHuntSettingsTip".Translate());
    }

    private static Mod? AutoHuntMod()
    {
        if (autoHuntMod != null)
        {
            return autoHuntMod;
        }

        foreach (Mod mod in LoadedModManager.ModHandles)
        {
            if (mod?.Content != null && string.Equals(mod.Content.PackageIdPlayerFacing, AutoHuntPackageId, StringComparison.OrdinalIgnoreCase))
            {
                autoHuntMod = mod;
                break;
            }
        }

        return autoHuntMod;
    }
}
