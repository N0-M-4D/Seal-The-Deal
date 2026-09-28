using CloseTheDeal.Combat;
using CloseTheDeal.Net;
using CloseTheDeal.Player;
using CloseTheDeal.UI;
using FishNet.Component.Spawning;
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        const int PlayerLayer = 6;
        const int TickRate = 60;

        const float BodyHeight = 1.8f;
        const float BodyRadius = 0.35f;
        const float BodyMass = 80f;

        [MenuItem("Close the Deal/Greybox/Set Up Scene")]
        public static void SetUpScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            EnsurePlayerLayer();
            NetworkObject playerPrefab = EnsurePlayerPrefab();

            EnsureFloorAndCamera();
            EnsureOrbitCamera();
            Transform spawnA = EnsureSpawn("SpawnA", new Vector3(-2f, 0.1f, 0f));
            Transform spawnB = EnsureSpawn("SpawnB", new Vector3(2f, 0.1f, 0f));
            EnsureNetworkManager(playerPrefab, spawnA, spawnB);
            SteamLobby lobby = EnsureSteam();
            EnsureLobbyPanel(lobby);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Greybox] Scene set up and saved.");
        }

        [MenuItem("Close the Deal/Greybox/Rebuild Player Prefab")]
        public static void RebuildPlayerPrefab()
        {
            EnsurePlayerLayer();
            BuildPlayerPrefab();
            SetUpScene();
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

            Transform graphics = BuildGraphics(root.transform);

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
            SetReference(serializedMotor, "_cameraTarget", graphics);
            serializedMotor.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[Greybox] Wrote " + PlayerPrefabPath);
            return prefab.GetComponent<NetworkObject>();
        }

        /// <summary>A capsule body with a small block for a nose, so facing reads in greybox.</summary>
        static Transform BuildGraphics(Transform root)
        {
            var graphics = new GameObject("Graphics") { layer = PlayerLayer };
            graphics.transform.SetParent(root, false);

            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Body";
            capsule.layer = PlayerLayer;
            Object.DestroyImmediate(capsule.GetComponent<Collider>());
            capsule.transform.SetParent(graphics.transform, false);
            capsule.transform.localPosition = new Vector3(0f, BodyHeight * 0.5f, 0f);
            capsule.transform.localScale = new Vector3(BodyRadius * 2f, BodyHeight * 0.5f, BodyRadius * 2f);

            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            nose.layer = PlayerLayer;
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(graphics.transform, false);
            nose.transform.localPosition = new Vector3(0f, BodyHeight * 0.8f, BodyRadius + 0.05f);
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

        static void EnsureOrbitCamera()
        {
            Camera cam = Camera.main;
            if (cam != null && cam.GetComponent<ThirdPersonCamera>() == null)
                cam.gameObject.AddComponent<ThirdPersonCamera>();
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

                var spawner = go.AddComponent<PlayerSpawner>();
                spawner.Spawns = new[] { spawnA, spawnB };
            }

            // Always re-point the spawner, so a rebuilt prefab is picked up.
            manager.GetComponent<PlayerSpawner>().SetPlayerPrefab(playerPrefab);
            EnsureTimeManager(manager.gameObject);
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

        static void EnsurePlayerLayer()
        {
            if (LayerMask.LayerToName(PlayerLayer) == PlayerLayerName)
                return;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty slot = layers.GetArrayElementAtIndex(PlayerLayer);
            if (!string.IsNullOrEmpty(slot.stringValue) && slot.stringValue != PlayerLayerName)
            {
                Debug.LogError($"[Greybox] Layer {PlayerLayer} is already '{slot.stringValue}'; expected it free for '{PlayerLayerName}'.");
                return;
            }

            slot.stringValue = PlayerLayerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Greybox] Named layer {PlayerLayer} '{PlayerLayerName}'.");
        }

        // ---- UI ------------------------------------------------------------------------------

        static void EnsureLobbyPanel(SteamLobby lobby)
        {
            if (Object.FindAnyObjectByType<LobbyPanel>() != null)
                return;

            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            var canvasGo = new GameObject("LobbyCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Button host = MakeButton(canvasGo.transform, "HostButton", "Host", new Vector2(120f, -60f), font);
            Button invite = MakeButton(canvasGo.transform, "InviteButton", "Invite", new Vector2(120f, -120f), font);
            Button leave = MakeButton(canvasGo.transform, "LeaveButton", "Leave", new Vector2(120f, -180f), font);
            Text status = MakeText(canvasGo.transform, "Status", "Starting...", new Vector2(260f, -60f), new Vector2(900f, 40f), font);
            Text players = MakeText(canvasGo.transform, "Players", string.Empty, new Vector2(260f, -120f), new Vector2(900f, 40f), font);

            var panel = canvasGo.AddComponent<LobbyPanel>();
            var serialized = new SerializedObject(panel);
            SetReference(serialized, "_lobby", lobby);
            SetReference(serialized, "_hostButton", host);
            SetReference(serialized, "_inviteButton", invite);
            SetReference(serialized, "_leaveButton", leave);
            SetReference(serialized, "_statusText", status);
            SetReference(serialized, "_playersText", players);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static Button MakeButton(Transform parent, string name, string label, Vector2 topLeftOffset, Font font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            Button button = go.AddComponent<Button>();

            RectTransform rect = go.GetComponent<RectTransform>();
            AnchorTopLeft(rect, topLeftOffset, new Vector2(200f, 48f));

            Text text = MakeText(go.transform, "Label", label, Vector2.zero, new Vector2(200f, 48f), font);
            text.alignment = TextAnchor.MiddleCenter;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        static Text MakeText(Transform parent, string name, string content, Vector2 topLeftOffset, Vector2 size, Font font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = 24;
            text.color = Color.white;
            text.text = content;
            text.alignment = TextAnchor.MiddleLeft;
            AnchorTopLeft(text.rectTransform, topLeftOffset, size);
            return text;
        }

        static void AnchorTopLeft(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
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

        static void SetBool(SerializedObject so, string field, bool value)
        {
            SerializedProperty p = Find(so, field);
            if (p != null) p.boolValue = value;
        }

        static void SetInt(SerializedObject so, string field, int value)
        {
            SerializedProperty p = Find(so, field);
            if (p != null) p.intValue = value;
        }

        static void SetEnum(SerializedObject so, string field, int index)
        {
            SerializedProperty p = Find(so, field);
            if (p != null) p.enumValueIndex = index;
        }

        static void SetReference(SerializedObject so, string field, Object value)
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
