using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SingleItemInventory))]
public class PlayerItemInteractor : MonoBehaviour
{
    [SerializeField] private float interactRadius = 2.2f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PickupPromptUI promptUi;
    [SerializeField] private DialogueChoiceUI dialogueChoiceUi;

    private SingleItemInventory inventory;
    private ItemPickup currentTarget;
    private bool isPromptCursorMode;

    private void Awake()
    {
        inventory = GetComponent<SingleItemInventory>();

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (promptUi == null)
        {
            promptUi = FindAnyObjectByType<PickupPromptUI>();
            if (promptUi == null)
            {
                GameObject ui = new GameObject("PickupPromptUI");
                promptUi = ui.AddComponent<PickupPromptUI>();
            }
        }

        if (dialogueChoiceUi == null)
        {
            dialogueChoiceUi = FindAnyObjectByType<DialogueChoiceUI>();
        }

    }

    private void Update()
    {
        if (ShouldYieldCursorToDialogue())
        {
            promptUi.Hide();
            currentTarget = null;
            return;
        }

        if (inventory == null || inventory.HasItem)
        {
            promptUi.Hide();
            SetPromptCursorMode(false);
            return;
        }

        currentTarget = FindNearestPickup();
        if (currentTarget == null)
        {
            promptUi.Hide();
            SetPromptCursorMode(false);
            return;
        }

        string prompt = $"{currentTarget.PickupPromptText} {currentTarget.ItemDisplayName}";
        promptUi.Show(currentTarget.PickupKeyLabel, prompt, TryPickupCurrentTarget);
        SetPromptCursorMode(false);

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryPickupCurrentTarget();
        }
    }

    private void TryPickupCurrentTarget()
    {
        if (currentTarget == null)
        {
            return;
        }

        if (inventory.TryPickup(currentTarget))
        {
            promptUi.Hide();
            SetPromptCursorMode(false);
            currentTarget = null;
        }
    }

    private ItemPickup FindNearestPickup()
    {
        ItemPickup[] pickups = FindObjectsByType<ItemPickup>();
        if (pickups == null || pickups.Length == 0)
        {
            return null;
        }

        Vector3 origin = playerCamera != null ? playerCamera.transform.position : transform.position;
        ItemPickup nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (ItemPickup pickup in pickups)
        {
            if (pickup == null || pickup.IsPickedUp)
            {
                continue;
            }

            float distance = Vector3.Distance(origin, pickup.PromptPoint.position);
            if (distance <= interactRadius && distance < nearestDistance)
            {
                nearest = pickup;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void SetPromptCursorMode(bool enabled)
    {
        if (isPromptCursorMode == enabled)
        {
            return;
        }

        isPromptCursorMode = enabled;
        Cursor.lockState = enabled ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = enabled;
    }

    private bool ShouldYieldCursorToDialogue()
    {
        bool choiceVisible = dialogueChoiceUi != null && dialogueChoiceUi.IsVisible;
        return choiceVisible;
    }
}
