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
    private const string ShopPrefabPath = "Assets/arhitektura/Generated/Shop.prefab";
    private const string ExitLobbyPrefabPath = "Assets/arhitektura/Generated/ExitLobby.prefab";

    private const float RoomWidth = 8f;
    private const float RoomDepth = 7f;
    private const float RoomHeight = 3.5f;
    private const float CorridorWidth = 3f;
    private const float WallThickness = 0.2f;
    private const float FloorThickness = 0.15f;
    private const float WindowSillHeight = 1f;
    private const float WindowHeight = 1.5f;
    private const int RoomsPerSide = 6;
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
        GameObject shopPrefab = BuildAndSaveShop(mats, doorPrefab);
        GameObject exitLobbyPrefab = BuildAndSaveExitLobby(mats, doorPrefab);
        GameObject buildingPrefab = BuildAndSaveBuilding(roomPrefab, stairsPrefab, bathroomPrefab, shopPrefab, exitLobbyPrefab, doorPrefab, mats);

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
        public Material ShopShelf;
        public Material ShopCounter;
        public Material MonsterCan;
        public Material WaterBottle;
        public Material ColaCan;
        public Material Sandwich;
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
            ShopShelf = MakeMat("Mat_ShopShelf", new Color(0.55f, 0.4f, 0.25f)),
            ShopCounter = MakeMat("Mat_ShopCounter", new Color(0.85f, 0.85f, 0.85f)),
            MonsterCan = MakeMat("Mat_MonsterCan", new Color(0.1f, 0.7f, 0.2f)),
            WaterBottle = MakeMat("Mat_WaterBottle", new Color(0.65f, 0.85f, 0.95f)),
            ColaCan = MakeMat("Mat_ColaCan", new Color(0.78f, 0.1f, 0.12f)),
            Sandwich = MakeMat("Mat_Sandwich", new Color(0.92f, 0.78f, 0.45f)),
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

    // ===================== SHOP =====================
    private static GameObject BuildAndSaveShop(Mats mats, GameObject doorPrefab)
    {
        var root = new GameObject("Shop");

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

        BuildShopInterior(root.transform, mats);

        for (int i = 0; i < 2; i++)
        {
            float z = RoomDepth * (0.33f + i * 0.34f);
            MakeCube($"CeilingLight_{i}", root.transform,
                new Vector3(RoomWidth / 2f, RoomHeight - 0.05f, z),
                new Vector3(1.2f, 0.05f, 0.4f),
                mats.Screen);
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, ShopPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void BuildShopInterior(Transform parent, Mats mats)
    {
        // Counter near the corridor (separates customer area from the cashier area).
        float counterZ = RoomDepth - 2f;
        MakeCube("Counter", parent,
            new Vector3(RoomWidth / 2f, 0.45f, counterZ),
            new Vector3(RoomWidth - 1.5f, 0.9f, 0.7f),
            mats.ShopCounter);

        // Counter top
        MakeCube("CounterTop", parent,
            new Vector3(RoomWidth / 2f, 0.93f, counterZ),
            new Vector3(RoomWidth - 1.4f, 0.04f, 0.78f),
            mats.WindowFrame);

        // Cash register on the counter
        MakeCube("CashRegister", parent,
            new Vector3(RoomWidth - 2f, 1.1f, counterZ),
            new Vector3(0.5f, 0.35f, 0.35f),
            mats.Blackboard);

        // Three shelves along the +X wall (the right wall) facing the customer area.
        int shelfCount = 3;
        float shelfDepth = 0.4f;
        float shelfThickness = 0.04f;
        float shelfWidth = 3.5f;
        float shelfX = RoomWidth - 0.05f - shelfDepth / 2f;
        for (int s = 0; s < shelfCount; s++)
        {
            float shelfY = 0.6f + s * 0.7f;
            MakeCube($"Shelf_{s}", parent,
                new Vector3(shelfX, shelfY, RoomDepth * 0.4f),
                new Vector3(shelfDepth, shelfThickness, shelfWidth),
                mats.ShopShelf);

            // Stock the shelf with various drinks/snacks.
            float[] zSpots = {
                RoomDepth * 0.4f - shelfWidth / 2f + 0.3f,
                RoomDepth * 0.4f - shelfWidth / 2f + 0.9f,
                RoomDepth * 0.4f - shelfWidth / 2f + 1.5f,
                RoomDepth * 0.4f - shelfWidth / 2f + 2.1f,
                RoomDepth * 0.4f - shelfWidth / 2f + 2.7f,
                RoomDepth * 0.4f - shelfWidth / 2f + 3.2f,
            };
            Material[] cycle = s switch
            {
                0 => new[] { mats.MonsterCan, mats.MonsterCan, mats.ColaCan, mats.ColaCan, mats.WaterBottle, mats.WaterBottle },
                1 => new[] { mats.WaterBottle, mats.ColaCan, mats.MonsterCan, mats.WaterBottle, mats.ColaCan, mats.MonsterCan },
                _ => new[] { mats.Sandwich, mats.Sandwich, mats.Sandwich, mats.Sandwich, mats.Sandwich, mats.Sandwich },
            };
            for (int i = 0; i < zSpots.Length; i++)
            {
                bool isSandwich = cycle[i] == mats.Sandwich;
                Vector3 size = isSandwich ? new Vector3(0.25f, 0.08f, 0.35f) : new Vector3(0.18f, 0.32f, 0.18f);
                float yOffset = isSandwich ? 0.05f : 0.18f;
                MakeCube($"Item_{s}_{i}", parent,
                    new Vector3(shelfX - 0.05f, shelfY + shelfThickness / 2f + yOffset, zSpots[i]),
                    size,
                    cycle[i]);
            }
        }

        // SHOP sign above the counter, hanging from the ceiling.
        MakeCube("ShopSign", parent,
            new Vector3(RoomWidth / 2f, RoomHeight - 0.4f, counterZ - 0.5f),
            new Vector3(2.5f, 0.5f, 0.06f),
            mats.ExitSign);
    }

    // ===================== EXIT LOBBY =====================
    private static GameObject BuildAndSaveExitLobby(Mats mats, GameObject doorPrefab)
    {
        var root = new GameObject("ExitLobby");

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

        // Exterior wall (-Z) with the EXIT door cut into it (instead of windows).
        BuildExitDoorExteriorWall(root.transform, mats, doorPrefab);

        // Corridor-side door (same as Room)
        BuildDoorWall(root.transform, mats, doorPrefab);

        for (int i = 0; i < 2; i++)
        {
            float z = RoomDepth * (0.33f + i * 0.34f);
            MakeCube($"CeilingLight_{i}", root.transform,
                new Vector3(RoomWidth / 2f, RoomHeight - 0.05f, z),
                new Vector3(1.2f, 0.05f, 0.4f),
                mats.Screen);
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, ExitLobbyPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void BuildExitDoorExteriorWall(Transform parent, Mats mats, GameObject doorPrefab)
    {
        // External wall is at z = -WallThickness/2 (the room's -Z face).
        float wallZ = -WallThickness / 2f;
        float doorOpeningWidth = 1.2f;
        float doorOpeningHeight = 2.2f;
        float doorCenterX = RoomWidth / 2f;
        float doorLeftEdge = doorCenterX - doorOpeningWidth / 2f;
        float doorRightEdge = doorCenterX + doorOpeningWidth / 2f;

        // Left wall segment
        MakeCube("ExitDoorWall_Left", parent,
            new Vector3(doorLeftEdge / 2f, RoomHeight / 2f, wallZ),
            new Vector3(doorLeftEdge, RoomHeight, WallThickness),
            mats.WallRed);

        // Right wall segment
        float rightSegW = RoomWidth - doorRightEdge;
        MakeCube("ExitDoorWall_Right", parent,
            new Vector3(doorRightEdge + rightSegW / 2f, RoomHeight / 2f, wallZ),
            new Vector3(rightSegW, RoomHeight, WallThickness),
            mats.WallRed);

        // Transom above door
        float transomH = RoomHeight - doorOpeningHeight;
        MakeCube("ExitDoorWall_Transom", parent,
            new Vector3(doorCenterX, doorOpeningHeight + transomH / 2f, wallZ),
            new Vector3(doorOpeningWidth, transomH, WallThickness),
            mats.WallRed);

        // EXIT sign above the door (inside the room)
        MakeCube("ExitDoor_Sign", parent,
            new Vector3(doorCenterX, doorOpeningHeight + 0.25f, 0.05f),
            new Vector3(0.8f, 0.3f, 0.04f),
            mats.ExitSign);

        // The escape door itself. Use the door prefab; mount it like the corridor door,
        // but on the external wall and opening outward (no rotation).
        if (doorPrefab != null)
        {
            var door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, parent);
            door.name = "ExitDoor";
            // Door hinge at left edge of opening on the external wall. Door's local +Z is door length.
            // We need door to span +X across opening, so rotate -90° around Y (same as corridor room doors).
            door.transform.localPosition = new Vector3(doorLeftEdge, 0, 0);
            door.transform.localRotation = Quaternion.Euler(0, -90f, 0);
            door.transform.localScale = new Vector3(1, 1, doorOpeningWidth);
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
    private const int ShopFloor0NorthIndex = 0; // NW corner of floor 0 (ground floor) becomes the shop.
    // Floor 0 east-end north slot (RoomsPerSide-1) becomes the exit lobby (NE corner).

    // Π-shape geometry: top connector (horizontal) + two south-extending wings.
    private const int ConnectorRoomsPerSide = 5;      // 5×8 = 40m connector length
    private const int WingRoomsPerSide = 3;           // 3×8 = 24m wing length
    private static readonly float ConnectorLength = ConnectorRoomsPerSide * RoomWidth;    // 40
    private static readonly float ConnectorDepth = RoomDepth + CorridorWidth;             // 10 (north rooms + corridor only)
    private static readonly float WingLength = WingRoomsPerSide * RoomWidth;              // 24
    private static readonly float WingWidth = RoomDepth + CorridorWidth + RoomDepth;      // 17

    private static GameObject BuildAndSaveBuilding(GameObject roomPrefab, GameObject stairsPrefab, GameObject bathroomPrefab, GameObject shopPrefab, GameObject exitLobbyPrefab, GameObject doorPrefab, Mats mats)
    {
        var root = new GameObject("Block9Building");

        float floorVerticalSize = RoomHeight + FloorThickness;            // 3.65
        float connectorCorridorCenterZ = RoomDepth + CorridorWidth / 2f;  // 8.5

        // Wing positions (south-extending from the connector ends)
        float leftWingX = 0f;
        float rightWingX = ConnectorLength - WingWidth;                   // 40 - 17 = 23
        float wingStartZ = ConnectorDepth;                                // 10 (south edge of connector)
        float wingEndZ = wingStartZ + WingLength;                         // 34
        float leftWingCorridorCenterX = leftWingX + RoomDepth + CorridorWidth / 2f;   // 8.5
        float rightWingCorridorCenterX = rightWingX + RoomDepth + CorridorWidth / 2f; // 31.5

        // Stair tower geometry — stays in connector at east end of connector corridor.
        float stairLength = StairSteps * StepRun;
        float stairBottomX = ConnectorLength - stairLength - 2.5f;        // 32.46
        float stairTopX = stairBottomX + stairLength;
        float stairZMin = connectorCorridorCenterZ - StairWidth / 2f;
        float stairZMax = connectorCorridorCenterZ + StairWidth / 2f;

        for (int f = 0; f < Floors; f++)
        {
            var floorParent = new GameObject($"Floor_{f}");
            floorParent.transform.SetParent(root.transform, false);
            floorParent.transform.localPosition = new Vector3(0, f * floorVerticalSize, 0);

            // =========== TOP CONNECTOR ===========
            // Connector corridor floor/ceiling — solid on floor 0, donut on floor 1 (stair hole).
            if (f == 0)
            {
                MakeCube("ConnectorCorridorFloor", floorParent.transform,
                    new Vector3(ConnectorLength / 2f, -FloorThickness / 2f, connectorCorridorCenterZ),
                    new Vector3(ConnectorLength, FloorThickness, CorridorWidth),
                    mats.Floor);

                BuildDonutSlab(floorParent.transform, "ConnectorCorridorCeiling", RoomHeight + FloorThickness / 2f,
                    ConnectorLength, stairBottomX, stairTopX, stairZMin, stairZMax, connectorCorridorCenterZ, mats.Ceiling);
            }
            else
            {
                BuildDonutSlab(floorParent.transform, "ConnectorCorridorFloor", -FloorThickness / 2f,
                    ConnectorLength, stairBottomX, stairTopX, stairZMin, stairZMax, connectorCorridorCenterZ, mats.Floor);

                MakeCube("ConnectorCorridorCeiling", floorParent.transform,
                    new Vector3(ConnectorLength / 2f, RoomHeight + FloorThickness / 2f, connectorCorridorCenterZ),
                    new Vector3(ConnectorLength, FloorThickness, CorridorWidth),
                    mats.Ceiling);
            }

            // Connector NORTH rooms — special slots on floor 0:
            //   index 0 = Shop (NW corner of whole building)
            //   index 1 = Bathroom (both floors)
            //   index ConnectorRoomsPerSide-1 = ExitLobby (NE corner of whole building, floor 0 only)
            int exitLobbyIndex = ConnectorRoomsPerSide - 1;
            for (int r = 0; r < ConnectorRoomsPerSide; r++)
            {
                GameObject sourcePrefab = roomPrefab;
                string roomName = $"Room_F{f}_N{r}";

                if (r == BathroomRoomIndex)
                {
                    sourcePrefab = bathroomPrefab;
                    roomName = $"Bathroom_F{f}_N";
                }
                else if (f == 0 && r == exitLobbyIndex)
                {
                    sourcePrefab = exitLobbyPrefab;
                    roomName = $"ExitLobby_F{f}_N";
                }
                else if (f == 0 && r == ShopFloor0NorthIndex)
                {
                    sourcePrefab = shopPrefab;
                    roomName = $"Shop_F{f}_N";
                }

                var room = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab, floorParent.transform);
                room.name = roomName;
                room.transform.localPosition = new Vector3(r * RoomWidth, 0, 0);
                room.transform.localRotation = Quaternion.identity;
            }

            // Connector SOUTH wall (between corridor and wings/courtyard).
            // Solid except at the wing-corridor entries (passage to each wing).
            BuildConnectorSouthWall(floorParent.transform, mats,
                leftWingCorridorCenterX, rightWingCorridorCenterX);

            // Connector corridor end-cap walls (west/east, solid)
            MakeCube("ConnectorWall_West", floorParent.transform,
                new Vector3(-WallThickness / 2f, RoomHeight / 2f, connectorCorridorCenterZ),
                new Vector3(WallThickness, RoomHeight, CorridorWidth),
                mats.WallRed);
            MakeCube("ConnectorWall_East", floorParent.transform,
                new Vector3(ConnectorLength + WallThickness / 2f, RoomHeight / 2f, connectorCorridorCenterZ),
                new Vector3(WallThickness, RoomHeight, CorridorWidth),
                mats.WallRed);

            // =========== LEFT WING ===========
            BuildWing(floorParent.transform, "LeftWing", leftWingX, wingStartZ, f,
                roomPrefab, mats);

            // =========== RIGHT WING ===========
            BuildWing(floorParent.transform, "RightWing", rightWingX, wingStartZ, f,
                roomPrefab, mats);
        }

        // =========== ROOF — covers connector + both wings ===========
        float totalHeight = Floors * floorVerticalSize;
        // Connector roof
        MakeCube("Roof_Connector", root.transform,
            new Vector3(ConnectorLength / 2f, totalHeight + FloorThickness / 2f, ConnectorDepth / 2f),
            new Vector3(ConnectorLength + 2 * WallThickness, FloorThickness, ConnectorDepth + 2 * WallThickness),
            mats.Roof);
        // Left wing roof
        MakeCube("Roof_LeftWing", root.transform,
            new Vector3(leftWingX + WingWidth / 2f, totalHeight + FloorThickness / 2f, wingStartZ + WingLength / 2f),
            new Vector3(WingWidth + 2 * WallThickness, FloorThickness, WingLength + 2 * WallThickness),
            mats.Roof);
        // Right wing roof
        MakeCube("Roof_RightWing", root.transform,
            new Vector3(rightWingX + WingWidth / 2f, totalHeight + FloorThickness / 2f, wingStartZ + WingLength / 2f),
            new Vector3(WingWidth + 2 * WallThickness, FloorThickness, WingLength + 2 * WallThickness),
            mats.Roof);

        // =========== STAIRS — at east end of connector corridor ===========
        var stairs = (GameObject)PrefabUtility.InstantiatePrefab(stairsPrefab, root.transform);
        stairs.name = "Stairs";
        stairs.transform.localRotation = Quaternion.Euler(0, 90f, 0);
        stairs.transform.localPosition = new Vector3(stairBottomX, 0, connectorCorridorCenterZ + StairWidth / 2f);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, BuildingPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>
    /// Build a south-extending wing: corridor running N-S in the middle, rooms east and west.
    /// </summary>
    private static void BuildWing(Transform floorParent, string wingName, float wingX, float wingStartZ, int floorIndex,
        GameObject roomPrefab, Mats mats)
    {
        float corridorCenterX = wingX + RoomDepth + CorridorWidth / 2f;
        float corridorWestX = wingX + RoomDepth;
        float corridorEastX = corridorWestX + CorridorWidth;

        // Wing corridor floor (solid, no stairs here)
        MakeCube($"{wingName}_CorridorFloor", floorParent,
            new Vector3(corridorCenterX, -FloorThickness / 2f, wingStartZ + WingLength / 2f),
            new Vector3(CorridorWidth, FloorThickness, WingLength),
            mats.Floor);

        // Wing corridor ceiling
        MakeCube($"{wingName}_CorridorCeiling", floorParent,
            new Vector3(corridorCenterX, RoomHeight + FloorThickness / 2f, wingStartZ + WingLength / 2f),
            new Vector3(CorridorWidth, FloorThickness, WingLength),
            mats.Ceiling);

        // West-side rooms (rotated 90° CW so their door faces +X = into corridor)
        for (int r = 0; r < WingRoomsPerSide; r++)
        {
            var room = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab, floorParent);
            room.name = $"Room_F{floorIndex}_{wingName}_W{r}";
            // After +90° Y rotation, the room corner (0,0,0) stays, room occupies x=[0, RoomDepth], z=[-RoomWidth, 0].
            // Translate so room sits at x=[wingX, wingX + RoomDepth], z=[wingStartZ + r*RoomWidth, wingStartZ + (r+1)*RoomWidth].
            room.transform.localPosition = new Vector3(wingX, 0, wingStartZ + (r + 1) * RoomWidth);
            room.transform.localRotation = Quaternion.Euler(0, 90f, 0);
        }

        // East-side rooms (rotated -90° CCW so their door faces -X = into corridor)
        for (int r = 0; r < WingRoomsPerSide; r++)
        {
            var room = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab, floorParent);
            room.name = $"Room_F{floorIndex}_{wingName}_E{r}";
            // After -90° Y rotation, original (0,0,0) → (0,0,0), (8,0,0) → (0,0,8), (0,0,7) → (-7,0,0).
            // Place at (wingX + WingWidth, 0, wingStartZ + r*RoomWidth) to get x=[wingX+10, wingX+17], z=[wingStartZ + r*8, wingStartZ + (r+1)*8].
            room.transform.localPosition = new Vector3(wingX + WingWidth, 0, wingStartZ + r * RoomWidth);
            room.transform.localRotation = Quaternion.Euler(0, -90f, 0);
        }

        // Wing south end-cap wall (closes off the south end of wing corridor)
        MakeCube($"{wingName}_CorridorWall_South", floorParent,
            new Vector3(corridorCenterX, RoomHeight / 2f, wingStartZ + WingLength + WallThickness / 2f),
            new Vector3(CorridorWidth, RoomHeight, WallThickness),
            mats.WallRed);
    }

    /// <summary>
    /// The connector's south wall — solid except for two openings where the wing corridors connect.
    /// </summary>
    private static void BuildConnectorSouthWall(Transform parent, Mats mats,
        float leftWingCorridorCenterX, float rightWingCorridorCenterX)
    {
        float wallZ = ConnectorDepth + WallThickness / 2f;  // just south of corridor

        float leftPassageMin = leftWingCorridorCenterX - CorridorWidth / 2f;
        float leftPassageMax = leftWingCorridorCenterX + CorridorWidth / 2f;
        float rightPassageMin = rightWingCorridorCenterX - CorridorWidth / 2f;
        float rightPassageMax = rightWingCorridorCenterX + CorridorWidth / 2f;

        // Segment 1: west of left passage
        if (leftPassageMin > 0f)
        {
            MakeCube("ConnectorWall_South_1", parent,
                new Vector3(leftPassageMin / 2f, RoomHeight / 2f, wallZ),
                new Vector3(leftPassageMin, RoomHeight, WallThickness),
                mats.WallRed);
        }
        // Segment 2: between the two passages
        float midSegStart = leftPassageMax;
        float midSegEnd = rightPassageMin;
        if (midSegEnd > midSegStart)
        {
            float midLen = midSegEnd - midSegStart;
            MakeCube("ConnectorWall_South_2", parent,
                new Vector3(midSegStart + midLen / 2f, RoomHeight / 2f, wallZ),
                new Vector3(midLen, RoomHeight, WallThickness),
                mats.WallRed);
        }
        // Segment 3: east of right passage
        if (rightPassageMax < ConnectorLength)
        {
            float eastLen = ConnectorLength - rightPassageMax;
            MakeCube("ConnectorWall_South_3", parent,
                new Vector3(rightPassageMax + eastLen / 2f, RoomHeight / 2f, wallZ),
                new Vector3(eastLen, RoomHeight, WallThickness),
                mats.WallRed);
        }
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
