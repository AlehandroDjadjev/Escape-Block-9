using System.Reflection;
using UnityEngine;

/// <summary>
/// Teacher AI — three-state behavior:
///   Wander       : pace the corridors, picking new target points.
///   Chase        : line-of-sight on the player, sprint at them.
///   Investigate  : lost sight, walk to where they were last seen, then give up.
/// Powers query CanSeePlayer to gate their activation.
/// </summary>
public class SimpleTeacherWander : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float wanderRadius = 6f;
    [SerializeField] private float wanderSpeed = 0.8f;
    [SerializeField] private float chaseSpeed = 2.0f;
    [SerializeField] private float investigateSpeed = 1.4f;
    [SerializeField] private float arrivalDistance = 0.6f;
    [SerializeField] private float pickNewTargetEverySeconds = 5f;
    [SerializeField] private float turnSpeed = 6f;
    [SerializeField] private float chaseTurnSpeed = 10f;

    [Header("Player Damage")]
    [SerializeField] private int contactDamage = 20;
    [SerializeField] private float contactDamageDistance = 1.15f;
    [SerializeField] private float contactDamageCooldown = 1.25f;

    // The teacher prefabs are authored with their visible face on the model's -Z side
    // (root prefab is rotated -180° in the editor). LookRotation points the GameObject's
    // +Z at the target, so we have to rotate an extra 180° around Y to bring the face
    // to the front. Set to 0 if you swap in a model whose face is already on +Z.
    [SerializeField] private float modelYawOffset = 180f;

    [Header("Vision")]
    [SerializeField] private float visionRange = 18f;
    [SerializeField] private float visionConeAngle = 270f; // total cone (135° to each side — only true blind spot is directly behind)
    [SerializeField] private float eyeHeight = 1.6f;
    [SerializeField] private float playerHeadOffset = 1.0f;

    [Header("Memory")]
    [Tooltip("Seconds the teacher stays at the last-seen spot before giving up and wandering.")]
    [SerializeField] private float lookAroundDuration = 4f;

    [Header("Ground Snap")]
    [SerializeField] private float standingOffset = 0.05f;
    [SerializeField] private LayerMask groundMask = ~0;

    private enum AIState { Wander, Chase, Investigate }
    private AIState state = AIState.Wander;

    private Transform player;
    private Vector3 startPosition;
    private Vector3 wanderTarget;
    private Vector3 lastKnownPlayerPosition;
    private float nextRefreshTime;
    private float giveUpInvestigatingAt;
    private float nextContactDamageTime;
    private float externalSpeedMultiplier = 1f;
    private Coroutine speedMultiplierRoutine;

    private Vector3 stuckCheckLastPos;
    private float stuckCheckLastTime;
    private const float StuckCheckInterval = 1.5f;
    private const float StuckMinMoveDist = 0.35f;

    /// <summary>True if the teacher currently has line-of-sight on the player.</summary>
    public bool CanSeePlayer { get; private set; }
    public string NetworkStateName => state.ToString();
    public Vector3 LastKnownPlayerPosition => lastKnownPlayerPosition;

    /// <summary>True while this teacher is actively chasing the player (used to
    /// switch the game music to the tense chase track).</summary>
    public bool IsChasing => state == AIState.Chase;

    private static readonly (float xMin, float xMax, float zMin, float zMax)[] Corridors =
    {
        (7.8f,   9.2f, 13.0f, 32.0f),
        (30.8f, 32.2f, 13.0f, 32.0f),
    };

    private static int spawnCounter;

    private Animator cachedAnimator;

    private void Awake()
    {
        wanderSpeed = 1.4f;
        chaseSpeed = 3.5f;
        investigateSpeed = 2.4f;
        visionConeAngle = 270f; // override any scene-instance value

        // Hard-disable the Animator. Its suspicious_turn clip drives the root's Y
        // rotation and produces "spinning head" behavior that fights our facing.
        // Disabling here as well as in the generator covers scene instances that
        // predate the latest prefab regeneration.
        cachedAnimator = GetComponent<Animator>();
        if (cachedAnimator != null) cachedAnimator.enabled = false;

        // Hide the white "vision ray" strips that fan out from each teacher.
        var visionRaysRoot = transform.Find("VisionRaysRoot");
        if (visionRaysRoot != null) visionRaysRoot.gameObject.SetActive(false);

        var dolly = GetComponent("EntityCinemachineDollyFollower") as Behaviour;
        if (dolly != null) dolly.enabled = false;
        var cart = GetComponent("CinemachineSplineCart") as Behaviour;
        if (cart != null) cart.enabled = false;

        foreach (var power in GetComponents<TeacherPower>())
        {
            FieldInfo f = typeof(TeacherPower).GetField(
                "requireAggro", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null) f.SetValue(power, false);
        }
    }

    private void Start()
    {
        TeleportInsideSchool();
        startPosition = transform.position;
        FindPlayer();
        PickNewWanderTarget();
        SnapToGround();
    }

    private void TeleportInsideSchool()
    {
        var building = GameObject.Find("Block9Building");
        if (building == null) return;
        Transform br = building.transform;

        int myIndex = spawnCounter++;
        float floorLocalY = (myIndex % 2 == 0) ? 0.20f : 3.70f;

        var zone = Corridors[Random.Range(0, Corridors.Length)];
        float lx = Random.Range(zone.xMin, zone.xMax);
        float lz = Random.Range(zone.zMin, zone.zMax);

        transform.position = br.TransformPoint(new Vector3(lx, floorLocalY, lz));
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    // Direction to face this frame, written by Update and applied in LateUpdate
    // (so the Animator's suspicious_turn clip — which animates the root's Y angle —
    // can't overwrite us after Update returns).
    private Vector3 desiredFaceDir;
    private bool hasDesiredFace;
    private bool isChasingFace; // true when locking onto the player (use chaseTurnSpeed)
    private bool networkControlled;

    public void SetNetworkControlled(bool value)
    {
        networkControlled = value;
    }

    public void ApplyNetworkState(Vector3 position, Vector3 eulerAngles, string aiState, bool canSeePlayer, Vector3 lastKnownPosition)
    {
        transform.position = position;
        transform.rotation = Quaternion.Euler(eulerAngles);
        CanSeePlayer = canSeePlayer;
        lastKnownPlayerPosition = lastKnownPosition;

        if (System.Enum.TryParse(aiState, out AIState parsedState))
        {
            state = parsedState;
        }

        if (player == null)
        {
            FindPlayer();
        }

        TryDealContactDamage();
    }

    private void Update()
    {
        if (player == null) FindPlayer();

        if (networkControlled)
        {
            TryDealContactDamage();
            return;
        }

        CanSeePlayer = HasLineOfSightOnPlayer();
        UpdateState();
        CheckIfStuck();

        hasDesiredFace = false;

        // When chasing, lock the facing onto the player every frame so they never
        // chase sideways or lag behind a strafing target.
        if (state == AIState.Chase && player != null)
        {
            Vector3 lookDir = player.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                desiredFaceDir = lookDir.normalized;
                hasDesiredFace = true;
                isChasingFace = true;
            }
        }

        Vector3 dir;
        float speed;
        if (!PickMoveTarget(out dir, out speed)) { SnapToGround(); return; }

        Vector3 step = dir * speed * Time.deltaTime;
        Vector3 allowed = SlideAlongWalls(step);
        transform.position += allowed;
        SnapToGround();

        // Outside chase, face the direction of motion as before.
        if (state != AIState.Chase && dir.sqrMagnitude > 0.0001f)
        {
            desiredFaceDir = dir;
            hasDesiredFace = true;
            isChasingFace = false;
        }

        TryDealContactDamage();
    }

    private void LateUpdate()
    {
        if (!hasDesiredFace) return;

        Quaternion target = Quaternion.LookRotation(desiredFaceDir, Vector3.up)
                            * Quaternion.Euler(0f, modelYawOffset, 0f);
        float speed = isChasingFace ? chaseTurnSpeed : turnSpeed;
        transform.rotation = Quaternion.Slerp(transform.rotation, target, speed * Time.deltaTime);
    }

    // If the teacher hasn't moved much in StuckCheckInterval seconds while trying to
    // wander/investigate, assume they're jammed on a wall and pick a fresh target.
    // Chase state is exempt — getting close to the player is what we want.
    private void CheckIfStuck()
    {
        if (state == AIState.Chase) return;
        if (Time.time - stuckCheckLastTime < StuckCheckInterval) return;

        float moved = Vector3.Distance(transform.position, stuckCheckLastPos);
        if (moved < StuckMinMoveDist)
        {
            if (state == AIState.Investigate)
            {
                // Give up investigating — they got stuck on the way.
                state = AIState.Wander;
            }
            PickNewWanderTarget();
        }
        stuckCheckLastPos = transform.position;
        stuckCheckLastTime = Time.time;
    }

    // Capsule-cast in the move direction. If a wall blocks, project the motion onto
    // the wall's surface so the teacher slides along it instead of phasing through.
    private Vector3 SlideAlongWalls(Vector3 step)
    {
        float magnitude = step.magnitude;
        if (magnitude < 0.0001f) return step;
        Vector3 moveDir = step / magnitude;

        const float bodyRadius = 0.35f;
        Vector3 capBot = transform.position + Vector3.up * (bodyRadius + 0.05f);
        Vector3 capTop = transform.position + Vector3.up * 1.5f;

        if (Physics.CapsuleCast(capBot, capTop, bodyRadius, moveDir,
                                out RaycastHit hit, magnitude + 0.05f,
                                ~0, QueryTriggerInteraction.Ignore))
        {
            // Ignore self / other teachers / the player.
            if (hit.collider.GetComponentInParent<SimpleTeacherWander>() != null) return step;
            if (hit.collider.CompareTag("Player")) return step;
            if (hit.collider.GetComponentInParent<FirstPersonController>() != null) return step;

            // Project remaining motion onto the wall surface so we slide instead of stop.
            float allowedDist = Mathf.Max(0f, hit.distance - 0.02f);
            Vector3 toContact = moveDir * allowedDist;
            Vector3 remainder = step - toContact;
            Vector3 slide = Vector3.ProjectOnPlane(remainder, hit.normal);
            return toContact + slide * 0.9f;
        }
        return step;
    }

    private void UpdateState()
    {
        if (CanSeePlayer)
        {
            state = AIState.Chase;
            lastKnownPlayerPosition = player.position;
            return;
        }

        if (state == AIState.Chase)
        {
            // Just lost sight — start investigating the last known spot.
            state = AIState.Investigate;
            giveUpInvestigatingAt = Time.time + lookAroundDuration;
            return;
        }

        if (state == AIState.Investigate)
        {
            float dToTarget = Vector3.Distance(transform.position, lastKnownPlayerPosition);
            bool arrived = dToTarget < arrivalDistance * 1.5f;
            bool timeUp = Time.time > giveUpInvestigatingAt;
            if (arrived || timeUp)
            {
                state = AIState.Wander;
                PickNewWanderTarget();
            }
        }
    }

    private bool PickMoveTarget(out Vector3 dir, out float speed)
    {
        Vector3 to;
        switch (state)
        {
            case AIState.Chase:
                to = player.position - transform.position;
                speed = chaseSpeed;
                break;
            case AIState.Investigate:
                to = lastKnownPlayerPosition - transform.position;
                speed = investigateSpeed;
                break;
            default:
                if (Time.time > nextRefreshTime ||
                    Vector3.Distance(transform.position, wanderTarget) < arrivalDistance)
                    PickNewWanderTarget();
                to = wanderTarget - transform.position;
                speed = wanderSpeed;
                break;
        }

        speed *= externalSpeedMultiplier;
        to.y = 0f;
        float d = to.magnitude;
        if (d < arrivalDistance) { dir = Vector3.zero; return false; }
        dir = to / d;
        return true;
    }

    public void ApplyTemporarySpeedMultiplier(float multiplier, float duration)
    {
        if (speedMultiplierRoutine != null)
        {
            StopCoroutine(speedMultiplierRoutine);
        }

        speedMultiplierRoutine = StartCoroutine(SpeedMultiplierRoutine(multiplier, duration));
    }

    private System.Collections.IEnumerator SpeedMultiplierRoutine(float multiplier, float duration)
    {
        externalSpeedMultiplier = Mathf.Max(0.1f, multiplier);
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        externalSpeedMultiplier = 1f;
        speedMultiplierRoutine = null;
    }

    private void TryDealContactDamage()
    {
        if (state != AIState.Chase || player == null || Time.time < nextContactDamageTime)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > contactDamageDistance)
        {
            return;
        }

        if (PlayerHealth.TryDamage(player, contactDamage, $"{name} chase contact", gameObject))
        {
            nextContactDamageTime = Time.time + contactDamageCooldown;
        }
    }

    private bool HasLineOfSightOnPlayer()
    {
        if (player == null) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 head = player.position + Vector3.up * playerHeadOffset;
        Vector3 toPlayer = head - eye;
        float dist = toPlayer.magnitude;
        if (dist > visionRange) return false;

        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 flatToPlayer = Vector3.ProjectOnPlane(toPlayer, Vector3.up).normalized;
        if (flatForward.sqrMagnitude > 0.001f && flatToPlayer.sqrMagnitude > 0.001f)
        {
            float angle = Vector3.Angle(flatForward, flatToPlayer);
            if (angle > visionConeAngle * 0.5f) return false;
        }

        Vector3 dir = toPlayer / dist;
        var hits = Physics.RaycastAll(eye, dir, dist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<SimpleTeacherWander>() != null) continue;
            // Anything sharing the player's transform hierarchy counts as the player.
            if (hit.transform == player || hit.transform.IsChildOf(player)) return true;
            if (hit.collider.CompareTag("Player")) return true;
            if (hit.collider.GetComponentInParent<FirstPersonController>() != null) return true;
            // First non-teacher, non-player hit — wall in the way.
            return false;
        }
        // Raycast hit nothing → no obstruction → we can see the player.
        return true;
    }

    private void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        var hits = Physics.RaycastAll(origin, Vector3.down, 1.5f, groundMask, QueryTriggerInteraction.Ignore);
        float bestY = float.NegativeInfinity;
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<SimpleTeacherWander>() != null) continue;
            if (hit.point.y > bestY) bestY = hit.point.y;
        }
        if (bestY > float.NegativeInfinity)
        {
            Vector3 p = transform.position;
            p.y = bestY + standingOffset;
            transform.position = p;
        }
    }

    private void FindPlayer()
    {
        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null) { player = tagged.transform; return; }
        FirstPersonController fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null) player = fpc.transform;
    }

    private void PickNewWanderTarget()
    {
        // Pick a target inside whichever wing corridor we're currently closest to.
        // Using building-local space guarantees the target is on a real walkable
        // corridor (no random points in walls or outside the building).
        var building = GameObject.Find("Block9Building");
        if (building == null)
        {
            Vector2 r = Random.insideUnitCircle * wanderRadius;
            wanderTarget = transform.position + new Vector3(r.x, 0f, r.y);
        }
        else
        {
            var br = building.transform;
            Vector3 localPos = br.InverseTransformPoint(transform.position);

            // Pick the wing closest in X (left-wing center ≈ 8.5, right-wing center ≈ 31.5).
            var zone = (Mathf.Abs(localPos.x - 8.5f) < Mathf.Abs(localPos.x - 31.5f))
                ? Corridors[0]
                : Corridors[1];

            float lx = Random.Range(zone.xMin, zone.xMax);
            float lz = Random.Range(zone.zMin, zone.zMax);

            // Keep the target on the current floor (don't try to walk through stairs).
            wanderTarget = br.TransformPoint(new Vector3(lx, localPos.y, lz));
        }
        nextRefreshTime = Time.time + pickNewTargetEverySeconds;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = CanSeePlayer ? Color.red : new Color(0.2f, 0.7f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, visionRange);
    }
}
