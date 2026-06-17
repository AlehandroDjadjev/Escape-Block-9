using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Validation;
using UnityEditor;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Editor
{
    public static class ModuleAuthoringHardeningMenu
    {
        private const string TilePrefabRoot = "Assets/ProcGen/TilePrefabs";
        private const string ConnectorPrefabPath = "Assets/ProcGen/Connectors/OpenFrameConnector.prefab";
        private const string BlockerPrefabPath = "Assets/ProcGen/Blockers/WallPanelBlocker.prefab";
        private const string DoorPrefabPath = "Assets/arhitektura/Door.prefab";
        private const string CorridorSocketPath = "Assets/ProcGen/Sockets/Socket_Corridor3m.asset";
        private const string RoomSocketPath = "Assets/ProcGen/Sockets/Socket_RoomDoor.asset";
        private const string FireExitSocketPath = "Assets/ProcGen/Sockets/Socket_FireExit.asset";
        private const string StairSocketPath = "Assets/ProcGen/Sockets/Socket_Stair.asset";
        private const string ReportPath = "Assets/Docs/ProcGen/ModuleAuthoringHardeningReport.md";

        [MenuItem("Tools/ProcGen/Harden Module Authoring (Auto-fix)")]
        public static void HardenAuthoring()
        {
            GameObject connectorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConnectorPrefabPath);
            GameObject blockerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockerPrefabPath);
            GameObject doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            DoorwaySocket corridorSocket = AssetDatabase.LoadAssetAtPath<DoorwaySocket>(CorridorSocketPath);
            DoorwaySocket roomSocket = AssetDatabase.LoadAssetAtPath<DoorwaySocket>(RoomSocketPath);
            DoorwaySocket fireExitSocket = AssetDatabase.LoadAssetAtPath<DoorwaySocket>(FireExitSocketPath);
            DoorwaySocket stairSocket = AssetDatabase.LoadAssetAtPath<DoorwaySocket>(StairSocketPath);

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { TilePrefabRoot });
            int prefabCount = 0;
            int doorwayFixes = 0;
            var beforeIssues = new List<TileAuthoringIssue>();
            var afterIssues = new List<TileAuthoringIssue>();
            var changes = new List<string>();

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                Tile tile = prefab.GetComponent<Tile>();
                if (tile == null)
                {
                    continue;
                }

                prefabCount++;
                TileAuthoringValidator.Validate(tile, beforeIssues);
                Doorway[] doorways = tile.GetDoorways();
                bool prefabChanged = false;
                for (int doorwayIndex = 0; doorwayIndex < doorways.Length; doorwayIndex++)
                {
                    Doorway doorway = doorways[doorwayIndex];
                    if (doorway == null)
                    {
                        continue;
                    }

                    var serializedDoorway = new SerializedObject(doorway);
                    bool changed = false;
                    changed |= EnsureConnectorId(serializedDoorway, tile.ModuleId, doorwayIndex);
                    changed |= EnsureSocket(serializedDoorway, tile, doorway, corridorSocket, roomSocket, fireExitSocket, stairSocket);
                    changed |= EnsureVisualReferences(serializedDoorway, doorway, connectorPrefab, blockerPrefab, doorPrefab);
                    if (!changed)
                    {
                        continue;
                    }

                    serializedDoorway.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(doorway);
                    prefabChanged = true;
                    doorwayFixes++;
                    changes.Add($"- `{path}` doorway `{doorway.name}` fixed defaults for socket/visual references.");
                }

                if (prefabChanged)
                {
                    EditorUtility.SetDirty(prefab);
                }

                TileAuthoringValidator.Validate(tile, afterIssues);
            }

            AssetDatabase.SaveAssets();
            WriteReport(prefabCount, doorwayFixes, beforeIssues, afterIssues, changes);
            AssetDatabase.Refresh();
            Debug.Log($"ProcGen module hardening complete. Prefabs={prefabCount} DoorwaysFixed={doorwayFixes}");
        }

        private static bool EnsureConnectorId(SerializedObject serializedDoorway, string moduleId, int doorwayIndex)
        {
            SerializedProperty connectorId = serializedDoorway.FindProperty("connectorId");
            if (connectorId == null || !string.IsNullOrWhiteSpace(connectorId.stringValue))
            {
                return false;
            }

            connectorId.stringValue = $"{moduleId}_dw_{doorwayIndex:00}";
            return true;
        }

        private static bool EnsureSocket(
            SerializedObject serializedDoorway,
            Tile tile,
            Doorway doorway,
            DoorwaySocket corridorSocket,
            DoorwaySocket roomSocket,
            DoorwaySocket fireExitSocket,
            DoorwaySocket stairSocket)
        {
            if (doorway.ConnectorKind == ConnectorKind.None || doorway.ConnectorKind == ConnectorKind.Sealed)
            {
                return false;
            }

            SerializedProperty socketProperty = serializedDoorway.FindProperty("socket");
            SerializedProperty socketNameProperty = serializedDoorway.FindProperty("socketName");
            bool missingSocket = socketProperty != null && socketProperty.objectReferenceValue == null &&
                                 (socketNameProperty == null || string.IsNullOrWhiteSpace(socketNameProperty.stringValue));
            if (!missingSocket)
            {
                return false;
            }

            DoorwaySocket targetSocket = corridorSocket;
            switch (doorway.ConnectorKind)
            {
                case ConnectorKind.FireExit:
                    targetSocket = fireExitSocket != null ? fireExitSocket : corridorSocket;
                    break;
                case ConnectorKind.Stair:
                    targetSocket = stairSocket != null ? stairSocket : corridorSocket;
                    break;
                default:
                    targetSocket = tile.Category == TileCategory.Room || tile.Category == TileCategory.Special
                        ? (roomSocket != null ? roomSocket : corridorSocket)
                        : corridorSocket;
                    break;
            }

            if (socketProperty != null && targetSocket != null)
            {
                socketProperty.objectReferenceValue = targetSocket;
            }

            if (socketNameProperty != null && targetSocket != null)
            {
                socketNameProperty.stringValue = targetSocket.SocketName;
            }

            return true;
        }

        private static bool EnsureVisualReferences(
            SerializedObject serializedDoorway,
            Doorway doorway,
            GameObject connectorPrefab,
            GameObject blockerPrefab,
            GameObject doorPrefab)
        {
            bool changed = false;
            SerializedProperty connectorPrefabProperty = serializedDoorway.FindProperty("connectorPrefab");
            SerializedProperty blockerPrefabProperty = serializedDoorway.FindProperty("blockerPrefab");

            if (doorway.ConnectorKind != ConnectorKind.None &&
                doorway.ConnectorKind != ConnectorKind.Sealed &&
                !doorway.HasConnectorReference &&
                connectorPrefabProperty != null)
            {
                connectorPrefabProperty.objectReferenceValue = doorway.ConnectorKind == ConnectorKind.Door && doorPrefab != null
                    ? doorPrefab
                    : connectorPrefab;
                changed = connectorPrefabProperty.objectReferenceValue != null;
            }

            if (!doorway.HasBlockerReference && blockerPrefabProperty != null && blockerPrefab != null)
            {
                blockerPrefabProperty.objectReferenceValue = blockerPrefab;
                changed = true;
            }

            return changed;
        }

        private static void WriteReport(
            int prefabCount,
            int doorwayFixes,
            IReadOnlyList<TileAuthoringIssue> beforeIssues,
            IReadOnlyList<TileAuthoringIssue> afterIssues,
            IReadOnlyList<string> changes)
        {
            int beforeErrors = CountIssues(beforeIssues, TileAuthoringSeverity.Error);
            int beforeWarnings = CountIssues(beforeIssues, TileAuthoringSeverity.Warning);
            int afterErrors = CountIssues(afterIssues, TileAuthoringSeverity.Error);
            int afterWarnings = CountIssues(afterIssues, TileAuthoringSeverity.Warning);

            var builder = new StringBuilder();
            builder.AppendLine("# Module Authoring Hardening Report");
            builder.AppendLine();
            builder.AppendLine($"Generated: {DateTime.UtcNow:O} UTC");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine($"- Tile prefabs scanned: `{prefabCount}`");
            builder.AppendLine($"- Doorway auto-fixes applied: `{doorwayFixes}`");
            builder.AppendLine($"- Issues before: errors `{beforeErrors}`, warnings `{beforeWarnings}`");
            builder.AppendLine($"- Issues after: errors `{afterErrors}`, warnings `{afterWarnings}`");
            builder.AppendLine();
            builder.AppendLine("## Applied Changes");
            builder.AppendLine();
            if (changes.Count == 0)
            {
                builder.AppendLine("- No changes were required.");
            }
            else
            {
                for (int i = 0; i < changes.Count; i++)
                {
                    builder.AppendLine(changes[i]);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Assets/Docs/ProcGen");
            File.WriteAllText(ReportPath, builder.ToString());
            Debug.Log($"Wrote module hardening report to {ReportPath}");
        }

        private static int CountIssues(IReadOnlyList<TileAuthoringIssue> issues, TileAuthoringSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == severity)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
