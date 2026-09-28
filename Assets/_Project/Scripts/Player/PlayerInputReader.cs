using UnityEngine;
using UnityEngine.InputSystem;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Reads the local player's input every frame. Held inputs are read live; presses (jump,
    /// attack) are latched until the motor consumes them on a tick, so a tap between two
    /// ticks is never lost. Enabled only on the owning client.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("The Input System actions asset. Uses its Player map: Move, Look, Sprint, Jump, Attack.")]
        [SerializeField] InputActionAsset _actions;

        [Tooltip("Smallest stick push that counts as movement, as a fraction of full tilt. 0.1 ignores stick drift; raise it if a pad creeps.")]
        [Range(0f, 0.5f)] [SerializeField] float _deadZone = 0.1f;

        InputActionMap _map;
        InputAction _move;
        InputAction _look;
        InputAction _sprint;
        InputAction _jump;
        InputAction _attack;
        bool _jumpLatched;
        bool _attackLatched;

        /// <summary>Movement stick with the dead zone removed and the rest rescaled to 0–1.</summary>
        public Vector2 Move
        {
            get
            {
                Vector2 raw = _move.ReadValue<Vector2>();
                float magnitude = Mathf.Clamp01(raw.magnitude);
                if (magnitude <= _deadZone)
                    return Vector2.zero;

                return raw.normalized * Mathf.InverseLerp(_deadZone, 1f, magnitude);
            }
        }
        public bool Sprint => _sprint.IsPressed();
        public Vector2 LookDelta => _look.ReadValue<Vector2>();

        void Awake()
        {
            _map = _actions.FindActionMap("Player", true);
            _move = _map.FindAction("Move", true);
            _look = _map.FindAction("Look", true);
            _sprint = _map.FindAction("Sprint", true);
            _jump = _map.FindAction("Jump", true);
            _attack = _map.FindAction("Attack", true);
        }

        void OnEnable()
        {
            _map.Enable();
        }

        void OnDisable()
        {
            _map.Disable();
            _jumpLatched = false;
            _attackLatched = false;
        }

        void Update()
        {
            if (_jump.WasPressedThisFrame())
                _jumpLatched = true;

            // A click with the mouse free is aimed at the lobby buttons, not the world.
            if (_attack.WasPressedThisFrame() && Cursor.lockState == CursorLockMode.Locked)
                _attackLatched = true;
        }

        public bool ConsumeJump()
        {
            bool pressed = _jumpLatched;
            _jumpLatched = false;
            return pressed;
        }

        public bool ConsumeAttack()
        {
            bool pressed = _attackLatched;
            _attackLatched = false;
            return pressed;
        }
    }
}
