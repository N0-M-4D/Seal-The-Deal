using CloseTheDeal.Net;
using CloseTheDeal.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// The playtest menu and HUD. Owns three things together so they can never disagree:
    /// whether the menu is open, whether the HUD shows, and whether the mouse is captured.
    /// Menu open = mouse free and the player stands still; menu closed = mouse captured, HUD up.
    /// The menu opens by itself whenever there is no local player (before joining, after
    /// leaving, on disconnect); in game, Esc toggles it. Greybox, not the final lobby.
    /// Text is rebuilt only on lobby events, never per frame. Design: docs/systems/MENU_AND_HUD.md.
    /// </summary>
    public sealed class GameMenu : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] SteamLobby _lobby;
        [SerializeField] GameObject _menuRoot;
        [SerializeField] GameObject _hudRoot;

        [Header("Header")]
        [SerializeField] TMP_Text _modeText;
        [SerializeField] TMP_Text _statusText;

        [Header("Not in a game")]
        [SerializeField] GameObject _startSection;
        [SerializeField] UnityEngine.UI.Button _hostButton;
        [SerializeField] GameObject _joinHint;
        [SerializeField] UnityEngine.UI.Button _joinLocalButton;

        [Header("In a game")]
        [SerializeField] GameObject _gameSection;
        [SerializeField] TMP_Text _playersText;
        [SerializeField] UnityEngine.UI.Button _inviteButton;
        [SerializeField] GameObject _inviteHint;
        [SerializeField] UnityEngine.UI.Button _resumeButton;
        [SerializeField] UnityEngine.UI.Button _leaveButton;

        bool _open = true;
        bool _inGame;
        string _lastPlayers;
        string _lastMode;

        void OnEnable()
        {
            _lobby.OnStatus += SetStatus;
            _lobby.OnChanged += Refresh;

            _hostButton.onClick.AddListener(_lobby.Host);
            _joinLocalButton.onClick.AddListener(_lobby.JoinLocal);
            _inviteButton.onClick.AddListener(_lobby.Invite);
            _resumeButton.onClick.AddListener(Resume);
            _leaveButton.onClick.AddListener(_lobby.Leave);
        }

        void OnDisable()
        {
            _lobby.OnStatus -= SetStatus;
            _lobby.OnChanged -= Refresh;

            _hostButton.onClick.RemoveListener(_lobby.Host);
            _joinLocalButton.onClick.RemoveListener(_lobby.JoinLocal);
            _inviteButton.onClick.RemoveListener(_lobby.Invite);
            _resumeButton.onClick.RemoveListener(Resume);
            _leaveButton.onClick.RemoveListener(_lobby.Leave);
        }

        void Start()
        {
            SetOpen(true);
            Refresh();
        }

        void Update()
        {
            bool inGame = PlayerMotor.Local != null;
            if (inGame != _inGame)
            {
                _inGame = inGame;
                SetOpen(!inGame);
                Refresh();
            }

            if (!_inGame)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetOpen(!_open);
            else if (!_open && Cursor.lockState != CursorLockMode.Locked)
                SetOpen(true); // The mouse was freed elsewhere (editor Esc, alt-tab): show the menu to match.
        }

        // ---- Open / close --------------------------------------------------------------------

        public void Resume()
        {
            if (_inGame)
                SetOpen(false);
        }

        void SetOpen(bool open)
        {
            _open = open;
            _menuRoot.SetActive(open);
            _hudRoot.SetActive(!open && _inGame);
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;

            if (open)
                SelectFirst();
        }

        /// <summary>Puts keyboard and pad focus on the most likely next action.</summary>
        void SelectFirst()
        {
            if (EventSystem.current == null)
                return;

            UnityEngine.UI.Button first = _inGame ? _resumeButton : _hostButton;
            if (_lobby.IsActive && !_inGame)
                first = _leaveButton;
            EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        // ---- State ---------------------------------------------------------------------------

        void SetStatus(string text)
        {
            _statusText.text = text;
            Refresh();
        }

        void Refresh()
        {
            bool active = _lobby.IsActive;
            bool local = _lobby.LocalMode;

            _startSection.SetActive(!active);
            _gameSection.SetActive(active);

            _hostButton.interactable = _lobby.Ready;
            _joinHint.SetActive(!local);
            _joinLocalButton.gameObject.SetActive(local);
            _joinLocalButton.interactable = _lobby.Ready;

            _inviteButton.gameObject.SetActive(_lobby.CanInviteViaOverlay);
            _inviteHint.SetActive(active && _lobby.InLobby);
            _resumeButton.gameObject.SetActive(_inGame);

            SetIfChanged(_playersText, ref _lastPlayers, active ? $"Players  {_lobby.PlayerCount} / {_lobby.MaxPlayers}" : string.Empty);
            SetIfChanged(_modeText, ref _lastMode, ModeLine(local));
        }

        string ModeLine(bool local)
        {
            if (!_lobby.Ready)
                return "Starting...";
            if (local)
                return SteamService.IsReady ? "Local test mode (forced)" : "Local test mode · Steam isn't running";
            return "Steam · signed in as " + SteamService.LocalName;
        }

        static void SetIfChanged(TMP_Text target, ref string last, string value)
        {
            if (value == last)
                return;
            last = value;
            target.text = value;
        }
    }
}
