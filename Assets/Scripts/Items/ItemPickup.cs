using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string itemId = "item_default";
    [SerializeField] private string itemDisplayName = "Item";

    [Header("Prompt")]
    [SerializeField] private string pickupKeyLabel = "E";
    [SerializeField] private string pickupPromptText = "Pick up";
    [SerializeField] private Transform promptPoint;

    [Header("Hand Pose")]
    [SerializeField] private Vector3 heldLocalPosition = new Vector3(0.32f, -0.24f, 0.62f);
    [SerializeField] private Vector3 heldLocalEulerAngles = new Vector3(10f, 90f, 0f);
    [SerializeField] private Vector3 heldLocalScale = Vector3.one;

    public string ItemId => itemId;
    public string ItemDisplayName => itemDisplayName;
    public string PickupKeyLabel => pickupKeyLabel;
    public string PickupPromptText => pickupPromptText;
    public Transform PromptPoint => promptPoint != null ? promptPoint : transform;
    public bool IsPickedUp { get; private set; }

    public void ConfigureIdentity(string newItemId, string newDisplayName)
    {
        itemId = string.IsNullOrWhiteSpace(newItemId) ? itemId : newItemId;
        itemDisplayName = string.IsNullOrWhiteSpace(newDisplayName) ? itemDisplayName : newDisplayName;
    }

    public void ConfigurePrompt(string keyLabel, string promptText)
    {
        if (!string.IsNullOrWhiteSpace(keyLabel))
        {
            pickupKeyLabel = keyLabel;
        }

        if (!string.IsNullOrWhiteSpace(promptText))
        {
            pickupPromptText = promptText;
        }
    }

    public void ConfigureHeldPose(Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
    {
        heldLocalPosition = localPosition;
        heldLocalEulerAngles = localEulerAngles;
        heldLocalScale = localScale;
    }

    public void AttachToHand(Transform handAnchor)
    {
        if (IsPickedUp || handAnchor == null)
        {
            return;
        }

        IsPickedUp = true;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.SetParent(handAnchor, false);
        transform.localPosition = heldLocalPosition;
        transform.localEulerAngles = heldLocalEulerAngles;
        transform.localScale = heldLocalScale;

        FlashlightItem flashlight = GetComponent<FlashlightItem>();
        if (flashlight != null)
        {
            flashlight.SetHeldState(true);
        }
    }

    public void DropFromHand(Vector3 worldPosition, Vector3 worldForward, float forwardForce)
    {
        if (!IsPickedUp)
        {
            return;
        }

        IsPickedUp = false;
        transform.SetParent(null, true);
        transform.position = worldPosition;

        Vector3 flattenedForward = Vector3.ProjectOnPlane(worldForward, Vector3.up);
        if (flattenedForward.sqrMagnitude <= 0.0001f)
        {
            flattenedForward = Vector3.forward;
        }

        transform.rotation = Quaternion.LookRotation(flattenedForward.normalized, Vector3.up);

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            col.enabled = true;
        }

        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = false;
            rb.linearVelocity = flattenedForward.normalized * Mathf.Max(0f, forwardForce);
            rb.angularVelocity = Vector3.zero;
        }

        FlashlightItem flashlight = GetComponent<FlashlightItem>();
        if (flashlight != null)
        {
            flashlight.SetHeldState(false);
        }
    }
}
