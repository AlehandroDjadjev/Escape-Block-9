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

    private CharacterController characterController;
    private float currentSprintBonus;
    private float verticalVelocity;
    private float pitch;
    private bool isSneaking;
    private float standingHeight;
    private Vector3 standingCenter;
    private Vector3 standingCameraLocalPosition;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
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

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
}
