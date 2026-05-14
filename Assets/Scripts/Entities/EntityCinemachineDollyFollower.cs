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

    [Header("Suspicion Control")]
    [SerializeField] private bool stopWhenSuspicious = true;
    [SerializeField] private Animator animator;
    [SerializeField] private string suspicionParameter = "Suspicion";
    [SerializeField] private float fallbackSuspiciousThreshold = 0.01f;
    [SerializeField] private string[] suspiciousStateNames = { "Suspicious" };
    [SerializeField] private string[] aggroStateNames = { "Gnevna", "Aggro" };
    [SerializeField] private bool raiseSuspicionAtWaypoints = true;
    [SerializeField] private float waypointSuspicionValue = 1f;
    [SerializeField] private float waypointTriggerDistance = 0.5f;
    [SerializeField] private float waypointResetDistance = 1.25f;

    [Header("Facing")]
    [SerializeField] private bool rotateAlongMovement = true;
    [SerializeField] private bool lockRotationToY = true;
    [SerializeField] private float rotationLerpSpeed = 12f;
    [SerializeField] private Vector3 modelForwardEulerOffset = Vector3.zero;
    [SerializeField, Range(0.001f, 0.1f)] private float tangentSampleStep = 0.01f;
    [SerializeField] private bool forceInstantFacing = true;

    private CinemachineSplineCart splineCart;
    private float direction = 1f;
    private int suspicionParamHash;
    private int lastTriggeredWaypoint = -1;

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

        if (stopWhenSuspicious && IsInSuspiciousOrAggroAnimationState())
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
        TryRaiseSuspicionAtWaypoint();
    }

    private void LateUpdate()
    {
        if (!rotateAlongMovement || splineCart == null || patrolPath == null)
        {
            return;
        }

        // Let suspicious/aggro clips drive orientation when active.
        if (IsInSuspiciousOrAggroAnimationState())
        {
            return;
        }

        if (!TryGetPathDirection(out Vector3 pathDirection))
        {
            return;
        }

        if (lockRotationToY)
        {
            pathDirection.y = 0f;
        }

        if (pathDirection.sqrMagnitude < 0.000001f)
        {
            return;
        }

        Quaternion lookRotation = Quaternion.LookRotation(pathDirection.normalized, Vector3.up);
        Quaternion correctedRotation = lookRotation * Quaternion.Euler(modelForwardEulerOffset);
        if (forceInstantFacing || rotationLerpSpeed <= 0f)
        {
            transform.rotation = correctedRotation;
        }
        else
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                correctedRotation,
                Mathf.Clamp01(rotationLerpSpeed * Time.deltaTime));
        }
    }

    private void EnsureSetup()
    {
        splineCart ??= GetComponent<CinemachineSplineCart>();
        animator ??= GetComponent<Animator>();
        suspicionParamHash = Animator.StringToHash(suspicionParameter);
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

    private bool IsAtOrAboveSuspiciousThreshold()
    {
        if (animator == null || !HasSuspicionParameter())
        {
            return false;
        }

        float threshold = fallbackSuspiciousThreshold;
        if (SuspicionSettings.Instance != null)
        {
            threshold = SuspicionSettings.Instance.SuspiciousThreshold;
        }

        return animator.GetFloat(suspicionParamHash) >= threshold;
    }

    private bool IsInSuspiciousOrAggroAnimationState()
    {
        if (animator == null || !animator.isActiveAndEnabled)
        {
            return false;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (MatchesAnyStateName(current, aggroStateNames) || MatchesAnyStateName(current, suspiciousStateNames))
        {
            return true;
        }

        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
        if (animator.IsInTransition(0) &&
            (MatchesAnyStateName(next, aggroStateNames) || MatchesAnyStateName(next, suspiciousStateNames)))
        {
            return true;
        }

        return IsAtOrAboveSuspiciousThreshold();
    }

    private static bool MatchesAnyStateName(AnimatorStateInfo info, string[] names)
    {
        if (names == null || names.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (info.IsName(name) || info.shortNameHash == Animator.StringToHash(name))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasSuspicionParameter()
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == suspicionParameter && parameters[i].type == AnimatorControllerParameterType.Float)
            {
                return true;
            }
        }

        return false;
    }

    private void TryRaiseSuspicionAtWaypoint()
    {
        if (!raiseSuspicionAtWaypoints || animator == null || !HasSuspicionParameter() || patrolPath == null || patrolPath.Spline == null)
        {
            return;
        }

        int waypointCount = patrolPath.Spline.Count;
        if (waypointCount < 2)
        {
            return;
        }

        if (lastTriggeredWaypoint >= 0 && lastTriggeredWaypoint < waypointCount)
        {
            Vector3 lastWaypointWorld = patrolPath.transform.TransformPoint((Vector3)patrolPath.Spline[lastTriggeredWaypoint].Position);
            if (Vector3.Distance(transform.position, lastWaypointWorld) >= waypointResetDistance)
            {
                lastTriggeredWaypoint = -1;
            }
        }

        float triggerDistance = Mathf.Max(0.01f, waypointTriggerDistance);
        for (int i = 0; i < waypointCount; i++)
        {
            Vector3 waypointWorld = patrolPath.transform.TransformPoint((Vector3)patrolPath.Spline[i].Position);
            if (Vector3.Distance(transform.position, waypointWorld) > triggerDistance)
            {
                continue;
            }

            if (i == lastTriggeredWaypoint)
            {
                return;
            }

            float current = animator.GetFloat(suspicionParamHash);
            animator.SetFloat(suspicionParamHash, Mathf.Max(current, waypointSuspicionValue));
            lastTriggeredWaypoint = i;
            return;
        }
    }

    private bool TryGetPathDirection(out Vector3 directionWorld)
    {
        directionWorld = Vector3.zero;
        float current = splineCart.SplinePosition;
        float step = Mathf.Max(0.001f, tangentSampleStep);
        float sampleSign = direction >= 0f ? 1f : -1f;
        float next = current + (step * sampleSign);

        if (loopPath)
        {
            next = Mathf.Repeat(next, 1f);
        }
        else
        {
            next = Mathf.Clamp01(next);
            if (Mathf.Approximately(next, current))
            {
                next = Mathf.Clamp01(current - (step * sampleSign));
            }
        }

        Vector3 currentPos = patrolPath.EvaluatePosition(current);
        Vector3 nextPos = patrolPath.EvaluatePosition(next);
        directionWorld = nextPos - currentPos;
        return directionWorld.sqrMagnitude > 0.000001f;
    }
}
