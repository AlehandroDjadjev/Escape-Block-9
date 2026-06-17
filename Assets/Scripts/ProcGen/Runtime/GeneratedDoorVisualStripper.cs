using System.Collections.Generic;
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

                if (child.name == "Door" || child.name == "ExitDoor")
                {
                    removals.Add(child.gameObject);
                }
            }

            for (int i = 0; i < removals.Count; i++)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(removals[i]);
                }
                else
                {
                    Object.DestroyImmediate(removals[i]);
                }
            }
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
