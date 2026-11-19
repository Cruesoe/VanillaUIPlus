using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// RimHUD's docked pane: while it draws the inspect pane, its height and position win over ours.
/// </summary>
public static class RimHudCompat
{
    private const string PackageId = "Jaxe.RimHUD";

    private static bool resolved;
    private static Func<bool>? modifyPane;
    private static Func<float>? paneHeight;

    // True while RimHUD replaces the inspect pane with its own
    public static bool ModifiesPane
    {
        get
        {
            Resolve();
            return modifyPane != null && paneHeight != null && modifyPane();
        }
    }

    // RimHUD's pane height setting; only meaningful while ModifiesPane is true
    public static float PaneHeight => paneHeight!();

    private static void Resolve()
    {
        if (resolved)
        {
            return;
        }

        resolved = true;
        if (!ModsConfig.IsActive(PackageId))
        {
            return;
        }

        MethodInfo? modify = AccessTools.PropertyGetter(AccessTools.TypeByName("RimHUD.Engine.State"), "ModifyPane");
        MethodInfo? height = AccessTools.PropertyGetter(AccessTools.TypeByName("RimHUD.Access.Reflection"), "Wrapper_InspectPaneHeight");
        if (modify == null || height == null || modify.ReturnType != typeof(bool) || height.ReturnType != typeof(float))
        {
            Log.Warning("[Vanilla UI+] RimHUD is active but its inspect pane settings were not found; the two panes may overlap.");
            return;
        }

        modifyPane = AccessTools.MethodDelegate<Func<bool>>(modify);
        paneHeight = AccessTools.MethodDelegate<Func<float>>(height);
    }
}
