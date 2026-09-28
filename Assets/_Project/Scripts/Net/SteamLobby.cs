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
    ///
    /// Local test mode swaps Steam for a direct connection on this PC (FishNet's Tugboat), so
    /// an editor and a build of the same commit can play together without two Steam accounts.
    /// It is used when Steam is not running, when the game is launched with -local, or when
    /// forced in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteamLobby : MonoBehaviour
    {
        [Tooltip("How many players one lobby holds, host included. 2 for the first greybox test; raise to 4 once 2v2 is in.")]
        [SerializeField] int _maxPlayers = 2;

        [Tooltip("Tick to ignore Steam and connect directly on this PC, for testing an editor against a build. Off for anything a friend joins.")]
        [SerializeField] bool _forceLocalTestMode;

        /// <summary>Human-readable state for the lobby panel. Raised only when something changes.</summary>
        public event Action<string> OnStatus;

        /// <summary>Raised whenever the lobby's membership or our connection changes, so UI can re-read state.</summary>
        public event Action OnChanged;

        public CSteamID LobbyId { get; private set; } = CSteamID.Nil;

        /// <summary>True when connecting directly on this PC instead of through Steam.</summary>
        public bool LocalMode { get; private set; }

        public bool InLobby => LobbyId.IsValid();

        /// <summary>Hosting or joined, by either route.</summary>
        public bool IsActive => InLobby || _net.ServerManager.Started || _net.ClientManager.Started;

        public bool IsLobbyOwner => InLobby && SteamMatchmaking.GetLobbyOwner(LobbyId) == SteamService.LocalId;

        public int MemberCount => InLobby ? SteamMatchmaking.GetNumLobbyMembers(LobbyId) : 0;

        const string HostKey = "host";
        const string ConnectLobbyArg = "+connect_lobby";
        const string LocalArg = "-local";
        const string LocalAddress = "localhost";

        NetworkManager _net;
        Transport _steamTransport;
        Transport _localTransport;
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
            _steamTransport = _net.GetComponent<global::FishySteamworks.FishySteamworks>();
            _localTransport = _net.GetComponent<FishNet.Transporting.Tugboat.Tugboat>();

            LocalMode = _forceLocalTestMode || HasArg(LocalArg) || !SteamService.IsReady;
            if (LocalMode)
            {
                StartLocalMode();
                return;
            }

            _net.TransportManager.Transport = _steamTransport;
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
            if (IsActive)
                return;

            if (LocalMode)
            {
                _net.ServerManager.StartConnection();
                _net.ClientManager.StartConnection();
                Status("Hosting on this PC. Start a second copy and press Join local.");
                OnChanged?.Invoke();
                return;
            }

            Status("Creating lobby...");
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, _maxPlayers);
        }

        /// <summary>The second button: a Steam invite, or in local mode a join to the host on this PC.</summary>
        public void InviteOrJoin()
        {
            if (LocalMode)
            {
                if (IsActive)
                    return;

                _net.TransportManager.Transport.SetClientAddress(LocalAddress);
                _net.ClientManager.StartConnection();
                Status("Joining the host on this PC...");
                OnChanged?.Invoke();
                return;
            }

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
            Status(LocalMode ? "Left. Local test mode." : "Left the lobby.");
            OnChanged?.Invoke();
        }

        // ---- Local test mode -----------------------------------------------------------------

        void StartLocalMode()
        {
            if (_localTransport == null)
            {
                Status("Local test mode needs a Tugboat transport on the NetworkManager.");
                Debug.LogError("[Lobby] No Tugboat on the NetworkManager; run Close the Deal > Greybox > Set Up Scene.");
                return;
            }

            _net.TransportManager.Transport = _localTransport;
            string why = SteamService.IsReady ? "forced" : "Steam is not running";
            Status($"Local test mode ({why}). Host here, or Join local from a second copy.");
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
            if (IsActive)
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
                    Status(ConnectedStatus());
                    break;
                case LocalConnectionState.Stopped:
                    if (IsActive && !_net.ServerManager.Started)
                        Status("Disconnected from the host.");
                    break;
            }

            OnChanged?.Invoke();
        }

        string ConnectedStatus()
        {
            if (_net.ServerManager.Started)
                return LocalMode ? "Hosting on this PC. Start a second copy and press Join local." : "Hosting. Press Invite and pick a friend.";

            return LocalMode ? "Connected to the host on this PC." : $"Connected to {HostName()}.";
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

        static bool HasArg(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
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
