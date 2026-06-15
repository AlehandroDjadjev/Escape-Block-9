using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;
using System;

[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineSplineCart))]
public class EntityCinemachineDollyFollower : MonoBehaviour
{
    public static event Action<EntityCinemachineDollyFollower> PlayerCaught;

    private enum ThreatState
    {
        Calm = 0,
        Suspicious = 1,
        Aggro = 2
    }

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
    [SerializeField] private float fallbackAggroThreshold = 2.5f;
    [SerializeField] private string[] suspiciousStateNames = { "Suspicious" };
    [SerializeField] private string[] aggroStateNames = { "Gnevna", "Aggro" };
    [SerializeField] private bool raiseSuspicionAtWaypoints = true;
    [SerializeField] private float waypointSuspicionValue = 1f;
    [SerializeField] private float waypointTriggerDistance = 0.5f;
    [SerializeField] private float waypointResetDistance = 1.25f;

    [Header("Aggro Chase")]
    [SerializeField] private bool chaseDuringAggro = true;
    [SerializeField] private Transform chaseTarget;
    [SerializeField] private string chaseTargetTag = "Player";
    [SerializeField] private float chaseSpeed = 3.25f;
    [SerializeField] private float chaseStopDistance = 1.15f;
    [SerializeField] private bool faceChaseDirection = true;
    [SerializeField] private bool instantChaseFacing = true;
    [SerializeField] private float chaseRotationLerpSpeed = 16f;

    [Header("Facing")]
    [SerializeField] private bool rotateAlongMovement = true;
    [SerializeField] private bool lockRotationToY = true;
    [SerializeField] private float rotationLerpSpeed = 12f;
    [SerializeField] private Vector3 modelForwardEulerOffset = Vector3.zero;
    [SerializeField, Range(0.001f, 0.1f)] private float tangentSampleStep = 0.01f;
    [SerializeField] private bool forceInstantFacing = true;

    [Header("Suspicious Look")]
    [SerializeField] private bool useScriptedSuspiciousLook = true;
    [SerializeField] private float suspiciousLookAngle = 52f;
    [SerializeField] private float suspiciousLookSpeed = 1.8f;

    private CinemachineSplineCart splineCart;
    private float direction = 1f;
    private int suspicionParamHash;
    private int lastTriggeredWaypoint = -1;
    private ThreatState threatState = ThreatState.Calm;
    private ThreatState previousThreatState = ThreatState.Calm;
    private bool hasAggroChaseDirection;
    private Vector3 lastAggroChaseDirection;
    private Quaternion suspiciousBaseRotation;
    private bool hasTriggeredCatch;

    private void Awake()
    {
        EnsureSetup();
    }

