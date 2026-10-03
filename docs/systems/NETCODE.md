# NETCODE

**Status:** Current · **Last updated:** 2026-10-03 · **Verified against:** the playtest menu commit

## What happens in the game

One player presses **Host game**. A Steam lobby is created and the game starts a server on their PC. The menu shows a **lobby code** with a **Copy** button.
The host sends the code to a friend (Discord, text). The friend pastes it into **Join with a lobby code** and presses **Join**: their game enters the lobby, reads the host's Steam ID from it and connects.
Both now stand in the towers as capsules and can walk, jump, climb ledges and blast each other about (see [PLAYER_MOVEMENT.md](PLAYER_MOVEMENT.md)).
**Esc** brings the menu back; **Leave game** disconnects and leaves the lobby. The menu itself is in [MENU_AND_HUD.md](MENU_AND_HUD.md).

The lobby is **friends-only**: the two players must be Steam friends, or Steam refuses the join and the menu says so.
A Steam invite (from the overlay or a friend's chat) also works when accepted with the game already open; the overlay invite button only appears when Steam's overlay is actually present, which it is not in the Unity editor or in a build started outside Steam.

Stack: **FishNet 4.7.3** with the **FishySteamworks 4.1.1** transport over **Steamworks.NET 2025.164.1**. Steam relays the traffic, so no ports are opened.

## Who owns what

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Steam lobby | Steam; the host is its owner | Lobby data: `host` = host's SteamID64 | On change |
| Server | Host's PC | — | 60 ticks/s; FishNet also steps physics per tick |
| Player body | **Host.** Spawned by the host, owned by the connecting client, which predicts its own moves | Inputs up (~30 B), host state down (~70 B) | Every tick |
| Knockback | Host only | Inside the body state | On hit |
| Props (furniture) | **Host** simulates; clients hold them kinematic | Position and rotation, via NetworkTransform; see [PROPS.md](PROPS.md) | While moving, 30/s |
| Towers | Host picks a seed; everyone builds locally; see [TOWER.md](TOWER.md) | One number | Once |

## Transports

Both transports sit inside a **Multipass** on the NetworkManager: FishySteamworks first, Tugboat (direct connection) second, with server actions per transport rather than global. FishNet only wires up the transport it has when it starts, so the lobby picks Steam or local inside Multipass per session rather than swapping transports afterwards, and the Steam server is never started when Steam is not running. **Set Up Scene** builds this; a scene from before it shows "Network setup is out of date" in the menu.

## Scripts

All under [Assets/_Project/Scripts/](../../Assets/_Project/Scripts/):

- `Net/SteamService.cs`: starts Steam on launch, pumps its callbacks every frame, shuts it down on quit. Nothing else touches `SteamAPI.Init`. Steam missing is a warning, not an error: the lobby falls back to local test mode.
- `Net/SteamLobby.cs`: Host, join by code, join local, invite, leave. Creates or joins the Steam lobby, then starts FishNet's server and/or client on the chosen transport. Turns Steam's join refusals into plain reasons. Also handles a Steam invite accepted while the game is open, and `+connect_lobby` when the game was launched from one.
- `UI/GameMenu.cs`: the menu and HUD; see [MENU_AND_HUD.md](MENU_AND_HUD.md).
- `Player/`, `Combat/`: the predicted body and knockback; owned by [PLAYER_MOVEMENT.md](PLAYER_MOVEMENT.md).

Scene wiring is built by the menu item **Close the Deal > Greybox > Set Up Scene** ([GreyboxSceneSetup.cs](../../Assets/_Project/Editor/Greybox/GreyboxSceneSetup.cs)). It only adds what is missing and never rebuilds anything already in the scene. **Rebuild Player Prefab** and **Rebuild Game UI** on the same menu are the deliberate overwrites.

## Known limitations

- **If the host leaves, the run ends.** No host migration; this is by design (see the GDD).
- **No rejoin.** A dropped client has to join again with the code.
- Steam runs under Valve's test app id **480** until we own an app id. Anyone with a Steam account can use it; Steam shows the game as "Spacewar".
- Lobbies are friends-only; there is no public list or matchmaking (out of scope for v1).

## Testing it

### Over Steam, two PCs

- Steam running and signed in on both PCs, and the two accounts are **Steam friends**.
- Both on the same commit, opening `Assets/_Project/Scenes/Greybox.unity` in Unity 6000.4.8f1 (or a build of it).
- Host: Play, **Host game**, **Copy**, send the code. Friend: Play, paste the code, **Join**.
- `steam_appid.txt` (containing `480`) must sit next to the executable or in the project root. Steamworks.NET writes it into the project root the first time the editor opens; it is git-ignored and must never ship.

### On one PC, without Steam: local test mode

Two copies of the game on the same PC connect directly instead of through Steam. The lobby uses this when Steam is not running, when the game is launched with `-local`, or when **Force Local Test Mode** is ticked on the `Steam` object's SteamLobby.

1. Build the game (File > Build Profiles > Windows) from the same commit as the editor.
2. In the editor press Play, then **Host game**.
3. Launch the build with `-local` (or with Steam closed) and press **Join game on this PC**.

Everything after joining (movement, blasts, corrections) is the real network path, only over loopback, so it shows logic bugs but not lag.

### What to watch

The bottom-left readout shows the local body's state, speed and ping, and how often and how far the host has corrected it. Corrections of a few centimetres are normal; repeated corrections of half a metre or more while walking on flat ground mean prediction and host disagree, which is a bug in `PlayerMotor`, not lag.

Nothing here has been seen running yet; the first two-PC session is the next step.
