using UnityEngine;

public class TeacherSpawner : MonoBehaviour
{
    [Header("Teacher Prefabs")]
    [SerializeField] private GameObject[] teacherPrefabs;

    [Header("Building")]
    [Tooltip("Drag in Block9Building. If left empty, auto-finds by name at runtime.")]
    [SerializeField] private Transform buildingRoot;

    [Header("Spawn Settings")]
    [SerializeField] private int teachersOnFloor0 = 5;
    [SerializeField] private int teachersOnFloor1 = 5;

    // Corridor rectangles in building-local XZ space (mirrors BuildingGenerator constants).
    // 1 m inset from walls keeps teachers away from geometry edges.
    private static readonly (float xMin, float xMax, float zMin, float zMax)[] Corridors =
    {
        (1.5f,  38.5f,  7.8f,  9.2f),  // connector corridor (top bar)
        (7.8f,   9.2f, 11.5f, 32.5f),  // left-wing N-S corridor
        (30.8f, 32.2f, 11.5f, 32.5f),  // right-wing N-S corridor
    };

    // Building-local Y where the downward raycast STARTS for each floor.
    // Floor 0 surface is at building-local y ≈ 0  →  start 1.5 m above it.
    // Floor 1 surface is at building-local y ≈ 3.65 →  start at 5.0 m (above surface, below ceiling ≈ 7.15).
    private const float Floor0RayStartLocalY = 1.5f;
    private const float Floor1RayStartLocalY = 5.0f;

    // Runs BEFORE Start so we beat any other Start that might move teachers.
    private void Awake()
    {
        if (buildingRoot == null)
        {
            var go = GameObject.Find("Block9Building");
            if (go != null) buildingRoot = go.transform;
        }

        if (buildingRoot == null)
        {
            Debug.LogWarning("[TeacherSpawner] Block9Building not found in scene.");
            return;
        }

        RelocateExistingTeachers();
    }

    // Finds every teacher already placed in the scene, kills their dolly-follower spline,
    // and teleports each one onto a random corridor floor position inside the school.
    private void RelocateExistingTeachers()
    {
        // Find all SimpleTeacherWander components — these are our teachers.
        var allTeachers = FindObjectsByType<SimpleTeacherWander>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (allTeachers.Length == 0)
        {
            Debug.LogWarning("[TeacherSpawner] No teachers found in scene.");
            return;
        }

        // Alternate floors so teachers are split between ground and upstairs.
        int placed = 0;
        for (int i = 0; i < allTeachers.Length; i++)
        {
            float rayStartLocalY = (i % 2 == 0) ? Floor0RayStartLocalY : Floor1RayStartLocalY;
            if (TryRelocate(allTeachers[i].gameObject, rayStartLocalY))
                placed++;
        }

        Debug.Log($"[TeacherSpawner] Relocated {placed}/{allTeachers.Length} teachers onto corridor floors.");
    }

    private bool TryRelocate(GameObject teacher, float rayStartLocalY)
    {
        // Kill any spline-follower path that would yank this teacher to Y=120.
        var dolly = teacher.GetComponent("EntityCinemachineDollyFollower") as Behaviour;
        if (dolly != null) dolly.enabled = false;
        var cart = teacher.GetComponent("CinemachineSplineCart") as Behaviour;
        if (cart != null) cart.enabled = false;

        // Try several random corridor positions until one has solid floor below.
        for (int attempt = 0; attempt < 25; attempt++)
        {
            var zone = Corridors[Random.Range(0, Corridors.Length)];
            float lx = Random.Range(zone.xMin, zone.xMax);
            float lz = Random.Range(zone.zMin, zone.zMax);

            Vector3 rayOrigin = buildingRoot.TransformPoint(new Vector3(lx, rayStartLocalY, lz));

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 6f))
            {
                teacher.transform.position = hit.point + Vector3.up * 0.05f;
                teacher.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                return true;
            }
        }

        // Fallback: drop straight down from a fixed corridor center point above the building.
        Vector3 fallbackOrigin = buildingRoot.TransformPoint(new Vector3(20f, rayStartLocalY, 8.5f));
        if (Physics.Raycast(fallbackOrigin, Vector3.down, out RaycastHit fallbackHit, 6f))
        {
            teacher.transform.position = fallbackHit.point + Vector3.up * 0.05f;
            return true;
        }
        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (buildingRoot == null) return;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
        foreach (var (xMin, xMax, zMin, zMax) in Corridors)
        {
            float cx = (xMin + xMax) * 0.5f;
            float cz = (zMin + zMax) * 0.5f;
            float wx = xMax - xMin;
            float wz = zMax - zMin;
            Gizmos.matrix = buildingRoot.localToWorldMatrix;
            Gizmos.DrawCube(new Vector3(cx, Floor0RayStartLocalY, cz), new Vector3(wx, 0.1f, wz));
            Gizmos.DrawCube(new Vector3(cx, Floor1RayStartLocalY, cz), new Vector3(wx, 0.1f, wz));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
#endif
}
