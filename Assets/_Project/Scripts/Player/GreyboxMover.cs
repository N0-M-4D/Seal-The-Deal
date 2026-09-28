using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// TEMPORARY greybox movement so two players can be seen walking about after the lobby
    /// connects. The owning client moves itself and NetworkTransform copies the result, which
    /// is client-authoritative and breaks AGENTS.md §3 on purpose for this one step.
    /// Replaced wholesale by host-authoritative predicted movement in the movement task;
    /// do not build on it.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class GreyboxMover : NetworkBehaviour
    {
        [Tooltip("Walking speed in metres per second. Higher = faster across the floor.")]
        [SerializeField] float _moveSpeed = 6f;

        const float Gravity = -9.81f;
        const float GroundedFallSpeed = -1f;

        CharacterController _controller;
        float _verticalSpeed;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Only the owner reads input. Everyone else just receives the synced transform.
            enabled = IsOwner;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float z = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            Vector3 planar = Vector3.ClampMagnitude(new Vector3(x, 0f, z), 1f) * _moveSpeed;

            if (_controller.isGrounded && _verticalSpeed < 0f)
                _verticalSpeed = GroundedFallSpeed;
            _verticalSpeed += Gravity * Time.deltaTime;

            Vector3 motion = planar;
            motion.y = _verticalSpeed;
            _controller.Move(motion * Time.deltaTime);
        }
    }
}
