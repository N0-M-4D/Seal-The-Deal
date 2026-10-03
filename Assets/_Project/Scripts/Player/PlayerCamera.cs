using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Local-only first-person camera. Sits at the owning player's eye on their smoothed
    /// visual, so it never steps between ticks. Leaning slides it sideways and tilts it, and
    /// a wall beside the head stops the slide before the view clips through. Supplies the yaw
    /// the player faces and the aim direction weapons fire along. Mouse look only applies
    /// while the mouse is captured; the game menu decides that. Nothing here is networked:
    /// the lean itself is predicted state on PlayerMotor, and this only shows it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PlayerCamera : MonoBehaviour
    {
        public static PlayerCamera Instance { get; private set; }

        [Tooltip("Mouse look speed in degrees per mouse unit. 0.12 is a typical shooter feel; higher turns faster.")]
        [SerializeField] float _sensitivity = 0.12f;

        [Tooltip("How far the camera may look up (x, negative) and down (y, positive), in degrees. Keep inside ±89 or the view flips at the top.")]
        [SerializeField] Vector2 _pitchLimits = new(-85f, 85f);

        [Tooltip("Closest the eye gets to a wall when leaning into it, in metres. Keep above the camera's near clip distance or the wall shows through.")]
        [SerializeField] float _wallClearance = 0.2f;

        const float AimRange = 200f;
        const string PlayerLayer = "Player";
        const string PropLayer = "Prop";

        Transform _target;
        PlayerInputReader _input;
        PlayerMotor _motor;
        float _yaw;
        float _pitch;
        float _shownLean;
        int _aimMask;
        int _wallMask;

        /// <summary>Direction the player faces, in degrees around the vertical axis.</summary>
        public float Yaw => _yaw;

        void Awake()
        {
            Instance = this;
            int player = 1 << LayerMask.NameToLayer(PlayerLayer);
            int prop = 1 << LayerMask.NameToLayer(PropLayer);
            _aimMask = ~player;            // Skips all players so you never aim at yourself; the shot from your eye still hits anyone in its path.
            _wallMask = ~(player | prop);  // Only the building stops a lean; furniture doesn't.
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Follow(Transform target, PlayerInputReader input, PlayerMotor motor)
        {
            _target = target;
            _input = input;
            _motor = motor;
            _yaw = target.eulerAngles.y;
            _pitch = 0f;
            _shownLean = 0f;
        }

        public void Release(Transform target)
        {
            if (_target != target)
                return;

            _target = null;
            _input = null;
            _motor = null;
        }

        /// <summary>
        /// Unit direction from a point on the player (say their eye) to whatever is under the
        /// crosshair, so a shot from the player lands where the crosshair is.
        /// </summary>
        public Vector3 AimDirectionFrom(Vector3 origin)
        {
            Transform cam = transform;
            Vector3 target = Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, AimRange, _aimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : cam.position + cam.forward * AimRange;

            Vector3 direction = target - origin;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : cam.forward;
        }

        void LateUpdate()
        {
            if (_target == null)
                return;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = _input.LookDelta * _sensitivity;
                _yaw += look.x;
                _pitch = Mathf.Clamp(_pitch - look.y, _pitchLimits.x, _pitchLimits.y);
            }

            MovementProfile profile = _motor.Profile;

            // The motor's lean moves once per tick; ease toward it at the same pace so it never steps.
            _shownLean = Mathf.MoveTowards(_shownLean, _motor.Lean, profile.LeanSpeed * Time.deltaTime);

            Vector3 head = _target.position + Vector3.up * _motor.EyeHeight;
            Vector3 side = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            float reach = profile.LeanDistance * Mathf.Abs(_shownLean);
            Vector3 direction = side * Mathf.Sign(_shownLean);

            if (reach > 0.001f && Physics.SphereCast(head, _wallClearance, direction, out RaycastHit wall, reach, _wallMask, QueryTriggerInteraction.Ignore))
                reach = wall.distance;

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, -_shownLean * profile.LeanAngle);
            transform.SetPositionAndRotation(head + direction * reach, rotation);
        }
    }
}
