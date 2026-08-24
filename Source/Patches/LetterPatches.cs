using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(LetterStack), nameof(LetterStack.LettersOnGUI))]
public static class Patch_LetterStack_LettersOnGUI
{
    public static bool Prefix(LetterStack __instance, float baseY)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        LetterDrawer.DrawLetters(__instance, baseY);
        return false;
    }
}
