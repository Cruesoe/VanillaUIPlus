using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

// The other Messages.Message overloads all end here.
[HarmonyPatch(typeof(Messages), nameof(Messages.Message), typeof(Message), typeof(bool))]
public static class Patch_Messages_Message
{
    public static bool Prefix()
    {
        return !AnimalGroupControls.SuppressMessages;
    }
}
