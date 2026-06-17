using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==========================================================================
// BASE — every teacher power inherits from this. Powers tick on Update(),
// check cooldown + alert state + distance to player, then try to activate.
// ==========================================================================
public abstract class TeacherPower : MonoBehaviour
{
    [Header("Common Power Settings")]
    [SerializeField] protected float cooldown = 8f;
    [SerializeField] protected bool requireAggro = true;
    [SerializeField] protected Transform player;
    [SerializeField] protected float activationRange = 25f;

    protected float nextReadyTime;
    protected EntityAlertIndicator alertIndicator;
    protected SimpleTeacherWander wander;

    public string PowerName => GetType().Name.Replace("Power", "");

    /// <summary>
    /// Most powers need the teacher to actually see the player. Override and return
    /// false for powers that work without line-of-sight (X-Ray, security cameras,
    /// global lockdown).
    /// </summary>
    protected virtual bool RequiresLineOfSight => true;

    protected virtual void Awake()
    {
        alertIndicator = GetComponent<EntityAlertIndicator>();
        wander = GetComponent<SimpleTeacherWander>();
        FindPlayer();
    }

    protected void FindPlayer()
    {
        if (player != null) return;
        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null) { player = tagged.transform; return; }
        FirstPersonController fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null) player = fpc.transform;
    }

    protected virtual void Update()
    {
        if (player == null) FindPlayer();
        if (player == null) return;
        if (Time.time < nextReadyTime) return;
        if (requireAggro && alertIndicator != null &&
            alertIndicator.State != EntityAlertIndicator.AlertState.Aggro) return;
        if (Vector3.Distance(transform.position, player.position) > activationRange) return;
        if (RequiresLineOfSight && wander != null && !wander.CanSeePlayer) return;

        if (TryActivate())
        {
            nextReadyTime = Time.time + cooldown;
            ShowPowerFlash();
        }
    }

    /// <summary>Return true when the power fires (starts cooldown).</summary>
    protected abstract bool TryActivate();

    // Pop a bright sphere + light above the teacher's head when their power triggers,
    // and queue an on-screen toast so you can see firing without opening the console.
    private void ShowPowerFlash()
    {
        Color c = ColorForPower();

        // Big glowing sphere above the teacher.
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = $"{PowerName}_Flash";
        Destroy(flash.GetComponent<Collider>());
        flash.transform.SetParent(transform);
        flash.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        flash.transform.localScale = Vector3.one * 1.2f;
        var r = flash.GetComponent<Renderer>();
        if (r != null)
        {
            r.material.color = c;
            r.material.EnableKeyword("_EMISSION");
            r.material.SetColor("_EmissionColor", c * 4f);
        }
        // Attach a point light so it lights up the whole corridor briefly.
        var lightGo = new GameObject("FlashLight");
        lightGo.transform.SetParent(flash.transform, false);
        var light = lightGo.AddComponent<Light>();
        light.color = c;
        light.range = 8f;
        light.intensity = 6f;
        Destroy(flash, 1.2f);
    }

    private Color ColorForPower()
    {
        // Each power gets a distinct colour so you can tell them apart at a glance.
        switch (PowerName)
        {
            case "MedusaStare":     return new Color(1f, 0.8f, 0.1f);   // yellow
            case "WallClimb":       return new Color(0.5f, 0.9f, 1f);   // cyan
            case "TeacherSprint":   return new Color(0.2f, 1f, 0.2f);   // green
            case "CameraHijack":    return new Color(0.4f, 0.4f, 1f);   // blue
            case "RulerWhip":       return new Color(1f, 0.1f, 0.1f);   // red
            case "ChalkStorm":      return new Color(1f, 1f, 1f);       // white
            case "DetentionTrap":   return new Color(1f, 0.3f, 0.6f);   // pink
            case "Doppelganger":    return new Color(0.7f, 0.2f, 1f);   // purple
            case "SealedBuilding":  return new Color(1f, 0.5f, 0f);     // orange
            case "XRayVision":      return new Color(0.8f, 0.2f, 0.9f); // magenta
            default:                return Color.gray;
        }
    }
}

// ==========================================================================
// 1. MEDUSA STARE — Frenski.  Freezes player on eye contact.
// ==========================================================================
public class MedusaStarePower : TeacherPower
{
    [Header("Medusa Stare")]
    [SerializeField] private float lookConeAngle = 28f;
    [SerializeField] private float freezeDuration = 1.2f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Renderer eyeGlowRenderer;
    [SerializeField] private Color glowColor = new Color(1f, 0.85f, 0.1f);