    private void OnEnable()
    {
        EnsureSetup();
        EnsureChaseTarget();
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

        EnsureChaseTarget();

        ThreatState currentState = EvaluateThreatState();
        HandleThreatStateTransitions(currentState);
        threatState = currentState;
        OnThreatStateUpdated();

        if (currentState == ThreatState.Aggro && chaseDuringAggro)
        {
            RunAggroChase();
            return;
        }

        if (stopWhenSuspicious && currentState == ThreatState.Suspicious)
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
        if (threatState == ThreatState.Aggro && chaseDuringAggro && faceChaseDirection)
        {
            ApplyAggroFacingInLateUpdate();
            return;
        }

        if (threatState == ThreatState.Suspicious && useScriptedSuspiciousLook)
        {
            ApplySuspiciousFacingInLateUpdate();
            return;
        }

        if (!rotateAlongMovement || splineCart == null || patrolPath == null)
        {
            return;
        }

        // Let suspicious/aggro clips drive orientation when active.
        if (threatState != ThreatState.Calm)
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

    private void EnsureChaseTarget()
    {
        if (chaseTarget != null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(chaseTargetTag))
        {
            GameObject taggedTarget = GameObject.FindGameObjectWithTag(chaseTargetTag);
            if (taggedTarget != null)
            {
                chaseTarget = taggedTarget.transform;
                return;
            }
        }

        FirstPersonController playerController = FindAnyObjectByType<FirstPersonController>();
        if (playerController != null)
        {
            chaseTarget = playerController.transform;
        }
    }

    private ThreatState EvaluateThreatState()
    {
        if (animator == null)
        {
            return ThreatState.Calm;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (MatchesAnyStateName(current, aggroStateNames))
        {
            return ThreatState.Aggro;
        }

        if (MatchesAnyStateName(current, suspiciousStateNames))
        {
            return ThreatState.Suspicious;
        }

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (MatchesAnyStateName(next, aggroStateNames))
            {
                return ThreatState.Aggro;
            }

            if (MatchesAnyStateName(next, suspiciousStateNames))
            {
                return ThreatState.Suspicious;
            }
        }

        if (!HasSuspicionParameter())
        {
            return ThreatState.Calm;
        }

        float aggroThreshold = fallbackAggroThreshold;
        float suspiciousThreshold = fallbackSuspiciousThreshold;
        if (SuspicionSettings.Instance != null)
        {
            aggroThreshold = SuspicionSettings.Instance.AggroThreshold;
            suspiciousThreshold = SuspicionSettings.Instance.SuspiciousThreshold;
        }

        float suspicion = animator.GetFloat(suspicionParamHash);
        if (suspicion >= aggroThreshold)
        {
            return ThreatState.Aggro;
        }

        if (suspicion >= suspiciousThreshold)
        {
            return ThreatState.Suspicious;
        }

        return ThreatState.Calm;
    }

    private void HandleThreatStateTransitions(ThreatState currentState)
    {
        if (splineCart == null)
        {
            return;
        }

        if (threatState != ThreatState.Aggro && currentState == ThreatState.Aggro)
        {
            hasTriggeredCatch = false;
            splineCart.enabled = false;
            return;
        }

        if (threatState == ThreatState.Aggro && currentState != ThreatState.Aggro)
        {
            hasTriggeredCatch = false;
            if (!splineCart.enabled)
            {
                splineCart.enabled = true;
            }

            if (patrolPath != null)
            {
                splineCart.SplinePosition = FindNearestSplinePosition(64);
            }
        }
    }

    private void OnThreatStateUpdated()
    {
        if (threatState != previousThreatState)
        {
            if (threatState == ThreatState.Suspicious)
            {
                suspiciousBaseRotation = transform.rotation;
            }

            previousThreatState = threatState;
        }
    }

    private void RunAggroChase()
    {
        hasAggroChaseDirection = false;
        if (chaseTarget == null)
        {
            return;
        }

        Vector3 toTarget = chaseTarget.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance <= chaseStopDistance)
        {
            if (!hasTriggeredCatch)
            {
                hasTriggeredCatch = true;
                PlayerCaught?.Invoke(this);
            }

            return;
        }

        Vector3 moveDirection = toTarget / Mathf.Max(distance, 0.0001f);
        lastAggroChaseDirection = moveDirection;
        hasAggroChaseDirection = true;
        transform.position += moveDirection * chaseSpeed * Time.deltaTime;

        if (faceChaseDirection)
        {
            Quaternion chaseRotation = Quaternion.LookRotation(moveDirection, Vector3.up) * Quaternion.Euler(modelForwardEulerOffset);
            if (instantChaseFacing || chaseRotationLerpSpeed <= 0f)
            {
                transform.rotation = chaseRotation;
            }
            else
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    chaseRotation,
                    Mathf.Clamp01(chaseRotationLerpSpeed * Time.deltaTime));
            }
        }
    }

    private void ApplyAggroFacingInLateUpdate()
    {
        Vector3 facingDirection = Vector3.zero;
        if (chaseTarget != null)
        {
            facingDirection = chaseTarget.position - transform.position;
            facingDirection.y = 0f;
        }

        if (facingDirection.sqrMagnitude < 0.000001f && hasAggroChaseDirection)
        {
            facingDirection = lastAggroChaseDirection;
            facingDirection.y = 0f;
        }

        if (facingDirection.sqrMagnitude < 0.000001f)
        {
            return;
        }

        Quaternion chaseRotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up) * Quaternion.Euler(modelForwardEulerOffset);
        if (instantChaseFacing || chaseRotationLerpSpeed <= 0f)
        {
            transform.rotation = chaseRotation;
        }
        else
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                chaseRotation,
                Mathf.Clamp01(chaseRotationLerpSpeed * Time.deltaTime));
        }
    }

    private void ApplySuspiciousFacingInLateUpdate()
    {
        float yawOffset = Mathf.Sin(Time.time * suspiciousLookSpeed) * suspiciousLookAngle;
        Quaternion lookRotation = suspiciousBaseRotation * Quaternion.Euler(0f, yawOffset, 0f);
        transform.rotation = lookRotation;
    }

    private float FindNearestSplinePosition(int samples)
    {
        if (patrolPath == null || samples <= 1)
        {
            return splineCart != null ? splineCart.SplinePosition : 0f;
        }

        float bestT = 0f;
        float bestDistSq = float.MaxValue;
        Vector3 currentPos = transform.position;
        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 samplePos = patrolPath.EvaluatePosition(t);
            float d = (samplePos - currentPos).sqrMagnitude;
            if (d < bestDistSq)
            {
                bestDistSq = d;
                bestT = t;
            }
        }

        return bestT;
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
