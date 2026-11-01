using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// Stops hotkeys that run ahead of the window stack from firing while a text field has focus.
public static class HotkeyGuard
{
    // Focus only counts while a window that can hold a text field is open.
    public static bool TypingInWindow
    {
        get
        {
            if (GUIUtility.keyboardControl == 0)
            {
                return false;
            }

            foreach (Window window in Find.WindowStack.Windows)
            {
                if (window is not (MainTabWindow_Inspect or ImmediateWindow))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
