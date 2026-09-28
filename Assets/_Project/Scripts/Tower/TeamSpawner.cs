using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Tower
{
    /// <summary>
    /// Spawns each joining player in their team's lobby. Replaces FishNet's PlayerSpawner on
    /// the NetworkManager. Host only: the host decides teams and positions. The host is
    /// team A; joiners alternate B, A, B by their connection id.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TeamSpawner : MonoBehaviour
    {
        [Tooltip("The networked player prefab.")]
        [SerializeField] NetworkObject _playerPrefab;

        [Tooltip("Tick to spawn in the team lobbies of the tower. Untick to use the fallback spawns on the flat test area instead.")]
        [SerializeField] bool _spawnInTower = true;

        [Tooltip("Used when there is no tower, or Spawn In Tower is off. Cycled in order.")]
        public Transform[] FallbackSpawns = new Transform[0];

        NetworkManager _networkManager;
        readonly int[] _spawnedPerTeam = new int[TowerBuilder.TeamCount];
        int _nextFallback;

        public void SetPlayerPrefab(NetworkObject prefab) => _playerPrefab = prefab;

        void Awake()
        {
            _networkManager = GetComponentInParent<NetworkManager>();
            if (_networkManager == null)
                _networkManager = InstanceFinder.NetworkManager;

            if (_networkManager == null)
            {
                Debug.LogWarning("[TeamSpawner] No NetworkManager on this object or its parents.");
                return;
            }

            _networkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
        }

        void OnDestroy()
        {
            if (_networkManager != null)
                _networkManager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
        }

        void OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (!asServer)
                return;

            if (_playerPrefab == null)
            {
                Debug.LogWarning($"[TeamSpawner] No player prefab; nothing spawned for connection {conn.ClientId}.");
                return;
            }

            int team = conn.ClientId % TowerBuilder.TeamCount;
            ChooseSpawn(team, out Vector3 position, out Quaternion rotation);

            NetworkObject player = _networkManager.GetPooledInstantiated(_playerPrefab, position, rotation, true);
            _networkManager.ServerManager.Spawn(player, conn);
            _networkManager.SceneManager.AddOwnerToDefaultScene(player);
        }

        void ChooseSpawn(int team, out Vector3 position, out Quaternion rotation)
        {
            TowerBuilder tower = TowerBuilder.Instance;
            if (_spawnInTower && tower != null && tower.TryGetSpawn(team, _spawnedPerTeam[team], out position, out rotation))
            {
                _spawnedPerTeam[team]++;
                return;
            }

            if (FallbackSpawns.Length > 0)
            {
                Transform point = FallbackSpawns[_nextFallback % FallbackSpawns.Length];
                _nextFallback++;
                position = point.position;
                rotation = point.rotation;
                return;
            }

            position = _playerPrefab.transform.position;
            rotation = _playerPrefab.transform.rotation;
        }
    }
}
