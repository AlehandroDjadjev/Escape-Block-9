using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float baseMoveSpeed = 6f;
    [SerializeField] private float maxSprintBonus = 0.5f; // 50% of base speed
    [SerializeField] private float sprintAcceleration = 2f;
    [SerializeField] private float sprintDeceleration = 3f;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpHeight = 1.3f;
    [SerializeField] private float gravity = -20f;

    [Header("Sneak")]
    [SerializeField] private float sneakSpeedMultiplier = 0.55f;
    [SerializeField] private float sneakHeightMultiplier = 0.7f;
    [SerializeField] private float stanceTransitionSpeed = 10f;

    [Header("Look")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float mouseSensitivity = 120f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Starting Flashlight")]
    [SerializeField] private bool enableStartingFlashlight = true;
    [SerializeField] private Vector3 flashlightLocalPosition = new Vector3(0.16f, -0.08f, 0.28f);
    [SerializeField] private Vector3 flashlightLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 flashlightLocalScale = Vector3.one;
    [SerializeField] private Color flashlightColor = new Color(1f, 0.96f, 0.88f, 1f);
    [SerializeField] private float flashlightIntensity = 0.675f;
    [SerializeField] private float flashlightRange = 9f;
    [SerializeField] private float flashlightSpotAngle = 52f;
    [SerializeField] private float flashlightInnerSpotAngle = 28f;
    [SerializeField] private LightShadows flashlightShadows = LightShadows.None;
    [SerializeField] private float flashlightBatteryLifeSeconds = 240f;

    private CharacterController characterController;
    private SingleItemInventory inventory;
    private float currentSprintBonus;
    private float verticalVelocity;
    private float pitch;
    private bool isSneaking;
    private float standingHeight;
    private Vector3 standingCenter;
    private Vector3 standingCameraLocalPosition;

    public Vector3 CurrentVelocity => characterController != null ? characterController.velocity : Vector3.zero;
    public Transform CameraPivot => cameraPivot;

    private void Awake()
    {
        if (GetComponent<PlayerHealth>() == null)
        {
            gameObject.AddComponent<PlayerHealth>();
        }

        characterController = GetComponent<CharacterController>();
        inventory = GetComponent<SingleItemInventory>();
        standingHeight = characterController.height;
        standingCenter = characterController.center;

        if (cameraPivot == null && Camera.main != null)
        {
            cameraPivot = Camera.main.transform;
        }

        if (cameraPivot != null)
        {
            standingCameraLocalPosition = cameraPivot.localPosition;
        }

        EnsureStartingFlashlightItem();
        EnsureBatteryHud();
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        HandleLook();
        HandleStance();
        HandleMovement();
    }

    private void HandleLook()
    {
        if (cameraPivot == null)
        {
            return;
        }
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Vector2 lookInput = Vector2.zero;
        if (Mouse.current != null)
        {
            lookInput = Mouse.current.delta.ReadValue();
        }

        // Mouse delta is already frame-relative, so no Time.deltaTime here.
        float mouseX = lookInput.x * mouseSensitivity * 0.01f;
        float mouseY = lookInput.y * mouseSensitivity * 0.01f;

        transform.Rotate(Vector3.up * mouseX);

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        cameraPivot.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        Vector2 moveAxes = ReadMoveAxes();
        float moveX = moveAxes.x;
        float moveZ = moveAxes.y;
        Vector3 moveInput = (transform.right * moveX + transform.forward * moveZ).normalized;

        bool sprintHeld = IsSprintHeld() && !isSneaking;
        float targetBonus = sprintHeld ? maxSprintBonus : 0f;
        float accel = sprintHeld ? sprintAcceleration : sprintDeceleration;
        currentSprintBonus = Mathf.MoveTowards(currentSprintBonus, targetBonus, accel * Time.deltaTime);

        float stanceSpeed = isSneaking ? sneakSpeedMultiplier : 1f;
        float currentSpeed = baseMoveSpeed * stanceSpeed * (1f + currentSprintBonus);
        Vector3 horizontalVelocity = moveInput * currentSpeed;

        if (characterController.isGrounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (IsJumpPressed())
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleStance()
    {
        isSneaking = IsSneakHeld();

        float targetHeight = isSneaking ? standingHeight * sneakHeightMultiplier : standingHeight;
        Vector3 targetCenter = new Vector3(standingCenter.x, targetHeight * 0.5f, standingCenter.z);

        characterController.height = Mathf.MoveTowards(
            characterController.height,
            targetHeight,
            stanceTransitionSpeed * Time.deltaTime);

        characterController.center = Vector3.MoveTowards(
            characterController.center,
            targetCenter,
            stanceTransitionSpeed * Time.deltaTime);

        if (cameraPivot != null)
        {
            float targetCameraY = isSneaking
                ? standingCameraLocalPosition.y - (standingHeight - targetHeight)
                : standingCameraLocalPosition.y;

            Vector3 camLocal = cameraPivot.localPosition;
            camLocal.y = Mathf.MoveTowards(camLocal.y, targetCameraY, stanceTransitionSpeed * Time.deltaTime);
            cameraPivot.localPosition = camLocal;
        }
    }

    private static Vector2 ReadMoveAxes()
    {
        if (Keyboard.current == null)
        {
            return Vector2.zero;
        }

        float x = 0f;
        if (Keyboard.current.aKey.isPressed) x -= 1f;
        if (Keyboard.current.dKey.isPressed) x += 1f;

        float y = 0f;
        if (Keyboard.current.sKey.isPressed) y -= 1f;
        if (Keyboard.current.wKey.isPressed) y += 1f;

        return new Vector2(x, y);
    }

    private static bool IsSprintHeld()
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
    }

    private static bool IsJumpPressed()
    {
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    }

    private static bool IsSneakHeld()
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        return Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
    }

    private void EnsureStartingFlashlightItem()
    {
        if (!enableStartingFlashlight || cameraPivot == null || inventory == null)
        {
            return;
        }

        FlashlightItem existingFlashlight = inventory.HeldPickup != null
            ? inventory.HeldPickup.GetComponent<FlashlightItem>()
            : null;
        if (existingFlashlight != null)
        {
            existingFlashlight.ConfigureLight(
                flashlightBatteryLifeSeconds,
                flashlightColor,
                flashlightIntensity,
                flashlightRange,
                flashlightSpotAngle,
                flashlightInnerSpotAngle,
                flashlightShadows,
                refillBattery: true);
            return;
        }

        if (inventory.HasItem)
        {
            return;
        }

        GameObject flashlightObject = new GameObject("PlayerFlashlightItem");
        flashlightObject.transform.SetParent(transform, false);
        BuildFlashlightVisual(flashlightObject.transform);

        Rigidbody rb = flashlightObject.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = 0.35f;

        ItemPickup pickup = flashlightObject.AddComponent<ItemPickup>();
        pickup.ConfigureIdentity("flashlight_item", "Flashlight");
        pickup.ConfigurePrompt("E", "Pick up");
        pickup.ConfigureHeldPose(flashlightLocalPosition, flashlightLocalEulerAngles, flashlightLocalScale);

        FlashlightItem flashlightItem = flashlightObject.AddComponent<FlashlightItem>();
        flashlightItem.ConfigureLight(
            flashlightBatteryLifeSeconds,
            flashlightColor,
            flashlightIntensity,
            flashlightRange,
            flashlightSpotAngle,
            flashlightInnerSpotAngle,
            flashlightShadows,
            refillBattery: true);

        inventory.TryPickup(pickup);
    }

    private void EnsureBatteryHud()
    {
        if (GetComponent<FlashlightBatteryHud>() == null)
        {
            gameObject.AddComponent<FlashlightBatteryHud>();
        }
    }

    private static void BuildFlashlightVisual(Transform root)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        body.transform.SetParent(root, false);
        body.transform.localPosition = new Vector3(0f, 0f, 0f);
        body.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
        body.transform.localScale = new Vector3(0.12f, 0.22f, 0.12f);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        head.name = "Head";
        head.transform.SetParent(root, false);
        head.transform.localPosition = new Vector3(0f, 0f, 0.22f);
        head.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
        head.transform.localScale = new Vector3(0.16f, 0.06f, 0.16f);

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material.color = i == 0
                ? new Color(0.2f, 0.2f, 0.24f, 1f)
                : new Color(0.58f, 0.58f, 0.62f, 1f);
        }

        root.localScale = Vector3.one;
    }
}
