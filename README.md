# Vanilla UI+

Small RimWorld UI improvements that stay close to the vanilla look.

RimWorld 1.6 Harmony mod covering the right-side HUD (colony and world map) and the bottom main menu bar.

## HUD (bottom right)

- Draws alerts, letters, date, weather, speed controls, and play-settings icons as equal-width bars (172px). Labels stay on one line with an ellipsis by default.
- Optional temperature tint (human comfort band, about 16–26°C), outdoor temperature, day/night clock tint, a **Day x** line under the date (first day is Day 1), and a **colony wealth** line with an items/buildings/pawns breakdown on hover.
- Five speed buttons including ultrafast without development mode, key 4, right-click event slowdown, and tick-rate sliders.
- Reverse alert and letter order, hide individual play-settings buttons, or hide the speed buttons while keeping keyboard shortcuts.

Turning **Enable custom HUD** off restores vanilla drawing for this section only; the rest of the mod keeps working.

## Custom notifications

Alerts added by Vanilla UI+ that are not part of the base game, plus alert snoozing. Each is toggleable on its own, and all of them work whether or not the custom HUD is enabled.

- **Hostiles present** — pinned above letters when the custom HUD is on, otherwise it sits in the normal alert stack.
- **Bleeding out** — critical alert when a colonist or prisoner will die from blood loss soon.
- **Trader available** — stays up while a trade caravan or orbital trade ship can still be traded with, so a trader you were told about but forgot does not quietly leave.
- **Snoozing** (default on) — right-click an alert to hide that *kind* of alert for a set number of in-game days (default 3). Snoozes are saved with the world. Left-click still jumps to the problem. Turning snoozing off ignores existing snoozes rather than deleting them; **Clear snoozes** removes them for good.

## Main menu bar

Reorder tabs, move them into a **More** menu, hide them, change their icons, and choose icon-only, text-and-icon, or text-only. Includes a play-settings cog that opens Vanilla UI+ options.

Change options under **Options → Mod options → Vanilla UI+**.

Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077). Incompatible with Smart Speed (`sarg.smartspeed`).

If you previously used packageId `cruesoe.vanillauiplus.alerts`, re-enable this mod in your load order. Existing settings from `AlertsMod` are copied once into the new `UiPlusMod` config file.

## Install

Copy this folder to `RimWorld\Mods\`, or add it as a local mod in RimSort.

## Build

```
dotnet build Source\VanillaUIPlus.csproj -c Release
```

Use `-c Debug` for an unoptimized build with symbols. Optionally set `RIMWORLD_DIR` (and `HARMONY_DLL` if Harmony is not in the default Workshop path).

The DLL is copied to `1.6\Assemblies\VanillaUIPlus.dll` and to `RimWorld\Mods\Vanilla UI+\1.6\Assemblies\`.

Linux and cloud builds are supported via `Directory.Build.props` together with the reference assemblies fetched by `.cursor/install.sh`; on Windows that file is a no-op.
