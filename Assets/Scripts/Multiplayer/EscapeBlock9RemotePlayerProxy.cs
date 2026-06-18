using UnityEngine;

[DisallowMultipleComponent]
public sealed class EscapeBlock9RemotePlayerProxy : MonoBehaviour
{
    private const float PositionLerpSpeed = 10f;
    private const float RotationLerpSpeed = 12f;
    private const float NameplateVisibleDistance = 4f;
    private const float NameplateVisibleDistanceSqr = NameplateVisibleDistance * NameplateVisibleDistance;

    private static readonly Color BodyColor = new Color(0.34f, 0.02f, 0.02f, 1f);
    private static readonly Color DownBodyColor = new Color(0.16f, 0.01f, 0.01f, 1f);
    private static readonly Color NameplateColor = new Color(1f, 0.62f, 0.58f, 1f);

    private Transform bodyTransform;
    private Transform nameplateTransform;
    private TextMesh nameplateText;
    private Renderer bodyRenderer;
    private string displayName;
    private Vector3 targetPosition;
    private Quaternion targetRotation = Quaternion.identity;

    public string PlayerId { get; private set; }
    public int UserId { get; private set; }
    public int Slot { get; private set; }
    public int CurrentHealth { get; private set; } = 100;
    public int MaxHealth { get; private set; } = 100;
    public bool IsDead { get; private set; }

    public void Initialize(string playerId, int userId, int slot, string displayName, Vector3 startPosition, Quaternion startRotation)
    {
        PlayerId = playerId ?? string.Empty;
        UserId = userId;
        Slot = slot;
        EnsureVisuals();
        SetDisplayName(displayName);
        CurrentHealth = 100;
        MaxHealth = 100;
        IsDead = false;
        RefreshPlayerVisuals();
        transform.position = startPosition;
        transform.rotation = startRotation;
        targetPosition = startPosition;
        targetRotation = startRotation;
    }

    public void SetDisplayName(string displayName)
    {
        if (nameplateText == null)
        {
            EnsureVisuals();
        }

        this.displayName = string.IsNullOrWhiteSpace(displayName) ? $"Player {Slot + 1}" : displayName;
        RefreshPlayerVisuals();
    }

    public void ApplyNetworkState(MultiplayerPlayerStateDto state)
    {
        if (state == null)
        {
            return;
        }

        targetPosition = MultiplayerJson.ArrayToVector(state.position);
        targetRotation = Quaternion.Euler(MultiplayerJson.ArrayToVector(state.rotation));
        MaxHealth = Mathf.Max(1, state.maxHealth);
        CurrentHealth = Mathf.Clamp(state.currentHealth, 0, MaxHealth);
        IsDead = state.isDead || CurrentHealth <= 0;
        RefreshPlayerVisuals();
    }

    private void RefreshPlayerVisuals()
    {
        if (nameplateText != null)
        {
            string nameText = string.IsNullOrWhiteSpace(displayName) ? $"Player {Slot + 1}" : displayName;
            nameplateText.text = nameText;
            nameplateText.color = NameplateColor;
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = IsDead ? DownBodyColor : BodyColor;
        }
    }

    private void Update()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, 1f - Mathf.Exp(-PositionLerpSpeed * Time.deltaTime));
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-RotationLerpSpeed * Time.deltaTime));

        if (nameplateTransform != null && Camera.main != null)
        {
            Vector3 dir = nameplateTransform.position - Camera.main.transform.position;
            bool visible = dir.sqrMagnitude <= NameplateVisibleDistanceSqr;
            if (nameplateTransform.gameObject.activeSelf != visible)
            {
                nameplateTransform.gameObject.SetActive(visible);
            }

            if (visible && dir.sqrMagnitude > 0.001f)
            {
                nameplateTransform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }
    }

    private void EnsureVisuals()
    {
        if (bodyTransform == null)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.75f, 0.9f, 0.75f);
            Collider collider = body.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null && Application.isPlaying)
            {
                bodyRenderer.material.color = BodyColor;
            }

            bodyTransform = body.transform;
        }

        if (nameplateTransform == null)
        {
            GameObject nameplate = new GameObject("Nameplate");
            nameplateTransform = nameplate.transform;
            nameplateTransform.SetParent(transform, false);
            nameplateTransform.localPosition = new Vector3(0f, 2.15f, 0f);
            nameplateText = nameplate.AddComponent<TextMesh>();
            nameplateText.anchor = TextAnchor.MiddleCenter;
            nameplateText.alignment = TextAlignment.Center;
            nameplateText.characterSize = 0.12f;
            nameplateText.fontSize = 52;
            nameplateText.color = NameplateColor;
            nameplateText.text = "Remote Player";
            nameplate.SetActive(false);
        }

        RefreshPlayerVisuals();
    }
}