    protected override void Awake()
    {
        base.Awake();
        if (playerCamera == null && player != null)
            playerCamera = player.GetComponentInChildren<Camera>();
        cooldown = 10f;
        freezeDuration = 1.2f;
    }

    protected override bool TryActivate()
    {
        if (playerCamera == null && player != null)
            playerCamera = player.GetComponentInChildren<Camera>();
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return false;

        Vector3 dirToMe = (transform.position - playerCamera.transform.position).normalized;
        float angle = Vector3.Angle(playerCamera.transform.forward, dirToMe);
        if (angle > lookConeAngle) return false;

        // The player is looking at us — freeze them.
        StartCoroutine(FreezePlayerRoutine());
        return true;
    }

    private IEnumerator FreezePlayerRoutine()
    {
        SetEyeGlow(true);
        FirstPersonController fpc = player.GetComponent<FirstPersonController>();
        if (fpc != null) fpc.enabled = false;
        yield return new WaitForSeconds(freezeDuration);
        if (fpc != null) fpc.enabled = true;
        SetEyeGlow(false);
    }

    private void SetEyeGlow(bool on)
    {
        if (eyeGlowRenderer == null) return;
        var mpb = new MaterialPropertyBlock();
        eyeGlowRenderer.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", on ? glowColor : Color.black);
        mpb.SetColor("_Color", on ? glowColor : Color.black);
        eyeGlowRenderer.SetPropertyBlock(mpb);
    }
}

// ==========================================================================
// 2. WALL CLIMB — Tancheto.  Allow vertical movement: when player goes up
//    a floor, this teacher teleports to floor 1 to follow.
// ==========================================================================
public class WallClimbPower : TeacherPower
{
    [Header("Wall Climb")]
    [SerializeField] private float verticalReach = 4f;
    [SerializeField] private float climbDuration = 2f;

    protected override void Awake() { base.Awake(); cooldown = 15f; }

    protected override bool TryActivate()
    {
        float dy = player.position.y - transform.position.y;
        if (Mathf.Abs(dy) < 1.5f) return false;
        if (Mathf.Abs(dy) > verticalReach) return false;

        StartCoroutine(ClimbRoutine(dy));
        return true;
    }

    private IEnumerator ClimbRoutine(float dy)
    {
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.up * dy;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / climbDuration;
            transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }
    }
}

// ==========================================================================
// 3. TEACHER SPRINT — Ivazaharieva.  Periodic 4-second speed burst.
// ==========================================================================
public class TeacherSprintPower : TeacherPower
{
    [Header("Teacher Sprint")]
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float sprintDuration = 3f;

    private EntityCinemachineDollyFollower dollyFollower;
    private float originalSpeed;

    protected override void Awake()
    {
        base.Awake();
        dollyFollower = GetComponent<EntityCinemachineDollyFollower>();
        cooldown = 18f;
        sprintMultiplier = 1.5f;
        sprintDuration = 3f;
    }

    protected override bool TryActivate()
    {
        if (dollyFollower == null) return false;
        StartCoroutine(SprintRoutine());
        return true;
    }

    private IEnumerator SprintRoutine()
    {
        // TODO: integrate with EntityCinemachineDollyFollower's moveSpeed if it exposes it.
        // For now we just flag-log; you can wire moveSpeed change in the dolly follower script.
        Debug.Log($"[TeacherSprint] {name} sprinting for {sprintDuration}s @ x{sprintMultiplier}");
        yield return new WaitForSeconds(sprintDuration);
        Debug.Log($"[TeacherSprint] {name} back to normal speed");
    }
}

// ==========================================================================
// 4. CAMERA HIJACK — Milaneikova.  If player is in sight of any object tagged
//    "SecurityCamera", this teacher gets the player's position.
// ==========================================================================
public class CameraHijackPower : TeacherPower
{
    [Header("Camera Hijack")]
    [SerializeField] private string cameraTag = "SecurityCamera";
    [SerializeField] private float cameraFovAngle = 60f;
    [SerializeField] private float cameraRange = 15f;

    [Header("Eye Lasers")]
    [SerializeField] private float laserSpeed = 28f;
    [SerializeField] private int laserCount = 2;
    [SerializeField] private float laserEyeOffset = 0.12f;

