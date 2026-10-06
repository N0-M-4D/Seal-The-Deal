# MENU AND HUD

**Status:** Current · **Last updated:** 2026-10-03 · **Verified against:** the playtest menu commit

## What happens in the game

The game opens on a menu card over the scene: the title, whether you're on Steam or in local test mode, and one status line saying what's happening right now. Below it, **Host game**, and either a note on how to join a friend (Steam: accept their invite, or **Join Game** on their name in the friends list) or **Join game on this PC** (local). There are no lobby codes. Once you're in a game the card shows the player count, **Invite with the Steam overlay** when the overlay exists, a note that friends can join from your name, **Back to game** and **Leave game**. The controls are listed at the bottom of the card.

In game the menu is gone: a crosshair in the centre, **Esc · Menu** top-left and the network readout bottom-left. **Esc** opens the menu again. While the menu is open the mouse is free and your character stands still, so clicking never moves, jumps or fires.

Greybox: this is a playtest tool, not the final lobby. The final one lives in the reception with the secretary (GDD).

## Rules the menu keeps

- **Menu open ⇔ mouse free ⇔ player idle.** One script (`GameMenu`) owns all three so they can't disagree. If something else frees the mouse (the editor's own Esc, alt-tab), the menu opens to match.
- The menu opens by itself whenever you have no player: before joining, after leaving, and when the connection drops.
- Every wait says what it's doing in the status line ("Creating a Steam lobby…", "Joining lobby…", "Connecting to Adam's game…").
- A failed join explains itself in the status line, in plain words: friends-only, full, a version mismatch, or a host that can't be reached.
- Buttons that can't be used are dimmed, not hidden, except whole sections that don't apply in the current mode.
- Keyboard focus lands on the likeliest next button when the menu opens.

## Look

Flat dark card, amber for the one primary action per state, white text with a muted grey for secondary lines. Contrast on the card: text 15.6:1, muted 7.5:1, error 8:1, primary button label 10:1. HUD text sits on dark chips so it reads over the light grey walls. TextMeshPro throughout (essentials committed under `Assets/TextMesh Pro/`).

## Scripts and tools

- [GameMenu.cs](../../Assets/_Project/Scripts/UI/GameMenu.cs): state, buttons, the mouse. Text is rebuilt only on lobby events, never per frame.
- [PredictionDebugHud.cs](../../Assets/_Project/Scripts/UI/PredictionDebugHud.cs): the network readout; see NETCODE.md "What to watch".
- [GreyboxUiSetup.cs](../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs): builds the whole `GameUI` canvas. **Close the Deal > Greybox > Rebuild Game UI** deletes and rebuilds it after asking, so change layout, copy and colours there rather than in the scene; hand edits in the scene are lost on a rebuild.
- [GreyboxUiPreview.cs](../../Assets/_Project/Editor/Greybox/GreyboxUiPreview.cs): **Capture UI Preview** renders the start, hosting, local and in-game states to PNGs without entering Play Mode.

## Known limitations

- No settings: mouse sensitivity, volume, resolution and key rebinding don't exist yet. Sensitivity is on the Main Camera's PlayerCamera in the Inspector.
- No player names over heads and no team indicator yet.
- Square greybox corners; no motion on open and close.
