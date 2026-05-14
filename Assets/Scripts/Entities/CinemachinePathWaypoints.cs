using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SplineContainer))]
public class CinemachinePathWaypoints : MonoBehaviour
{
    [SerializeField] private bool closedLoop = true;
    [SerializeField] private Vector3[] waypoints =
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(4f, 0f, 0f),
        new Vector3(4f, 0f, 4f),
        new Vector3(0f, 0f, 4f)
    };

    private SplineContainer splineContainer;

    private void Awake()
    {
        ApplyWaypoints();
    }

    private void OnValidate()
    {
        ApplyWaypoints();
    }

    public void ApplyWaypoints()
    {
        splineContainer ??= GetComponent<SplineContainer>();
        if (splineContainer == null || waypoints == null || waypoints.Length < 2)
        {
            return;
        }

        Spline spline = splineContainer.Spline ?? new Spline();
        spline.Clear();
        spline.Closed = closedLoop;

        for (int i = 0; i < waypoints.Length; i++)
        {
            float3 localPoint = waypoints[i];
            spline.Add(new BezierKnot(localPoint));
        }

        splineContainer.Spline = spline;
    }
}
