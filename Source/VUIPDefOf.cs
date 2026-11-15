using RimWorld;
using Verse;

namespace VanillaUIPlus;

[DefOf]
public static class VUIPDefOf
{
    public static KeyBindingDef VUIP_UnforbidAll = null!;
    public static KeyBindingDef VUIP_ToggleTemperatureOverlay = null!;
    public static KeyBindingDef VUIP_ToggleDevMode = null!;
    public static JobDef VUIP_RemoveApparelToInventory = null!;
    public static JobDef VUIP_WearFromInventory = null!;
    public static StatDef MeleeWeapon_AverageArmorPenetration = null!;

    static VUIPDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(VUIPDefOf));
    }
}
