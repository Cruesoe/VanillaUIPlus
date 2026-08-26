# Vanilla UI+

Small RimWorld UI improvements that stay close to the vanilla look.

RimWorld 1.6 Harmony mod for the right-side HUD (colony and world map).

- Draws alerts, letters, date, weather, speed controls, and play-settings icons as equal-width bars (172px). Labels stay on one line with an ellipsis by default.
- Right-click snoozes that *kind* of alert for a set number of in-game days (default 3). Snoozes are saved with the world. Left-click still jumps to the problem.
- Optional temperature tint (human comfort band, about 16–26°C), outdoor temperature, day/night clock tint, and a **Day x** line under the date (first day is Day 1).
- Five speed buttons including ultrafast without development mode, key 4, right-click event slowdown, and tick-rate sliders.
- Pins **Hostiles present** above letters instead of in the top alert queue. Adds a **Bleeding out** critical alert.
- Reverse alert and letter order, hide individual play-settings buttons, or hide the speed buttons while keeping keyboard shortcuts.

Change options under **Options → Mod options → Vanilla UI+**.

Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077). Incompatible with Smart Speed (`sarg.smartspeed`).

If you previously used packageId `cruesoe.vanillauiplus.alerts`, re-enable this mod in your load order. Existing settings from `AlertsMod` are copied once into the new `UiPlusMod` config file.

## Install

Copy this folder to `RimWorld\Mods\`, or add it as a local mod in RimSort.

## Build

```
dotnet build Source\VanillaUIPlus.csproj -c Debug
```

Optional: set `RIMWORLD_DIR` (and `HARMONY_DLL` if Harmony is not in the default Workshop path).

The DLL is copied to `1.6\Assemblies\VanillaUIPlus.dll` and to `RimWorld\Mods\Vanilla UI+\1.6\Assemblies\`.
