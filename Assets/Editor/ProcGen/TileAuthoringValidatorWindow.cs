using System.Collections.Generic;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Validation;
using UnityEditor;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Editor
{
    public sealed class TileAuthoringValidatorWindow : EditorWindow
    {
        private readonly List<TileAuthoringIssue> issues = new List<TileAuthoringIssue>();
        private readonly List<Tile> tiles = new List<Tile>();
        private Vector2 scrollPosition;
        private bool includePrefabs = true;
        private bool includeSceneObjects = true;

        [MenuItem("Tools/ProcGen/Tile Authoring Validator")]
        public static void Open()
        {
            GetWindow<TileAuthoringValidatorWindow>("Tile Validator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Tile Authoring Validator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            includeSceneObjects = EditorGUILayout.ToggleLeft("Validate scene tiles", includeSceneObjects);
            includePrefabs = EditorGUILayout.ToggleLeft("Validate prefab assets", includePrefabs);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate All"))
                {
                    ValidateAll();
                }

                if (GUILayout.Button("Validate Selection"))
                {
                    ValidateSelection();
                }

                if (GUILayout.Button("Clear"))
                {
                    issues.Clear();
                    tiles.Clear();
                }
            }

            EditorGUILayout.Space();
            DrawSummary();
            EditorGUILayout.Space();
            DrawIssues();
        }

        private void ValidateAll()
        {
            issues.Clear();
            tiles.Clear();

            if (includeSceneObjects)
            {
                Tile[] sceneTiles = UnityEngine.Object.FindObjectsByType<Tile>(FindObjectsInactive.Include);
                for (int i = 0; i < sceneTiles.Length; i++)
                {
                    AddTile(sceneTiles[i]);
                }
            }

            if (includePrefabs)
            {
                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
                for (int i = 0; i < prefabGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                    {
                        continue;
                    }

                    Tile[] prefabTiles = prefab.GetComponentsInChildren<Tile>(true);
                    for (int j = 0; j < prefabTiles.Length; j++)
                    {
                        AddTile(prefabTiles[j]);
                    }
                }
            }
        }

        private void ValidateSelection()
        {
            issues.Clear();
            tiles.Clear();

            for (int i = 0; i < Selection.gameObjects.Length; i++)
            {
                GameObject selected = Selection.gameObjects[i];
                if (selected == null)
                {
                    continue;
                }

                Tile[] selectedTiles = selected.GetComponentsInChildren<Tile>(true);
                for (int j = 0; j < selectedTiles.Length; j++)
                {
                    AddTile(selectedTiles[j]);
                }
            }

            if (tiles.Count == 0)
            {
                issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Info, null, "Selection contains no Tile components."));
            }
        }

        private void AddTile(Tile tile)
        {
            if (tile == null || tiles.Contains(tile))
            {
                return;
            }

            tiles.Add(tile);
            TileAuthoringValidator.Validate(tile, issues);
        }

        private void DrawSummary()
        {
            int errors = 0;
            int warnings = 0;
            int info = 0;

            for (int i = 0; i < issues.Count; i++)
            {
                switch (issues[i].Severity)
                {
                    case TileAuthoringSeverity.Error:
                        errors++;
                        break;
                    case TileAuthoringSeverity.Warning:
                        warnings++;
                        break;
                    default:
                        info++;
                        break;
                }
            }

            EditorGUILayout.HelpBox($"Validated {tiles.Count} tile(s). Errors: {errors}, Warnings: {warnings}, Info: {info}", MessageType.None);
        }

        private void DrawIssues()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("No validation issues. Run validation to refresh results.", MessageType.Info);
            }

            for (int i = 0; i < issues.Count; i++)
            {
                TileAuthoringIssue issue = issues[i];
                MessageType messageType = ToMessageType(issue.Severity);
                EditorGUILayout.HelpBox(issue.Message, messageType);
                if (issue.Context != null)
                {
                    EditorGUILayout.ObjectField("Context", issue.Context, typeof(UnityEngine.Object), true);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static MessageType ToMessageType(TileAuthoringSeverity severity)
        {
            switch (severity)
            {
                case TileAuthoringSeverity.Error:
                    return MessageType.Error;
                case TileAuthoringSeverity.Warning:
                    return MessageType.Warning;
                default:
                    return MessageType.Info;
            }
        }
    }
}
