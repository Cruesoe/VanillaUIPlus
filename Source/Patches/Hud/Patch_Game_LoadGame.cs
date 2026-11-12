using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

// A loaded game keeps its saved play button states.
[HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
public static class Patch_Game_LoadGame
{
    public static void Prefix()
    {
        PlayButtonDefaults.EndNewGame();
    }
}