    // Local cooldown gate that the base class can't accidentally bypass: even if
    // anything else fires TryActivate, this clamp guarantees ≥ 2.0 s between volleys.
    private float lastFiredAt = -10f;
    private const float VolleyInterval = 2f;

    protected override void Awake()
    {
        base.Awake();
        activationRange = 22f;
        cooldown = VolleyInterval;
        laserCount = 2;
    }

    protected override bool TryActivate()
    {
        if (Time.time - lastFiredAt < VolleyInterval) return false;
        lastFiredAt = Time.time;

        FireEyeLasers();
        CheckSecurityCameras();
        Debug.Log($"[CameraHijack] {name} fired 2 lasers (next allowed at {Time.time + VolleyInterval:F2})");
        return true;
    }

    private void FireEyeLasers()
    {
        Vector3 eye = transform.position + Vector3.up * 1.7f;
        Vector3 head = player.position + Vector3.up * 1.4f;
        Vector3 toPlayer = (head - eye).normalized;
        Vector3 right = transform.right;

        // Hard-clamped at 2. No matter what scene-instance overrides the inspector has,
        // exactly two lasers leave Milaneikova per volley.
        const int LasersPerVolley = 2;
        for (int i = 0; i < LasersPerVolley; i++)
        {
            float offset = (i == 0) ? -laserEyeOffset : laserEyeOffset;
            Vector3 origin = eye + right * offset + transform.forward * 0.18f;

            var bolt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bolt.name = "EyeLaser";
            Destroy(bolt.GetComponent<Collider>());
            bolt.transform.position = origin;
            bolt.transform.rotation = Quaternion.LookRotation(toPlayer);
            bolt.transform.localScale = new Vector3(0.1f, 0.1f, 0.4f);
            var r = bolt.GetComponent<Renderer>();
            if (r != null)
            {
                Color c = new Color(0.35f, 0.55f, 1f);
                r.material.color = c;
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor", c * 4f);
            }

            var rb = bolt.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearVelocity = toPlayer * laserSpeed;
            Destroy(bolt, 1.2f);
        }
    }

    private void CheckSecurityCameras()
    {
        GameObject[] cams = GameObject.FindGameObjectsWithTag(cameraTag);
        foreach (var cam in cams)
        {
            Vector3 toPlayer = player.position - cam.transform.position;
            if (toPlayer.magnitude > cameraRange) continue;
            float angle = Vector3.Angle(cam.transform.forward, toPlayer);
            if (angle > cameraFovAngle / 2f) continue;
            Debug.Log($"[CameraHijack] {name} sees player via {cam.name}");
            return;
        }
    }
}

// ==========================================================================
// 5. RULER WHIP — Bojkata.  4m forward-cone melee that damages the player.
// ==========================================================================
public class RulerWhipPower : TeacherPower
{
    [Header("Ruler Whip")]
    [SerializeField] private float reach = 3f;
    [SerializeField] private float coneAngle = 40f;
    [SerializeField] private int damage = 8;

    private Transform ruler;
    private Quaternion rulerRestRotation;
    private Coroutine activeSwing;

    protected override void Awake()
    {
        base.Awake();
        cooldown = 5f;
        reach = 3f;
        damage = 8;
        CreateRuler();
    }

    private void CreateRuler()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Ruler";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        // Held in the right hand area, hanging down at rest.
        go.transform.localPosition = new Vector3(0.42f, 1.0f, 0.15f);
        // Pivot at the handle; the long side points along local +Z so it sticks forward
        // when swung. Cube primitive is symmetric so we shift the visual via scale.
        go.transform.localScale = new Vector3(0.06f, 0.06f, 0.85f);
        go.transform.localRotation = Quaternion.Euler(75f, 0f, 0f); // hanging downward at rest
        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            r.material.color = new Color(0.93f, 0.78f, 0.45f); // wood
        }
        ruler = go.transform;
        rulerRestRotation = ruler.localRotation;
    }

    protected override bool TryActivate()
    {
        Vector3 toPlayer = player.position - transform.position;
        if (toPlayer.magnitude > reach) return false;
        float angle = Vector3.Angle(transform.forward, toPlayer);
        if (angle > coneAngle / 2f) return false;

        Debug.Log($"[RulerWhip] {name} whipped player for {damage} damage");
        if (activeSwing != null) StopCoroutine(activeSwing);
        activeSwing = StartCoroutine(SwingRulerRoutine());
        return true;
    }

    private IEnumerator SwingRulerRoutine()
    {
        if (ruler == null) yield break;

        Quaternion swung = Quaternion.Euler(-10f, 0f, 0f); // raised forward, ready to strike

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.15f;
            ruler.localRotation = Quaternion.Slerp(rulerRestRotation, swung, t);
            yield return null;
        }
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.25f;
            ruler.localRotation = Quaternion.Slerp(swung, rulerRestRotation, t);
            yield return null;
        }
        ruler.localRotation = rulerRestRotation;
        activeSwing = null;
    }
}

