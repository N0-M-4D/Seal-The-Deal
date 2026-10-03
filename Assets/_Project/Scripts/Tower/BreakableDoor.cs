using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace CloseTheDeal.Tower
{
    /// <summary>
    /// A locked door across the spiral stairs. Blasts wear it down; when it breaks it vanishes
    /// and the way up is open. The host owns the hit count; everyone hides the door once it
    /// reaches the limit. Spawned by the host at a PropMarker like furniture. Design: docs/systems/TOWER.md.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BreakableDoor : NetworkBehaviour
    {
        [Tooltip("How many blasts it takes to break the door. 1 = any hit opens it; 3 holds a team for a few seconds of shooting.")]
        [Range(1, 20)] [SerializeField] int _hitsToBreak = 3;

        // Hits taken, not hits left, so the unsynced default (0) reads as a whole door.
        readonly SyncVar<byte> _hits = new();
        Collider[] _colliders;
        Renderer[] _renderers;

        public bool IsBroken => _hits.Value >= _hitsToBreak;

        void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            _hits.OnChange += OnHitsChanged;
        }

        void OnDestroy()
        {
            _hits.OnChange -= OnHitsChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // A pooled door comes back whole.
            _hits.Value = 0;
            ApplyBroken(false);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // A late joiner gets the current count before this runs.
            ApplyBroken(IsBroken);
        }

        /// <summary>Host only. One blast's worth of damage; ignored anywhere else and once broken.</summary>
        public void TakeBlast()
        {
            if (!IsServerStarted || IsBroken)
                return;

            _hits.Value++;
        }

        void OnHitsChanged(byte previous, byte next, bool asServer)
        {
            ApplyBroken(IsBroken);
        }

        void ApplyBroken(bool broken)
        {
            foreach (Collider c in _colliders)
                c.enabled = !broken;
            foreach (Renderer r in _renderers)
                r.enabled = !broken;
        }
    }
}
