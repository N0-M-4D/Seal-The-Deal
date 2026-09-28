using CloseTheDeal.Net;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// Greybox lobby controls: Host, Invite, Leave and a status line.
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

        string _lastPlayers;

        void OnEnable()
        {
            _lobby.OnStatus += SetStatus;
            _lobby.OnChanged += Refresh;

            _hostButton.onClick.AddListener(_lobby.Host);
            _inviteButton.onClick.AddListener(_lobby.Invite);
            _leaveButton.onClick.AddListener(_lobby.Leave);

            Refresh();
        }

        void OnDisable()
        {
            _lobby.OnStatus -= SetStatus;
            _lobby.OnChanged -= Refresh;

            _hostButton.onClick.RemoveListener(_lobby.Host);
            _inviteButton.onClick.RemoveListener(_lobby.Invite);
            _leaveButton.onClick.RemoveListener(_lobby.Leave);
        }

        void SetStatus(string text)
        {
            _statusText.text = text;
        }

        void Refresh()
        {
            bool inLobby = _lobby.InLobby;
            _hostButton.interactable = !inLobby && SteamService.IsReady;
            _inviteButton.interactable = inLobby;
            _leaveButton.interactable = inLobby;

            string players = inLobby ? $"Players: {_lobby.MemberCount}" : string.Empty;
            if (players != _lastPlayers)
            {
                _lastPlayers = players;
                _playersText.text = players;
            }
        }
    }
}
