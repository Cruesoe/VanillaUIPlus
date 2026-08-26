using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(MainButtonsRoot), "DoButtons")]
public static class Patch_MainButtonsRoot_DoButtons
{
    public static bool Prefix()
    {
        if (!MainBarMod.Enabled || Current.ProgramState != ProgramState.Playing)
        {
            return true;
        }

        if (Event.current.type == EventType.Layout)
        {
            return false;
        }

        MainButtonLayout.DrawBar();
        return false;
    }
}
