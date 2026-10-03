using System;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using Steamworks;
using UnityEngine;

namespace CloseTheDeal.Net
{
    /// <summary>
    /// Host, join and invite through Steam lobbies, then start FishNet on top.
    /// The host creates a friends-only lobby, writes its SteamID64 and the game version into
    /// the lobby data and starts the server. A joiner enters the lobby by accepting a Steam
    /// invite or picking Join Game on the host in their Steam friends list, reads that ID and
    /// connects to it through FishySteamworks. There are no lobby codes.
    ///
    /// Local test mode connects directly on this PC instead (FishNet's Tugboat), so an editor
    /// and a build of the same commit can play together without two Steam accounts. It is
    /// used when Steam is not running, when the game is launched with -local, or when forced
    /// in the Inspector.
    ///
    /// Both transports sit inside a Multipass on the NetworkManager. FishNet only wires up the
    /// transport it has when it starts, so the choice is made per connection inside Multipass
    /// rather than by swapping transports afterwards.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteamLobby : MonoBehaviour
    {
        [Tooltip("How many players one lobby holds, host included. 8 = two teams of up to 4.")]
        [SerializeField] int _maxPlayers = 8;

        [Tooltip("Tick to ignore Steam and connect directly on this PC, for testing an editor against a build. Off for anything a friend joins.")]
        [SerializeField] bool _forceLocalTestMode;

        /// <summary>Human-readable state for the menu. Raised only when something changes.</summary>
        public event Action<string> OnStatus;

        /// <summary>Raised whenever the lobby's membership or our connection changes, so UI can re-read state.</summary>
        public event Action OnChanged;

        public CSteamID LobbyId { get; private set; } = CSteamID.Nil;

        /// <summary>True when connecting directly on this PC instead of through Steam.</summary>
        public bool LocalMode { get; private set; }

        /// <summary>False until Start has decided the mode, or if the network setup is broken.</summary>
        public bool Ready { get; private set; }

        public bool InLobby => LobbyId.IsValid();

        /// <summary>Hosting or joined, by either route. False until Start has found the NetworkManager,
        /// because UI can ask during its own OnEnable, which Unity runs before any Start.</summary>
        public bool IsActive => InLobby || (_net != null && (_net.ServerManager.Started || _net.ClientManager.Started));

        public bool IsHosting => _net != null && _net.ServerManager.Started;

        public bool IsLobbyOwner => InLobby && SteamMatchmaking.GetLobbyOwner(LobbyId) == SteamService.LocalId;

        public int MaxPlayers => _maxPlayers;

        /// <summary>Players in the lobby (Steam) or connected to this host (local); 0 when not in a game.</summary>
        public int PlayerCount
        {
            get
            {
                if (InLobby)
                    return SteamMatchmaking.GetNumLobbyMembers(LobbyId);
                return IsHosting ? _net.ServerManager.Clients.Count : 0;
            }
        }

        /// <summary>The Steam overlay's invite dialog only exists when Steam injected its overlay, which it
        /// does not for the Unity editor or a build started outside Steam.</summary>
        public bool CanInviteViaOverlay => !LocalMode && InLobby && SteamService.IsReady && SteamUtils.IsOverlayEnabled();

        const string HostKey = "host";
        const string VersionKey = "ver";
        const string ConnectLobbyArg = "+connect_lobby";
        const string LocalArg = "-local";
        const string LocalAddress = "localhost";

        // Rich presence: what Steam shows friends about us, and what Join Game on our name does.
        const string ConnectKey = "connect";
        const string GroupKey = "steam_player_group";
        const string GroupSizeKey = "steam_player_group_size";
        const string PresenceStatusKey = "status";

        /// <summary>Steam's own connect attempt gives up after about 10 s; this is the backstop behind it.</summary>
        const float ConnectTimeoutSeconds = 15f;

        NetworkManager _net;
        Multipass _multipass;
        int _transportIndex = -1;
        CSteamID _hostId = CSteamID.Nil;
        float _connectDeadline = -1f;
        bool _joined;
        bool _connected;
        bool _leaving;
        bool _hostClosedLink;
        Callback<LobbyCreated_t> _lobbyCreated;
        Callback<LobbyEnter_t> _lobbyEntered;
        Callback<GameLobbyJoinRequested_t> _joinRequested;
        Callback<GameRichPresenceJoinRequested_t> _presenceJoinRequested;
        Callback<LobbyChatUpdate_t> _lobbyChanged;
        Callback<SteamNetConnectionStatusChangedCallback_t> _linkChanged;

        void Start()
        {
            _net = InstanceFinder.NetworkManager;
            _multipass = _net != null ? _net.TransportManager.Transport as Multipass : null;
            if (_multipass == null)
            {
                Debug.LogError("[Lobby] The NetworkManager needs a Multipass transport holding FishySteamworks and Tugboat. Run Close the Deal > Greybox > Set Up Scene.");
                Status("Network setup is out of date. Run Close the Deal > Greybox > Set Up Scene.");
                return;
            }

            _net.ClientManager.OnClientConnectionState += OnClientConnectionState;
            _net.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;

            LocalMode = _forceLocalTestMode || HasArg(LocalArg) || !SteamService.IsReady;
            if (!UseTransport(LocalMode))
                return;

            Ready = true;
            if (LocalMode)
            {
                Status("Ready. Host a game, or join one running on this PC.");
                return;
            }

            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            _presenceJoinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(OnPresenceJoinRequested);
            _lobbyChanged = Callback<LobbyChatUpdate_t>.Create(OnLobbyChanged);
            _linkChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnLinkChanged);

            Status("Ready. Host a game, or join a friend from your Steam friends list.");
            JoinFromCommandLine();
        }

