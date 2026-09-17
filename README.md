# Rain World Stay Visible

A tiny Windows-only Rain World / Remix mod that keeps the game visible and running when it loses focus, so Rain World can stay fullscreen on one monitor while you work on another.

It does **not** launch a helper app, background service, script, or tray program. It runs inside Rain World through the existing Remix/BepInEx mod loader.

## IMPORTANT: load order

**Stay Visible must be at the very top / highest priority of the Remix mod list.**

This was required in real-world testing. If it is left lower in the load order, another module can win the window/display timing and Stay Visible may appear to do nothing.

## Tested

- Rain World `v1.11.8`
- Windows 64-bit
- BepInEx `5.4.17.0`
- Stay Visible `v1.1.0`
- Multi-monitor use: Rain World remains visible while another application has focus on another monitor

## Installation

1. Close Rain World.
2. Extract the `StayVisible` folder into:

   `RainWorld_Data\StreamingAssets\mods\`

   The final layout should be:

   ```text
   RainWorld_Data\StreamingAssets\mods\StayVisible\modinfo.json
   RainWorld_Data\StreamingAssets\mods\StayVisible\plugins\StayVisible.dll
   ```

3. Start Rain World.
4. Open **REMIX**.
5. Enable **Stay Visible**.
6. Move **Stay Visible to the very top of the mod load order**.
7. Press **APPLY MODS**.
8. Restart if Rain World asks you to.

No Steam launch options are required.

## What it does

At startup the mod:

1. enables Unity's background execution,
2. locates Rain World's real top-level Windows window,
3. keeps Unity genuinely windowed,
4. removes the native Windows frame,
5. expands the window to the monitor Rain World is already using,
6. reasserts the borderless state only when application focus changes.

There is no permanent polling loop and no external resident process.

## If it does not work

Check `BepInEx\LogOutput.log`.

A successful startup should contain lines similar to:

```text
Stay Visible found Rain World window ...
Stay Visible applied: borderless ...; runInBackground=true.
```

If those lines are missing, verify that the mod is enabled and is **first / highest priority in the Remix load order**.

## Source

The complete source and reproducible GitHub Actions build are included in this repository. The windowing approach intentionally uses ordinary Unity APIs plus Win32 window APIs rather than Rain World gameplay internals, keeping the mod as small and version-resistant as practical.
