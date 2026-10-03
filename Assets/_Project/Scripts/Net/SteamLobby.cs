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
    /// The host creates a friends-only lobby, writes its SteamID64 into the lobby data and
    /// starts the server. A joiner enters the lobby (by code, or by accepting a Steam invite),
    /// reads that ID and connects to it through FishySteamworks.
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
        [Tooltip("How many players one lobby holds, host included. 2 for the first greybox test; raise to 4 once 2v2 is in.")]
        [SerializeField] int _maxPlayers = 2;

        [Tooltip("Tick to ignore Steam and connect directly on this PC, for testing an editor against a build. Off for anything a friend joins.")]
        [SerializeField] bool _forceLocalTestMode;

        /// <summary>Human-readable state for the menu. Raised only when something changes.</summary>
        public event Action<string> OnStatus;

        /// <summary>A join attempt failed; the text says why, for showing under the code field.</summary>
        public event Action<string> OnJoinFailed;

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

        /// <summary>The number the host sends a friend so they can join. Empty when not in a Steam lobby.</summary>
        public string LobbyCode => InLobby ? LobbyId.m_SteamID.ToString() : string.Empty;

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
        const string ConnectLobbyArg = "+connect_lobby";
        const string LocalArg = "-local";
        const string LocalAddress = "localhost";

        NetworkManager _net;
        Multipass _multipass;
        int _transportIndex = -1;
        Callback<LobbyCreated_t> _lobbyCreated;
        Callback<LobbyEnter_t> _lobbyEntered;
        Callback<GameLobbyJoinRequested_t> _joinRequested;
        Callback<LobbyChatUpdate_t> _lobbyChanged;

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
            _lobbyChanged = Callback<LobbyChatUpdate_t>.Create(OnLobbyChanged);

            Status("Ready. Host a game, or paste a lobby code to join one.");
            JoinFromCommandLine();
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
            _lobbyChanged?.Dispose();
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

        /// <summary>
        /// Joins the Steam lobby whose code the host sent. Returns why it can't start, or null
        /// when the attempt is under way (a later failure arrives through OnJoinFailed).
        /// </summary>
        public string JoinByCode(string code)
        {
            if (!Ready || LocalMode)
                return "Joining by code needs Steam running on this PC.";
            if (IsActive)
                return "You're already in a game. Leave it first.";

            string trimmed = code == null ? string.Empty : code.Trim();
            if (trimmed.Length == 0)
                return "Paste the code the host sent you.";
            if (!ulong.TryParse(trimmed, out ulong id) || !new CSteamID(id).IsLobby())
                return "That isn't a lobby code. It's a long number the host copies from their menu.";

            Status("Joining lobby...");
            SteamMatchmaking.JoinLobby(new CSteamID(id));
            return null;
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

        public void Leave()
        {
            if (_net == null)
                return;

            if (_net.ClientManager.Started)
                _net.ClientManager.StopConnection();
            if (_net.ServerManager.Started)
                _multipass.StopServerConnection(true, _transportIndex);

            if (InLobby)
                SteamMatchmaking.LeaveLobby(LobbyId);

            LobbyId = CSteamID.Nil;
            Status("You left the game.");
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

            StartHosting();
            Status("Hosting. Copy the lobby code and send it to your friend.");
        }

        void OnLobbyEntered(LobbyEnter_t cb)
        {
            var response = (EChatRoomEnterResponse)cb.m_EChatRoomEnterResponse;
            if (response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                string reason = JoinFailureReason(response);
                Status("Couldn't join: " + reason);
                OnJoinFailed?.Invoke(reason);
                return;
            }

            LobbyId = new CSteamID(cb.m_ulSteamIDLobby);
            OnChanged?.Invoke();

            // Steam raises this for the host as well; the host is already connected to itself.
            if (IsLobbyOwner)
                return;

            string hostId = SteamMatchmaking.GetLobbyData(LobbyId, HostKey);
            if (string.IsNullOrEmpty(hostId))
                hostId = SteamMatchmaking.GetLobbyOwner(LobbyId).ToString();

            StartClient(hostId);
            Status($"Connecting to {HostName()}'s game...");
        }

        void OnJoinRequested(GameLobbyJoinRequested_t cb)
        {
            if (IsActive)
                Leave();

            Status("Joining from a Steam invite...");
            SteamMatchmaking.JoinLobby(cb.m_steamIDLobby);
        }

        void OnLobbyChanged(LobbyChatUpdate_t cb)
        {
            if (cb.m_ulSteamIDLobby != LobbyId.m_SteamID)
                return;

            OnChanged?.Invoke();
        }

        static string JoinFailureReason(EChatRoomEnterResponse response)
        {
            switch (response)
            {
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist:
                    return "that lobby has closed. Ask the host for a fresh code.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed:
                    return "the lobby is friends-only. Add the host as a Steam friend first.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseFull:
                    return "the lobby is full.";
                default:
                    return $"Steam refused ({response}). Try again.";
            }
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
                    if (!_net.ServerManager.Started && IsActive)
                        Status("Lost the connection to the host.");
                    break;
            }

            OnChanged?.Invoke();
        }

        void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            OnChanged?.Invoke();
        }

        string ConnectedStatus()
        {
            if (_net.ServerManager.Started)
                return LocalMode
                    ? "Hosting on this PC. A second copy can now press Join game on this PC."
                    : "Hosting. Copy the lobby code and send it to your friend.";

            return LocalMode ? "Connected to the game on this PC." : $"Connected to {HostName()}'s game.";
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

                Status("Joining from a Steam invite...");
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
                return "the host";

            return SteamFriends.GetFriendPersonaName(SteamMatchmaking.GetLobbyOwner(LobbyId));
        }

        void Status(string text)
        {
            Debug.Log("[Lobby] " + text);
            OnStatus?.Invoke(text);
        }
    }
}
