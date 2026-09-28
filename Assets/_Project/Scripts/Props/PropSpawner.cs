using System.Collections.Generic;
using CloseTheDeal.Tower;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Props
{
    /// <summary>
    /// Host only. Once the towers are built, spawns a networked prop at every marker in
    /// both buildings, and despawns them all if the towers are rebuilt.
    /// Lives on the Tower object next to TowerBuilder.
    /// </summary>
    [RequireComponent(typeof(TowerBuilder))]
    public sealed class PropSpawner : NetworkBehaviour
    {
        readonly List<NetworkObject> _spawned = new();
        readonly List<PropMarker> _markers = new();
        TowerBuilder _tower;

        public int SpawnedCount => _spawned.Count;

        void Awake()
        {
            _tower = GetComponent<TowerBuilder>();
            _tower.OnBuilt += OnTowerBuilt;
            _tower.OnCleared += DespawnAll;
        }

        void OnDestroy()
        {
            if (_tower == null)
                return;

            _tower.OnBuilt -= OnTowerBuilt;
            _tower.OnCleared -= DespawnAll;
        }

        void OnTowerBuilt()
        {
            if (!IsServerStarted)
                return;

            DespawnAll();
            _tower.GetComponentsInChildren(true, _markers);
            foreach (PropMarker marker in _markers)
            {
                if (marker.Prefab == null)
                {
                    Debug.LogWarning($"[Props] Marker {marker.name} has no prefab.");
                    continue;
                }

                NetworkObject prop = NetworkManager.GetPooledInstantiated(marker.Prefab, marker.transform.position, marker.transform.rotation, true);
                ServerManager.Spawn(prop);
                _spawned.Add(prop);
            }

            Debug.Log($"[Props] Spawned {_spawned.Count} props.");
        }

        void DespawnAll()
        {
            if (!IsServerStarted)
                return;

            foreach (NetworkObject prop in _spawned)
            {
                if (prop != null && prop.IsSpawned)
                    ServerManager.Despawn(prop);
            }

            _spawned.Clear();
        }
    }
}
