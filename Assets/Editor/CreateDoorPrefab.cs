using UnityEditor;
using UnityEngine;

public static class CreateDoorPrefab
{
    [MenuItem("Tools/Create Door Prefab")]
    public static void Create()
    {
        // Pivot root (place this at the hinge edge)
        GameObject root = new GameObject("Door");

        // Visual cube scaled to door proportions, offset so pivot is at hinge edge
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "DoorMesh";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = new Vector3(0.1f, 2.2f, 1f);
        visual.transform.localPosition = new Vector3(0f, 1.1f, 0.5f);

        root.AddComponent<DoorController>();

        string path = "Assets/arhitektura/Door.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        AssetDatabase.Refresh();
        Debug.Log("Door prefab saved to " + path);
    }
}
