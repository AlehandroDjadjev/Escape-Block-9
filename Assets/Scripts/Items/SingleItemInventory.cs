using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ItemSerializer))]
public class SingleItemInventory : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Vector3 handAnchorLocalPosition = new Vector3(0.3f, -0.25f, 0.65f);
    [SerializeField] private Vector3 handAnchorLocalEuler = Vector3.zero;
    [SerializeField] private float dropForwardDistance = 1.1f;
    [SerializeField] private float dropForwardForce = 2.25f;
    [SerializeField] private Key dropKey = Key.G;

    private Transform handAnchor;
    private ItemSerializer serializer;
    private ItemPickup heldPickup;

    public bool HasItem { get; private set; }
    public SerializedItemData CurrentItem { get; private set; }
    public Transform HandAnchor => handAnchor;
    public ItemPickup HeldPickup => heldPickup;

    private void Awake()
    {
        serializer = GetComponent<ItemSerializer>();

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        EnsureHandAnchor();
    }

    private void Update()
    {
        if (!HasItem || heldPickup == null)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current[dropKey].wasPressedThisFrame)
        {
            DropCurrentItem();
            return;
        }

        FlashlightItem flashlight = heldPickup.GetComponent<FlashlightItem>();
        if (flashlight != null && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            flashlight.Toggle();
        }
    }

    public bool TryPickup(ItemPickup pickup)
    {
        if (pickup == null || HasItem || handAnchor == null)
        {
            return false;
        }

        pickup.AttachToHand(handAnchor);

        CurrentItem = new SerializedItemData
        {
            itemId = pickup.ItemId,
            displayName = pickup.ItemDisplayName
        };

        heldPickup = pickup;
        HasItem = true;
        serializer.Save(CurrentItem);
        return true;
    }

    public bool HasItemId(string itemId)
    {
        return HasItem &&
               !string.IsNullOrWhiteSpace(itemId) &&
               string.Equals(CurrentItem.itemId, itemId, System.StringComparison.OrdinalIgnoreCase);
    }

    public bool DropCurrentItem()
    {
        if (!HasItem || heldPickup == null || handAnchor == null)
        {
            return false;
        }

        Vector3 forward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
        Vector3 dropPosition = (playerCamera != null ? playerCamera.transform.position : transform.position) +
                               forward.normalized * dropForwardDistance;
        heldPickup.DropFromHand(dropPosition, forward, dropForwardForce);

        heldPickup = null;
        HasItem = false;
        CurrentItem = default;
        serializer.Clear();
        return true;
    }

    private void EnsureHandAnchor()
    {
        if (playerCamera == null)
        {
            return;
        }

        Transform existing = playerCamera.transform.Find("HandAnchor");
        if (existing != null)
        {
            handAnchor = existing;
            return;
        }

        GameObject anchor = new GameObject("HandAnchor");
        handAnchor = anchor.transform;
        handAnchor.SetParent(playerCamera.transform, false);
        handAnchor.localPosition = handAnchorLocalPosition;
        handAnchor.localEulerAngles = handAnchorLocalEuler;
    }
}
