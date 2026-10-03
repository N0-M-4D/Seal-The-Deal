using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Local-only over-the-shoulder camera. Follows the owning player's smoothed visual, sits
    /// off to one side so the crosshair is never on the player's own back, and slides in when
    /// a wall is between it and the player. Supplies the yaw the player faces and the aim
    /// direction weapons fire along. Mouse look only applies while the mouse is captured; the
    /// game menu decides that. Nothing here is networked.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        public static ThirdPersonCamera Instance { get; private set; }

        [Tooltip("How far behind the player the camera sits when nothing is in the way, in metres.")]
        [SerializeField] float _distance = 4f;

        [Tooltip("Height above the player's feet the camera orbits around, in metres. Chest-to-head height (1.5) keeps the body low in frame.")]
        [SerializeField] float _pivotHeight = 1.5f;

        [Tooltip("How far right of the player the camera sits, in metres, so the crosshair is clear of the body. 0 = directly behind; 0.6 is a typical over-the-shoulder.")]
        [SerializeField] float _shoulderOffset = 0.6f;

        [Tooltip("Closest the camera gets to a wall it has slid in from, in metres. Keep above 0.1 or the near plane clips into the wall.")]
        [SerializeField] float _wallClearance = 0.25f;

        [Tooltip("Mouse look speed in degrees per mouse unit. 0.12 is a typical shooter feel; higher turns faster.")]
        [SerializeField] float _sensitivity = 0.12f;

        [Tooltip("How far the camera may look up (x, negative) and down (y, positive), in degrees.")]
        [SerializeField] Vector2 _pitchLimits = new(-35f, 70f);

        const float AimRange = 200f;
        const string PlayerLayer = "Player";
        const string PropLayer = "Prop";

        Transform _target;
        PlayerInputReader _input;
        float _yaw;
        float _pitch = 10f;
        int _aimMask;
        int _wallMask;

        /// <summary>Direction the player faces, in degrees around the vertical axis.</summary>
        public float Yaw => _yaw;

        void Awake()
        {
            Instance = this;
            int player = 1 << LayerMask.NameToLayer(PlayerLayer);
            int prop = 1 << LayerMask.NameToLayer(PropLayer);
            _aimMask = ~player;            // Skips all players so you never aim at your own back; the shot from your eye still hits anyone in its path.
            _wallMask = ~(player | prop);  // Only the building pushes the camera in; furniture doesn't.
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
        }

        public void Release(Transform target)
        {
            if (_target != target)
                return;

            _target = null;
            _input = null;
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

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = _target.position + Vector3.up * _pivotHeight;
            Vector3 shoulder = pivot + rotation * Vector3.right * _shoulderOffset;
            Vector3 back = rotation * Vector3.back;

            float distance = _distance;
            if (Physics.SphereCast(shoulder, _wallClearance, back, out RaycastHit wall, _distance, _wallMask, QueryTriggerInteraction.Ignore))
                distance = wall.distance;

            transform.SetPositionAndRotation(shoulder + back * distance, rotation);
        }
    }
}
