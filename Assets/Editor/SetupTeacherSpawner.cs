using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupTeacherSpawner
{
    private const string TriggerFile = "Assets/enteties/.setup_teacher_spawner";

    private static readonly string[] PrefabPaths =
    {
        "Assets/enteties/Teachers/Teacher_frenski.prefab",
        "Assets/enteties/Teachers/Teacher_tancheto.prefab",
        "Assets/enteties/Teachers/Teacher_ivazaharieva.prefab",
        "Assets/enteties/Teachers/Teacher_milaneikova.prefab",
        "Assets/enteties/Teachers/Teacher_bojkata.prefab",
        "Assets/enteties/Teachers/Teacher_ivanzaprqnov.prefab",
        "Assets/enteties/Teachers/Teacher_milenSpasov.prefab",
        "Assets/enteties/Teachers/Teacher_basheva.prefab",
        "Assets/enteties/Teachers/Teacher_direktorka.prefab",
        "Assets/enteties/Teachers/Teacher_hristov.prefab",
    };

    // Runs after every domain reload. If the trigger file exists, set up the spawner.
    [InitializeOnLoadMethod]
    private static void OnAfterDomainReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(TriggerFile)) return;
            File.Delete(TriggerFile);
            string meta = TriggerFile + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
            Run();
        };
    }

    [MenuItem("Tools/Setup Teacher Spawner")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[SetupTeacherSpawner] Exit Play Mode first, then run Tools/Setup Teacher Spawner.");
            return;
        }

        GameObject go = GameObject.Find("TeacherSpawner")
            ?? new GameObject("TeacherSpawner");

        TeacherSpawner spawner = go.GetComponent<TeacherSpawner>()
            ?? go.AddComponent<TeacherSpawner>();

        var so = new SerializedObject(spawner);

        var prefabsProp = so.FindProperty("teacherPrefabs");
        prefabsProp.arraySize = PrefabPaths.Length;
        for (int i = 0; i < PrefabPaths.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[i]);
            if (prefab == null)
                Debug.LogWarning($"[SetupTeacherSpawner] Prefab not found: {PrefabPaths[i]}");
            prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = prefab;
        }

        var buildingProp = so.FindProperty("buildingRoot");
        var building = GameObject.Find("Block9Building");
        if (building != null)
            buildingProp.objectReferenceValue = building.transform;
        else
            Debug.LogWarning("[SetupTeacherSpawner] Block9Building not found — will auto-find at runtime.");

        so.FindProperty("teachersOnFloor0").intValue = 5;
        so.FindProperty("teachersOnFloor1").intValue = 5;

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[SetupTeacherSpawner] Done! TeacherSpawner wired up with " +
                  PrefabPaths.Length + " teacher prefabs. Save the scene (Ctrl+S).");
    }
}
