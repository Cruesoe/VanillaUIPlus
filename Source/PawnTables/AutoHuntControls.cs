using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Auto Hunt and Auto Tame controls on the Wildlife tab: Auto Hunt's toggle with a settings cog moved to the bottom right, and both mods' controls redrawn on Numbers' wild animals table, where Numbers' tab buttons cover them.
/// </summary>
[StaticConstructorOnStartup]
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
    private static AccessTools.FieldRef<object, bool>? autoHuntEnabled;
    // Auto Hunt sets its settings once, in its mod constructor.
    private static object? autoHuntSettings;
    private static Mod? autoHuntMod;
    private static Type? autoTameDialogType;
    private static Type? numbersWindowType;
    private static AccessTools.FieldRef<object, PawnTableDef>? numbersTableDef;
    private static Texture2D? settingsIcon;

    private static bool? initialized;

    public static bool AutoHuntAvailable => autoHuntTabType != null && autoHuntSettingsField != null && autoHuntEnabled != null;

    public static bool AutoTameOnNumbersAvailable => autoTameDialogType != null && numbersTableDef != null;

    public static Type? AutoHuntTabType => autoHuntTabType;

    // The base pawn table drawing, called non-virtually so Auto Hunt's tab skips its own toggle; null if Auto Hunt's tab isn't patched.
    public static Action<MainTabWindow_PawnTable, Rect>? TableOnly { get; private set; }

    public static bool Init()
    {
        initialized ??= Resolve();
        return initialized.Value;
    }

    private static bool Resolve()
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
                autoHuntEnabled = ReflectionGuard.FieldRef<object, bool>("AutoHunt.Settings", "enabled", enabledField);
                MethodInfo? tableDraw = AccessTools.DeclaredMethod(typeof(MainTabWindow_PawnTable), nameof(MainTabWindow_PawnTable.DoWindowContents));
                if (AccessTools.DeclaredMethod(tabType, "DoWindowContents") != null && AccessTools.DeclaredPropertyGetter(tabType, "ExtraTopSpace") != null
                    && tableDraw != null)
                {
                    TableOnly = AccessTools.MethodDelegate<Action<MainTabWindow_PawnTable, Rect>>(tableDraw, virtualCall: false);
                }
            }
        }

        if (ModLister.GetActiveModWithIdentifier(NumbersPackageId, ignorePostfix: true) != null)
        {
            Type? windowType = AccessTools.TypeByName("Numbers.MainTabWindow_Numbers");
            FieldInfo? tableDefField = windowType == null ? null : AccessTools.Field(windowType, "pawnTableDef");
            if (ReflectionGuard.Found("Numbers.MainTabWindow_Numbers", "pawnTableDef", tableDefField))
            {
                numbersWindowType = windowType;
                numbersTableDef = ReflectionGuard.FieldRef<object, PawnTableDef>("Numbers.MainTabWindow_Numbers", "pawnTableDef", tableDefField);
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
        else if (autoHuntTabType != null && autoHuntTabType.IsInstanceOfType(window) && TableOnly != null)
        {
            DrawAutoHuntRow(rect);
        }
        else if (autoHuntTabType != null && autoHuntTabType.IsInstanceOfType(window))
        {
            DrawSettingsButton(new Rect(NativeCheckboxX + NativeCheckboxWidth + Gap, NativeCheckboxY + (CheckboxHeight - IconSize) / 2f, IconSize, IconSize));
        }
    }

    private static bool IsNumbersWildlife(MainTabWindow_PawnTable window)
    {
        return numbersWindowType != null && numbersWindowType.IsInstanceOfType(window)
            && numbersTableDef != null && numbersTableDef(window)?.defName == NumbersTableDefName;
    }

    // Auto Hunt's toggle and cog, right-aligned on Auto Tame's button row.
    private static void DrawAutoHuntRow(Rect rect)
    {
        object? settings = autoHuntSettings ??= autoHuntSettingsField!.GetValue(null);
        if (settings == null)
        {
            return;
        }

        bool enabled = autoHuntEnabled!(settings);
        bool before = enabled;

        GameFont font = Text.Font;
        TextAnchor anchor = Text.Anchor;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;

        string label = TextCache.Key("AutoHunt_Toggle");
        float checkboxWidth = TextCache.Width(label) + 28f;
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

        autoHuntEnabled(settings) = enabled;
        (settings as ModSettings)?.Write();
    }

    private static void DrawAutoTameButton(Rect rect)
    {
        Rect buttonRect = new Rect(rect.x, rect.yMax - RowOffsetY, AutoTameButtonWidth, RowHeight);
        TooltipHandler.TipRegion(buttonRect, TextCache.Key("VUIP.AutoTameSettingsTip"));
        if (Widgets.ButtonText(buttonRect, TextCache.Key("VUIP.AutoTameSettings")) && Activator.CreateInstance(autoTameDialogType!) is Window dialog)
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

        TooltipHandler.TipRegion(rect, TextCache.Key("VUIP.AutoHuntSettingsTip"));
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