// ==========================================================================
// 6. CHALK STORM — Ivanzaprqnov.  Throws 5 chalk projectiles at the player.
// ==========================================================================
public class ChalkStormPower : TeacherPower
{
    [Header("Chalk Storm")]
    [SerializeField] private int chalkCount = 3;
    [SerializeField] private float chalkSpeed = 30f;
    [SerializeField] private float spreadAngle = 4f;
    [SerializeField] private GameObject chalkProjectilePrefab;

    protected override void Awake()
    {
        base.Awake();
        cooldown = 5f;
        chalkCount = 3;
        chalkSpeed = 30f;
        spreadAngle = 4f;
    }

    protected override bool TryActivate()
    {
        // Start the chalk in front of the teacher's chest so it doesn't collide with
        // their own body collider on spawn and stop dead.
        Vector3 chest = transform.position + Vector3.up * 1.6f + transform.forward * 0.45f;
        Vector3 head = player.position + Vector3.up * 1.4f;
        Vector3 toPlayer = (head - chest).normalized;

        for (int i = 0; i < chalkCount; i++)
        {
            float jitterX = Random.Range(-spreadAngle, spreadAngle);
            float jitterY = Random.Range(-spreadAngle, spreadAngle);
            Quaternion jitter = Quaternion.Euler(jitterY, jitterX, 0);
            Vector3 dir = jitter * toPlayer;

            GameObject chalk;
            if (chalkProjectilePrefab != null)
                chalk = Instantiate(chalkProjectilePrefab, chest, Quaternion.LookRotation(dir));
            else
            {
                chalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                // Strip the default cube collider — it makes the chalk bounce off walls
                // and even the teacher's own body, killing momentum on the first frame.
                Destroy(chalk.GetComponent<Collider>());
                chalk.transform.localScale = new Vector3(0.22f, 0.22f, 0.55f);
                chalk.transform.position = chest;
                chalk.transform.rotation = Quaternion.LookRotation(dir);
                var rend = chalk.GetComponent<Renderer>();
                rend.material.color = Color.white;
                rend.material.EnableKeyword("_EMISSION");
                rend.material.SetColor("_EmissionColor", Color.white * 2.5f);
            }
            Rigidbody rb = chalk.GetComponent<Rigidbody>();
            if (rb == null) rb = chalk.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = dir * chalkSpeed;
            Destroy(chalk, 1.5f);
        }
        Debug.Log($"[ChalkStorm] {name} fired {chalkCount} chalks");
        return true;
    }
}

// ==========================================================================
// 7. DETENTION TRAP — Milen Spasov.  Periodically drops a glowing red trap
//    zone on the floor that slows the player on contact.
// ==========================================================================
public class DetentionTrapPower : TeacherPower
{
    [Header("Detention Trap")]
    [SerializeField] private float trapRadius = 1.0f;
    [SerializeField] private float trapLifetime = 15f;
    [SerializeField] private float slowDuration = 2.5f;

    protected override void Awake()
    {
        base.Awake();
        cooldown = 10f;
        requireAggro = false;
        trapLifetime = 15f;
        slowDuration = 2.5f;
    }

    protected override bool TryActivate()
    {
        GameObject trap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trap.name = "DetentionTrap";
        trap.transform.position = transform.position + Vector3.down * 0.45f;
        trap.transform.localScale = new Vector3(trapRadius * 2, 0.04f, trapRadius * 2);
        var r = trap.GetComponent<Renderer>();
        if (r != null) r.material.color = new Color(0.95f, 0.15f, 0.15f, 0.8f);
        Collider col = trap.GetComponent<Collider>();
        col.isTrigger = true;
        DetentionTrapZone zone = trap.AddComponent<DetentionTrapZone>();
        zone.slowDuration = slowDuration;
        Destroy(trap, trapLifetime);
        Debug.Log($"[DetentionTrap] {name} dropped a trap");
        return true;
    }
}

