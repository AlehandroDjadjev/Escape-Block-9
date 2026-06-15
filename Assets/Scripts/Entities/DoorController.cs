using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class DoorController : MonoBehaviour
{
    [SerializeField] private float interactRadius = 2.5f;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float animationSpeed = 3f;

    private PickupPromptUI promptUi;
    private Camera playerCamera;
    private bool isOpen;
    private bool isAnimating;

    private void Awake()
    {
        playerCamera = Camera.main;

        promptUi = FindAnyObjectByType<PickupPromptUI>();
        if (promptUi == null)
        {
            GameObject uiObj = new GameObject("PickupPromptUI");
            promptUi = uiObj.AddComponent<PickupPromptUI>();
        }
    }

    private void Update()
    {
        Vector3 origin = playerCamera != null ? playerCamera.transform.position : transform.position;
        float distance = Vector3.Distance(origin, transform.position);

        if (distance > interactRadius)
        {
            promptUi.Hide();
            return;
        }

        string action = isOpen ? "Close" : "Open";
        promptUi.Show("E", $"{action} Door", ToggleDoor);

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            ToggleDoor();
        }
    }

    private void ToggleDoor()
    {
        if (isAnimating)
            return;

        isOpen = !isOpen;
        StartCoroutine(AnimateDoor(isOpen ? openAngle : 0f));
    }

    private IEnumerator AnimateDoor(float targetY)
    {
        isAnimating = true;
        Quaternion from = transform.localRotation;
        Quaternion to = Quaternion.Euler(0f, targetY, 0f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * animationSpeed;
            transform.localRotation = Quaternion.Lerp(from, to, t);
            yield return null;
        }

        transform.localRotation = to;
        isAnimating = false;
    }
}
