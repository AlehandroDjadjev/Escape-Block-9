using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class EntityAlertIndicator : MonoBehaviour
{
    public enum AlertState
    {
        Calm = 0,
        Suspicious = 1,
        Aggro = 2
    }

    [Header("Visual")]
    [SerializeField] private Transform headPoint;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.55f, 0f);
    [SerializeField] private Vector2 screenOffset = Vector2.zero;
    [SerializeField] private int dotFontSize = 36;
    [SerializeField] private bool showWhenAggro = false;
    [SerializeField] private Color suspiciousColor = Color.white;
    [SerializeField] private Color aggroColor = Color.red;

    [Header("State")]
    [SerializeField] private AlertState state = AlertState.Calm;

    [Header("Shared Suspicion Settings")]
    [SerializeField] private bool useSharedSuspicionSettings = true;
    [SerializeField] private SuspicionSettings sharedSuspicionSettings;

    [Header("Animator Sync")]
    [SerializeField] private Animator animator;
    [SerializeField] private bool useAnimatorSuspicion = true;
    [SerializeField] private bool alignStateToAnimatorStates = true;
    [SerializeField] private string[] suspiciousStateNames = { "Suspicious" };
    [SerializeField] private string[] aggroStateNames = { "Gnevna", "Aggro" };
    [SerializeField] private string suspicionParameter = "Suspicion";
    [SerializeField] private float aggroThreshold = 2.5f;
    [SerializeField] private float suspiciousThreshold = 0.01f;
    [SerializeField] private float minimumSuspicion = -0.5f;
    [SerializeField] private float suspicionDecayPerSecond = 0.5f;
    [SerializeField] private float centralRaySuspicion = 6f;
    [SerializeField] private float peripheralSuspicionMin = 1f;
    [SerializeField] private float peripheralSuspicionMax = 2f;

    [Header("Vision Debug")]
    [SerializeField] private bool showVisionRays = true;
    [SerializeField] private bool alwaysShowVisionRays = true;
    [SerializeField] private bool updateVisionRaysInEditMode = true;
    [SerializeField] private Transform visionOrigin;
    [SerializeField] private float visionDistance = 9f;
    [SerializeField, Range(10f, 180f)] private float visionAngle = 70f;
    [SerializeField, Range(3, 40)] private int visionRayCount = 11;
    [SerializeField, Range(1, 9)] private int centralRayCount = 3;
    [SerializeField] private LayerMask visionMask = ~0;
    [SerializeField] private float visionRayWidth = 0.05f;
    [SerializeField] private float rayStartOffset = 0.08f;
    [SerializeField] private int visionSortingOrder = 5000;
    [SerializeField] private Color suspicionVisionColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color aggroVisionColor = new Color(1f, 0.2f, 0.2f, 1f);

    [Header("Vision Target")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private string fallbackTargetTag = "Player";
    [SerializeField] private Transform visionRaysRoot;

    private static Canvas sharedCanvas;
    private RectTransform dotRect;
    private Text dotText;
    private Camera targetCamera;
    private LineRenderer[] visionRays;
    private int suspicionParamHash;
    private bool hasSuspicionParameter;
    private static Material sharedVisionMaterial;
    private static readonly RaycastHit[] raycastBuffer = new RaycastHit[32];

    public AlertState State => state;

    private void Awake()
    {
        targetCamera = Camera.main;
        ResolveSuspicionSettings();
        EnsureAnimator();
        suspicionParamHash = Animator.StringToHash(suspicionParameter);
        hasSuspicionParameter = HasAnimatorFloatParameter();
        EnsureTargetTransform();
        EnsureUiDot();
        RemoveLegacyWorldDot();
        EnsureVisionRays();
        ApplyState();
    }

    private void LateUpdate()
    {
        EnsureTargetTransform();
        HandleVisionSuspicion();
        SyncStateFromAnimator();
        UpdateVisionRays();

        if (dotRect == null)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                return;
            }
        }

        bool visible = ShouldDisplayDot();
        dotRect.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        Vector3 worldPos = (headPoint != null ? headPoint.position : transform.position) + worldOffset;
        Vector3 screenPos = targetCamera.WorldToScreenPoint(worldPos);
        if (screenPos.z <= 0f)
        {
            dotRect.gameObject.SetActive(false);
            return;
        }
        dotRect.position = screenPos + (Vector3)screenOffset;
    }

    private void Update()
    {
        if (Application.isPlaying || !updateVisionRaysInEditMode)
        {
            return;
        }

        EnsureTargetTransform();
        EnsureVisionRays();
        SyncStateFromAnimator();
        UpdateVisionRays();
    }

    private void OnDestroy()
    {
        if (dotRect != null)
        {
            Destroy(dotRect.gameObject);
        }

        ClearVisionRays();
    }

    private void OnValidate()
    {
        ResolveSuspicionSettings();
        EnsureAnimator();
        suspicionParamHash = Animator.StringToHash(suspicionParameter);
        hasSuspicionParameter = HasAnimatorFloatParameter();
        EnsureUiDot();
        EnsureVisionRays();
        UpdateVisionRays();
        ApplyState();
    }

    public void SetCalm()
    {
        state = AlertState.Calm;
        ApplyState();
    }

    public void SetSuspicious()
    {
        state = AlertState.Suspicious;
        ApplyState();
    }

    public void SetAggro()
    {
        state = AlertState.Aggro;
        ApplyState();
    }

    private void EnsureUiDot()
    {
        if (dotRect != null && dotText != null)
        {
            dotText.fontSize = dotFontSize;
            return;
        }

        EnsureSharedCanvas();

        string dotName = $"AlertDotUI_{name}";
        if (dotRect == null && sharedCanvas != null)
        {
            Transform existingDot = sharedCanvas.transform.Find(dotName);
            if (existingDot != null)
            {
                dotRect = existingDot.GetComponent<RectTransform>();
                dotText = existingDot.GetComponent<Text>();
            }
        }

        if (dotRect != null && dotText != null)
        {
            dotText.fontSize = dotFontSize;
            return;
        }

        GameObject dot = new GameObject(dotName);
        dot.transform.SetParent(sharedCanvas.transform, false);
        dotRect = dot.AddComponent<RectTransform>();
        dotRect.sizeDelta = new Vector2(32f, 32f);

        dotText = dot.AddComponent<Text>();
        dotText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        dotText.fontSize = dotFontSize;
        dotText.alignment = TextAnchor.MiddleCenter;
        dotText.horizontalOverflow = HorizontalWrapMode.Overflow;
        dotText.verticalOverflow = VerticalWrapMode.Overflow;
        dotText.raycastTarget = false;
        dotText.text = "●";

        if (headPoint == null)
        {
            Transform head = transform.Find("Head");
            if (head != null)
            {
                headPoint = head;
            }
        }

        if (visionOrigin == null)
        {
            visionOrigin = headPoint != null ? headPoint : transform;
        }
    }

    private static void EnsureSharedCanvas()
    {
        if (sharedCanvas == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].name == "EntityAlertCanvas")
                {
                    sharedCanvas = canvases[i];
                    break;
                }
            }
        }

        if (sharedCanvas == null)
        {
            GameObject canvasObj = new GameObject("EntityAlertCanvas");
            sharedCanvas = canvasObj.AddComponent<Canvas>();
            sharedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            sharedCanvas.sortingOrder = 3300;
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        for (int i = 0; i < allCanvases.Length; i++)
        {
            Canvas canvas = allCanvases[i];
            if (canvas == null || canvas == sharedCanvas || canvas.name != "EntityAlertCanvas")
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(canvas.gameObject);
            }
            else
            {
                DestroyImmediate(canvas.gameObject);
            }
        }
    }

    private void RemoveLegacyWorldDot()
    {
        Transform oldDot = transform.Find("AlertDot");
        if (oldDot != null)
        {
            Destroy(oldDot.gameObject);
        }
    }

    private void EnsureAnimator()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private SuspicionSettings ResolveSuspicionSettings()
    {
        if (!useSharedSuspicionSettings)
        {
            return null;
        }

        if (sharedSuspicionSettings != null)
        {
            return sharedSuspicionSettings;
        }

        sharedSuspicionSettings = SuspicionSettings.Instance;
        if (sharedSuspicionSettings == null)
        {
            sharedSuspicionSettings = FindAnyObjectByType<SuspicionSettings>();
        }

        return sharedSuspicionSettings;
    }

    private float GetSuspiciousThreshold()
    {
        SuspicionSettings settings = ResolveSuspicionSettings();
        return settings != null ? settings.SuspiciousThreshold : suspiciousThreshold;
    }

    private float GetAggroThreshold()
    {
        SuspicionSettings settings = ResolveSuspicionSettings();
        return settings != null ? settings.AggroThreshold : aggroThreshold;
    }

    private float GetMinimumSuspicion()
    {
        SuspicionSettings settings = ResolveSuspicionSettings();
        return settings != null ? settings.MinimumSuspicion : minimumSuspicion;
    }

    private float GetSuspicionDecayPerSecond()
    {
        SuspicionSettings settings = ResolveSuspicionSettings();
        return settings != null ? settings.SuspicionDecayPerSecond : suspicionDecayPerSecond;
    }

    private float GetCentralRaySuspicion()
    {
        SuspicionSettings settings = ResolveSuspicionSettings();
        return settings != null ? settings.CentralRaySuspicion : centralRaySuspicion;
    }

    private float GetPeripheralSuspicionMin()
    {
        SuspicionSettings settings = ResolveSuspicionSettings();
        return settings != null ? settings.PeripheralSuspicionMin : peripheralSuspicionMin;
    }

    private float GetPeripheralSuspicionMax()
    {
        SuspicionSettings settings = ResolveSuspicionSettings();
        return settings != null ? settings.PeripheralSuspicionMax : peripheralSuspicionMax;
    }

    private bool GetShowDotWhenAggro()
    {
        SuspicionSettings settings = ResolveSuspicionSettings();
        return settings != null ? settings.ShowDotWhenAggro : showWhenAggro;
    }

    private bool GetShowVisionRays()
    {
        SuspicionSettings settings = ResolveSuspicionSettings();
        return settings != null ? settings.ShowVisionRays : showVisionRays;
    }

    private bool GetAlwaysShowVisionRays()
    {
        SuspicionSettings settings = ResolveSuspicionSettings();
        return settings != null ? settings.AlwaysShowVisionRays : alwaysShowVisionRays;
    }

    private void EnsureTargetTransform()
    {
        if (targetTransform != null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(fallbackTargetTag))
        {
            GameObject tagged = GameObject.FindGameObjectWithTag(fallbackTargetTag);
            if (tagged != null)
            {
                targetTransform = tagged.transform;
                return;
            }
        }

        FirstPersonController playerController = FindAnyObjectByType<FirstPersonController>();
        if (playerController != null)
        {
            targetTransform = playerController.transform;
        }
    }

    private void HandleVisionSuspicion()
    {
        if (!useAnimatorSuspicion || animator == null || !hasSuspicionParameter)
        {
            return;
        }

        EvaluateVision(out bool sawCentral, out bool sawPeripheral, drawRays: false);

        float suspicion = animator.GetFloat(suspicionParamHash);
        if (sawCentral)
        {
            suspicion = Mathf.Max(suspicion, GetCentralRaySuspicion());
        }
        else if (sawPeripheral)
        {
            float peripheralValue = Random.Range(
                Mathf.Min(GetPeripheralSuspicionMin(), GetPeripheralSuspicionMax()),
                Mathf.Max(GetPeripheralSuspicionMin(), GetPeripheralSuspicionMax()));
            suspicion = Mathf.Max(suspicion, peripheralValue);
        }
        else
        {
            suspicion = Mathf.Max(GetMinimumSuspicion(), suspicion - (GetSuspicionDecayPerSecond() * Time.deltaTime));
        }

        animator.SetFloat(suspicionParamHash, suspicion);
    }

    private void SyncStateFromAnimator()
    {
        if (!useAnimatorSuspicion || animator == null || !animator.isActiveAndEnabled)
        {
            return;
        }

        AlertState? byAnimationState = ResolveStateFromAnimatorState();
        if (byAnimationState.HasValue)
        {
            if (byAnimationState.Value != state)
            {
                state = byAnimationState.Value;
                ApplyState();
            }

            return;
        }

        if (!hasSuspicionParameter)
        {
            return;
        }

        float suspicion = animator.GetFloat(suspicionParamHash);
        AlertState nextState = suspicion >= GetAggroThreshold()
            ? AlertState.Aggro
            : (suspicion >= GetSuspiciousThreshold() ? AlertState.Suspicious : AlertState.Calm);

        if (nextState != state)
        {
            state = nextState;
            ApplyState();
        }
    }

    private AlertState? ResolveStateFromAnimatorState()
    {
        if (!alignStateToAnimatorStates || animator == null)
        {
            return null;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (MatchesAnyStateName(current, aggroStateNames))
        {
            return AlertState.Aggro;
        }

        if (MatchesAnyStateName(current, suspiciousStateNames))
        {
            return AlertState.Suspicious;
        }

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (MatchesAnyStateName(next, aggroStateNames))
            {
                return AlertState.Aggro;
            }

            if (MatchesAnyStateName(next, suspiciousStateNames))
            {
                return AlertState.Suspicious;
            }
        }

        return null;
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

    private bool ShouldDisplayDot()
    {
        if (state == AlertState.Suspicious)
        {
            return true;
        }

        if (state == AlertState.Aggro && GetShowDotWhenAggro())
        {
            return true;
        }

        return false;
    }

    private bool ShouldDisplayVision()
    {
        if (!GetShowVisionRays())
        {
            return false;
        }

        if (GetAlwaysShowVisionRays())
        {
            return true;
        }

        if (state == AlertState.Suspicious)
        {
            return true;
        }

        return state == AlertState.Aggro;
    }

    private void EnsureVisionRays()
    {
        if (!GetShowVisionRays())
        {
            ClearVisionRays();
            return;
        }

        EnsureVisionRoot();
        if (visionRays == null || visionRays.Length != visionRayCount)
        {
            ClearVisionRays();
            visionRays = new LineRenderer[visionRayCount];
        }

        if (sharedVisionMaterial == null)
        {
            sharedVisionMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        for (int i = 0; i < visionRayCount; i++)
        {
            LineRenderer line = visionRays[i];
            if (line == null)
            {
                string rayName = $"VisionRay_{i:00}";
                Transform existing = visionRaysRoot.Find(rayName);
                if (existing != null)
                {
                    line = existing.GetComponent<LineRenderer>();
                }

                if (line == null)
                {
                    GameObject rayObject = new GameObject(rayName);
                    rayObject.transform.SetParent(visionRaysRoot, false);
                    line = rayObject.AddComponent<LineRenderer>();
                }
            }

            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = visionRayWidth;
            line.endWidth = visionRayWidth;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = sharedVisionMaterial;
            line.sortingOrder = visionSortingOrder;
            line.enabled = false;
            visionRays[i] = line;
        }

        CleanupExtraVisionRayChildren();
    }

    private void ClearVisionRays()
    {
        if (visionRays == null)
        {
            return;
        }

        for (int i = 0; i < visionRays.Length; i++)
        {
            if (visionRays[i] != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(visionRays[i].gameObject);
                }
                else
                {
                    DestroyImmediate(visionRays[i].gameObject);
                }
            }
        }

        visionRays = null;
    }

    private void UpdateVisionRays()
    {
        EnsureVisionRays();
        if (visionRays == null)
        {
            return;
        }

        EvaluateVision(out _, out _, drawRays: true);
    }

    private void EnsureVisionRoot()
    {
        if (visionRaysRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("VisionRaysRoot");
        if (existing != null)
        {
            visionRaysRoot = existing;
            return;
        }

        GameObject root = new GameObject("VisionRaysRoot");
        root.transform.SetParent(transform, false);
        visionRaysRoot = root.transform;
    }

    private void CleanupExtraVisionRayChildren()
    {
        if (visionRaysRoot == null)
        {
            return;
        }

        for (int i = visionRaysRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = visionRaysRoot.GetChild(i);
            if (!child.name.StartsWith("VisionRay_"))
            {
                continue;
            }

            bool keep = false;
            for (int rayIndex = 0; rayIndex < visionRayCount; rayIndex++)
            {
                if (child.name == $"VisionRay_{rayIndex:00}")
                {
                    keep = true;
                    break;
                }
            }

            if (!keep)
            {
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }

    private void EvaluateVision(out bool sawCentral, out bool sawPeripheral, bool drawRays)
    {
        sawCentral = false;
        sawPeripheral = false;

        bool show = drawRays && ShouldDisplayVision();
        if (visionRays != null && !show)
        {
            for (int i = 0; i < visionRays.Length; i++)
            {
                if (visionRays[i] != null)
                {
                    visionRays[i].enabled = false;
                }
            }

        }

        Transform originTransform = visionOrigin != null ? visionOrigin : transform;
        Vector3 origin = originTransform.position;
        Vector3 flatForward = originTransform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f)
        {
            flatForward = transform.forward;
            flatForward.y = 0f;
        }

        flatForward.Normalize();
        float half = visionAngle * 0.5f;
        float step = visionRayCount > 1 ? visionAngle / (visionRayCount - 1f) : 0f;
        Color rayColor = state == AlertState.Aggro ? aggroVisionColor : suspicionVisionColor;
        int centerIndex = visionRayCount / 2;
        int halfCentral = Mathf.Max(0, centralRayCount / 2);

        for (int i = 0; i < visionRayCount; i++)
        {
            LineRenderer line = visionRays != null && i < visionRays.Length ? visionRays[i] : null;
            if (line == null && show)
            {
                continue;
            }

            float yaw = -half + step * i;
            Vector3 direction = Quaternion.AngleAxis(yaw, Vector3.up) * flatForward;
            Vector3 start = origin + direction * rayStartOffset;
            Vector3 endPoint = start + direction * visionDistance;
            Collider blockingCollider;
            if (TryRaycastVision(start, direction, out RaycastHit hit, out blockingCollider))
            {
                endPoint = hit.point;
                if (IsTargetHit(blockingCollider))
                {
                    bool isCentral = Mathf.Abs(i - centerIndex) <= halfCentral;
                    sawCentral |= isCentral;
                    sawPeripheral |= !isCentral;
                }
            }

            if (show)
            {
                line.startColor = rayColor;
                line.endColor = rayColor;
                line.startWidth = visionRayWidth;
                line.endWidth = visionRayWidth;
                line.sortingOrder = visionSortingOrder;
                line.SetPosition(0, start);
                line.SetPosition(1, endPoint);
                line.enabled = true;
            }
        }
    }

    private bool TryRaycastVision(Vector3 origin, Vector3 direction, out RaycastHit closestHit, out Collider closestCollider)
    {
        int count = Physics.RaycastNonAlloc(
            origin,
            direction,
            raycastBuffer,
            visionDistance,
            visionMask,
            QueryTriggerInteraction.Ignore);

        closestHit = default;
        closestCollider = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = raycastBuffer[i];
            Collider col = hit.collider;
            if (col == null || IsSelfCollider(col))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                closestCollider = col;
            }
        }

        return closestCollider != null;
    }

    private bool IsTargetHit(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (targetTransform != null)
        {
            return collider.transform == targetTransform || collider.transform.IsChildOf(targetTransform);
        }

        if (!string.IsNullOrWhiteSpace(fallbackTargetTag))
        {
            return collider.CompareTag(fallbackTargetTag) || collider.transform.CompareTag(fallbackTargetTag);
        }

        return false;
    }

    private bool IsSelfCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        Transform hitTransform = collider.transform;
        return hitTransform == transform || hitTransform.IsChildOf(transform);
    }

    private bool HasAnimatorFloatParameter()
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

    private void ApplyState()
    {
        if (dotText == null || dotRect == null)
        {
            return;
        }

        bool visible = ShouldDisplayDot();
        dotRect.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        dotText.color = state == AlertState.Aggro ? aggroColor : suspiciousColor;
    }
}
