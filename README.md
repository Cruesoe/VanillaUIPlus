# Vanilla UI+

Small RimWorld UI improvements that stay close to the vanilla look. This repository is a collection of independent 1.6 Harmony mods.

| Folder | Mod | packageId |
| --- | --- | --- |
| `VanillaUIPlus` | **Vanilla UI+** | `cruesoe.vanillauiplus` |
| `VanillaUIPlusMainBar` | **Vanilla UI+ Main Bar** | `cruesoe.vanillauiplus.mainbar` |

Use either or both. Each folder is a complete mod.

Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077). Vanilla UI+ is incompatible with Smart Speed (`sarg.smartspeed`).

## Vanilla UI+

Right-side HUD on the colony and the world map.

- Draws alerts, letters, date, weather, speed controls, and play-settings icons as equal-width bars (172px). Labels stay on one line with an ellipsis by default.
- Right-click snoozes that *kind* of alert for a set number of in-game days (default 3). Snoozes are saved with the world. Left-click still jumps to the problem.
- Optional temperature tint (human comfort band, about 16–26°C), outdoor temperature, day/night clock tint, and a **Day x** line under the date (first day is Day 1).
- Five speed buttons including ultrafast without development mode, key 4, right-click event slowdown, and tick-rate sliders.
- Pins **Hostiles present** above letters instead of in the top alert queue. Adds a **Bleeding out** critical alert.
- Reverse alert and letter order, hide individual play-settings buttons, or hide the speed buttons while keeping keyboard shortcuts.

Change options under **Options → Mod options → Vanilla UI+**.

If you previously used packageId `cruesoe.vanillauiplus.alerts`, re-enable this mod in your load order. Existing settings from `AlertsMod` are copied once into the new `UiPlusMod` config file.

## Vanilla UI+ Main Bar

Standalone bottom main menu bar.

- Reorder tabs, including a **More** overflow button.
- Move tabs into More, hide them, or keep them on the bar. Hidden tabs and tabs in More keep their hotkeys.
- Change icons and choose icon-only / text-and-icon / text-only.

Change options under **Options → Mod options → Vanilla UI+ Main Bar**.

If you used these options in an older Vanilla UI+, the layout is copied once from the old config.

## Install

Copy each mod folder you want into `RimWorld\Mods\`:

- `VanillaUIPlus`
- `VanillaUIPlusMainBar`

Or add them as local mods in RimSort. Do not copy the repository root; RimWorld needs the folder that contains `About/About.xml`.

## Build

```
dotnet build VanillaUIPlus.sln -c Debug
```

Or build one mod:

```
dotnet build VanillaUIPlus\Source\VanillaUIPlus.csproj -c Debug
dotnet build VanillaUIPlusMainBar\Source\VanillaUIPlusMainBar.csproj -c Debug
```

Optional: set `RIMWORLD_DIR` (and `HARMONY_DLL` if Harmony is not in the default Workshop path).

Each Debug build copies that mod's DLL into `1.6\Assemblies\` and into `RimWorld\Mods\<mod name>\`. Rebuild **both** assemblies after pulling this split: an older `VanillaUIPlus.dll` still contains main-bar patches and will conflict with Vanilla UI+ Main Bar.
