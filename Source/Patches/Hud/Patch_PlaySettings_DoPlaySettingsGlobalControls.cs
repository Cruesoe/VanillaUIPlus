using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
public static class Patch_PlaySettings_DoPlaySettingsGlobalControls
{
    public static void Prefix(bool worldView)
    {
        if (!UiPlusMod.Enabled)
        {
            return;
        }

        PlayButtonFilter.Begin(worldView);
    }

    public static void Postfix(WidgetRow row, bool worldView)
    {
        PlayButtonFilter.DrawSettingsButton(row, worldView);
        if (PlayButtonFilter.Filtering)
        {
            PlayButtonFilter.End();
        }
    }

    // Clears the filter if the original throws, or icons would stay hidden across the UI.
    public static void Finalizer()
    {
        PlayButtonFilter.Abort();
    }
}
