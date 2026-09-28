# NETCODE

**Status:** Current · **Last updated:** 2026-09-28 · **Verified against:** the greybox lobby commit

## What happens in the game

One player presses **Host**. A Steam lobby is created and the game starts a server on their PC.
They press **Invite** and pick a friend in the Steam overlay. The friend accepts, their game joins the lobby, reads the host's Steam ID from it and connects.
Both now stand in the greybox scene as capsules, one per player, and can walk about with WASD.
**Leave** disconnects and closes the lobby.

Stack: **FishNet 4.7.3** with the **FishySteamworks 4.1.1** transport over **Steamworks.NET 2025.164.1**. Steam relays the traffic, so no ports are opened.

## Who owns what

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Steam lobby | Steam; the host is its owner | Lobby data: `host` = host's SteamID64 | On change |
| Server | Host's PC | — | FishNet default tick, 30/s |
| Player capsule | Spawned by the host, owned by the connecting client | Position and rotation via NetworkTransform | Every tick, only when moved |
| Movement | **Temporary: the owning client** (see limitations) | — | — |

## Scripts

All under [Assets/_Project/Scripts/](../../Assets/_Project/Scripts/):

- `Net/SteamService.cs`: starts Steam on launch, pumps its callbacks every frame, shuts it down on quit. Nothing else touches `SteamAPI.Init`.
- `Net/SteamLobby.cs`: Host / Invite / Leave. Creates or joins the Steam lobby, then starts FishNet's server and/or client. Also handles a friend joining through the overlay, and `+connect_lobby` when the game was launched from an invite.
- `UI/LobbyPanel.cs`: the greybox buttons and status line. Event-driven; no per-frame work.
- `Player/GreyboxMover.cs`: placeholder WASD movement on the owning client.

Scene wiring is built by the menu item **Close the Deal > Greybox > Set Up Scene** ([GreyboxSceneSetup.cs](../../Assets/_Project/Editor/Greybox/GreyboxSceneSetup.cs)). It only adds what is missing and never rebuilds anything already in the scene.

## Known limitations

- **Movement is client-authoritative for now.** This breaks AGENTS.md §3 on purpose: it is the quickest way to see two players move, and it is replaced by host-authoritative predicted movement in the movement task. Nothing else should be built on `GreyboxMover`.
- **If the host leaves, the run ends.** No host migration; this is by design (see the GDD).
- **No rejoin.** A dropped client has to be invited again.
- Steam runs under Valve's test app id **480** until we own an app id. Anyone with a Steam account can use it.
- Lobbies are friends-only; there is no public list or matchmaking (out of scope for v1).

## Testing it

- Steam must be running and signed in on each PC.
- Two players need **two Steam accounts on two PCs**. Steam allows one signed-in client per PC.
- In the editor: open `Greybox.unity`, press Play, **Host**, **Invite**. The friend needs a build (File > Build Profiles > Windows) or their own editor on the same commit.
- `steam_appid.txt` (containing `480`) must sit next to the executable or in the project root. Steamworks.NET writes it into the project root the first time the editor opens; it is git-ignored and must never ship.

Nothing here has been seen running yet; that is the next step.
