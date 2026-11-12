using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

// A new game takes the play buttons' default states the first time each is drawn.
[HarmonyPatch(typeof(Game), nameof(Game.InitNewGame))]
public static class Patch_Game_InitNewGame
{
    public static void Postfix()
    {
        PlayButtonDefaults.BeginNewGame();
    }
}
