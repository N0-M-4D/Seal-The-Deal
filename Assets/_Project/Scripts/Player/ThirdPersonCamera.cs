using UnityEngine;
using UnityEngine.InputSystem;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Local-only orbit camera. Follows the owning player's smoothed visual and supplies the
    /// yaw the body faces. Escape toggles the mouse free so the lobby buttons can be clicked.
    /// Nothing here is networked.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        public static ThirdPersonCamera Instance { get; private set; }

        [Tooltip("How far behind the player the camera sits, in metres.")]
        [SerializeField] float _distance = 5f;

        [Tooltip("Height above the player's feet the camera orbits around, in metres. Chest height (1.4) keeps the body centred.")]
        [SerializeField] float _pivotHeight = 1.4f;

        [Tooltip("Mouse look speed in degrees per mouse unit. 0.12 is a typical shooter feel; higher turns faster.")]
        [SerializeField] float _sensitivity = 0.12f;

        [Tooltip("How far the camera may look down (x, negative) and up (y, positive), in degrees.")]
        [SerializeField] Vector2 _pitchLimits = new(-35f, 70f);

        Transform _target;
        PlayerInputReader _input;
        float _yaw;
        float _pitch = 15f;

        /// <summary>Direction the player faces, in degrees around the vertical axis.</summary>
        public float Yaw => _yaw;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Follow(Transform target, PlayerInputReader input)
        {
            _target = target;
            _input = input;
            _yaw = target.eulerAngles.y;
            SetCursorLocked(true);
        }

        public void Release(Transform target)
        {
            if (_target != target)
                return;

            _target = null;
            _input = null;
            SetCursorLocked(false);
        }

        void LateUpdate()
        {
            if (_target == null)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = _input.LookDelta * _sensitivity;
                _yaw += look.x;
                _pitch = Mathf.Clamp(_pitch - look.y, _pitchLimits.x, _pitchLimits.y);
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = _target.position + Vector3.up * _pivotHeight;
            transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * _distance, rotation);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
