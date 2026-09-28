using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Props
{
    /// <summary>
    /// A loose piece of furniture. The host simulates it; everyone else holds it kinematic
    /// and follows the host's positions through NetworkTransform. Design: docs/systems/PROPS.md.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Prop : NetworkBehaviour
    {
        [Tooltip("How much of a blast's throw this prop resists, as a fraction. 0 = flies like a chair; 0.7 = a heavy desk that mostly shudders.")]
        [Range(0f, 0.95f)] [SerializeField] float _throwResistance;

        Rigidbody _rigidbody;

        public float ThrowResistance => _throwResistance;

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            // Only the host runs prop physics; clients are moved by the synced transform.
            _rigidbody.isKinematic = !IsServerStarted;
        }

        /// <summary>Host only. Adds a velocity change so the throw reads the same whatever the mass.</summary>
        public void Throw(Vector3 velocityChange)
        {
            if (!IsServerStarted)
                return;

            _rigidbody.AddForce(velocityChange * (1f - _throwResistance), ForceMode.VelocityChange);
        }
    }
}
