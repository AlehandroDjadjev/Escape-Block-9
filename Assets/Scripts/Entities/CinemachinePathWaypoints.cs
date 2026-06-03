using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SplineContainer))]
public class CinemachinePathWaypoints : MonoBehaviour
{
    [Header("Authoring")]
    [SerializeField] private bool overwriteSplineFromWaypoints = false;
    [SerializeField] private bool autoApplyInEditMode = false;
    [SerializeField] private bool closedLoop = true;
    [Header("Debug View")]
    [SerializeField] private bool drawDebugPath = true;
    [SerializeField] private Color debugPathColor = new Color(0.2f, 1f, 0.9f, 0.95f);
    [SerializeField] private float debugPointRadius = 0.12f;
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
        if (overwriteSplineFromWaypoints)
        {
            ApplyWaypoints();
        }
    }

    private void OnValidate()
    {
        if (overwriteSplineFromWaypoints && autoApplyInEditMode)
        {
            ApplyWaypoints();
        }
    }

    [ContextMenu("Apply Waypoints To Spline")]
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

    [ContextMenu("Capture Waypoints From Spline")]
    public void CaptureWaypointsFromSpline()
    {
        splineContainer ??= GetComponent<SplineContainer>();
        if (splineContainer == null || splineContainer.Spline == null || splineContainer.Spline.Count < 2)
        {
            return;
        }

        Spline spline = splineContainer.Spline;
        waypoints = new Vector3[spline.Count];
        for (int i = 0; i < spline.Count; i++)
        {
            waypoints[i] = spline[i].Position;
        }

        closedLoop = spline.Closed;
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugPath)
        {
            return;
        }

        Vector3[] points = GetDebugPoints();
        if (points == null || points.Length < 2)
        {
            return;
        }

        Gizmos.color = debugPathColor;
        Vector3 previousWorld = transform.TransformPoint(points[0]);
        Gizmos.DrawSphere(previousWorld, debugPointRadius);

        for (int i = 1; i < points.Length; i++)
        {
            Vector3 currentWorld = transform.TransformPoint(points[i]);
            Gizmos.DrawLine(previousWorld, currentWorld);
            Gizmos.DrawSphere(currentWorld, debugPointRadius);
            previousWorld = currentWorld;
        }

        if (closedLoop)
        {
            Vector3 firstWorld = transform.TransformPoint(points[0]);
            Gizmos.DrawLine(previousWorld, firstWorld);
        }
    }

    private Vector3[] GetDebugPoints()
    {
        splineContainer ??= GetComponent<SplineContainer>();
        if (splineContainer != null && splineContainer.Spline != null && splineContainer.Spline.Count >= 2)
        {
            Vector3[] splinePoints = new Vector3[splineContainer.Spline.Count];
            for (int i = 0; i < splineContainer.Spline.Count; i++)
            {
                splinePoints[i] = splineContainer.Spline[i].Position;
            }

            closedLoop = splineContainer.Spline.Closed;
            return splinePoints;
        }

        return waypoints;
    }
}
