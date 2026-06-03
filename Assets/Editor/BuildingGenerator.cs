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
    private const string BathroomPrefabPath = "Assets/arhitektura/Generated/Bathroom.prefab";

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
    private const float StairWidth = 1.2f;
    private const int StairSteps = 18;
    private const float StepRun = 0.28f;

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
        GameObject bathroomPrefab = BuildAndSaveBathroom(mats, doorPrefab);
        GameObject buildingPrefab = BuildAndSaveBuilding(roomPrefab, stairsPrefab, bathroomPrefab, doorPrefab, mats);

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
        public Material BathroomGreen;
        public Material BathroomBlue;
        public Material BathroomYellow;
        public Material WhiteFixtures;
        public Material BathroomFloor;
        public Material ExitSign;
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
            BathroomGreen = MakeMat("Mat_BathroomGreen", new Color(0.38f, 0.78f, 0.32f)),
            BathroomBlue = MakeMat("Mat_BathroomBlue", new Color(0.55f, 0.78f, 0.88f)),
            BathroomYellow = MakeMat("Mat_BathroomYellow", new Color(0.96f, 0.86f, 0.18f)),
            WhiteFixtures = MakeMat("Mat_WhiteFixtures", new Color(0.96f, 0.96f, 0.96f)),
            BathroomFloor = MakeMat("Mat_BathroomFloor", new Color(0.18f, 0.20f, 0.25f)),
            ExitSign = MakeMat("Mat_ExitSign", new Color(0.05f, 0.6f, 0.25f)),
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

    // ===================== BATHROOM =====================
    private static GameObject BuildAndSaveBathroom(Mats mats, GameObject doorPrefab)
    {
        var root = new GameObject("Bathroom");

        // Dark tile floor (matches the photo)
        MakeCube("Floor", root.transform,
            new Vector3(RoomWidth / 2f, -FloorThickness / 2f, RoomDepth / 2f),
            new Vector3(RoomWidth, FloorThickness, RoomDepth),
            mats.BathroomFloor);

        // White ceiling
        MakeCube("Ceiling", root.transform,
            new Vector3(RoomWidth / 2f, RoomHeight + FloorThickness / 2f, RoomDepth / 2f),
            new Vector3(RoomWidth, FloorThickness, RoomDepth),
            mats.Ceiling);

        // Left wall (interior side facing in is plain white; outer side red)
        MakeCube("WallLeft", root.transform,
            new Vector3(-WallThickness / 2f, RoomHeight / 2f, RoomDepth / 2f),
            new Vector3(WallThickness, RoomHeight, RoomDepth),
            mats.WallRed);
        // White interior overlay on the inside of the left wall
        MakeCube("WallLeft_Interior", root.transform,
            new Vector3(0.02f, RoomHeight / 2f, RoomDepth / 2f),
            new Vector3(0.04f, RoomHeight, RoomDepth),
            mats.Ceiling);

        // Right wall - yellow on the inside, red on the outside (as in the photo)
        MakeCube("WallRight", root.transform,
            new Vector3(RoomWidth + WallThickness / 2f, RoomHeight / 2f, RoomDepth / 2f),
            new Vector3(WallThickness, RoomHeight, RoomDepth),
            mats.WallRed);
        MakeCube("WallRight_Interior", root.transform,
            new Vector3(RoomWidth - 0.02f, RoomHeight / 2f, RoomDepth / 2f),
            new Vector3(0.04f, RoomHeight, RoomDepth),
            mats.BathroomYellow);

        BuildExternalWallWithWindows(root.transform, mats);

        // Green interior accent panel over the external wall (between sill and ceiling, where windows aren't)
        MakeCube("BathroomGreenAccent", root.transform,
            new Vector3(RoomWidth / 2f, WindowSillHeight / 2f, 0.18f),
            new Vector3(RoomWidth, WindowSillHeight, 0.05f),
            mats.BathroomGreen);

        BuildDoorWall(root.transform, mats, doorPrefab);

        BuildBathroomStalls(root.transform, mats);

        // Ceiling lights
        for (int i = 0; i < 2; i++)
        {
            float z = RoomDepth * (0.33f + i * 0.34f);
            MakeCube($"CeilingLight_{i}", root.transform,
                new Vector3(RoomWidth / 2f, RoomHeight - 0.05f, z),
                new Vector3(1.2f, 0.05f, 0.4f),
                mats.Screen);
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, BathroomPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void BuildBathroomStalls(Transform parent, Mats mats)
    {
        // 3 stalls along the external (window) side, leaving open area near the entry door.
        const int numStalls = 3;
        const float stallDepth = 1.6f;        // From external wall toward room interior
        const float dividerThickness = 0.06f;
        const float dividerHeight = 2.0f;
        const float doorThickness = 0.05f;
        const float doorWidth = 0.85f;
        const float stallSideMargin = 0.5f;   // Gap between leftmost/rightmost stall and side walls

        float totalStallSpan = RoomWidth - 2f * stallSideMargin;
        float stallWidth = (totalStallSpan - (numStalls + 1) * dividerThickness) / numStalls;
        float stallStartX = stallSideMargin;
        // Stalls hug the external wall (windows) on the +Z=0 side, opening toward the corridor.
        const float stallNearZ = 0.3f;        // Front (door side) of stall
        float stallFarZ = stallNearZ + stallDepth;

        // Green back wall behind the stalls (visible between/over the dividers in the photo).
        MakeCube("StallBackWall", parent,
            new Vector3(RoomWidth / 2f, dividerHeight / 2f + 0.1f, stallNearZ - 0.02f),
            new Vector3(totalStallSpan + 2 * dividerThickness, dividerHeight + 0.2f, 0.04f),
            mats.BathroomGreen);

        for (int s = 0; s <= numStalls; s++)
        {
            // Vertical green dividers (between stalls + at each end of the run)
            float xDividerCenter = stallStartX + s * (stallWidth + dividerThickness) - dividerThickness / 2f + dividerThickness / 2f;
            // Simplified: divider centers
            float dividerX = stallStartX + s * (stallWidth + dividerThickness) - dividerThickness / 2f;
            MakeCube($"StallDivider_{s}", parent,
                new Vector3(dividerX, dividerHeight / 2f + 0.15f, (stallNearZ + stallFarZ) / 2f),
                new Vector3(dividerThickness, dividerHeight, stallDepth),
                mats.BathroomGreen);
        }

        for (int s = 0; s < numStalls; s++)
        {
            float xLeft = stallStartX + s * (stallWidth + dividerThickness);
            float xCenter = xLeft + stallWidth / 2f;

            // Light-blue stall door (front of stall, facing room interior)
            MakeCube($"StallDoor_{s}", parent,
                new Vector3(xCenter, dividerHeight / 2f + 0.15f, stallFarZ - doorThickness / 2f),
                new Vector3(doorWidth, dividerHeight - 0.3f, doorThickness),
                mats.BathroomBlue);

            // Toilet base
            MakeCube($"ToiletBase_{s}", parent,
                new Vector3(xCenter, 0.2f, stallNearZ + 0.35f),
                new Vector3(0.42f, 0.4f, 0.55f),
                mats.WhiteFixtures);
            // Toilet tank
            MakeCube($"ToiletTank_{s}", parent,
                new Vector3(xCenter, 0.7f, stallNearZ + 0.12f),
                new Vector3(0.5f, 0.6f, 0.2f),
                mats.WhiteFixtures);
            // Toilet seat
            MakeCube($"ToiletSeat_{s}", parent,
                new Vector3(xCenter, 0.42f, stallNearZ + 0.4f),
                new Vector3(0.46f, 0.05f, 0.45f),
                mats.WhiteFixtures);

            // Toilet paper holder on the left divider
            MakeCube($"ToiletPaper_{s}", parent,
                new Vector3(xLeft + 0.08f, 1.05f, stallNearZ + 0.65f),
                new Vector3(0.12f, 0.12f, 0.12f),
                mats.WhiteFixtures);
        }
    }

    // ===================== STAIRS =====================
    private static GameObject BuildAndSaveStairs(Mats mats)
    {
        var root = new GameObject("Stairs");
        int steps = StairSteps;
        float totalRise = RoomHeight + FloorThickness;
        float stepRise = totalRise / steps;
        float stepRun = StepRun;
        float width = StairWidth;

        for (int i = 0; i < steps; i++)
        {
            float y = (i + 1) * stepRise - stepRise / 2f;
            float z = i * stepRun + stepRun / 2f;
            MakeCube($"Step_{i}", root.transform,
                new Vector3(width / 2f, y, z),
                new Vector3(width, stepRise, stepRun),
                mats.Stairs);
        }

        // Landing at top — aligned with floor 1 walkable surface (top at y=totalRise).
        float landingZ = steps * stepRun;
        MakeCube("Landing", root.transform,
            new Vector3(width / 2f, totalRise - FloorThickness / 2f, landingZ + 1f),
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
    private const int BathroomRoomIndex = 1; // Room slot on the north side that becomes a bathroom on each floor.

    private static GameObject BuildAndSaveBuilding(GameObject roomPrefab, GameObject stairsPrefab, GameObject bathroomPrefab, GameObject doorPrefab, Mats mats)
    {
        var root = new GameObject("Block9Building");

        float buildingLength = RoomsPerSide * RoomWidth;             // 32
        float buildingDepth = RoomDepth + CorridorWidth + RoomDepth;  // 17
        float floorVerticalSize = RoomHeight + FloorThickness;        // 3.65
        float corridorCenterZ = RoomDepth + CorridorWidth / 2f;       // 8.5

        // Stair tower geometry (consistent across floors)
        float stairLength = StairSteps * StepRun;                     // 5.04
        float stairBottomX = buildingLength - stairLength - 2.5f;     // 24.46 — leave 2.5m east buffer for the landing
        float stairTopX = stairBottomX + stairLength;                 // 29.5
        float stairZMin = corridorCenterZ - StairWidth / 2f;          // 7.9
        float stairZMax = corridorCenterZ + StairWidth / 2f;          // 9.1

        for (int f = 0; f < Floors; f++)
        {
            var floorParent = new GameObject($"Floor_{f}");
            floorParent.transform.SetParent(root.transform, false);
            floorParent.transform.localPosition = new Vector3(0, f * floorVerticalSize, 0);

            // -------- Floor 0: solid corridor floor, donut ceiling (hole over stairs) --------
            // -------- Floor 1: donut corridor floor (hole over stairs), solid ceiling --------
            if (f == 0)
            {
                MakeCube("CorridorFloor", floorParent.transform,
                    new Vector3(buildingLength / 2f, -FloorThickness / 2f, corridorCenterZ),
                    new Vector3(buildingLength, FloorThickness, CorridorWidth),
                    mats.Floor);

                BuildDonutSlab(floorParent.transform, "CorridorCeiling", RoomHeight + FloorThickness / 2f,
                    buildingLength, stairBottomX, stairTopX, stairZMin, stairZMax, corridorCenterZ, mats.Ceiling);
            }
            else
            {
                BuildDonutSlab(floorParent.transform, "CorridorFloor", -FloorThickness / 2f,
                    buildingLength, stairBottomX, stairTopX, stairZMin, stairZMax, corridorCenterZ, mats.Floor);

                MakeCube("CorridorCeiling", floorParent.transform,
                    new Vector3(buildingLength / 2f, RoomHeight + FloorThickness / 2f, corridorCenterZ),
                    new Vector3(buildingLength, FloorThickness, CorridorWidth),
                    mats.Ceiling);
            }

            // North side rooms — one slot is a bathroom on each floor.
            for (int r = 0; r < RoomsPerSide; r++)
            {
                GameObject sourcePrefab = (r == BathroomRoomIndex) ? bathroomPrefab : roomPrefab;
                var room = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab, floorParent.transform);
                room.name = (r == BathroomRoomIndex) ? $"Bathroom_F{f}_N" : $"Room_F{f}_N{r}";
                room.transform.localPosition = new Vector3(r * RoomWidth, 0, 0);
                room.transform.localRotation = Quaternion.identity;
            }

            // South side rooms (180° rotated) — all classrooms.
            for (int r = 0; r < RoomsPerSide; r++)
            {
                var room = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab, floorParent.transform);
                room.name = $"Room_F{f}_S{r}";
                room.transform.localPosition = new Vector3((r + 1) * RoomWidth, 0, buildingDepth);
                room.transform.localRotation = Quaternion.Euler(0, 180, 0);
            }

            // Corridor end-cap walls.
            // Floor 0: west wall has the escape door cut into it.
            // Floor 1: solid west wall.
            if (f == 0)
            {
                BuildEscapeDoorWall(floorParent.transform, mats, doorPrefab, corridorCenterZ);
            }
            else
            {
                MakeCube("CorridorWall_West", floorParent.transform,
                    new Vector3(-WallThickness / 2f, RoomHeight / 2f, corridorCenterZ),
                    new Vector3(WallThickness, RoomHeight, CorridorWidth),
                    mats.WallRed);
            }
            MakeCube("CorridorWall_East", floorParent.transform,
                new Vector3(buildingLength + WallThickness / 2f, RoomHeight / 2f, corridorCenterZ),
                new Vector3(WallThickness, RoomHeight, CorridorWidth),
                mats.WallRed);
        }

        // Roof slab
        MakeCube("Roof", root.transform,
            new Vector3(buildingLength / 2f, Floors * floorVerticalSize + FloorThickness / 2f, buildingDepth / 2f),
            new Vector3(buildingLength + 2 * WallThickness, FloorThickness, buildingDepth + 2 * WallThickness),
            mats.Roof);

        // Stairs: narrow (1.2m) centered in corridor, ascending west-to-east.
        // After +90° Y rotation: local +Z (step ascent direction) maps to world +X,
        // local +X (width direction) maps to world -Z.
        // Pivot.z = corridor center + StairWidth/2 so step centers end up at corridor center.
        var stairs = (GameObject)PrefabUtility.InstantiatePrefab(stairsPrefab, root.transform);
        stairs.name = "Stairs";
        stairs.transform.localRotation = Quaternion.Euler(0, 90f, 0);
        stairs.transform.localPosition = new Vector3(stairBottomX, 0, corridorCenterZ + StairWidth / 2f);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, BuildingPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>
    /// Builds the west corridor end-cap wall with a door opening + Door prefab instance (the escape door).
    /// </summary>
    private static void BuildEscapeDoorWall(Transform parent, Mats mats, GameObject doorPrefab, float corridorCenterZ)
    {
        float wallX = -WallThickness / 2f;
        const float doorOpeningWidth = 1.2f;
        const float doorOpeningHeight = 2.2f;
        float doorLeftZ = corridorCenterZ - doorOpeningWidth / 2f;
        float doorRightZ = corridorCenterZ + doorOpeningWidth / 2f;
        float corridorNorthEdgeZ = corridorCenterZ - CorridorWidth / 2f;
        float corridorSouthEdgeZ = corridorCenterZ + CorridorWidth / 2f;

        float northSegWidth = doorLeftZ - corridorNorthEdgeZ;
        MakeCube("EscapeDoorWall_North", parent,
            new Vector3(wallX, RoomHeight / 2f, (corridorNorthEdgeZ + doorLeftZ) / 2f),
            new Vector3(WallThickness, RoomHeight, northSegWidth),
            mats.WallRed);

        float southSegWidth = corridorSouthEdgeZ - doorRightZ;
        MakeCube("EscapeDoorWall_South", parent,
            new Vector3(wallX, RoomHeight / 2f, (doorRightZ + corridorSouthEdgeZ) / 2f),
            new Vector3(WallThickness, RoomHeight, southSegWidth),
            mats.WallRed);

        float transomH = RoomHeight - doorOpeningHeight;
        MakeCube("EscapeDoorWall_Transom", parent,
            new Vector3(wallX, doorOpeningHeight + transomH / 2f, corridorCenterZ),
            new Vector3(WallThickness, transomH, doorOpeningWidth),
            mats.WallRed);

        // EXIT sign above the door (green, like emergency exit signs)
        MakeCube("EscapeDoor_ExitSign", parent,
            new Vector3(wallX - WallThickness / 2f - 0.03f, doorOpeningHeight + 0.25f, corridorCenterZ),
            new Vector3(0.05f, 0.3f, 0.8f),
            mats.ExitSign);

        if (doorPrefab != null)
        {
            var door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, parent);
            door.name = "EscapeDoor";
            door.transform.localPosition = new Vector3(wallX, 0, doorLeftZ);
            door.transform.localRotation = Quaternion.identity;
            door.transform.localScale = new Vector3(1, 1, doorOpeningWidth);
        }
    }

    /// <summary>
    /// Builds the corridor floor (or ceiling) as 4 strips forming a donut around the stair hole.
    /// </summary>
    private static void BuildDonutSlab(Transform parent, string baseName, float yCenter,
        float buildingLength, float holeWestX, float holeEastX, float holeZMin, float holeZMax,
        float corridorCenterZ, Material material)
    {
        float corridorNorthEdgeZ = corridorCenterZ - CorridorWidth / 2f;  // 7
        float corridorSouthEdgeZ = corridorCenterZ + CorridorWidth / 2f;  // 10

        // North strip — full length of corridor, on the north side of the hole.
        float northStripZ = (corridorNorthEdgeZ + holeZMin) / 2f;
        float northStripDepth = holeZMin - corridorNorthEdgeZ;
        MakeCube($"{baseName}_North", parent,
            new Vector3(buildingLength / 2f, yCenter, northStripZ),
            new Vector3(buildingLength, FloorThickness, northStripDepth),
            material);

        // South strip — full length of corridor, on the south side of the hole.
        float southStripZ = (holeZMax + corridorSouthEdgeZ) / 2f;
        float southStripDepth = corridorSouthEdgeZ - holeZMax;
        MakeCube($"{baseName}_South", parent,
            new Vector3(buildingLength / 2f, yCenter, southStripZ),
            new Vector3(buildingLength, FloorThickness, southStripDepth),
            material);

        // West center strip — covers stair-width band west of the hole.
        if (holeWestX > 0f)
        {
            MakeCube($"{baseName}_WestCenter", parent,
                new Vector3(holeWestX / 2f, yCenter, corridorCenterZ),
                new Vector3(holeWestX, FloorThickness, holeZMax - holeZMin),
                material);
        }

        // East center strip — covers stair-width band east of the hole.
        float eastCenterLen = buildingLength - holeEastX;
        if (eastCenterLen > 0f)
        {
            MakeCube($"{baseName}_EastCenter", parent,
                new Vector3(holeEastX + eastCenterLen / 2f, yCenter, corridorCenterZ),
                new Vector3(eastCenterLen, FloorThickness, holeZMax - holeZMin),
                material);
        }
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
