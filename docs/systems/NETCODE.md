# NETCODE

**Status:** Current · **Last updated:** 2026-10-03 · **Verified against:** the playtest menu commit

## What happens in the game

One player presses **Host game**. A Steam lobby is created and the game starts a server on their PC.
A friend joins in one of three ways, none of them a code: the host presses **Invite with the Steam overlay** and picks them; they pick **Join Game** on the host's name in their Steam friends list or profile; or they accept an invite in Steam chat. If their game is closed, Steam launches it straight into the lobby. Their game enters the lobby, reads the host's Steam ID from it and connects.
Up to **4** players fit in one lobby. Both now stand in the towers as capsules and can walk, jump, climb ledges and blast each other about (see [PLAYER_MOVEMENT.md](PLAYER_MOVEMENT.md)).
**Esc** brings the menu back; **Leave game** disconnects and leaves the lobby. The menu itself is in [MENU_AND_HUD.md](MENU_AND_HUD.md).

The lobby is **friends-only**: the players must be Steam friends, or Steam refuses the join and the menu says so. A friend on a different game version is turned away with a message saying who needs to update.
The overlay invite button only appears when Steam's overlay is actually present, which it is not in the Unity editor or in a build started outside Steam; the friends-list route works everywhere.
Every failure says what happened in the menu's status line: a host that can't be reached (the join gives up after 15 s), a host who closed the game, or a connection that dropped. Each sends the player back to the menu with the lobby cleaned up.

Stack: **FishNet 4.7.3** (the pinned tag; its own version strings still say 4.7.2) with the **FishySteamworks 4.1.1** transport over **Steamworks.NET 2025.164.1**. Steam connects two players directly when both allow it and relays through its own network otherwise; either way no ports are opened. Relay access is warmed up at launch so the first Host or Join doesn't wait for it.

## Who owns what

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Steam lobby | Steam; the host is its owner | Lobby data: `host` = host's SteamID64, `ver` = game version | On change |
| Rich presence | Each player, about themselves | `connect` (what Join Game does), `steam_player_group` and its size, a `status` line | On lobby events |
| Server | Host's PC | — | 60 ticks/s; FishNet also steps physics per tick |
| Player body | **Host.** Spawned by the host, owned by the connecting client, which predicts its own moves | Inputs up (~30 B), host state down (~80 B) | Every tick |
| Knockback | Host only | Inside the body state | On hit |
| Props (furniture) | **Host** simulates; clients hold them kinematic | Position and rotation, via NetworkTransform; see [PROPS.md](PROPS.md) | While moving, 30/s |
| Towers | Host picks a seed; everyone builds locally; see [TOWER.md](TOWER.md) | One number | Once |

## Transports

Both transports sit inside a **Multipass** on the NetworkManager: FishySteamworks first, Tugboat (direct connection) second, with server actions per transport rather than global. FishNet only wires up the transport it has when it starts, so the lobby picks Steam or local inside Multipass per session rather than swapping transports afterwards, and the Steam server is never started when Steam is not running. **Set Up Scene** builds this; a scene from before it shows "Network setup is out of date" in the menu.

## Scripts

All under [Assets/_Project/Scripts/](../../Assets/_Project/Scripts/):

- `Net/SteamService.cs`: starts Steam on launch, pumps its callbacks every frame, shuts it down on quit. Nothing else touches `SteamAPI.Init`. Steam missing is a warning, not an error: the lobby falls back to local test mode.
- `Net/SteamLobby.cs`: Host, join local, invite, leave. Creates or joins the Steam lobby, then starts FishNet's server and/or client on the chosen transport. Publishes rich presence so friends can Join Game on us, and handles that join whether it arrives as a Steam callback (game open) or as `+connect_lobby` on the command line (game launched by Steam). Checks the game version on entry, times out a join that never connects, and on a lost host leaves the lobby and says whether the host closed the game or the line dropped. Turns Steam's join refusals into plain reasons.
- `UI/GameMenu.cs`: the menu and HUD; see [MENU_AND_HUD.md](MENU_AND_HUD.md).
- `Player/`, `Combat/`: the predicted body and knockback; owned by [PLAYER_MOVEMENT.md](PLAYER_MOVEMENT.md).

Scene wiring is built by the menu item **Close the Deal > Greybox > Set Up Scene** ([GreyboxSceneSetup.cs](../../Assets/_Project/Editor/Greybox/GreyboxSceneSetup.cs)). It only adds what is missing and never rebuilds anything already in the scene. **Rebuild Player Prefab** and **Rebuild Game UI** on the same menu are the deliberate overwrites.

## Known limitations

- **If the host leaves, the run ends.** No host migration; this is by design (see the GDD).
- **No rejoin.** A dropped client has to join again from the friends list or a fresh invite.
- Steam runs under Valve's test app id **480** until we own an app id. Anyone with a Steam account can use it; Steam shows the game as "Spacewar", and Join Game from the friends list launches whatever is installed as app 480, so test the launch-from-Steam path only once we have our own id.
- Lobbies are friends-only; there is no public list or matchmaking (out of scope for v1).
- The lobby stays joinable during a run, because nothing marks a run's start yet. Once the secretary starts runs, the lobby should close then and reopen after.
- FishySteamworks is unmaintained upstream, so we carry it as a fork in [Assets/_Project/Plugins/FishySteamworks/](../../Assets/_Project/Plugins/FishySteamworks/) with its send bug fixed (its `FORK.md` lists the patches). Without that fix FishNet's latency simulator broke every packet over Steam.

## Testing it

### Over Steam, two PCs

- Steam running and signed in on both PCs, and the two accounts are **Steam friends**.
- Both on the same commit, opening `Assets/_Project/Scenes/Greybox.unity` in Unity 6000.4.8f1 (or a build of it).
- Host: Play, **Host game**. Friend: Play, then right-click the host in the Steam friends list and pick **Join Game** (or accept the host's invite). Both games must already be open: with app id 480, launching from Steam opens Spacewar, not ours.
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
