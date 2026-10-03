# FishySteamworks fork

This is FirstGearGames' FishySteamworks **4.1.1** (the release `.unitypackage`, last upstream commit Aug 2024), moved here from `Assets/FishNet/Plugins/` on 2026-10-03 because upstream is unmaintained and it needed a fix. Every change from upstream is listed below and marked `Close the Deal patch` at the code site, so a future upstream release can be diffed against this folder.

Upstream: https://github.com/FirstGearGames/FishySteamworks

## Patches

1. **`Core/CommonSocket.cs`, `Send`**: when an outgoing packet exactly fills its buffer, the original resized a copy of the array, wrote the channel byte into the copy, then built the segment from the original. A full buffer either threw `ArgumentException` or went out with a stale channel byte. FishNet's latency simulator hands the transport exactly-full buffers, so it hit on every packet. Upstream issue: https://github.com/FirstGearGames/FishySteamworks/issues/19.

## Known but left alone

- `Core/ClientSocket.cs`: the 8 s connect timeout never fires (it adds 8000 to a time in seconds), and it would stop the connection from a background thread if it did. `SteamLobby` runs its own 15 s timeout on the main thread instead.