        void Update()
        {
            if (_connectDeadline < 0f || Time.unscaledTime < _connectDeadline)
                return;

            _connectDeadline = -1f;
            Drop(UnreachableReason(HostName()));
        }

        /// <summary>Leaves the lobby before the process goes, so friends see it close at once rather than time out.</summary>
        void OnApplicationQuit()
        {
            if (!SteamService.IsReady)
                return;

            if (InLobby)
                SteamMatchmaking.LeaveLobby(LobbyId);
            SteamFriends.ClearRichPresence();
        }

        void OnDestroy()
        {
            if (_net != null)
            {
                _net.ClientManager.OnClientConnectionState -= OnClientConnectionState;
                _net.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            }

            _lobbyCreated?.Dispose();
            _lobbyEntered?.Dispose();
            _joinRequested?.Dispose();
            _presenceJoinRequested?.Dispose();
            _lobbyChanged?.Dispose();
            _linkChanged?.Dispose();
        }

        // ---- Actions -------------------------------------------------------------------------

        public void Host()
        {
            if (!Ready || IsActive)
                return;

            if (LocalMode)
            {
                StartHosting();
                Status("Hosting on this PC. A second copy can now press Join game on this PC.");
                return;
            }

            Status("Creating a Steam lobby...");
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, _maxPlayers);
        }

        /// <summary>Local test mode: join the host running on this PC.</summary>
        public void JoinLocal()
        {
            if (!Ready || !LocalMode || IsActive)
                return;

            StartClient(LocalAddress);
            Status("Joining the game on this PC...");
        }

        public void Invite()
        {
            if (CanInviteViaOverlay)
                SteamFriends.ActivateGameOverlayInviteDialog(LobbyId);
        }

        public void Leave() => Drop("You left the game.");

        /// <summary>Stops whatever is running, leaves the Steam lobby and says why.</summary>
        void Drop(string status)
        {
            if (_net == null)
                return;

            _leaving = true;
            _connectDeadline = -1f;
            _joined = false;
            _connected = false;

            // Started is false while still connecting, and that attempt must stop too.
            if (_multipass.GetConnectionState(false) != LocalConnectionState.Stopped)
                _net.ClientManager.StopConnection();
            if (_net.ServerManager.Started)
                _multipass.StopServerConnection(true, _transportIndex);

            if (InLobby)
                SteamMatchmaking.LeaveLobby(LobbyId);
            if (SteamService.IsReady)
                SteamFriends.ClearRichPresence();

            LobbyId = CSteamID.Nil;
            _hostId = CSteamID.Nil;
            _leaving = false;
            Status(status);
            OnChanged?.Invoke();
        }

        // ---- Transport -----------------------------------------------------------------------

        /// <summary>Points Multipass at the Steam or the local transport for everything this session does.</summary>
        bool UseTransport(bool local)
        {
            Transport transport = local
                ? _multipass.GetTransport<FishNet.Transporting.Tugboat.Tugboat>()
                : _multipass.GetTransport<global::FishySteamworks.FishySteamworks>();

            if (transport == null)
            {
                string missing = local ? "Tugboat" : "FishySteamworks";
                Debug.LogError($"[Lobby] Multipass has no {missing}. Run Close the Deal > Greybox > Set Up Scene.");
                Status($"Network setup is missing {missing}. Run Close the Deal > Greybox > Set Up Scene.");
                return false;
            }

            _transportIndex = transport.Index;
            _multipass.SetClientTransport(_transportIndex);
            return true;
        }

        /// <summary>Server on the chosen transport only, then this machine joins its own server.</summary>
        void StartHosting()
        {
            _multipass.StartConnection(true, _transportIndex);
            _net.ClientManager.StartConnection();
            OnChanged?.Invoke();
        }

        void StartClient(string address)
        {
            _joined = true;
            _connected = false;
            _hostClosedLink = false;
            _connectDeadline = Time.unscaledTime + ConnectTimeoutSeconds;
            _multipass.SetClientAddress(address, _transportIndex);
            _net.ClientManager.StartConnection();
            OnChanged?.Invoke();
        }

        // ---- Steam callbacks -----------------------------------------------------------------

        void OnLobbyCreated(LobbyCreated_t cb)
        {
            if (cb.m_eResult != EResult.k_EResultOK)
            {
                Status($"Steam couldn't create a lobby ({cb.m_eResult}). Check Steam is online and try again.");
                return;
            }

            LobbyId = new CSteamID(cb.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(LobbyId, HostKey, SteamService.LocalId.ToString());
            SteamMatchmaking.SetLobbyData(LobbyId, VersionKey, Application.version);

            StartHosting();
            Status(HostingStatus());
        }

        void OnLobbyEntered(LobbyEnter_t cb)
        {
            var response = (EChatRoomEnterResponse)cb.m_EChatRoomEnterResponse;
            if (response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Status("Couldn't join: " + JoinFailureReason(response));
                return;
            }

            LobbyId = new CSteamID(cb.m_ulSteamIDLobby);
            _hostId = ReadHostId();

            // Steam raises this for the host as well; the host is already connected to itself.
            if (IsLobbyOwner)
            {
                PublishPresence();
                OnChanged?.Invoke();
                return;
            }

            string theirs = SteamMatchmaking.GetLobbyData(LobbyId, VersionKey);
            if (theirs != Application.version)
            {
                string host = HostName();
                SteamMatchmaking.LeaveLobby(LobbyId);
                LobbyId = CSteamID.Nil;
                _hostId = CSteamID.Nil;
                Status(VersionMismatchReason(host, theirs));
                OnChanged?.Invoke();
                return;
            }

            PublishPresence();
            StartClient(_hostId.m_SteamID.ToString());
            Status($"Connecting to {HostName()}'s game...");
        }

        /// <summary>A friend accepted our invite, or picked Join Game on us, while their game was open.</summary>
        void OnJoinRequested(GameLobbyJoinRequested_t cb)
        {
            JoinLobby(cb.m_steamIDLobby);
        }

        /// <summary>Join Game from our Steam profile or the friends list, carrying the connect string we published.</summary>
        void OnPresenceJoinRequested(GameRichPresenceJoinRequested_t cb)
        {
            if (TryParseConnectLobby(cb.m_rgchConnect.Split(' '), out CSteamID lobby))
                JoinLobby(lobby);
        }

        void JoinLobby(CSteamID lobby)
        {
            if (IsActive)
                Leave();

            Status("Joining from Steam...");
            SteamMatchmaking.JoinLobby(lobby);
        }

        void OnLobbyChanged(LobbyChatUpdate_t cb)
        {
            if (cb.m_ulSteamIDLobby != LobbyId.m_SteamID)
                return;

            uint change = cb.m_rgfChatMemberStateChange;
            bool hostGone = !IsHosting
                && cb.m_ulSteamIDUserChanged == _hostId.m_SteamID
                && (change & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) == 0;
            if (hostGone)
            {
                bool closed = (change & (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft) != 0;
                string host = HostName();
                Drop(closed ? $"{host} closed the game." : $"Lost {host}'s game: their connection dropped.");
                return;
            }

            PublishPresence();
            OnChanged?.Invoke();
        }

        /// <summary>Remembers how our link to the host ended, so the message can tell a closed game from a dropped line.</summary>
        void OnLinkChanged(SteamNetConnectionStatusChangedCallback_t cb)
        {
            if (IsHosting)
                return;

            ESteamNetworkingConnectionState state = cb.m_info.m_eState;
            if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer)
                _hostClosedLink = true;
            else if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
                _hostClosedLink = false;
        }

        CSteamID ReadHostId()
        {
            string hostId = SteamMatchmaking.GetLobbyData(LobbyId, HostKey);
            if (ulong.TryParse(hostId, out ulong id))
                return new CSteamID(id);
            return SteamMatchmaking.GetLobbyOwner(LobbyId);
        }

        /// <summary>What friends see on our name in Steam, and what Join Game there does. Rebuilt on lobby events only.</summary>
        void PublishPresence()
        {
            if (!InLobby || LocalMode)
                return;

            string lobby = LobbyId.m_SteamID.ToString();
            SteamFriends.SetRichPresence(ConnectKey, ConnectLobbyArg + " " + lobby);
            SteamFriends.SetRichPresence(GroupKey, lobby);
            SteamFriends.SetRichPresence(GroupSizeKey, PlayerCount.ToString());
            SteamFriends.SetRichPresence(PresenceStatusKey, $"In {HostName()}'s lobby ({PlayerCount}/{_maxPlayers})");
        }

        static string JoinFailureReason(EChatRoomEnterResponse response)
        {
            switch (response)
            {
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist:
                    return "that game has closed.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed:
                    return "the lobby is friends-only. Add the host as a Steam friend first.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseFull:
                    return "the game is full.";
                default:
                    return $"Steam refused ({response}). Try again.";
            }
        }

        static string VersionMismatchReason(string host, string theirs)
        {
            string theirVersion = string.IsNullOrEmpty(theirs) ? "an older version" : "version " + theirs;
            return $"Can't join: {host} is on {theirVersion} and you're on version {Application.version}. Whoever is behind needs to update.";
        }

        static string UnreachableReason(string host)
        {
            return $"Couldn't reach {host}'s game. They may have left, or Steam couldn't connect you. Try joining again.";
        }

        // ---- FishNet -------------------------------------------------------------------------

        void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    _connected = true;
                    _connectDeadline = -1f;
                    Status(ConnectedStatus());
                    break;
                case LocalConnectionState.Stopped:
                    // Our own Leave stops the client too; only an uninvited stop is news.
                    if (!_leaving && _joined && !_net.ServerManager.Started)
                    {
                        Drop(StoppedReason(HostName()));
                        return;
                    }
                    break;
            }

            OnChanged?.Invoke();
        }

        void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            OnChanged?.Invoke();
        }

        string StoppedReason(string host)
        {
            if (!_connected)
                return UnreachableReason(host);
            if (_hostClosedLink)
                return $"{host} closed the game.";
            return $"Lost the connection to {host}'s game.";
        }

        string ConnectedStatus()
        {
            if (_net.ServerManager.Started)
                return HostingStatus();

            return LocalMode ? "Connected to the game on this PC." : $"Connected to {HostName()}'s game.";
        }

        string HostingStatus()
        {
            return LocalMode
                ? "Hosting on this PC. A second copy can now press Join game on this PC."
                : "Hosting. Invite a friend, or they can pick Join Game on your name in their Steam friends list.";
        }

        // ---- Helpers -------------------------------------------------------------------------

        /// <summary>Accepting an invite while the game is closed launches it with "+connect_lobby id".</summary>
        void JoinFromCommandLine()
        {
            if (TryParseConnectLobby(Environment.GetCommandLineArgs(), out CSteamID lobby))
                JoinLobby(lobby);
        }

        static bool TryParseConnectLobby(string[] args, out CSteamID lobby)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != ConnectLobbyArg || !ulong.TryParse(args[i + 1], out ulong id))
                    continue;

                lobby = new CSteamID(id);
                return true;
            }

            lobby = CSteamID.Nil;
            return false;
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
            if (!_hostId.IsValid())
                return "the host";

            return SteamFriends.GetFriendPersonaName(_hostId);
        }

        void Status(string text)
        {
            Debug.Log("[Lobby] " + text);
            OnStatus?.Invoke(text);
        }
    }
}
