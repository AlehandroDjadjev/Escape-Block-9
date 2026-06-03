using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildingGenerator
{
    private const string GeneratedFolder = "Assets/arhitektura/Generated";
    private const string DoorPrefabPath = "Assets/arhitektura/Door.prefab";
    private const string RoomPrefabPath = "Assets/arhitektura/Generated/Room.prefab";
    private const string StairsPrefabPath = "Assets/arhitektura/Generated/Stairs.prefab";
    private const string BuildingPrefabPath = "Assets/arhitektura/Generated/Block9Building.prefab";

    private const float RoomWidth = 8f;
    private const float RoomDepth = 7f;
    private const float RoomHeight = 3.5f;
    private const float CorridorWidth = 3f;
    private const float WallThickness = 0.2f;
    private const float FloorThickness = 0.15f;
    private const float WindowSillHeight = 1f;
    private const float WindowHeight = 1.5f;
    private const int RoomsPerSide = 4;
    private const int Floors = 2;

    private const string TriggerFile = "Assets/arhitektura/.generate_building";

    private static Shader litShader;

    [InitializeOnLoadMethod]
    private static void OnLoadCheckTrigger()
    {
        Debug.Log($"[BuildingGenerator] OnLoadCheckTrigger fired. Marker exists at startup: {File.Exists(TriggerFile)}");
        EditorApplication.delayCall += () =>
        {
            Debug.Log($"[BuildingGenerator] delayCall fired. Marker exists: {File.Exists(TriggerFile)}");
            if (File.Exists(TriggerFile))
            {
                File.Delete(TriggerFile);
                string metaFile = TriggerFile + ".meta";
                if (File.Exists(metaFile)) File.Delete(metaFile);
                Generate();
            }
        };
    }

    [MenuItem("Tools/Generate Block9 Building")]
    public static void Generate()
    {
        litShader = Shader.Find("Universal Render Pipeline/Lit")
                 ?? Shader.Find("Standard")
                 ?? Shader.Find("Diffuse");

        Directory.CreateDirectory(GeneratedFolder);
        AssetDatabase.Refresh();

        var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
        if (doorPrefab == null)
        {
            Debug.LogError($"Door prefab not found at {DoorPrefabPath}");
            return;
        }

        var mats = CreateMaterials();

        GameObject roomPrefab = BuildAndSaveRoom(mats, doorPrefab);
        GameObject stairsPrefab = BuildAndSaveStairs(mats);
        GameObject buildingPrefab = BuildAndSaveBuilding(roomPrefab, stairsPrefab, mats);

        var existing = GameObject.Find("Block9Building");
        if (existing != null) Object.DestroyImmediate(existing);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(buildingPrefab);
        instance.name = "Block9Building";
        instance.transform.position = new Vector3(0, 0, 10);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Block9 Building generated and placed in scene.");
    }

    private class Mats
    {
        public Material WallRed;
        public Material WallBeige;
        public Material Floor;
        public Material Ceiling;
        public Material Glass;
        public Material Wood;
        public Material Blackboard;
        public Material Screen;
        public Material Stairs;
        public Material WindowFrame;
        public Material Roof;
        public Material Metal;
    }

    private static Mats CreateMaterials()
    {
        return new Mats
        {
            WallRed = MakeMat("Mat_WallRed", new Color(0.71f, 0.24f, 0.16f)),
            WallBeige = MakeMat("Mat_WallBeige", new Color(0.85f, 0.78f, 0.65f)),
            Floor = MakeMat("Mat_Floor", new Color(0.45f, 0.45f, 0.45f)),
            Ceiling = MakeMat("Mat_Ceiling", new Color(0.95f, 0.95f, 0.95f)),
            Glass = MakeMat("Mat_Glass", new Color(0.55f, 0.75f, 0.85f)),
            Wood = MakeMat("Mat_Wood", new Color(0.58f, 0.38f, 0.22f)),
            Blackboard = MakeMat("Mat_Blackboard", new Color(0.08f, 0.18f, 0.12f)),
            Screen = MakeMat("Mat_Screen", Color.white),
            Stairs = MakeMat("Mat_Stairs", new Color(0.55f, 0.55f, 0.55f)),
            WindowFrame = MakeMat("Mat_WindowFrame", new Color(0.92f, 0.92f, 0.92f)),
            Roof = MakeMat("Mat_Roof", new Color(0.3f, 0.3f, 0.3f)),
            Metal = MakeMat("Mat_Metal", new Color(0.7f, 0.7f, 0.7f)),
        };
    }

    private static Material MakeMat(string name, Color color)
    {
        string path = $"{GeneratedFolder}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        Material m = existing != null ? existing : new Material(litShader);
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (existing == null) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
        return m;
    }

    // ===================== ROOM =====================
    private static GameObject BuildAndSaveRoom(Mats mats, GameObject doorPrefab)
    {
        // Local frame: room occupies x=[0,RoomWidth], z=[0,RoomDepth], y=[0,RoomHeight].
        // -Z = external wall (windows); +Z = corridor wall (door).
        var root = new GameObject("Room");

        MakeCube("Floor", root.transform,
            new Vector3(RoomWidth / 2f, -FloorThickness / 2f, RoomDepth / 2f),
            new Vector3(RoomWidth, FloorThickness, RoomDepth),
            mats.Floor);

        MakeCube("Ceiling", root.transform,
            new Vector3(RoomWidth / 2f, RoomHeight + FloorThickness / 2f, RoomDepth / 2f),
            new Vector3(RoomWidth, FloorThickness, RoomDepth),
            mats.Ceiling);

        MakeCube("WallLeft", root.transform,
            new Vector3(-WallThickness / 2f, RoomHeight / 2f, RoomDepth / 2f),
            new Vector3(WallThickness, RoomHeight, RoomDepth),
            mats.WallRed);

        MakeCube("WallRight", root.transform,
            new Vector3(RoomWidth + WallThickness / 2f, RoomHeight / 2f, RoomDepth / 2f),
            new Vector3(WallThickness, RoomHeight, RoomDepth),
            mats.WallRed);

        BuildExternalWallWithWindows(root.transform, mats);
        BuildDoorWall(root.transform, mats, doorPrefab);
        BuildClassroomInterior(root.transform, mats);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, RoomPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void BuildExternalWallWithWindows(Transform parent, Mats mats)
    {
        float wallZ = -WallThickness / 2f;

        // Bottom strip
        MakeCube("ExtWall_Bottom", parent,
            new Vector3(RoomWidth / 2f, WindowSillHeight / 2f, wallZ),
            new Vector3(RoomWidth, WindowSillHeight, WallThickness),
            mats.WallRed);

        // Top strip
        float topY = WindowSillHeight + WindowHeight;
        float topStripH = RoomHeight - topY;
        MakeCube("ExtWall_Top", parent,
            new Vector3(RoomWidth / 2f, topY + topStripH / 2f, wallZ),
            new Vector3(RoomWidth, topStripH, WallThickness),
            mats.WallRed);

        int windowCount = 3;
        float mullionThickness = 0.18f;
        float availableWidth = RoomWidth - (windowCount + 1) * mullionThickness;
        float paneWidth = availableWidth / windowCount;

        // Mullions (vertical wall pieces between windows + at each end)
        for (int i = 0; i <= windowCount; i++)
        {
            float xCenter = mullionThickness / 2f + i * (paneWidth + mullionThickness);
            MakeCube($"ExtWall_Mullion_{i}", parent,
                new Vector3(xCenter, WindowSillHeight + WindowHeight / 2f, wallZ),
                new Vector3(mullionThickness, WindowHeight, WallThickness),
                mats.WallRed);
        }

        // Windows: frame + glass
        for (int i = 0; i < windowCount; i++)
        {
            float xLeft = mullionThickness + i * (paneWidth + mullionThickness);
            float xCenter = xLeft + paneWidth / 2f;
            float frameT = 0.05f;

            MakeCube($"WindowFrameT_{i}", parent,
                new Vector3(xCenter, WindowSillHeight + WindowHeight - frameT / 2f, wallZ),
                new Vector3(paneWidth, frameT, WallThickness * 0.6f),
                mats.WindowFrame);
            MakeCube($"WindowFrameB_{i}", parent,
                new Vector3(xCenter, WindowSillHeight + frameT / 2f, wallZ),
                new Vector3(paneWidth, frameT, WallThickness * 0.6f),
                mats.WindowFrame);
            // Center horizontal divider (typical of these buildings)
            MakeCube($"WindowDivider_{i}", parent,
                new Vector3(xCenter, WindowSillHeight + WindowHeight / 2f, wallZ),
                new Vector3(paneWidth, frameT * 0.8f, WallThickness * 0.6f),
                mats.WindowFrame);

            MakeCube($"WindowGlass_{i}", parent,
                new Vector3(xCenter, WindowSillHeight + WindowHeight / 2f, wallZ),
                new Vector3(paneWidth - frameT * 1.5f, WindowHeight - frameT * 2f, WallThickness * 0.25f),
                mats.Glass);
        }
    }

    private static void BuildDoorWall(Transform parent, Mats mats, GameObject doorPrefab)
    {
        float wallZ = RoomDepth + WallThickness / 2f;
        float doorOpeningWidth = 1.2f;
        float doorOpeningHeight = 2.2f;
        float doorCenterX = RoomWidth / 2f;
        float doorLeftEdge = doorCenterX - doorOpeningWidth / 2f;
        float doorRightEdge = doorCenterX + doorOpeningWidth / 2f;

        MakeCube("DoorWall_Left", parent,
            new Vector3(doorLeftEdge / 2f, RoomHeight / 2f, wallZ),
            new Vector3(doorLeftEdge, RoomHeight, WallThickness),
            mats.WallRed);

        float rightSegWidth = RoomWidth - doorRightEdge;
        MakeCube("DoorWall_Right", parent,
            new Vector3(doorRightEdge + rightSegWidth / 2f, RoomHeight / 2f, wallZ),
            new Vector3(rightSegWidth, RoomHeight, WallThickness),
            mats.WallRed);

        float transomH = RoomHeight - doorOpeningHeight;
        MakeCube("DoorWall_Transom", parent,
            new Vector3(doorCenterX, doorOpeningHeight + transomH / 2f, wallZ),
            new Vector3(doorOpeningWidth, transomH, WallThickness),
            mats.WallRed);

        if (doorPrefab != null)
        {
            var door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, parent);
            door.name = "Door";
            // Door pivot at left hinge of opening; door swings inward (-Z) by default rotation.
            door.transform.localPosition = new Vector3(doorLeftEdge, 0, RoomDepth);
            door.transform.localRotation = Quaternion.Euler(0, -90, 0);
            door.transform.localScale = new Vector3(1, 1, doorOpeningWidth);
        }
    }

    private static void BuildClassroomInterior(Transform parent, Mats mats)
    {
        float boardCenterZ = RoomDepth / 2f;
        float boardW = 3.5f;
        float boardH = 1.3f;
        float boardY = 1.5f;

        // Blackboard on the +X side wall, facing -X
        MakeCube("Blackboard", parent,
            new Vector3(RoomWidth - 0.08f, boardY, boardCenterZ),
            new Vector3(0.06f, boardH, boardW),
            mats.Blackboard);

        // Podium
        MakeCube("Podium", parent,
            new Vector3(RoomWidth - 1.2f, 0.55f, boardCenterZ),
            new Vector3(0.6f, 1.1f, 0.5f),
            mats.Wood);

        // Projector screen on the ceiling near blackboard wall
        MakeCube("Screen", parent,
            new Vector3(RoomWidth - 0.9f, RoomHeight - 0.8f, boardCenterZ),
            new Vector3(0.06f, 1.4f, 1.8f),
            mats.Screen);

        // Desks + chairs
        int cols = 4;
        int rows = 3;
        float deskW = 0.7f;
        float deskH = 0.75f;
        float deskD = 0.5f;
        float marginFront = 2.5f;
        float marginBack = 1f;
        float availX = RoomWidth - marginFront - marginBack;
        float spacingX = availX / cols;
        float marginSide = 1f;
        float availZ = RoomDepth - 2 * marginSide;
        float spacingZ = availZ / rows;

        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                float deskX = marginBack + spacingX * (c + 0.5f);
                float deskZ = marginSide + spacingZ * (r + 0.5f);
                MakeCube($"Desk_{c}_{r}", parent,
                    new Vector3(deskX, deskH / 2f, deskZ),
                    new Vector3(deskW, deskH, deskD),
                    mats.Wood);
                MakeCube($"Chair_{c}_{r}", parent,
                    new Vector3(deskX - 0.5f, 0.45f / 2f, deskZ),
                    new Vector3(0.4f, 0.45f, 0.4f),
                    mats.Wood);
                MakeCube($"ChairBack_{c}_{r}", parent,
                    new Vector3(deskX - 0.7f, 0.7f, deskZ),
                    new Vector3(0.08f, 0.6f, 0.4f),
                    mats.Wood);
            }
        }

        // Ceiling lights (simple rectangular panels)
        for (int i = 0; i < 2; i++)
        {
            float z = RoomDepth * (0.33f + i * 0.34f);
            MakeCube($"CeilingLight_{i}", parent,
                new Vector3(RoomWidth / 2f, RoomHeight - 0.05f, z),
                new Vector3(1.2f, 0.05f, 0.4f),
                mats.Screen);
        }
    }

    // ===================== STAIRS =====================
    private static GameObject BuildAndSaveStairs(Mats mats)
    {
        var root = new GameObject("Stairs");
        int steps = 18;
        float totalRise = RoomHeight + FloorThickness;
        float stepRise = totalRise / steps;
        float stepRun = 0.28f;
        float width = 2.5f;

        for (int i = 0; i < steps; i++)
        {
            float y = (i + 1) * stepRise - stepRise / 2f;
            float z = i * stepRun + stepRun / 2f;
            MakeCube($"Step_{i}", root.transform,
                new Vector3(width / 2f, y, z),
                new Vector3(width, stepRise, stepRun),
                mats.Stairs);
        }

        // Landing at top
        float landingZ = steps * stepRun;
        MakeCube("Landing", root.transform,
            new Vector3(width / 2f, totalRise + FloorThickness / 2f, landingZ + 1f),
            new Vector3(width, FloorThickness, 2.5f),
            mats.Stairs);

        // Side railings
        float railingH = 1f;
        for (int side = 0; side < 2; side++)
        {
            float x = side == 0 ? 0.05f : width - 0.05f;
            MakeCube($"Railing_{side}", root.transform,
                new Vector3(x, totalRise / 2f + railingH / 2f, steps * stepRun / 2f),
                new Vector3(0.05f, 0.05f, steps * stepRun * 1.05f),
                mats.Metal);
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, StairsPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ===================== BUILDING =====================
    private static GameObject BuildAndSaveBuilding(GameObject roomPrefab, GameObject stairsPrefab, Mats mats)
    {
        var root = new GameObject("Block9Building");

        float buildingLength = RoomsPerSide * RoomWidth;
        float buildingDepth = RoomDepth + CorridorWidth + RoomDepth;
        float floorVerticalSize = RoomHeight + FloorThickness;
        float stairLengthX = 18 * 0.28f + 2.5f; // stair length + landing

        for (int f = 0; f < Floors; f++)
        {
            var floorParent = new GameObject($"Floor_{f}");
            floorParent.transform.SetParent(root.transform, false);
            floorParent.transform.localPosition = new Vector3(0, f * floorVerticalSize, 0);

            // Corridor floor and ceiling. For Floor_1, leave a hole over the stairs.
            if (f == 0)
            {
                MakeCube("CorridorFloor", floorParent.transform,
                    new Vector3(buildingLength / 2f, -FloorThickness / 2f, RoomDepth + CorridorWidth / 2f),
                    new Vector3(buildingLength, FloorThickness, CorridorWidth),
                    mats.Floor);
            }
            else
            {
                float corridorFloorLength = buildingLength - stairLengthX;
                MakeCube("CorridorFloor", floorParent.transform,
                    new Vector3(corridorFloorLength / 2f, -FloorThickness / 2f, RoomDepth + CorridorWidth / 2f),
                    new Vector3(corridorFloorLength, FloorThickness, CorridorWidth),
                    mats.Floor);
            }

            MakeCube("CorridorCeiling", floorParent.transform,
                new Vector3(buildingLength / 2f, RoomHeight + FloorThickness / 2f, RoomDepth + CorridorWidth / 2f),
                new Vector3(buildingLength, FloorThickness, CorridorWidth),
                mats.Ceiling);

            // North side rooms
            for (int r = 0; r < RoomsPerSide; r++)
            {
                var room = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab, floorParent.transform);
                room.name = $"Room_F{f}_N{r}";
                room.transform.localPosition = new Vector3(r * RoomWidth, 0, 0);
                room.transform.localRotation = Quaternion.identity;
            }

            // South side rooms (180° rotated)
            for (int r = 0; r < RoomsPerSide; r++)
            {
                var room = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab, floorParent.transform);
                room.name = $"Room_F{f}_S{r}";
                room.transform.localPosition = new Vector3((r + 1) * RoomWidth, 0, buildingDepth);
                room.transform.localRotation = Quaternion.Euler(0, 180, 0);
            }

            // Corridor end-cap walls
            MakeCube("CorridorWall_West", floorParent.transform,
                new Vector3(-WallThickness / 2f, RoomHeight / 2f, RoomDepth + CorridorWidth / 2f),
                new Vector3(WallThickness, RoomHeight, CorridorWidth),
                mats.WallRed);
            MakeCube("CorridorWall_East", floorParent.transform,
                new Vector3(buildingLength + WallThickness / 2f, RoomHeight / 2f, RoomDepth + CorridorWidth / 2f),
                new Vector3(WallThickness, RoomHeight, CorridorWidth),
                mats.WallRed);
        }

        // Roof slab
        MakeCube("Roof", root.transform,
            new Vector3(buildingLength / 2f, Floors * floorVerticalSize + FloorThickness / 2f, buildingDepth / 2f),
            new Vector3(buildingLength + 2 * WallThickness, FloorThickness, buildingDepth + 2 * WallThickness),
            mats.Roof);

        // Stairs at east end of the corridor, going from floor 0 up to floor 1.
        var stairs = (GameObject)PrefabUtility.InstantiatePrefab(stairsPrefab, root.transform);
        stairs.name = "Stairs";
        // Stairs original frame: width along X, length along Z. Rotate -90° so length runs along -X.
        stairs.transform.localRotation = Quaternion.Euler(0, -90, 0);
        // Place so the bottom step starts at the east end and stairs run west.
        stairs.transform.localPosition = new Vector3(
            buildingLength - 0.2f,
            0,
            RoomDepth + (CorridorWidth - 2.5f) / 2f);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, BuildingPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ===================== HELPERS =====================
    private static GameObject MakeCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        var rend = go.GetComponent<MeshRenderer>();
        if (rend != null && material != null) rend.sharedMaterial = material;
        return go;
    }
}
