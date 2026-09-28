using System;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using Steamworks;
using UnityEngine;

namespace CloseTheDeal.Net
{
    /// <summary>
    /// Host, invite and join through Steam lobbies, then start FishNet on top.
    /// The host creates a friends-only lobby, writes its SteamID64 into the lobby data and
    /// starts the server. A joiner reads that ID and connects to it through FishySteamworks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteamLobby : MonoBehaviour
    {
        [Tooltip("How many players one lobby holds, host included. 2 for the first greybox test; raise to 4 once 2v2 is in.")]
        [SerializeField] int _maxPlayers = 2;

        /// <summary>Human-readable state for the lobby panel. Raised only when something changes.</summary>
        public event Action<string> OnStatus;

        /// <summary>Raised whenever the lobby's membership or our connection changes, so UI can re-read state.</summary>
        public event Action OnChanged;

        public CSteamID LobbyId { get; private set; } = CSteamID.Nil;

        public bool InLobby => LobbyId.IsValid();

        public bool IsLobbyOwner => InLobby && SteamMatchmaking.GetLobbyOwner(LobbyId) == SteamService.LocalId;

        public int MemberCount => InLobby ? SteamMatchmaking.GetNumLobbyMembers(LobbyId) : 0;

        const string HostKey = "host";
        const string ConnectLobbyArg = "+connect_lobby";

        NetworkManager _net;
        Callback<LobbyCreated_t> _lobbyCreated;
        Callback<LobbyEnter_t> _lobbyEntered;
        Callback<GameLobbyJoinRequested_t> _joinRequested;
        Callback<LobbyChatUpdate_t> _lobbyChanged;

        void Start()
        {
            _net = InstanceFinder.NetworkManager;
            if (_net == null)
            {
                Debug.LogError("[Lobby] No NetworkManager in the scene.");
                enabled = false;
                return;
            }

            _net.ClientManager.OnClientConnectionState += OnClientConnectionState;

            if (!SteamService.IsReady)
            {
                Status("Steam is not running. Start Steam, then restart the game.");
                return;
            }

            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            _lobbyChanged = Callback<LobbyChatUpdate_t>.Create(OnLobbyChanged);

            Status($"Signed in as {SteamService.LocalName}.");
            JoinFromCommandLine();
        }

        void OnDestroy()
        {
            if (_net != null)
                _net.ClientManager.OnClientConnectionState -= OnClientConnectionState;

            _lobbyCreated?.Dispose();
            _lobbyEntered?.Dispose();
            _joinRequested?.Dispose();
            _lobbyChanged?.Dispose();
        }

        // ---- Actions -------------------------------------------------------------------------

        public void Host()
        {
            if (!SteamService.IsReady || InLobby)
                return;

            Status("Creating lobby...");
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, _maxPlayers);
        }

        public void Invite()
        {
            if (InLobby)
                SteamFriends.ActivateGameOverlayInviteDialog(LobbyId);
        }

        public void Leave()
        {
            if (_net.ClientManager.Started)
                _net.ClientManager.StopConnection();
            if (_net.ServerManager.Started)
                _net.ServerManager.StopConnection(true);

            if (InLobby)
                SteamMatchmaking.LeaveLobby(LobbyId);

            LobbyId = CSteamID.Nil;
            Status("Left the lobby.");
            OnChanged?.Invoke();
        }

        // ---- Steam callbacks -----------------------------------------------------------------

        void OnLobbyCreated(LobbyCreated_t cb)
        {
            if (cb.m_eResult != EResult.k_EResultOK)
            {
                Status($"Steam could not create a lobby ({cb.m_eResult}).");
                return;
            }

            LobbyId = new CSteamID(cb.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(LobbyId, HostKey, SteamService.LocalId.ToString());

            _net.ServerManager.StartConnection();
            _net.ClientManager.StartConnection();
            Status("Hosting. Press Invite and pick a friend.");
            OnChanged?.Invoke();
        }

        void OnLobbyEntered(LobbyEnter_t cb)
        {
            LobbyId = new CSteamID(cb.m_ulSteamIDLobby);
            OnChanged?.Invoke();

            // Steam raises this for the host as well; the host is already connected to itself.
            if (IsLobbyOwner)
                return;

            string hostId = SteamMatchmaking.GetLobbyData(LobbyId, HostKey);
            if (string.IsNullOrEmpty(hostId))
                hostId = SteamMatchmaking.GetLobbyOwner(LobbyId).ToString();

            _net.TransportManager.Transport.SetClientAddress(hostId);
            _net.ClientManager.StartConnection();
            Status($"Joining {HostName()}'s game...");
        }

        void OnJoinRequested(GameLobbyJoinRequested_t cb)
        {
            if (InLobby)
                Leave();

            Status("Joining lobby from invite...");
            SteamMatchmaking.JoinLobby(cb.m_steamIDLobby);
        }

        void OnLobbyChanged(LobbyChatUpdate_t cb)
        {
            if (cb.m_ulSteamIDLobby != LobbyId.m_SteamID)
                return;

            OnChanged?.Invoke();
        }

        // ---- FishNet -------------------------------------------------------------------------

        void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    Status(IsLobbyOwner ? "Hosting. Press Invite and pick a friend." : $"Connected to {HostName()}.");
                    break;
                case LocalConnectionState.Stopped:
                    if (InLobby && !IsLobbyOwner)
                        Status("Disconnected from the host.");
                    break;
            }

            OnChanged?.Invoke();
        }

        // ---- Helpers -------------------------------------------------------------------------

        /// <summary>Accepting an invite while the game is closed launches it with "+connect_lobby id".</summary>
        void JoinFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != ConnectLobbyArg || !ulong.TryParse(args[i + 1], out ulong id))
                    continue;

                Status("Joining lobby from invite...");
                SteamMatchmaking.JoinLobby(new CSteamID(id));
                return;
            }
        }

        string HostName()
        {
            if (!InLobby)
                return "host";

            return SteamFriends.GetFriendPersonaName(SteamMatchmaking.GetLobbyOwner(LobbyId));
        }

        void Status(string text)
        {
            Debug.Log("[Lobby] " + text);
            OnStatus?.Invoke(text);
        }
    }
}
