# Vanilla UI+ Alerts

Part of **Vanilla UI+**: small RimWorld UI improvements that stay close to the vanilla look.

RimWorld 1.6 Harmony mod for the right-side HUD.

- Draws every alert as an equal-width bar (vanilla 154px column). Labels stay on one line with an ellipsis by default.
- Right-click snoozes that alert for a set number of in-game days (default 3). Snoozes are saved with the world. Left-click still jumps to the problem.
- Letters, date, weather, speed controls, and play-settings icons use the same bar width and optional dark backgrounds.
- Adds **Bleeding out** and **Hostiles present** critical alerts.
- Hide individual play-settings buttons, or hide the speed buttons while keeping keyboard shortcuts.

Change duration, wrapping, backgrounds, or clear snoozes under **Options → Mod options → Vanilla UI+ Alerts**.

Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077).

## Install

Copy this folder to `RimWorld\Mods\`, or add it as a local mod in RimSort.

## Build

```
dotnet build Source\VanillaUIPlusAlerts.csproj -c Debug
```

The DLL is copied to `1.6\Assemblies\VanillaUIPlusAlerts.dll` and to `RimWorld\Mods\Vanilla UI+ Alerts\1.6\Assemblies\`.
