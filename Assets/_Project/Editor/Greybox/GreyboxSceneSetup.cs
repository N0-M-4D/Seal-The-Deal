using CloseTheDeal.Combat;
using CloseTheDeal.Net;
using CloseTheDeal.Player;
using CloseTheDeal.Tower;
using CloseTheDeal.UI;
using FishNet.Component.Spawning;
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Multipass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Editor.Greybox
{
    /// <summary>
    /// Builds the greybox scene, the player prefab and the tuning assets from menu items.
    /// "Set Up Scene" only adds what is missing and never rebuilds anything already there.
    /// "Rebuild Player Prefab" is the one deliberate overwrite, kept on its own menu item.
    /// </summary>
    public static class GreyboxSceneSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Greybox.unity";
        const string PrefabFolder = "Assets/_Project/Prefabs";
        const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";
        const string ProfileFolder = "Assets/_Project/Profiles";
        const string MovementProfilePath = ProfileFolder + "/DefaultMovement.asset";
        const string BlastProfilePath = ProfileFolder + "/TestBlast.asset";
        const string PhysicsFolder = "Assets/_Project/Physics";
        const string PlayerPhysicsMaterialPath = PhysicsFolder + "/Frictionless.physicsMaterial";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        const string PlayerLayerName = "Player";
        const string PropLayerName = "Prop";
        public const int PlayerLayer = 6;
        public const int PropLayer = 7;
        const int TickRate = 60;

        const float BodyHeight = 1.8f;
        const float BodyRadius = 0.35f;
        const float BodyMass = 80f;
        const float HipHeight = 0.9f;

        [MenuItem("Close the Deal/Greybox/Set Up Scene")]
        public static void SetUpScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            EnsureLayers();
            NetworkObject playerPrefab = EnsurePlayerPrefab();

            EnsureFloorAndCamera();
            EnsureObstacles();
            EnsurePlayerCamera();
            Transform spawnA = EnsureSpawn("SpawnA", new Vector3(-2f, 0.1f, 0f));
            Transform spawnB = EnsureSpawn("SpawnB", new Vector3(2f, 0.1f, 0f));
            EnsureNetworkManager(playerPrefab, spawnA, spawnB);
            GreyboxTowerSetup.Ensure();
            SteamLobby lobby = EnsureSteam();
            GreyboxUiSetup.Ensure(lobby);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Greybox] Scene set up and saved.");
        }

        [MenuItem("Close the Deal/Greybox/Rebuild Player Prefab")]
        public static void RebuildPlayerPrefab()
        {
            EnsureLayers();
            BuildPlayerPrefab();
            SetUpScene();
        }

        /// <summary>Player prefab, prop prefabs, floor templates and the scene, all rewritten from the tool.</summary>
        [MenuItem("Close the Deal/Greybox/Rebuild Everything")]
        public static void RebuildEverything()
        {
            EnsureLayers();
            BuildPlayerPrefab();
            GreyboxPropSetup.Ensure(true);
            GreyboxTowerSetup.RebuildFloorTemplates();
        }

        // ---- Player prefab -------------------------------------------------------------------

        static NetworkObject EnsurePlayerPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (existing == null)
                return BuildPlayerPrefab();

            if (existing.GetComponent<PlayerMotor>() == null)
                Debug.LogWarning("[Greybox] Player.prefab is from before PlayerMotor. Run Close the Deal > Greybox > Rebuild Player Prefab.");

            return existing.GetComponent<NetworkObject>();
        }

        /// <summary>Writes Player.prefab from scratch. Saving over the existing path keeps its GUID, so scene references survive.</summary>
        static NetworkObject BuildPlayerPrefab()
        {
            EnsureFolder("Assets/_Project", "Prefabs");
            MovementProfile movement = EnsureAsset<MovementProfile>(ProfileFolder, MovementProfilePath);
            BlastProfile blast = EnsureAsset<BlastProfile>(ProfileFolder, BlastProfilePath);
            PhysicsMaterial frictionless = EnsureFrictionlessMaterial();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null)
                Debug.LogError("[Greybox] Input actions asset missing at " + InputActionsPath);

            var root = new GameObject("Player") { layer = PlayerLayer };

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = BodyHeight;
            capsule.radius = BodyRadius;
            capsule.center = new Vector3(0f, BodyHeight * 0.5f, 0f);
            capsule.material = frictionless;

            var body = root.AddComponent<Rigidbody>();
            body.mass = BodyMass;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;

            Transform graphics = BuildGraphics(root.transform, out Transform leanPivot);

            var networkObject = root.AddComponent<NetworkObject>();
            var input = root.AddComponent<PlayerInputReader>();
            var motor = root.AddComponent<PlayerMotor>();

            var serializedNetworkObject = new SerializedObject(networkObject);
            SetBool(serializedNetworkObject, "_enablePrediction", true);
            SetEnum(serializedNetworkObject, "_predictionType", 1); // Rigidbody
            SetReference(serializedNetworkObject, "_graphicalObject", graphics);
            serializedNetworkObject.ApplyModifiedPropertiesWithoutUndo();

            var serializedInput = new SerializedObject(input);
            SetReference(serializedInput, "_actions", actions);
            serializedInput.ApplyModifiedPropertiesWithoutUndo();

            var serializedMotor = new SerializedObject(motor);
            SetReference(serializedMotor, "_profile", movement);
            SetReference(serializedMotor, "_blast", blast);
            SetInt(serializedMotor, "_groundMask", ~(1 << PlayerLayer));
            SetInt(serializedMotor, "_playerMask", 1 << PlayerLayer);
            SetInt(serializedMotor, "_propMask", 1 << PropLayer);
            SetReference(serializedMotor, "_cameraTarget", graphics);
            SetReference(serializedMotor, "_leanPivot", leanPivot);
            serializedMotor.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[Greybox] Wrote " + PlayerPrefabPath);
            return prefab.GetComponent<NetworkObject>();
        }

        /// <summary>
        /// A capsule body with a small block for a nose, so facing reads in greybox. Both hang
        /// off a pivot at hip height that the motor tilts when the player leans.
        /// </summary>
        static Transform BuildGraphics(Transform root, out Transform leanPivot)
        {
            var graphics = new GameObject("Graphics") { layer = PlayerLayer };
            graphics.transform.SetParent(root, false);

            var lean = new GameObject("Lean") { layer = PlayerLayer };
            lean.transform.SetParent(graphics.transform, false);
            lean.transform.localPosition = new Vector3(0f, HipHeight, 0f);
            leanPivot = lean.transform;

            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Body";
            capsule.layer = PlayerLayer;
            Object.DestroyImmediate(capsule.GetComponent<Collider>());
            capsule.transform.SetParent(leanPivot, false);
            capsule.transform.localPosition = new Vector3(0f, BodyHeight * 0.5f - HipHeight, 0f);
            capsule.transform.localScale = new Vector3(BodyRadius * 2f, BodyHeight * 0.5f, BodyRadius * 2f);

            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            nose.layer = PlayerLayer;
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(leanPivot, false);
            nose.transform.localPosition = new Vector3(0f, BodyHeight * 0.8f - HipHeight, BodyRadius + 0.05f);
            nose.transform.localScale = new Vector3(0.15f, 0.15f, 0.2f);

            return graphics.transform;
        }

        static PhysicsMaterial EnsureFrictionlessMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PlayerPhysicsMaterialPath);
            if (existing != null)
                return existing;

            EnsureFolder("Assets/_Project", "Physics");
            var material = new PhysicsMaterial("Frictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(material, PlayerPhysicsMaterialPath);
            return material;
        }

        // ---- World ---------------------------------------------------------------------------

        static void EnsureFloorAndCamera()
        {
            if (GameObject.Find("Floor") != null)
                return;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(4f, 1f, 4f); // a 40 m square

            // First time only: point the template camera down at the floor.
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 14f, -14f);
                cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            }
        }

        /// <summary>Things to climb and bump into: blocks at desk, sill and chest height, one too tall, and a ramp.</summary>
        static void EnsureObstacles()
        {
            if (GameObject.Find("Obstacles") != null)
                return;

            var parent = new GameObject("Obstacles");
            MakeBlock(parent.transform, "Desk 0.75m", new Vector3(-6f, 0f, 6f), new Vector3(2f, 0.75f, 1f));
            MakeBlock(parent.transform, "Sill 1.1m", new Vector3(-2f, 0f, 6f), new Vector3(2f, 1.1f, 1f));
            MakeBlock(parent.transform, "Ledge 1.5m", new Vector3(2f, 0f, 6f), new Vector3(2f, 1.5f, 1f));
            MakeBlock(parent.transform, "Wall 2.5m", new Vector3(6f, 0f, 6f), new Vector3(2f, 2.5f, 1f));
            MakeBlock(parent.transform, "Upper floor", new Vector3(0f, 0f, 12f), new Vector3(10f, 3f, 4f));

            // Stairs must be ramps for the capsule body (PLAYER_MOVEMENT.md, level rules).
            GameObject ramp = MakeBlock(parent.transform, "Ramp", new Vector3(-8f, 0f, 0f), new Vector3(2f, 0.2f, 6f));
            ramp.transform.position = new Vector3(-8f, 0.75f, -3f);
            ramp.transform.rotation = Quaternion.Euler(-14f, 0f, 0f);
        }

        static GameObject MakeBlock(Transform parent, string name, Vector3 footprintOrigin, Vector3 size)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localScale = size;
            block.transform.position = footprintOrigin + Vector3.up * (size.y * 0.5f);
            return block;
        }

        static void EnsurePlayerCamera()
        {
            Camera cam = Camera.main;
            if (cam != null && cam.GetComponent<PlayerCamera>() == null)
                cam.gameObject.AddComponent<PlayerCamera>();
        }

        static Transform EnsureSpawn(string name, Vector3 position)
        {
            GameObject found = GameObject.Find(name);
            if (found != null)
                return found.transform;

            var spawn = new GameObject(name);
            spawn.transform.position = position;
            return spawn.transform;
        }

        // ---- Networking ----------------------------------------------------------------------

        static void EnsureNetworkManager(NetworkObject playerPrefab, Transform spawnA, Transform spawnB)
        {
            NetworkManager manager = Object.FindAnyObjectByType<NetworkManager>();
            if (manager == null)
            {
                var go = new GameObject("NetworkManager");
                manager = go.AddComponent<NetworkManager>();

                var transportManager = go.AddComponent<TransportManager>();
                var transport = go.AddComponent<global::FishySteamworks.FishySteamworks>();
                transportManager.Transport = transport;

                // Steam relays traffic between friends over the peer-to-peer socket, not an IP socket.
                var serialized = new SerializedObject(transport);
                SetBool(serialized, "_peerToPeer", true);
                SetInt(serialized, "_maximumClients", 4);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EnsureTeamSpawner(manager.gameObject, playerPrefab, spawnA, spawnB);
            EnsureTimeManager(manager.gameObject);
            EnsureMultipass(manager.gameObject);
        }

        /// <summary>
        /// FishNet wires up only the transport set when it starts, so Steam and local mode both
        /// live inside a Multipass from the start: index 0 Steam, index 1 direct on this PC.
        /// SteamLobby picks one per session. Server actions are per transport, never global,
        /// so the Steam server is never started when Steam is not running.
        /// </summary>
        static void EnsureMultipass(GameObject managerObject)
        {
            var steam = managerObject.GetComponent<global::FishySteamworks.FishySteamworks>();
            if (steam == null)
                steam = managerObject.AddComponent<global::FishySteamworks.FishySteamworks>();

            var local = managerObject.GetComponent<FishNet.Transporting.Tugboat.Tugboat>();
            if (local == null)
                local = managerObject.AddComponent<FishNet.Transporting.Tugboat.Tugboat>();

            var multipass = managerObject.GetComponent<Multipass>();
            if (multipass == null)
                multipass = managerObject.AddComponent<Multipass>();

            multipass.GlobalServerActions = false;
            var serialized = new SerializedObject(multipass);
            SerializedProperty transports = Find(serialized, "_transports");
            if (transports != null)
            {
                transports.arraySize = 2;
                transports.GetArrayElementAtIndex(0).objectReferenceValue = steam;
                transports.GetArrayElementAtIndex(1).objectReferenceValue = local;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var transportManager = managerObject.GetComponent<TransportManager>();
            var serializedManager = new SerializedObject(transportManager);
            SetReference(serializedManager, "Transport", multipass);
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Our team-aware spawner replaces FishNet's PlayerSpawner; the prefab is always re-pointed so a rebuilt one is picked up.</summary>
        static void EnsureTeamSpawner(GameObject managerObject, NetworkObject playerPrefab, Transform spawnA, Transform spawnB)
        {
            PlayerSpawner old = managerObject.GetComponent<PlayerSpawner>();
            if (old != null)
                Object.DestroyImmediate(old);

            TeamSpawner spawner = managerObject.GetComponent<TeamSpawner>();
            if (spawner == null)
            {
                spawner = managerObject.AddComponent<TeamSpawner>();
                spawner.FallbackSpawns = new[] { spawnA, spawnB };
            }

            spawner.SetPlayerPrefab(playerPrefab);
        }

        /// <summary>FishNet must drive physics itself for rigidbody prediction; 60 ticks a second.</summary>
        static void EnsureTimeManager(GameObject managerObject)
        {
            TimeManager timeManager = managerObject.GetComponent<TimeManager>();
            if (timeManager == null)
                timeManager = managerObject.AddComponent<TimeManager>();

            var serialized = new SerializedObject(timeManager);
            SetEnum(serialized, "_physicsMode", (int)PhysicsMode.TimeManager);
            SetInt(serialized, "_tickRate", TickRate);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static SteamLobby EnsureSteam()
        {
            SteamLobby existing = Object.FindAnyObjectByType<SteamLobby>();
            if (existing != null)
                return existing;

            var go = new GameObject("Steam");
            go.AddComponent<SteamService>();
            return go.AddComponent<SteamLobby>();
        }

        // ---- Project settings ----------------------------------------------------------------

        static void EnsureLayers()
        {
            EnsureLayer(PlayerLayer, PlayerLayerName);
            EnsureLayer(PropLayer, PropLayerName);
        }

        static void EnsureLayer(int index, string name)
        {
            if (LayerMask.LayerToName(index) == name)
                return;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty slot = layers.GetArrayElementAtIndex(index);
            if (!string.IsNullOrEmpty(slot.stringValue) && slot.stringValue != name)
            {
                Debug.LogError($"[Greybox] Layer {index} is already '{slot.stringValue}'; expected it free for '{name}'.");
                return;
            }

            slot.stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Greybox] Named layer {index} '{name}'.");
        }

        // ---- Asset and serialized-field helpers ----------------------------------------------

        static T EnsureAsset<T>(string folder, string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            EnsureFolder("Assets/_Project", folder.Substring(folder.LastIndexOf('/') + 1));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log("[Greybox] Created " + path);
            return asset;
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        internal static void SetBool(SerializedObject so, string field, bool value)
        {
            SerializedProperty p = Find(so, field);
            if (p != null) p.boolValue = value;
        }

        internal static void SetInt(SerializedObject so, string field, int value)
        {
            SerializedProperty p = Find(so, field);
            if (p != null) p.intValue = value;
        }

        internal static void SetFloat(SerializedObject so, string field, float value)
        {
            SerializedProperty p = Find(so, field);
            if (p != null) p.floatValue = value;
        }

        internal static void SetEnum(SerializedObject so, string field, int index)
        {
            SerializedProperty p = Find(so, field);
            if (p != null) p.enumValueIndex = index;
        }

        internal static void SetReference(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = Find(so, field);
            if (p != null) p.objectReferenceValue = value;
        }

        static SerializedProperty Find(SerializedObject so, string field)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
                Debug.LogWarning($"[Greybox] {so.targetObject.GetType().Name} has no field '{field}'; set it by hand in the Inspector.");
            return p;
        }
    }
}
