using CloseTheDeal.Net;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// Greybox lobby controls: Host, Invite (or Join local), Leave and a status line.
    /// Placeholder, not designed; the real lobby UI replaces it. Everything updates from
    /// SteamLobby events, so there is no per-frame work.
    /// </summary>
    public sealed class LobbyPanel : MonoBehaviour
    {
        [SerializeField] SteamLobby _lobby;
        [SerializeField] Button _hostButton;
        [SerializeField] Button _inviteButton;
        [SerializeField] Button _leaveButton;
        [SerializeField] Text _statusText;
        [SerializeField] Text _playersText;

        const string InviteLabel = "Invite";
        const string JoinLocalLabel = "Join local";

        Text _inviteLabel;
        string _lastPlayers;
        string _lastInviteLabel;

        void Awake()
        {
            _inviteLabel = _inviteButton.GetComponentInChildren<Text>();
        }

        void OnEnable()
        {
            _lobby.OnStatus += SetStatus;
            _lobby.OnChanged += Refresh;

            _hostButton.onClick.AddListener(_lobby.Host);
            _inviteButton.onClick.AddListener(_lobby.InviteOrJoin);
            _leaveButton.onClick.AddListener(_lobby.Leave);

            Refresh();
        }

        void OnDisable()
        {
            _lobby.OnStatus -= SetStatus;
            _lobby.OnChanged -= Refresh;

            _hostButton.onClick.RemoveListener(_lobby.Host);
            _inviteButton.onClick.RemoveListener(_lobby.InviteOrJoin);
            _leaveButton.onClick.RemoveListener(_lobby.Leave);
        }

        void Start()
        {
            // SteamLobby decides its mode in its own Start; read it once that has run.
            Refresh();
        }

        void SetStatus(string text)
        {
            _statusText.text = text;
            Refresh();
        }

        void Refresh()
        {
            bool local = _lobby.LocalMode;
            bool active = _lobby.IsActive;

            _hostButton.interactable = !active && (local || SteamService.IsReady);
            _inviteButton.interactable = local ? !active : _lobby.InLobby;
            _leaveButton.interactable = active;

            string inviteLabel = local ? JoinLocalLabel : InviteLabel;
            if (_inviteLabel != null && inviteLabel != _lastInviteLabel)
            {
                _lastInviteLabel = inviteLabel;
                _inviteLabel.text = inviteLabel;
            }

            string players = _lobby.InLobby ? $"Players: {_lobby.MemberCount}" : string.Empty;
            if (players != _lastPlayers)
            {
                _lastPlayers = players;
                _playersText.text = players;
            }
        }
    }
}
