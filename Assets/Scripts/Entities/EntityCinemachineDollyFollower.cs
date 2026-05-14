using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineSplineCart))]
public class EntityCinemachineDollyFollower : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] private SplineContainer patrolPath;
    [SerializeField] private string fallbackPathObjectName = "CinemachinePath";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.4f;
    [SerializeField] private bool loopPath = true;
    [SerializeField] private bool pingPongIfNotLooping = true;
    [SerializeField, Range(0f, 1f)] private float startNormalizedPosition;
    [SerializeField] private bool setStartOnEnable = true;

    private CinemachineSplineCart splineCart;
    private float direction = 1f;

    private void Awake()
    {
        EnsureSetup();
    }

    private void OnEnable()
    {
        EnsureSetup();
        if (setStartOnEnable && splineCart != null)
        {
            splineCart.SplinePosition = Mathf.Clamp01(startNormalizedPosition);
        }
    }

    private void OnValidate()
    {
        EnsureSetup();
    }

    private void Update()
    {
        if (splineCart == null || patrolPath == null)
        {
            return;
        }

        float length = patrolPath.CalculateLength();
        if (length <= 0.001f)
        {
            return;
        }

        float normalizedDelta = (moveSpeed / length) * Time.deltaTime * direction;
        float next = splineCart.SplinePosition + normalizedDelta;

        if (loopPath)
        {
            next = Mathf.Repeat(next, 1f);
        }
        else
        {
            if (next > 1f)
            {
                next = 1f;
                if (pingPongIfNotLooping)
                {
                    direction = -1f;
                }
            }
            else if (next < 0f)
            {
                next = 0f;
                if (pingPongIfNotLooping)
                {
                    direction = 1f;
                }
            }
        }

        splineCart.SplinePosition = next;
    }

    private void EnsureSetup()
    {
        splineCart ??= GetComponent<CinemachineSplineCart>();
        if (splineCart == null)
        {
            return;
        }

        if (patrolPath == null && !string.IsNullOrWhiteSpace(fallbackPathObjectName))
        {
            GameObject pathObject = GameObject.Find(fallbackPathObjectName);
            if (pathObject != null)
            {
                patrolPath = pathObject.GetComponent<SplineContainer>();
            }
        }

        if (patrolPath != null)
        {
            splineCart.Spline = patrolPath;
            splineCart.PositionUnits = PathIndexUnit.Normalized;
        }
    }
}