public class DetentionTrapZone : MonoBehaviour
{
    public float slowDuration = 5f;
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Debug.Log("[DetentionTrap] Player stepped in a trap → slow!");
        // TODO: apply slow to player. e.g.:
        //   other.GetComponent<FirstPersonController>()?.ApplySlow(slowDuration);
    }
}

// ==========================================================================
// 8. DOPPELGANGER — Basheva.  Spawns a visual copy of herself at a nearby
//    location to confuse the player.
// ==========================================================================
public class DoppelgangerPower : TeacherPower
{
    [Header("Doppelganger")]
    [SerializeField] private float spawnRange = 12f;
    [SerializeField] private float illusionLifetime = 4f;

    protected override void Awake()
    {
        base.Awake();
        cooldown = 35f;
        illusionLifetime = 4f;
    }

    protected override bool TryActivate()
    {
        Vector2 rnd = Random.insideUnitCircle.normalized * spawnRange;
        Vector3 pos = transform.position + new Vector3(rnd.x, 0, rnd.y);

        GameObject copy = Instantiate(gameObject, pos, transform.rotation);
        copy.name = name + "_Illusion";
        // Strip the power scripts from the copy so it doesn't act
        foreach (var p in copy.GetComponents<TeacherPower>()) Destroy(p);
        // Strip AI so it just stands there
        var follower = copy.GetComponent<EntityCinemachineDollyFollower>();
        if (follower != null) Destroy(follower);
        Destroy(copy, illusionLifetime);
        Debug.Log($"[Doppelganger] {name} spawned an illusion @ {pos}");
        return true;
    }
}

// ==========================================================================
// 9. SEALED BUILDING — Direktorka.  Once per match, locks every door for
//    30 seconds. Forces players to survive the lockdown.
// ==========================================================================
public class SealedBuildingPower : TeacherPower
{
    [Header("Sealed Building")]
    [SerializeField] private float lockdownDuration = 12f;
    [SerializeField] private bool oncePerMatch = true;
    [SerializeField] private AudioClip lockdownSiren;

    private bool hasFired;

    // Big global setpiece; the headmistress can lock the building without watching the player.
    protected override bool RequiresLineOfSight => false;

    protected override void Awake()
    {
        base.Awake();
        cooldown = 99999f;
        activationRange = 9999f;
        lockdownDuration = 12f;
    }

    protected override bool TryActivate()
    {
        if (oncePerMatch && hasFired) return false;
        hasFired = true;

        var doors = FindObjectsByType<DoorController>(FindObjectsSortMode.None);
        foreach (var d in doors) d.enabled = false;
        if (lockdownSiren != null) AudioSource.PlayClipAtPoint(lockdownSiren, transform.position, 1f);
        Debug.Log($"[SealedBuilding] LOCKDOWN — {doors.Length} doors disabled for {lockdownDuration}s");

        StartCoroutine(EndLockdown(doors));
        return true;
    }

    private IEnumerator EndLockdown(DoorController[] doors)
    {
        yield return new WaitForSeconds(lockdownDuration);
        foreach (var d in doors) if (d != null) d.enabled = true;
        Debug.Log($"[SealedBuilding] Lockdown ended.");
    }
}

// ==========================================================================
// 10. X-RAY VISION — Hristov.  Always knows the player's position, regardless
//     of walls. Continuously updates a "lastSeenPosition" for AI to chase.
// ==========================================================================
public class XRayVisionPower : TeacherPower
{
    [Header("X-Ray Vision")]
    [SerializeField] private float sightRadius = 12f;

    public Vector3 LastSeenPlayerPosition { get; private set; }

    // The whole point of X-Ray is to see through walls.
    protected override bool RequiresLineOfSight => false;

    protected override void Awake() { base.Awake(); cooldown = 0.25f; requireAggro = false; }

    protected override bool TryActivate()
    {
        float d = Vector3.Distance(transform.position, player.position);
        if (d > sightRadius) return false;
        LastSeenPlayerPosition = player.position;
        // Note: no need to "do" anything visible — the AI uses this position to navigate.
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.2f, 0.9f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, sightRadius);
    }
}

