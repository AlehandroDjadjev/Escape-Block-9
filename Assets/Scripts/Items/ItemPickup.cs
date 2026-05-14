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
    }
}
