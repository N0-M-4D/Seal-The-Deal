using CloseTheDeal.Tower;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Greybox
{
    /// <summary>
    /// Builds the greybox floor template prefabs and the tower profile, and places the Tower
    /// object in the scene. Templates follow the shell in docs/systems/TOWER.md and read their
    /// dimensions from the tower profile, so change the profile and rebuild rather than
    /// editing the prefabs. "Rebuild Floor Templates" is the one deliberate overwrite.
    /// </summary>
    public static class GreyboxTowerSetup
    {
        const string FloorFolder = "Assets/_Project/Floors";
        const string ProfileFolder = "Assets/_Project/Profiles";
        const string TowerProfilePath = ProfileFolder + "/DefaultTower.asset";
        const string TowerObjectName = "Tower";
        static readonly Vector3 TowerOrigin = new(80f, 0f, 0f);

        const float Slab = 0.2f;
        const float Wall = 0.2f;
        const float SillHeight = 1f;
        const float PillarWidth = 0.4f;
        const float PillarSpacing = 4f;
        const float DoorWidth = 1.2f;
        const float RampWidth = 2f;
        const float LandingWidth = 1f;
        const float TopLandingWidth = 1.6f;
        const float ParapetHeight = 1.2f;

        // Service spine rooms across the width, left to right, as fractions of the width.
        static readonly (string Name, float Fraction)[] SpineRooms =
        {
            ("WC", 0.15f), ("STORE", 0.25f), ("STAIRS", 0.40f), ("UTIL", 0.20f)
        };
        const int StairsRoom = 2;

        [MenuItem("Close the Deal/Greybox/Rebuild Floor Templates")]
        public static void RebuildFloorTemplates()
        {
            TowerProfile profile = EnsureProfile();
            BuildAllTemplates(profile, true);
            GreyboxSceneSetup.SetUpScene();
        }

        /// <summary>Called by Set Up Scene: creates only what is missing.</summary>
        public static void Ensure()
        {
            TowerProfile profile = EnsureProfile();
            BuildAllTemplates(profile, false);
            EnsureTowerObject(profile);
        }

        // ---- Profile and templates -----------------------------------------------------------

        static TowerProfile EnsureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TowerProfile>(TowerProfilePath);
            if (profile != null)
                return profile;

            if (!AssetDatabase.IsValidFolder(ProfileFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Profiles");

            profile = ScriptableObject.CreateInstance<TowerProfile>();
            AssetDatabase.CreateAsset(profile, TowerProfilePath);
            Debug.Log("[Tower] Created " + TowerProfilePath);
            return profile;
        }

        static void BuildAllTemplates(TowerProfile profile, bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder(FloorFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Floors");

            var serialized = new SerializedObject(profile);
            AssignIfBuilt(serialized, "Lobby", BuildTemplate(profile, "Lobby", FloorKind.Lobby, 0, overwrite));
            AssignIfBuilt(serialized, "Checkpoint", BuildTemplate(profile, "Checkpoint", FloorKind.Checkpoint, 0, overwrite));
            AssignIfBuilt(serialized, "Boardroom", BuildTemplate(profile, "Boardroom", FloorKind.Boardroom, 0, overwrite));
            AssignIfBuilt(serialized, "Roof", BuildTemplate(profile, "Roof", FloorKind.Roof, 0, overwrite));

            SerializedProperty offices = serialized.FindProperty("Offices");
            offices.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                FloorTemplate office = BuildTemplate(profile, $"Office{(char)('A' + i)}", FloorKind.Office, i, overwrite);
                if (office != null)
                    offices.GetArrayElementAtIndex(i).objectReferenceValue = office;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        static void AssignIfBuilt(SerializedObject profile, string field, FloorTemplate template)
        {
            if (template != null)
                profile.FindProperty(field).objectReferenceValue = template;
        }

        /// <summary>Writes one floor prefab. Saving over an existing path keeps its GUID.</summary>
        static FloorTemplate BuildTemplate(TowerProfile profile, string name, FloorKind kind, int variant, bool overwrite)
        {
            string path = $"{FloorFolder}/Floor_{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !overwrite)
                return existing.GetComponent<FloorTemplate>();

            var root = new GameObject("Floor_" + name);
            var template = root.AddComponent<FloorTemplate>();
            template.Kind = kind;

            BuildShell(root.transform, profile, kind);
            BuildContents(root.transform, profile, kind, variant);
            if (kind == FloorKind.Lobby)
                template.SpawnPoints = BuildSpawns(root.transform, profile);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log("[Tower] Wrote " + path);
            return prefab.GetComponent<FloorTemplate>();
        }

        // ---- Shell ---------------------------------------------------------------------------

        static void BuildShell(Transform floor, TowerProfile p, FloorKind kind)
        {
            float w = p.Width;
            float depth = p.MainDepth + p.SpineDepth;
            float h = p.FloorHeight;
            float half = w * 0.5f;
            bool hasHole = kind != FloorKind.Lobby;
            bool hasRamp = kind != FloorKind.Roof;

            Transform shell = Group(floor, "Shell");
            RoomBounds stairs = RoomX(p, StairsRoom);

            // Slab: full main floor, and the spine with a hole over the stairs room for the ramp below.
            Box(shell, "Slab main", new Vector3(0f, -Slab * 0.5f, -p.MainDepth * 0.5f), new Vector3(w, Slab, p.MainDepth));
            float spineZ = -p.MainDepth - p.SpineDepth * 0.5f;
            if (hasHole)
            {
                Box(shell, "Slab spine left", new Vector3((-half + stairs.Left) * 0.5f, -Slab * 0.5f, spineZ), new Vector3(stairs.Left + half, Slab, p.SpineDepth));
                Box(shell, "Slab spine right", new Vector3((stairs.Right + half) * 0.5f, -Slab * 0.5f, spineZ), new Vector3(half - stairs.Right, Slab, p.SpineDepth));
            }
            else
            {
                Box(shell, "Slab spine", new Vector3(0f, -Slab * 0.5f, spineZ), new Vector3(w, Slab, p.SpineDepth));
            }

            float wallHeight = kind == FloorKind.Roof ? ParapetHeight : h;
            float wallY = wallHeight * 0.5f;
            Box(shell, "Wall back", new Vector3(0f, wallY, -depth + Wall * 0.5f), new Vector3(w, wallHeight, Wall));
            Box(shell, "Wall left", new Vector3(-half + Wall * 0.5f, wallY, -depth * 0.5f), new Vector3(Wall, wallHeight, depth));
            Box(shell, "Wall right", new Vector3(half - Wall * 0.5f, wallY, -depth * 0.5f), new Vector3(Wall, wallHeight, depth));

            if (kind == FloorKind.Roof)
            {
                Box(shell, "Parapet front", new Vector3(0f, wallY, -Wall * 0.5f), new Vector3(w, wallHeight, Wall));
            }
            else
            {
                BuildWindowWall(shell, p);
                BuildSpinePartitions(shell, p);
            }

            if (hasRamp)
                BuildRamp(shell, p, stairs);
        }

        /// <summary>The rival-facing side: a sill and pillars, open above the sill so sightlines always line up.</summary>
        static void BuildWindowWall(Transform shell, TowerProfile p)
        {
            float half = p.Width * 0.5f;
            Box(shell, "Sill", new Vector3(0f, SillHeight * 0.5f, -Wall * 0.5f), new Vector3(p.Width, SillHeight, Wall));

            float pillarHeight = p.FloorHeight - SillHeight;
            for (float x = -half; x <= half + 0.01f; x += PillarSpacing)
            {
                float clampedX = Mathf.Clamp(x, -half + PillarWidth * 0.5f, half - PillarWidth * 0.5f);
                Box(shell, "Pillar", new Vector3(clampedX, SillHeight + pillarHeight * 0.5f, -Wall * 0.5f), new Vector3(PillarWidth, pillarHeight, Wall));
            }
        }

        /// <summary>The wall between main floor and spine with a door per room, and the walls between rooms.</summary>
        static void BuildSpinePartitions(Transform shell, TowerProfile p)
        {
            float h = p.FloorHeight;
            float frontZ = -p.MainDepth + Wall * 0.5f;
            float backZ = -p.MainDepth - p.SpineDepth;

            for (int i = 0; i < SpineRooms.Length; i++)
            {
                RoomBounds room = RoomX(p, i);
                // The stairs door sits over the top landing, where the ramp from below arrives.
                float doorCentre = i == StairsRoom ? room.Left + TopLandingWidth * 0.5f : room.Centre;
                doorCentre = Mathf.Clamp(doorCentre, room.Left + DoorWidth * 0.5f + Wall, room.Right - DoorWidth * 0.5f - Wall);

                float leftLength = doorCentre - DoorWidth * 0.5f - room.Left;
                float rightLength = room.Right - (doorCentre + DoorWidth * 0.5f);
                Box(shell, SpineRooms[i].Name + " front left", new Vector3(room.Left + leftLength * 0.5f, h * 0.5f, frontZ), new Vector3(leftLength, h, Wall));
                Box(shell, SpineRooms[i].Name + " front right", new Vector3(room.Right - rightLength * 0.5f, h * 0.5f, frontZ), new Vector3(rightLength, h, Wall));

                if (i > 0)
                    Box(shell, SpineRooms[i].Name + " divider", new Vector3(room.Left, h * 0.5f, (frontZ + backZ) * 0.5f), new Vector3(Wall, h, p.SpineDepth - Wall));
            }
        }

        /// <summary>
        /// Switchback ramp: up along +X at the back, a landing, then back along −X at the front
        /// onto a flat top landing level with the next floor, right behind that floor's door.
        /// </summary>
        static void BuildRamp(Transform shell, TowerProfile p, RoomBounds stairs)
        {
            float rise = p.FloorHeight * 0.5f;
            float turnX = stairs.Right - LandingWidth;
            float backZ = -p.MainDepth - p.SpineDepth + RampWidth * 0.5f;
            float frontZ = -p.MainDepth - RampWidth * 0.5f;

            Ramp(shell, "Ramp up", stairs.Left, turnX, 0f, rise, backZ);
            Box(shell, "Landing", new Vector3(stairs.Right - LandingWidth * 0.5f, rise - Slab * 0.5f, -p.MainDepth - p.SpineDepth * 0.5f), new Vector3(LandingWidth, Slab, p.SpineDepth));
            Ramp(shell, "Ramp on", turnX, stairs.Left + TopLandingWidth, rise, p.FloorHeight, frontZ);
            Box(shell, "Top landing", new Vector3(stairs.Left + TopLandingWidth * 0.5f, p.FloorHeight - Slab * 0.5f, frontZ), new Vector3(TopLandingWidth, Slab, RampWidth));
        }

        /// <summary>A sloped slab whose top surface runs from (fromX, fromY) to (toX, toY) at depth z.</summary>
        static void Ramp(Transform parent, string name, float fromX, float toX, float fromY, float toY, float z)
        {
            float dx = toX - fromX;
            float dy = toY - fromY;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            // Drop the centre by half the thickness along the slab's own down direction, so the top face is the path.
            Vector3 centre = new Vector3((fromX + toX) * 0.5f, (fromY + toY) * 0.5f, z) + rotation * new Vector3(0f, -Slab * 0.5f, 0f);
            Box(parent, name, centre, new Vector3(length, Slab, RampWidth), rotation);
        }

        // ---- Contents ------------------------------------------------------------------------

        static void BuildContents(Transform floor, TowerProfile p, FloorKind kind, int variant)
        {
            Transform contents = Group(floor, "Contents");
            float midZ = -p.MainDepth * 0.5f;

            switch (kind)
            {
                case FloorKind.Lobby:
                    Box(contents, "Reception desk", new Vector3(0f, 0.55f, -p.MainDepth + 2f), new Vector3(5f, 1.1f, 0.8f));
                    break;
                case FloorKind.Checkpoint:
                    Box(contents, "Checkpoint marker", new Vector3(0f, 1.5f, midZ), new Vector3(1f, 3f, 1f));
                    break;
                case FloorKind.Boardroom:
                    Box(contents, "Board table", new Vector3(0f, 0.4f, midZ), new Vector3(8f, 0.8f, 2.5f));
                    for (int i = 0; i < 6; i++)
                        Box(contents, "Chair", new Vector3(-5f + i * 2f, 0.5f, midZ + (i % 2 == 0 ? 2f : -2f)), new Vector3(0.6f, 1f, 0.6f));
                    break;
                case FloorKind.Roof:
                    Box(contents, "Roof access", new Vector3(p.Width * 0.5f - 3f, 1.5f, -p.MainDepth - 2f), new Vector3(3f, 3f, 3f));
                    break;
                default:
                    BuildOffice(contents, p, variant);
                    break;
            }
        }

        /// <summary>Three greybox office layouts: desk rows, two glass rooms, and a cluttered open plan.</summary>
        static void BuildOffice(Transform contents, TowerProfile p, int variant)
        {
            float midZ = -p.MainDepth * 0.5f;
            switch (variant)
            {
                case 0:
                    for (int row = 0; row < 2; row++)
                        for (int i = 0; i < 4; i++)
                            Box(contents, "Desk", new Vector3(-6f + i * 4f, 0.375f, -3f - row * 4f), new Vector3(1.6f, 0.75f, 0.8f));
                    break;
                case 1:
                    GlassRoom(contents, new Vector3(-5f, 0f, midZ), p);
                    GlassRoom(contents, new Vector3(5f, 0f, midZ), p);
                    break;
                default:
                    Box(contents, "Filing cabinets", new Vector3(-7f, 0.9f, -7f), new Vector3(0.6f, 1.8f, 3f));
                    Box(contents, "Filing cabinets", new Vector3(7f, 0.9f, -7f), new Vector3(0.6f, 1.8f, 3f));
                    Box(contents, "Desk", new Vector3(-3f, 0.375f, -2.5f), new Vector3(1.6f, 0.75f, 0.8f));
                    Box(contents, "Desk", new Vector3(3f, 0.375f, -2.5f), new Vector3(1.6f, 0.75f, 0.8f));
                    Box(contents, "Sofa", new Vector3(0f, 0.4f, -6f), new Vector3(2.4f, 0.8f, 0.9f));
                    Box(contents, "Copier", new Vector3(0f, 0.6f, -8.5f), new Vector3(1.2f, 1.2f, 0.8f));
                    break;
            }
        }

        /// <summary>A 4 × 3 m room with three low-transparent walls and an open side toward the windows. Greybox: solid walls.</summary>
        static void GlassRoom(Transform contents, Vector3 centre, TowerProfile p)
        {
            const float roomW = 4f, roomD = 3f, glassH = 2.5f;
            Box(contents, "Glass back", centre + new Vector3(0f, glassH * 0.5f, -roomD * 0.5f), new Vector3(roomW, glassH, 0.1f));
            Box(contents, "Glass left", centre + new Vector3(-roomW * 0.5f, glassH * 0.5f, 0f), new Vector3(0.1f, glassH, roomD));
            Box(contents, "Glass right", centre + new Vector3(roomW * 0.5f, glassH * 0.5f, 0f), new Vector3(0.1f, glassH, roomD));
            Box(contents, "Meeting table", centre + new Vector3(0f, 0.375f, 0f), new Vector3(2f, 0.75f, 1f));
        }

        static Transform[] BuildSpawns(Transform floor, TowerProfile p)
        {
            Transform group = Group(floor, "Spawns");
            var points = new Transform[2];
            for (int i = 0; i < points.Length; i++)
            {
                var point = new GameObject("Spawn " + (i + 1));
                point.transform.SetParent(group, false);
                point.transform.localPosition = new Vector3(-2f + i * 4f, 0.1f, -p.MainDepth * 0.5f);
                point.transform.localRotation = Quaternion.identity; // facing +Z: the rival tower
                points[i] = point.transform;
            }

            return points;
        }

        // ---- Scene ---------------------------------------------------------------------------

        static void EnsureTowerObject(TowerProfile profile)
        {
            if (Object.FindAnyObjectByType<TowerBuilder>() != null)
                return;

            var go = new GameObject(TowerObjectName);
            go.transform.position = TowerOrigin;
            go.AddComponent<NetworkObject>();
            var builder = go.AddComponent<TowerBuilder>();

            var serialized = new SerializedObject(builder);
            serialized.FindProperty("_profile").objectReferenceValue = profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Tower] Placed the Tower object.");
        }

        // ---- Helpers -------------------------------------------------------------------------

        struct RoomBounds
        {
            public float Left;
            public float Right;
            public float Width => Right - Left;
            public float Centre => (Left + Right) * 0.5f;
        }

        static RoomBounds RoomX(TowerProfile p, int index)
        {
            float x = -p.Width * 0.5f;
            for (int i = 0; i < index; i++)
                x += p.Width * SpineRooms[i].Fraction;

            return new RoomBounds { Left = x, Right = x + p.Width * SpineRooms[index].Fraction };
        }

        static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Quaternion? rotation = null)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.isStatic = true;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = centre;
            box.transform.localRotation = rotation ?? Quaternion.identity;
            box.transform.localScale = size;
            return box;
        }
    }
}
