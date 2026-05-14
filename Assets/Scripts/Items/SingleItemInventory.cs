using UnityEngine;

[RequireComponent(typeof(ItemSerializer))]
public class SingleItemInventory : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Vector3 handAnchorLocalPosition = new Vector3(0.3f, -0.25f, 0.65f);
    [SerializeField] private Vector3 handAnchorLocalEuler = Vector3.zero;

    private Transform handAnchor;
    private ItemSerializer serializer;

    public bool HasItem { get; private set; }
    public SerializedItemData CurrentItem { get; private set; }
    public Transform HandAnchor => handAnchor;

    private void Awake()
    {
        serializer = GetComponent<ItemSerializer>();

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        EnsureHandAnchor();
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

        HasItem = true;
        serializer.Save(CurrentItem);
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
