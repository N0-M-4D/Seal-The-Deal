using CloseTheDeal.Net;
using CloseTheDeal.Player;
using CloseTheDeal.UI;
using FishNet.Component.Spawning;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CloseTheDeal.Editor.Greybox
{
    /// <summary>
    /// Builds the greybox lobby scene and the player prefab from a menu item.
    /// Only adds what is missing: anything already in the scene or on disk is left exactly
    /// as it is, so re-running it never destroys hand-placed work.
    /// </summary>
    public static class GreyboxSceneSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Greybox.unity";
        const string PrefabFolder = "Assets/_Project/Prefabs";
        const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";

        [MenuItem("Close the Deal/Greybox/Set Up Scene")]
        public static void SetUpScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            NetworkObject playerPrefab = EnsurePlayerPrefab();

            EnsureFloorAndCamera();
            Transform spawnA = EnsureSpawn("SpawnA", new Vector3(-2f, 1.1f, 0f));
            Transform spawnB = EnsureSpawn("SpawnB", new Vector3(2f, 1.1f, 0f));
            EnsureNetworkManager(playerPrefab, spawnA, spawnB);
            SteamLobby lobby = EnsureSteam();
            EnsureLobbyPanel(lobby);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Greybox] Scene set up and saved.");
        }

        // ---- Player prefab -------------------------------------------------------------------

        static NetworkObject EnsurePlayerPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (existing != null)
                return existing.GetComponent<NetworkObject>();

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            temp.name = "Player";
            // CharacterController brings its own capsule; the primitive's collider would double up.
            Object.DestroyImmediate(temp.GetComponent<CapsuleCollider>());

            var controller = temp.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;

            temp.AddComponent<NetworkObject>();
            temp.AddComponent<NetworkTransform>();
            temp.AddComponent<GreyboxMover>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, PlayerPrefabPath);
            Object.DestroyImmediate(temp);
            Debug.Log("[Greybox] Created " + PlayerPrefabPath);
            return prefab.GetComponent<NetworkObject>();
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
            if (Object.FindFirstObjectByType<NetworkManager>() != null)
                return;

            var go = new GameObject("NetworkManager");
            go.AddComponent<NetworkManager>();

            var transportManager = go.AddComponent<TransportManager>();
            var transport = go.AddComponent<global::FishySteamworks.FishySteamworks>();
            transportManager.Transport = transport;

            // Steam relays traffic between friends over the peer-to-peer socket, not an IP socket.
            var serialized = new SerializedObject(transport);
            SetBool(serialized, "_peerToPeer", true);
            SetInt(serialized, "_maximumClients", 4);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var spawner = go.AddComponent<PlayerSpawner>();
            spawner.SetPlayerPrefab(playerPrefab);
            spawner.Spawns = new[] { spawnA, spawnB };
        }

        static SteamLobby EnsureSteam()
        {
            SteamLobby existing = Object.FindFirstObjectByType<SteamLobby>();
            if (existing != null)
                return existing;

            var go = new GameObject("Steam");
            go.AddComponent<SteamService>();
            return go.AddComponent<SteamLobby>();
        }

        // ---- UI ------------------------------------------------------------------------------

        static void EnsureLobbyPanel(SteamLobby lobby)
        {
            if (Object.FindFirstObjectByType<LobbyPanel>() != null)
                return;

            if (Object.FindFirstObjectByType<EventSystem>() == null)
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
            serialized.FindProperty("_lobby").objectReferenceValue = lobby;
            serialized.FindProperty("_hostButton").objectReferenceValue = host;
            serialized.FindProperty("_inviteButton").objectReferenceValue = invite;
            serialized.FindProperty("_leaveButton").objectReferenceValue = leave;
            serialized.FindProperty("_statusText").objectReferenceValue = status;
            serialized.FindProperty("_playersText").objectReferenceValue = players;
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

        static void SetBool(SerializedObject so, string field, bool value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) Debug.LogWarning($"[Greybox] FishySteamworks has no field '{field}'; set it by hand in the Inspector.");
            else p.boolValue = value;
        }

        static void SetInt(SerializedObject so, string field, int value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) Debug.LogWarning($"[Greybox] FishySteamworks has no field '{field}'; set it by hand in the Inspector.");
            else p.intValue = value;
        }
    }
}
