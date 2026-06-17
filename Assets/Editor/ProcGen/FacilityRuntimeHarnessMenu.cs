using EscapeBlock9.ProcGen.Runtime;
using UnityEditor;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Editor
{
    public static class FacilityRuntimeHarnessMenu
    {
        [MenuItem("Tools/ProcGen/Setup Runtime Generation Harness")]
        public static void SetupRuntimeGenerationHarness()
        {
            FacilityRuntimeGenerator generator = Object.FindAnyObjectByType<FacilityRuntimeGenerator>();
            if (generator == null)
            {
                var generatorObject = new GameObject("FacilityRuntimeGenerator");
                generator = generatorObject.AddComponent<FacilityRuntimeGenerator>();
                Undo.RegisterCreatedObjectUndo(generatorObject, "Create Runtime Generator");
            }

            FirstPersonController player = Object.FindAnyObjectByType<FirstPersonController>();
            if (player == null)
            {
                const string playerPrefabPath = "Assets/enteties/Player.prefab";
                GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
                if (playerPrefab != null)
                {
                    GameObject playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                    Undo.RegisterCreatedObjectUndo(playerInstance, "Create Player");
                }
                else
                {
                    Debug.LogWarning($"Could not find player prefab at {playerPrefabPath}.");
                }
            }

            Selection.activeObject = generator.gameObject;
            Debug.Log("Runtime generation harness is ready. Press Play to generate a facility at startup.");
        }
    }
}
