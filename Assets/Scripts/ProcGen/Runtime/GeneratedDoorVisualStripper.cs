using System;
using System.Collections.Generic;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Runtime
{
    public static class GeneratedDoorVisualStripper
    {
        public static void RemoveDoorVisuals(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Transform[] children = instance.GetComponentsInChildren<Transform>(true);
            var removals = new List<GameObject>();

            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child == instance.transform || IsUnderProcGenAuthoring(child))
                {
                    continue;
                }

                if (child.name == "Door" || (child.name == "ExitDoor" && !ShouldKeepFunctionalExitDoor(instance)))
                {
                    removals.Add(child.gameObject);
                }
            }

            for (int i = 0; i < removals.Count; i++)
            {
                if (ShouldDestroyAtRuntime(removals[i]))
                {
                    UnityEngine.Object.Destroy(removals[i]);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(removals[i]);
                }
            }
        }

        private static bool ShouldKeepFunctionalExitDoor(GameObject instance)
        {
            Tile tile = instance != null ? instance.GetComponent<Tile>() : null;
            if (tile == null)
            {
                return false;
            }

            if (tile.Category == TileCategory.Exit ||
                ContainsIgnoreCase(tile.ModuleId, "fire_exit") ||
                ContainsIgnoreCase(tile.ModuleId, "exit_lobby"))
            {
                return true;
            }

            string[] tags = tile.Tags;
            if (tags == null)
            {
                return false;
            }

            for (int i = 0; i < tags.Length; i++)
            {
                if (ContainsIgnoreCase(tags[i], "fire-exit") ||
                    ContainsIgnoreCase(tags[i], "exit"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsIgnoreCase(string value, string fragment)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !string.IsNullOrWhiteSpace(fragment) &&
                   value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ShouldDestroyAtRuntime(GameObject target)
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(target))
            {
                return false;
            }
#endif
            return Application.isPlaying;
        }

        private static bool IsUnderProcGenAuthoring(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                if (current.name == "_ProcGenAuthoring" || current.name == "Doorways")
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
