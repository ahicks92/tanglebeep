# Overnight session log — 2026-06-16/17

A running summary for the morning review, so you can see what changed and how to try it
without reading every commit. Architecture detail lives in `gameplay-access.md`,
`ui-framework.md`, `new-game-menu.md`.

## What now works

Starting from a mod that only spoke a startup line, the game is playable by ear from the
title screen into gameplay:

- **Title / save-slot / character creation** all speak. The new-game story intros, the
  image-only job buttons (full job readout derived and spoken), feat names, and the
  name-entry screen (prompt + current name + job/mode/feats; RANDOM re-reads) are audible.
- **Modal dialogs** read their full body text, then choices — via a new one-shot
  "announcement" channel in the overlay framework (reusable for tutorials, level-ups).
- **In gameplay** the turn-by-turn log is spoken (combat, status, pickups, NPC barks),
  filtered by the game's own line-of-sight and verbose settings.
- **Spatial awareness** controls (keys chosen to not collide with the game):
  - `K` read-here, `L` scan (LOS sweep by direction/distance), `Y` status (HP/stamina/
    energy/level/effects), `;` look cursor (arrow-stepped tile examiner, Home re-centers).

## How to try it

1. `./build.ps1` then launch via `./run-game.ps1` (a background task; it sets
   `TANGLEDEEP_DEV=1` and is silent unless `-Speech` is passed — turn NVDA on and pass
   `-Speech` for a real listen).
2. Drive the whole new-game flow with the game's own keys; everything should speak. Or, to
   jump straight into a game for testing, run `bash scripts/drive-newgame.sh` (uses the dev
   HTTP endpoints).
3. In gameplay, press `K`, `L`, `Y`, `;` + arrows. With speech off, read what *would* have
   been spoken via `curl -s http://127.0.0.1:8770/speech?since=0`.

The dev endpoints (`/eval`, `/speech`, `/gui/*`, `/screenshot`, `/input`) are unchanged
this session and documented in `CLAUDE.md`.

## Known gaps / next (in rough priority)

- **Feat descriptions** on the PERKSELECT screen (only feat names read today). Needs
  `CharCreation` to handle PERKSELECT and outrank the dialog overlay for that stage.
- **Look cursor** is 4-directional; numpad diagonals are an easy add.
- **Ranged targeting** (`PlayerInputTargetingManager`) has no spoken support yet.
- **Status names** are cleaned refNames, not localized; find the game's status-name source.
- **Custom name typing** in creation is deferred (default + RANDOM suffice).
- Terrain is the coarse tile type ("ground"/"water"/"wall").

## Notes / decisions worth your eye

- Character creation runs in title-screen context, where the in-game input hook never
  fires, so the mod is pure follow-and-speak there (the game drives navigation). That is
  why driving it over HTTP uses the game's own methods (e.g. `OnSelectSlotConfirmPressed`,
  `ConfirmedAndGameIsReadyToStart`) rather than synthetic input.
- All game-state reads and speech happen on the per-frame pump; Harmony hooks only set
  flags / enqueue, per the project rule. New mod control keys are `K L Y ;` + arrows/Home
  while the look cursor is active — all unbound in the game's Default layout.
