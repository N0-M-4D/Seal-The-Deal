using UnityEngine;
using UnityEngine.InputSystem;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Reads the local player's input every frame. Held inputs are read live; presses (jump,
    /// attack) are latched until the motor consumes them on a tick, so a tap between two
    /// ticks is never lost. Enabled only on the owning client.
    /// Everything reads as idle while the mouse is free, which is exactly while the game menu
    /// is open: typing a lobby code or clicking a button never moves, jumps or fires.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("The Input System actions asset. Uses its Player map: Move, Look, Sprint, Jump, Attack, Lean.")]
        [SerializeField] InputActionAsset _actions;

        [Tooltip("Smallest stick push that counts as movement, as a fraction of full tilt. 0.1 ignores stick drift; raise it if a pad creeps.")]
        [Range(0f, 0.5f)] [SerializeField] float _deadZone = 0.1f;

        InputActionMap _map;
        InputAction _move;
        InputAction _look;
        InputAction _sprint;
        InputAction _jump;
        InputAction _attack;
        InputAction _lean;
        bool _jumpLatched;
        bool _attackLatched;

        static bool Captured => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>Movement stick with the dead zone removed and the rest rescaled to 0–1.</summary>
        public Vector2 Move
        {
            get
            {
                if (!Captured)
                    return Vector2.zero;

                Vector2 raw = _move.ReadValue<Vector2>();
                float magnitude = Mathf.Clamp01(raw.magnitude);
                if (magnitude <= _deadZone)
                    return Vector2.zero;

                return raw.normalized * Mathf.InverseLerp(_deadZone, 1f, magnitude);
            }
        }
        public bool Sprint => Captured && _sprint.IsPressed();
        public Vector2 LookDelta => Captured ? _look.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>True while jump is held, so letting go early can cut a jump short.</summary>
        public bool JumpHeld => Captured && _jump.IsPressed();

        /// <summary>-1 leaning left, +1 leaning right, 0 upright. Both held cancel out.</summary>
        public sbyte Lean
        {
            get
            {
                if (!Captured)
                    return 0;

                float axis = _lean.ReadValue<float>();
                return axis < -0.5f ? (sbyte)-1 : axis > 0.5f ? (sbyte)1 : (sbyte)0;
            }
        }

        void Awake()
        {
            _map = _actions.FindActionMap("Player", true);
            _move = _map.FindAction("Move", true);
            _look = _map.FindAction("Look", true);
            _sprint = _map.FindAction("Sprint", true);
            _jump = _map.FindAction("Jump", true);
            _attack = _map.FindAction("Attack", true);
            _lean = _map.FindAction("Lean", true);
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
            if (!Captured)
                return;

            if (_jump.WasPressedThisFrame())
                _jumpLatched = true;
            if (_attack.WasPressedThisFrame())
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
