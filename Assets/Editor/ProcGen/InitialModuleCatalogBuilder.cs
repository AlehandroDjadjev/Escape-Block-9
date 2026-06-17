using System;
using System.Collections.Generic;
using System.IO;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Validation;
using UnityEditor;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Editor
{
    public static class InitialModuleCatalogBuilder
    {
        private const string Root = "Assets/ProcGen";
        private const string PrefabRoot = Root + "/TilePrefabs";
        private const string SocketRoot = Root + "/Sockets";
        private const string ConnectorRoot = Root + "/Connectors";
        private const string BlockerRoot = Root + "/Blockers";
        private const string CatalogRoot = Root + "/Catalogs";
        private const string DefinitionRoot = CatalogRoot + "/Definitions";
        private const string ReportPath = "Assets/Docs/ProcGen/ModuleConversionReport.md";

        private const float RoomWidth = 8f;
        private const float RoomDepth = 7f;
        private const float RoomHeight = 3.5f;
        private const float CorridorWidth = 3f;
        private const float WallThickness = 0.2f;
        private const float DoorWidth = 1.2f;
        private const float DoorHeight = 2.2f;
        private const float StairWidth = 1.2f;
        private const float StairLength = 18f * 0.28f;

        [MenuItem("Tools/ProcGen/Build Initial Module Catalog")]
        public static void BuildInitialCatalog()
        {
            EnsureFolders();

            SocketSet sockets = CreateSockets();
            MaterialSet materials = LoadMaterials();
            GameObject doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/arhitektura/Door.prefab");
            GameObject openFrameConnector = CreateOpenFrameConnector(materials);
            GameObject wallPanelBlocker = CreateWallPanelBlocker(materials);
            GameObject corridorEndBlocker = CreateCorridorEndBlocker(materials);

            var definitions = new List<TileDefinition>();
            var moduleResults = new List<ModuleBuildResult>();
            foreach (ModuleSpec spec in CreateModuleSpecs(sockets, doorPrefab, openFrameConnector, wallPanelBlocker, corridorEndBlocker))
            {
                GameObject prefab = spec.SourcePrefabPath == null
                    ? CreateConstructedPrefab(spec, materials)
                    : CreateVariantPrefab(spec);

                TileDefinition definition = CreateDefinition(spec, prefab);
                definitions.Add(definition);

                Tile tile = prefab.GetComponent<Tile>();
                List<TileAuthoringIssue> issues = TileAuthoringValidator.Validate(tile);
                moduleResults.Add(new ModuleBuildResult(spec, prefab, definition, issues));
            }

            TileCatalog catalog = CreateCatalog(definitions);
            WriteReport(moduleResults, catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Initial procgen module catalog built: {catalog.name} with {definitions.Count} modules.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "ProcGen");
            EnsureFolder(Root, "TilePrefabs");
            EnsureFolder(Root, "Sockets");
            EnsureFolder(Root, "Connectors");
            EnsureFolder(Root, "Blockers");
            EnsureFolder(Root, "Catalogs");
            EnsureFolder(CatalogRoot, "Definitions");
            EnsureFolder("Assets", "Docs");
            EnsureFolder("Assets/Docs", "ProcGen");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static SocketSet CreateSockets()
        {
            DoorwaySocket corridor = CreateSocket("Socket_Corridor3m", "corridor_3m", "corridor_3m", "room_door", "stair", "fire_exit");
            DoorwaySocket room = CreateSocket("Socket_RoomDoor", "room_door", "corridor_3m");
            DoorwaySocket stair = CreateSocket("Socket_Stair", "stair", "corridor_3m", "stair");
            DoorwaySocket fireExit = CreateSocket("Socket_FireExit", "fire_exit", "corridor_3m");
            return new SocketSet(corridor, room, stair, fireExit);
        }

        private static DoorwaySocket CreateSocket(string assetName, string socketName, params string[] compatibleSocketNames)
        {
            string path = $"{SocketRoot}/{assetName}.asset";
            DoorwaySocket socket = AssetDatabase.LoadAssetAtPath<DoorwaySocket>(path);
            if (socket == null)
            {
                socket = ScriptableObject.CreateInstance<DoorwaySocket>();
                AssetDatabase.CreateAsset(socket, path);
            }

            SerializedObject serialized = new SerializedObject(socket);
            serialized.FindProperty("socketName").stringValue = socketName;
            SerializedProperty compatible = serialized.FindProperty("compatibleSocketNames");
            compatible.arraySize = compatibleSocketNames.Length;
            for (int i = 0; i < compatibleSocketNames.Length; i++)
            {
                compatible.GetArrayElementAtIndex(i).stringValue = compatibleSocketNames[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(socket);
            return socket;
        }

        private static MaterialSet LoadMaterials()
        {
            return new MaterialSet(
                AssetDatabase.LoadAssetAtPath<Material>("Assets/arhitektura/Generated/Mat_Floor.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/arhitektura/Generated/Mat_Ceiling.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/arhitektura/Generated/Mat_WallBeige.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/arhitektura/Generated/Mat_WallRed.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/arhitektura/Generated/Mat_Metal.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/arhitektura/Generated/Mat_Stairs.mat"));
        }

        private static GameObject CreateOpenFrameConnector(MaterialSet materials)
        {
            string path = $"{ConnectorRoot}/OpenFrameConnector.prefab";
            GameObject root = new GameObject("OpenFrameConnector");
            AddCube(root.transform, "Header", new Vector3(0f, 2.35f, 0f), new Vector3(DoorWidth + 0.4f, 0.18f, 0.18f), materials.WallRed);
            AddCube(root.transform, "LeftJamb", new Vector3(-(DoorWidth * 0.5f + 0.1f), DoorHeight * 0.5f, 0f), new Vector3(0.18f, DoorHeight, 0.18f), materials.WallBeige);
            AddCube(root.transform, "RightJamb", new Vector3(DoorWidth * 0.5f + 0.1f, DoorHeight * 0.5f, 0f), new Vector3(0.18f, DoorHeight, 0.18f), materials.WallBeige);
            return SaveOrReplacePrefab(root, path);
        }

        private static GameObject CreateWallPanelBlocker(MaterialSet materials)
        {
            string path = $"{BlockerRoot}/WallPanelBlocker.prefab";
            GameObject root = new GameObject("WallPanelBlocker");
            AddCube(root.transform, "Panel", new Vector3(0f, DoorHeight * 0.5f, 0f), new Vector3(DoorWidth + 0.25f, DoorHeight, WallThickness), materials.WallBeige);
            AddCube(root.transform, "RedBand", new Vector3(0f, 1.15f, -0.01f), new Vector3(DoorWidth + 0.28f, 0.55f, WallThickness + 0.02f), materials.WallRed);
            return SaveOrReplacePrefab(root, path);
        }

        // Seals an unused corridor opening across the FULL 3m x 3.5m corridor cross-section.
        // Parented to the doorway (local origin at doorway, local +Z = outward), so a single
        // prefab caps north/south/east/west arms regardless of arm orientation.
        private static GameObject CreateCorridorEndBlocker(MaterialSet materials)
        {
            string path = $"{BlockerRoot}/CorridorEndBlocker.prefab";
            GameObject root = new GameObject("CorridorEndBlocker");
            // Doorway anchor is at y=1.1; offset the panel so it spans floor (y=0) to ceiling (y=RoomHeight).
            float verticalOffset = RoomHeight * 0.5f - 1.1f;
            float redBandOffset = 1.2f - 1.1f;
            float panelWidth = CorridorWidth + WallThickness * 2f;
            AddCube(root.transform, "Panel", new Vector3(0f, verticalOffset, 0f), new Vector3(panelWidth, RoomHeight, WallThickness), materials.WallBeige);
            AddCube(root.transform, "RedBand", new Vector3(0f, redBandOffset, -(WallThickness * 0.5f + 0.01f)), new Vector3(panelWidth + 0.02f, 0.55f, WallThickness + 0.02f), materials.WallRed);
            return SaveOrReplacePrefab(root, path);
        }

        private static List<ModuleSpec> CreateModuleSpecs(SocketSet sockets, GameObject doorPrefab, GameObject openFrame, GameObject blocker, GameObject corridorBlocker)
        {
            var modules = new List<ModuleSpec>();
            modules.Add(new ModuleSpec(
                "start_exit_lobby",
                "Start Entrance - Exit Lobby",
                "Start_Entrance_ExitLobby",
                "Assets/arhitektura/Generated/ExitLobby.prefab",
                TileCategory.Exit,
                new[] { "start", "entrance", "exit-lobby", "fire-exit-capable" },
                1f,
                1,
                true,
                AllowedYawRotations.OnlyAuthored,
                new Vector3(RoomWidth * 0.5f, RoomHeight * 0.5f, RoomDepth * 0.5f),
                new Vector3(RoomWidth + 0.4f, RoomHeight + 0.3f, RoomDepth + 0.4f),
                new[]
                {
                    DoorwaySpec.RoomDoor("interior_door", new Vector3(RoomWidth * 0.5f, 1.1f, RoomDepth + 0.05f), Vector3.forward, sockets.RoomDoor, doorPrefab, blocker),
                    DoorwaySpec.FireExit("exterior_exit", new Vector3(RoomWidth * 0.5f, 1.1f, -0.05f), Vector3.back, sockets.FireExit, doorPrefab, blocker),
                },
                new[]
                {
                    new SpawnSpec("player_start", SpawnMarkerKind.PlayerStart, new Vector3(RoomWidth * 0.5f, 0.05f, RoomDepth * 0.35f), Vector3.forward, "player"),
                    new SpawnSpec("exit_marker", SpawnMarkerKind.Exit, new Vector3(RoomWidth * 0.5f, 0.05f, 0.6f), Vector3.back, "exit"),
                }));

            modules.Add(CorridorStraight(sockets, corridorBlocker));
            modules.Add(CorridorCorner(sockets, corridorBlocker));
            modules.Add(CorridorTJunction(sockets, corridorBlocker));
            modules.Add(CorridorCrossJunction(sockets, corridorBlocker));

            modules.Add(RoomModule("room_classroom", "Normal Room - Classroom", "Room_Classroom", "Assets/arhitektura/Generated/Room.prefab", TileCategory.Room, new[] { "room", "classroom", "normal" }, false, 1f, sockets.RoomDoor, doorPrefab, blocker));
            modules.Add(RoomModule("room_bathroom", "Special Room - Bathroom", "Room_Bathroom", "Assets/arhitektura/Generated/Bathroom.prefab", TileCategory.Special, new[] { "room", "bathroom", "utility", "special" }, false, 0.75f, sockets.RoomDoor, doorPrefab, blocker));
            modules.Add(RoomModule("room_shop_special", "Special Room - Shop", "Room_Shop_Special", "Assets/arhitektura/Generated/Shop.prefab", TileCategory.Special, new[] { "room", "shop", "special", "large" }, true, 0.4f, sockets.RoomDoor, doorPrefab, blocker));

            modules.Add(DeadEnd(sockets, corridorBlocker));

            modules.Add(new ModuleSpec(
                "fire_exit_lobby",
                "Fire Exit Lobby",
                "FireExit_ExitLobby",
                "Assets/arhitektura/Generated/ExitLobby.prefab",
                TileCategory.Exit,
                new[] { "exit", "fire-exit", "exit-lobby" },
                0.6f,
                1,
                true,
                AllowedYawRotations.Yaw180,
                new Vector3(RoomWidth * 0.5f, RoomHeight * 0.5f, RoomDepth * 0.5f),
                new Vector3(RoomWidth + 0.4f, RoomHeight + 0.3f, RoomDepth + 0.4f),
                new[]
                {
                    DoorwaySpec.RoomDoor("interior_door", new Vector3(RoomWidth * 0.5f, 1.1f, RoomDepth + 0.05f), Vector3.forward, sockets.RoomDoor, doorPrefab, blocker),
                    DoorwaySpec.FireExit("fire_exit", new Vector3(RoomWidth * 0.5f, 1.1f, -0.05f), Vector3.back, sockets.FireExit, doorPrefab, blocker),
                },
                new[]
                {
                    new SpawnSpec("exit_marker", SpawnMarkerKind.Exit, new Vector3(RoomWidth * 0.5f, 0.05f, 0.65f), Vector3.back, "exit"),
                    new SpawnSpec("loot_low", SpawnMarkerKind.Loot, new Vector3(RoomWidth * 0.3f, 0.05f, RoomDepth * 0.55f), Vector3.right, "low-value"),
                }));

            modules.Add(new ModuleSpec(
                "stairs_vertical",
                "Vertical Link - Stairs",
                "Stairs_Vertical",
                "Assets/arhitektura/Generated/Stairs.prefab",
                TileCategory.Stair,
                new[] { "stairs", "vertical-link", "floor-transition" },
                0.7f,
                2,
                false,
                AllowedYawRotations.Yaw180,
                new Vector3(StairWidth * 0.5f, RoomHeight * 0.5f, StairLength * 0.5f),
                new Vector3(StairWidth + 0.4f, RoomHeight + 0.3f, StairLength + 0.4f),
                new[]
                {
                    DoorwaySpec.Stair("bottom_landing", new Vector3(StairWidth * 0.5f, 0.25f, -0.05f), Vector3.back, sockets.Stair, openFrame, blocker, 1),
                    DoorwaySpec.Stair("top_landing", new Vector3(StairWidth * 0.5f, RoomHeight - 0.15f, StairLength + 0.05f), Vector3.forward, sockets.Stair, openFrame, blocker, -1),
                },
                new[]
                {
                    new SpawnSpec("light_mid", SpawnMarkerKind.Light, new Vector3(StairWidth * 0.5f, RoomHeight - 0.25f, StairLength * 0.5f), Vector3.down, "fluorescent"),
                }));

            return modules;
        }

        private static ModuleSpec RoomModule(string id, string displayName, string prefabName, string sourcePath, TileCategory category, string[] tags, bool unique, float selectionWeight, DoorwaySocket socket, GameObject doorPrefab, GameObject blocker)
        {
            return new ModuleSpec(
                id,
                displayName,
                prefabName,
                sourcePath,
                category,
                tags,
                selectionWeight,
                unique ? 1 : -1,
                unique,
                AllowedYawRotations.AnyRightAngle,
                new Vector3(RoomWidth * 0.5f, RoomHeight * 0.5f, RoomDepth * 0.5f),
                new Vector3(RoomWidth + 0.4f, RoomHeight + 0.3f, RoomDepth + 0.4f),
                new[] { DoorwaySpec.RoomDoor("room_door", new Vector3(RoomWidth * 0.5f, 1.1f, RoomDepth + 0.05f), Vector3.forward, socket, doorPrefab, blocker) },
                new[]
                {
                    new SpawnSpec("loot_mid", SpawnMarkerKind.Loot, new Vector3(RoomWidth * 0.25f, 0.05f, RoomDepth * 0.5f), Vector3.right, "loot"),
                    new SpawnSpec("enemy_patrol", SpawnMarkerKind.Enemy, new Vector3(RoomWidth * 0.7f, 0.05f, RoomDepth * 0.45f), Vector3.left, "enemy"),
                    new SpawnSpec("ceiling_light", SpawnMarkerKind.Light, new Vector3(RoomWidth * 0.5f, RoomHeight - 0.2f, RoomDepth * 0.5f), Vector3.down, "fluorescent"),
                });
        }

        private static ModuleSpec CorridorStraight(SocketSet sockets, GameObject corridorBlocker)
        {
            return new ModuleSpec(
                "corridor_straight_8m",
                "Straight Corridor 8m",
                "Corridor_Straight_8m",
                null,
                TileCategory.Corridor,
                new[] { "corridor", "straight", "3m-wide" },
                1.25f,
                -1,
                false,
                AllowedYawRotations.AnyRightAngle,
                new Vector3(0f, RoomHeight * 0.5f, 0f),
                new Vector3(CorridorWidth + 0.4f, RoomHeight + 0.3f, 8.4f),
                new[]
                {
                    DoorwaySpec.Corridor("north", new Vector3(0f, 1.1f, 4f), Vector3.forward, sockets.Corridor3m, null, corridorBlocker),
                    DoorwaySpec.Corridor("south", new Vector3(0f, 1.1f, -4f), Vector3.back, sockets.Corridor3m, null, corridorBlocker),
                },
                new[] { new SpawnSpec("ceiling_light", SpawnMarkerKind.Light, new Vector3(0f, RoomHeight - 0.2f, 0f), Vector3.down, "fluorescent") },
                ConstructedShape.StraightCorridor);
        }

        private static ModuleSpec CorridorCorner(SocketSet sockets, GameObject corridorBlocker)
        {
            return CorridorShape("corridor_corner_3m", "Corner Corridor 3m", "Corridor_Corner_3m", new[] { "corridor", "corner", "3m-wide" },
                new[]
                {
                    DoorwaySpec.Corridor("south", new Vector3(0f, 1.1f, -3f), Vector3.back, sockets.Corridor3m, null, corridorBlocker),
                    DoorwaySpec.Corridor("east", new Vector3(3f, 1.1f, 0f), Vector3.right, sockets.Corridor3m, null, corridorBlocker),
                },
                ConstructedShape.CornerCorridor,
                1.1f);
        }

        private static ModuleSpec CorridorTJunction(SocketSet sockets, GameObject corridorBlocker)
        {
            return CorridorShape("corridor_t_junction_3m", "T Junction Corridor 3m", "Corridor_TJunction_3m", new[] { "corridor", "junction", "t-junction", "3m-wide" },
                new[]
                {
                    DoorwaySpec.Corridor("north", new Vector3(0f, 1.1f, 3f), Vector3.forward, sockets.Corridor3m, null, corridorBlocker),
                    DoorwaySpec.Corridor("south", new Vector3(0f, 1.1f, -3f), Vector3.back, sockets.Corridor3m, null, corridorBlocker),
                    DoorwaySpec.Corridor("east", new Vector3(3f, 1.1f, 0f), Vector3.right, sockets.Corridor3m, null, corridorBlocker),
                },
                ConstructedShape.TJunction,
                1f);
        }

        private static ModuleSpec CorridorCrossJunction(SocketSet sockets, GameObject corridorBlocker)
        {
            return CorridorShape("corridor_cross_junction_3m", "Cross Junction Corridor 3m", "Corridor_CrossJunction_3m", new[] { "corridor", "junction", "cross-junction", "3m-wide" },
                new[]
                {
                    DoorwaySpec.Corridor("north", new Vector3(0f, 1.1f, 3f), Vector3.forward, sockets.Corridor3m, null, corridorBlocker),
                    DoorwaySpec.Corridor("south", new Vector3(0f, 1.1f, -3f), Vector3.back, sockets.Corridor3m, null, corridorBlocker),
                    DoorwaySpec.Corridor("east", new Vector3(3f, 1.1f, 0f), Vector3.right, sockets.Corridor3m, null, corridorBlocker),
                    DoorwaySpec.Corridor("west", new Vector3(-3f, 1.1f, 0f), Vector3.left, sockets.Corridor3m, null, corridorBlocker),
                },
                ConstructedShape.CrossJunction,
                0.55f);
        }

        private static ModuleSpec CorridorShape(string id, string displayName, string prefabName, string[] tags, DoorwaySpec[] doorways, ConstructedShape shape, float selectionWeight)
        {
            return new ModuleSpec(
                id,
                displayName,
                prefabName,
                null,
                TileCategory.Corridor,
                tags,
                selectionWeight,
                -1,
                false,
                AllowedYawRotations.AnyRightAngle,
                new Vector3(0f, RoomHeight * 0.5f, 0f),
                new Vector3(6.4f, RoomHeight + 0.3f, 6.4f),
                doorways,
                new[] { new SpawnSpec("ceiling_light", SpawnMarkerKind.Light, new Vector3(0f, RoomHeight - 0.2f, 0f), Vector3.down, "fluorescent") },
                shape);
        }

        private static ModuleSpec DeadEnd(SocketSet sockets, GameObject corridorBlocker)
        {
            return new ModuleSpec(
                "corridor_dead_end",
                "Dead End Corridor Cap",
                "DeadEnd_CorridorCap",
                null,
                TileCategory.Corridor,
                new[] { "corridor", "dead-end", "cap", "3m-wide" },
                0.45f,
                -1,
                false,
                AllowedYawRotations.AnyRightAngle,
                new Vector3(0f, RoomHeight * 0.5f, 0f),
                new Vector3(CorridorWidth + 0.4f, RoomHeight + 0.3f, 4.4f),
                new[] { DoorwaySpec.Corridor("south", new Vector3(0f, 1.1f, -2f), Vector3.back, sockets.Corridor3m, null, corridorBlocker) },
                new[]
                {
                    new SpawnSpec("loot_low", SpawnMarkerKind.Loot, new Vector3(0f, 0.05f, 1.1f), Vector3.back, "low-value"),
                    new SpawnSpec("ceiling_light", SpawnMarkerKind.Light, new Vector3(0f, RoomHeight - 0.2f, 0f), Vector3.down, "fluorescent"),
                },
                ConstructedShape.DeadEnd);
        }

        private static GameObject CreateVariantPrefab(ModuleSpec spec)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(spec.SourcePrefabPath);
            if (source == null)
            {
                throw new InvalidOperationException($"Source prefab missing: {spec.SourcePrefabPath}");
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = spec.PrefabName;
            ConfigureTileRoot(instance, spec);
            string path = $"{PrefabRoot}/{spec.PrefabName}.prefab";
            GameObject prefab = SaveOrReplacePrefab(instance, path);
            return prefab;
        }

        private static GameObject CreateConstructedPrefab(ModuleSpec spec, MaterialSet materials)
        {
            GameObject root = new GameObject(spec.PrefabName);
            ConfigureTileRoot(root, spec);

            switch (spec.Shape)
            {
                case ConstructedShape.StraightCorridor:
                    BuildStraightCorridor(root.transform, materials, 8f);
                    break;
                case ConstructedShape.CornerCorridor:
                    BuildCrossLikeCorridor(root.transform, materials, true, false, false, true);
                    break;
                case ConstructedShape.TJunction:
                    BuildCrossLikeCorridor(root.transform, materials, true, true, false, true);
                    break;
                case ConstructedShape.CrossJunction:
                    BuildCrossLikeCorridor(root.transform, materials, true, true, true, true);
                    break;
                case ConstructedShape.DeadEnd:
                    BuildStraightCorridor(root.transform, materials, 4f);
                    AddCube(root.transform, "EndBlockerWall", new Vector3(0f, RoomHeight * 0.5f, 2f), new Vector3(CorridorWidth + WallThickness * 2f, RoomHeight, WallThickness), materials.WallBeige);
                    break;
            }

            string path = $"{PrefabRoot}/{spec.PrefabName}.prefab";
            return SaveOrReplacePrefab(root, path);
        }

        private static void ConfigureTileRoot(GameObject root, ModuleSpec spec)
        {
            Tile tile = root.GetComponent<Tile>();
            if (tile == null)
            {
                tile = root.AddComponent<Tile>();
            }

            SetTile(tile, spec);

            Transform authoring = FindOrCreateChild(root.transform, "_ProcGenAuthoring");
            ClearChildren(authoring);

            OccupancyBounds occupancy = new GameObject("Occupancy_Main").AddComponent<OccupancyBounds>();
            occupancy.transform.SetParent(authoring, false);
            SetOccupancy(occupancy, spec.OccupancyCenter, spec.OccupancySize);

            Transform doorways = FindOrCreateChild(authoring, "Doorways");
            foreach (DoorwaySpec doorwaySpec in spec.Doorways)
            {
                Doorway doorway = new GameObject(doorwaySpec.ConnectorId).AddComponent<Doorway>();
                doorway.transform.SetParent(doorways, false);
                doorway.transform.localPosition = doorwaySpec.LocalPosition;
                doorway.transform.localRotation = Quaternion.LookRotation(doorwaySpec.Forward.normalized, Vector3.up);
                SetDoorway(doorway, doorwaySpec);
            }

            Transform spawnMarkers = FindOrCreateChild(authoring, "SpawnMarkers");
            foreach (SpawnSpec spawnSpec in spec.SpawnMarkers)
            {
                SpawnMarker marker = new GameObject(spawnSpec.MarkerId).AddComponent<SpawnMarker>();
                marker.transform.SetParent(spawnMarkers, false);
                marker.transform.localPosition = spawnSpec.LocalPosition;
                marker.transform.localRotation = Quaternion.LookRotation(spawnSpec.Forward.normalized, Vector3.up);
                SetSpawnMarker(marker, spawnSpec);
            }
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(name);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static void SetTile(Tile tile, ModuleSpec spec)
        {
            SerializedObject serialized = new SerializedObject(tile);
            serialized.FindProperty("moduleId").stringValue = spec.ModuleId;
            serialized.FindProperty("category").enumValueIndex = (int)spec.Category;
            SetStringArray(serialized.FindProperty("tags"), spec.Tags);
            serialized.FindProperty("selectionWeight").floatValue = spec.SelectionWeight;
            serialized.FindProperty("maxUseCount").intValue = spec.MaxUseCount;
            serialized.FindProperty("unique").boolValue = spec.Unique;
            serialized.FindProperty("allowedYawRotations").intValue = (int)spec.AllowedYawRotations;
            serialized.FindProperty("includeInactiveAuthoringChildren").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tile);
        }

        private static void SetOccupancy(OccupancyBounds occupancy, Vector3 center, Vector3 size)
        {
            SerializedObject serialized = new SerializedObject(occupancy);
            serialized.FindProperty("center").vector3Value = center;
            serialized.FindProperty("size").vector3Value = size;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(occupancy);
        }

        private static void SetDoorway(Doorway doorway, DoorwaySpec spec)
        {
            SerializedObject serialized = new SerializedObject(doorway);
            serialized.FindProperty("connectorId").stringValue = spec.ConnectorId;
            serialized.FindProperty("socket").objectReferenceValue = spec.Socket;
            serialized.FindProperty("socketName").stringValue = spec.Socket != null ? spec.Socket.SocketName : string.Empty;
            serialized.FindProperty("connectorKind").enumValueIndex = (int)spec.Kind;
            serialized.FindProperty("connectorPrefab").objectReferenceValue = spec.ConnectorPrefab;
            serialized.FindProperty("blockerPrefab").objectReferenceValue = spec.BlockerPrefab;
            serialized.FindProperty("width").floatValue = DoorWidth;
            serialized.FindProperty("height").floatValue = DoorHeight;
            serialized.FindProperty("floorDelta").intValue = spec.FloorDelta;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(doorway);
        }

        private static void SetSpawnMarker(SpawnMarker marker, SpawnSpec spec)
        {
            SerializedObject serialized = new SerializedObject(marker);
            serialized.FindProperty("markerId").stringValue = spec.MarkerId;
            serialized.FindProperty("kind").enumValueIndex = (int)spec.Kind;
            SetStringArray(serialized.FindProperty("tags"), spec.Tags);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(marker);
        }

        private static TileDefinition CreateDefinition(ModuleSpec spec, GameObject prefab)
        {
            string path = $"{DefinitionRoot}/{spec.PrefabName}_Definition.asset";
            TileDefinition definition = AssetDatabase.LoadAssetAtPath<TileDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<TileDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("moduleId").stringValue = spec.ModuleId;
            serialized.FindProperty("displayName").stringValue = spec.DisplayName;
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.FindProperty("category").enumValueIndex = (int)spec.Category;
            SetStringArray(serialized.FindProperty("tags"), spec.Tags);
            serialized.FindProperty("defaultConnectorKind").enumValueIndex = (int)spec.DefaultConnectorKind;
            serialized.FindProperty("selectionWeight").floatValue = spec.SelectionWeight;
            serialized.FindProperty("maxUseCount").intValue = spec.MaxUseCount;
            serialized.FindProperty("unique").boolValue = spec.Unique;
            serialized.FindProperty("allowedYawRotations").intValue = (int)spec.AllowedYawRotations;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static TileCatalog CreateCatalog(List<TileDefinition> definitions)
        {
            string path = $"{CatalogRoot}/InitialBlock9TileCatalog.asset";
            TileCatalog catalog = AssetDatabase.LoadAssetAtPath<TileCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<TileCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            SerializedObject serialized = new SerializedObject(catalog);
            serialized.FindProperty("catalogId").stringValue = "initial_block9_tile_catalog";
            serialized.FindProperty("version").stringValue = "0.1";
            SerializedProperty array = serialized.FindProperty("definitions");
            array.arraySize = definitions.Count;
            for (int i = 0; i < definitions.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void SetStringArray(SerializedProperty property, IReadOnlyList<string> values)
        {
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        private static GameObject SaveOrReplacePrefab(GameObject instance, string path)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path, out bool success);
            UnityEngine.Object.DestroyImmediate(instance);
            if (!success || prefab == null)
            {
                throw new InvalidOperationException($"Failed to save prefab at {path}");
            }

            return prefab;
        }

        private static void BuildStraightCorridor(Transform root, MaterialSet materials, float length)
        {
            AddCube(root, "Floor", new Vector3(0f, -0.075f, 0f), new Vector3(CorridorWidth, 0.15f, length), materials.Floor);
            AddCube(root, "Ceiling", new Vector3(0f, RoomHeight + 0.075f, 0f), new Vector3(CorridorWidth, 0.15f, length), materials.Ceiling);
            AddCube(root, "LeftWall", new Vector3(-(CorridorWidth * 0.5f + WallThickness * 0.5f), RoomHeight * 0.5f, 0f), new Vector3(WallThickness, RoomHeight, length), materials.WallBeige);
            AddCube(root, "RightWall", new Vector3(CorridorWidth * 0.5f + WallThickness * 0.5f, RoomHeight * 0.5f, 0f), new Vector3(WallThickness, RoomHeight, length), materials.WallBeige);
            AddCube(root, "LeftRedBand", new Vector3(-(CorridorWidth * 0.5f + WallThickness * 0.51f), 1.2f, 0f), new Vector3(WallThickness + 0.02f, 0.55f, length), materials.WallRed);
            AddCube(root, "RightRedBand", new Vector3(CorridorWidth * 0.5f + WallThickness * 0.51f, 1.2f, 0f), new Vector3(WallThickness + 0.02f, 0.55f, length), materials.WallRed);
        }

        private static void BuildCrossLikeCorridor(Transform root, MaterialSet materials, bool north, bool south, bool west, bool east)
        {
            const float halfWidth = 1.5f;
            const float armLength = 1.5f;
            const float floorThickness = 0.15f;
            const float ceilThickness = 0.15f;

            // Central intersection block.
            AddCube(root, "Center_Floor", new Vector3(0f, -floorThickness * 0.5f, 0f), new Vector3(CorridorWidth, floorThickness, CorridorWidth), materials.Floor);
            AddCube(root, "Center_Ceiling", new Vector3(0f, RoomHeight + ceilThickness * 0.5f, 0f), new Vector3(CorridorWidth, ceilThickness, CorridorWidth), materials.Ceiling);

            // Arm floors/ceilings only for enabled openings.
            if (north)
            {
                AddCube(root, "North_Floor", new Vector3(0f, -floorThickness * 0.5f, halfWidth + armLength * 0.5f), new Vector3(CorridorWidth, floorThickness, armLength), materials.Floor);
                AddCube(root, "North_Ceiling", new Vector3(0f, RoomHeight + ceilThickness * 0.5f, halfWidth + armLength * 0.5f), new Vector3(CorridorWidth, ceilThickness, armLength), materials.Ceiling);
            }

            if (south)
            {
                AddCube(root, "South_Floor", new Vector3(0f, -floorThickness * 0.5f, -halfWidth - armLength * 0.5f), new Vector3(CorridorWidth, floorThickness, armLength), materials.Floor);
                AddCube(root, "South_Ceiling", new Vector3(0f, RoomHeight + ceilThickness * 0.5f, -halfWidth - armLength * 0.5f), new Vector3(CorridorWidth, ceilThickness, armLength), materials.Ceiling);
            }

            if (east)
            {
                AddCube(root, "East_Floor", new Vector3(halfWidth + armLength * 0.5f, -floorThickness * 0.5f, 0f), new Vector3(armLength, floorThickness, CorridorWidth), materials.Floor);
                AddCube(root, "East_Ceiling", new Vector3(halfWidth + armLength * 0.5f, RoomHeight + ceilThickness * 0.5f, 0f), new Vector3(armLength, ceilThickness, CorridorWidth), materials.Ceiling);
            }

            if (west)
            {
                AddCube(root, "West_Floor", new Vector3(-halfWidth - armLength * 0.5f, -floorThickness * 0.5f, 0f), new Vector3(armLength, floorThickness, CorridorWidth), materials.Floor);
                AddCube(root, "West_Ceiling", new Vector3(-halfWidth - armLength * 0.5f, RoomHeight + ceilThickness * 0.5f, 0f), new Vector3(armLength, ceilThickness, CorridorWidth), materials.Ceiling);
            }

            // Corridor side walls for enabled arms.
            if (north)
            {
                AddCube(root, "North_LeftWall", new Vector3(-halfWidth - WallThickness * 0.5f, RoomHeight * 0.5f, halfWidth + armLength * 0.5f), new Vector3(WallThickness, RoomHeight, armLength), materials.WallBeige);
                AddCube(root, "North_RightWall", new Vector3(halfWidth + WallThickness * 0.5f, RoomHeight * 0.5f, halfWidth + armLength * 0.5f), new Vector3(WallThickness, RoomHeight, armLength), materials.WallBeige);
            }

            if (south)
            {
                AddCube(root, "South_LeftWall", new Vector3(-halfWidth - WallThickness * 0.5f, RoomHeight * 0.5f, -halfWidth - armLength * 0.5f), new Vector3(WallThickness, RoomHeight, armLength), materials.WallBeige);
                AddCube(root, "South_RightWall", new Vector3(halfWidth + WallThickness * 0.5f, RoomHeight * 0.5f, -halfWidth - armLength * 0.5f), new Vector3(WallThickness, RoomHeight, armLength), materials.WallBeige);
            }

            if (east)
            {
                AddCube(root, "East_UpperWall", new Vector3(halfWidth + armLength * 0.5f, RoomHeight * 0.5f, halfWidth + WallThickness * 0.5f), new Vector3(armLength, RoomHeight, WallThickness), materials.WallBeige);
                AddCube(root, "East_LowerWall", new Vector3(halfWidth + armLength * 0.5f, RoomHeight * 0.5f, -halfWidth - WallThickness * 0.5f), new Vector3(armLength, RoomHeight, WallThickness), materials.WallBeige);
            }

            if (west)
            {
                AddCube(root, "West_UpperWall", new Vector3(-halfWidth - armLength * 0.5f, RoomHeight * 0.5f, halfWidth + WallThickness * 0.5f), new Vector3(armLength, RoomHeight, WallThickness), materials.WallBeige);
                AddCube(root, "West_LowerWall", new Vector3(-halfWidth - armLength * 0.5f, RoomHeight * 0.5f, -halfWidth - WallThickness * 0.5f), new Vector3(armLength, RoomHeight, WallThickness), materials.WallBeige);
            }

            // Closed directions get solid wall at intersection boundary.
            AddTerminalCap(root, "NorthCap", new Vector3(0f, RoomHeight * 0.5f, halfWidth + WallThickness * 0.5f), new Vector3(CorridorWidth + WallThickness * 2f, RoomHeight, WallThickness), north, materials);
            AddTerminalCap(root, "SouthCap", new Vector3(0f, RoomHeight * 0.5f, -halfWidth - WallThickness * 0.5f), new Vector3(CorridorWidth + WallThickness * 2f, RoomHeight, WallThickness), south, materials);
            AddTerminalCap(root, "EastCap", new Vector3(halfWidth + WallThickness * 0.5f, RoomHeight * 0.5f, 0f), new Vector3(WallThickness, RoomHeight, CorridorWidth + WallThickness * 2f), east, materials);
            AddTerminalCap(root, "WestCap", new Vector3(-halfWidth - WallThickness * 0.5f, RoomHeight * 0.5f, 0f), new Vector3(WallThickness, RoomHeight, CorridorWidth + WallThickness * 2f), west, materials);
        }

        private static void AddTerminalCap(Transform root, string baseName, Vector3 position, Vector3 size, bool isOpen, MaterialSet materials)
        {
            if (isOpen)
            {
                return;
            }

            AddCube(root, baseName, position, size, materials.WallBeige);
            Vector3 redBandSize = new Vector3(size.x + 0.02f, 0.55f, size.z + 0.02f);
            AddCube(root, baseName + "_RedBand", new Vector3(position.x, 1.2f, position.z), redBandSize, materials.WallRed);
        }

        private static GameObject AddCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = localScale;
            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return cube;
        }

        private static void WriteReport(List<ModuleBuildResult> results, TileCatalog catalog)
        {
            bool inventoryExists = File.Exists("Assets/Docs/ProcGen/CurrentBuildingInventory.md");
            string inventoryNote = inventoryExists
                ? "- `Assets/Docs/ProcGen/CurrentBuildingInventory.md` is present and was used as the source inventory baseline alongside measured dimensions from `BuildingGenerator.cs`."
                : "- `Assets/Docs/ProcGen/CurrentBuildingInventory.md` was requested but is not present in this workspace. The conversion used the existing generated prefab inventory and the measured dimensions from `BuildingGenerator.cs`.";

            var lines = new List<string>
            {
                "# Module Conversion Report",
                string.Empty,
                "Generated by `Tools/ProcGen/Build Initial Module Catalog`.",
                string.Empty,
                "## Source Notes",
                string.Empty,
                inventoryNote,
                "- Requested `.ai/skills/...` files were not present; this pass used `AGENTS.md` and `.codex/skills/facility-procgen` instructions.",
                "- Source prefabs under `Assets/arhitektura` were preserved. Converted room modules are saved as prefab variants/wrappers under `Assets/ProcGen/TilePrefabs`.",
                "- Corridor modules are new prefab wrappers built from the current building dimensions and existing generated materials because no standalone corridor prefabs existed.",
                string.Empty,
                "## Measured Dimensions",
                string.Empty,
                $"- Room footprint: {RoomWidth}m x {RoomDepth}m, height {RoomHeight}m.",
                $"- Corridor width: {CorridorWidth}m.",
                $"- Doorway opening: {DoorWidth}m x {DoorHeight}m.",
                $"- Stair run: approximately {StairLength:0.00}m from {18} steps x {0.28f:0.00}m.",
                string.Empty,
                "## Catalog",
                string.Empty,
                $"- Catalog asset: `{AssetDatabase.GetAssetPath(catalog)}`",
                $"- Module count: {results.Count}",
                string.Empty,
                "## Converted Modules",
                string.Empty,
                "| Module ID | Prefab | Source | Category | Connectors | Validation |",
                "| --- | --- | --- | --- | --- | --- |",
            };

            foreach (ModuleBuildResult result in results)
            {
                int errors = 0;
                int warnings = 0;
                foreach (TileAuthoringIssue issue in result.Issues)
                {
                    if (issue.Severity == TileAuthoringSeverity.Error)
                    {
                        errors++;
                    }
                    else if (issue.Severity == TileAuthoringSeverity.Warning)
                    {
                        warnings++;
                    }
                }

                string validation = errors == 0 && warnings == 0 ? "Pass" : $"{errors} errors, {warnings} warnings";
                lines.Add($"| `{result.Spec.ModuleId}` | `{AssetDatabase.GetAssetPath(result.Prefab)}` | `{result.Spec.SourceLabel}` | {result.Spec.Category} | {result.Spec.Doorways.Count} | {validation} |");
            }

            lines.Add(string.Empty);
            lines.Add("## Validation Details");
            lines.Add(string.Empty);
            foreach (ModuleBuildResult result in results)
            {
                lines.Add($"### {result.Spec.ModuleId}");
                if (result.Issues.Count == 0)
                {
                    lines.Add("- Pass.");
                }
                else
                {
                    foreach (TileAuthoringIssue issue in result.Issues)
                    {
                        lines.Add($"- {issue.Severity}: {issue.Message}");
                    }
                }

                lines.Add(string.Empty);
            }

            lines.Add("## Preservation Notes");
            lines.Add(string.Empty);
            lines.Add("- Existing room art, scripts, generated building prefabs, door prefab, and material assets were not rewritten.");
            lines.Add("- Placeholder connector and blocker prefabs only cover missing standalone connector/blocker assets and use existing Block 9 materials.");
            lines.Add("- The initial catalog is intentionally small and avoids locking runtime generation to DunGen; it stores DunGen-style Tile/Doorway/Socket metadata behind project-owned types.");

            File.WriteAllLines(ReportPath, lines);
            AssetDatabase.ImportAsset(ReportPath);
        }

        private readonly struct SocketSet
        {
            public SocketSet(DoorwaySocket corridor3m, DoorwaySocket roomDoor, DoorwaySocket stair, DoorwaySocket fireExit)
            {
                Corridor3m = corridor3m;
                RoomDoor = roomDoor;
                Stair = stair;
                FireExit = fireExit;
            }

            public DoorwaySocket Corridor3m { get; }
            public DoorwaySocket RoomDoor { get; }
            public DoorwaySocket Stair { get; }
            public DoorwaySocket FireExit { get; }
        }

        private readonly struct MaterialSet
        {
            public MaterialSet(Material floor, Material ceiling, Material wallBeige, Material wallRed, Material metal, Material stairs)
            {
                Floor = floor;
                Ceiling = ceiling;
                WallBeige = wallBeige;
                WallRed = wallRed;
                Metal = metal;
                Stairs = stairs;
            }

            public Material Floor { get; }
            public Material Ceiling { get; }
            public Material WallBeige { get; }
            public Material WallRed { get; }
            public Material Metal { get; }
            public Material Stairs { get; }
        }

        private sealed class ModuleSpec
        {
            public ModuleSpec(
                string moduleId,
                string displayName,
                string prefabName,
                string sourcePrefabPath,
                TileCategory category,
                IReadOnlyList<string> tags,
                float selectionWeight,
                int maxUseCount,
                bool unique,
                AllowedYawRotations allowedYawRotations,
                Vector3 occupancyCenter,
                Vector3 occupancySize,
                IReadOnlyList<DoorwaySpec> doorways,
                IReadOnlyList<SpawnSpec> spawnMarkers,
                ConstructedShape shape = ConstructedShape.None)
            {
                ModuleId = moduleId;
                DisplayName = displayName;
                PrefabName = prefabName;
                SourcePrefabPath = sourcePrefabPath;
                Category = category;
                Tags = tags;
                SelectionWeight = selectionWeight;
                MaxUseCount = maxUseCount;
                Unique = unique;
                AllowedYawRotations = allowedYawRotations;
                OccupancyCenter = occupancyCenter;
                OccupancySize = occupancySize;
                Doorways = doorways;
                SpawnMarkers = spawnMarkers;
                Shape = shape;
            }

            public string ModuleId { get; }
            public string DisplayName { get; }
            public string PrefabName { get; }
            public string SourcePrefabPath { get; }
            public TileCategory Category { get; }
            public IReadOnlyList<string> Tags { get; }
            public float SelectionWeight { get; }
            public int MaxUseCount { get; }
            public bool Unique { get; }
            public AllowedYawRotations AllowedYawRotations { get; }
            public Vector3 OccupancyCenter { get; }
            public Vector3 OccupancySize { get; }
            public IReadOnlyList<DoorwaySpec> Doorways { get; }
            public IReadOnlyList<SpawnSpec> SpawnMarkers { get; }
            public ConstructedShape Shape { get; }
            public string SourceLabel => string.IsNullOrEmpty(SourcePrefabPath) ? "constructed from current dimensions/materials" : SourcePrefabPath;
            public ConnectorKind DefaultConnectorKind => Doorways.Count > 0 ? Doorways[0].Kind : ConnectorKind.Door;
        }

        private readonly struct DoorwaySpec
        {
            private DoorwaySpec(string connectorId, Vector3 localPosition, Vector3 forward, DoorwaySocket socket, ConnectorKind kind, GameObject connectorPrefab, GameObject blockerPrefab, int floorDelta)
            {
                ConnectorId = connectorId;
                LocalPosition = localPosition;
                Forward = forward;
                Socket = socket;
                Kind = kind;
                ConnectorPrefab = connectorPrefab;
                BlockerPrefab = blockerPrefab;
                FloorDelta = floorDelta;
            }

            public string ConnectorId { get; }
            public Vector3 LocalPosition { get; }
            public Vector3 Forward { get; }
            public DoorwaySocket Socket { get; }
            public ConnectorKind Kind { get; }
            public GameObject ConnectorPrefab { get; }
            public GameObject BlockerPrefab { get; }
            public int FloorDelta { get; }

            public static DoorwaySpec RoomDoor(string connectorId, Vector3 localPosition, Vector3 forward, DoorwaySocket socket, GameObject connectorPrefab, GameObject blockerPrefab)
            {
                return new DoorwaySpec(connectorId, localPosition, forward, socket, ConnectorKind.Door, connectorPrefab, blockerPrefab, 0);
            }

            public static DoorwaySpec Corridor(string connectorId, Vector3 localPosition, Vector3 forward, DoorwaySocket socket, GameObject connectorPrefab, GameObject blockerPrefab)
            {
                return new DoorwaySpec(connectorId, localPosition, forward, socket, ConnectorKind.CorridorJoin, connectorPrefab, blockerPrefab, 0);
            }

            public static DoorwaySpec FireExit(string connectorId, Vector3 localPosition, Vector3 forward, DoorwaySocket socket, GameObject connectorPrefab, GameObject blockerPrefab)
            {
                return new DoorwaySpec(connectorId, localPosition, forward, socket, ConnectorKind.FireExit, connectorPrefab, blockerPrefab, 0);
            }

            public static DoorwaySpec Stair(string connectorId, Vector3 localPosition, Vector3 forward, DoorwaySocket socket, GameObject connectorPrefab, GameObject blockerPrefab, int floorDelta)
            {
                return new DoorwaySpec(connectorId, localPosition, forward, socket, ConnectorKind.Stair, connectorPrefab, blockerPrefab, floorDelta);
            }
        }

        private readonly struct SpawnSpec
        {
            public SpawnSpec(string markerId, SpawnMarkerKind kind, Vector3 localPosition, Vector3 forward, params string[] tags)
            {
                MarkerId = markerId;
                Kind = kind;
                LocalPosition = localPosition;
                Forward = forward;
                Tags = tags;
            }

            public string MarkerId { get; }
            public SpawnMarkerKind Kind { get; }
            public Vector3 LocalPosition { get; }
            public Vector3 Forward { get; }
            public IReadOnlyList<string> Tags { get; }
        }

        private readonly struct ModuleBuildResult
        {
            public ModuleBuildResult(ModuleSpec spec, GameObject prefab, TileDefinition definition, List<TileAuthoringIssue> issues)
            {
                Spec = spec;
                Prefab = prefab;
                Definition = definition;
                Issues = issues;
            }

            public ModuleSpec Spec { get; }
            public GameObject Prefab { get; }
            public TileDefinition Definition { get; }
            public List<TileAuthoringIssue> Issues { get; }
        }

        private enum ConstructedShape
        {
            None,
            StraightCorridor,
            CornerCorridor,
            TJunction,
            CrossJunction,
            DeadEnd
        }
    }
}
