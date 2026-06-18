using UnityEngine;

[DisallowMultipleComponent]
public sealed class EscapeBlock9RemotePlayerProxy : MonoBehaviour
{
    private const float PositionLerpSpeed = 10f;
    private const float RotationLerpSpeed = 12f;

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
        RefreshHealthVisuals();
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
        RefreshHealthVisuals();
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
        RefreshHealthVisuals();
    }

    private void RefreshHealthVisuals()
    {
        if (nameplateText != null)
        {
            string nameText = string.IsNullOrWhiteSpace(displayName) ? $"Player {Slot + 1}" : displayName;
            nameplateText.text = IsDead ? $"{nameText}\nDOWN" : $"{nameText}\nHP {CurrentHealth}/{MaxHealth}";
            nameplateText.color = IsDead
                ? new Color(1f, 0.36f, 0.32f, 1f)
                : new Color(0.92f, 0.95f, 1f, 1f);
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = IsDead
                ? new Color(0.42f, 0.1f, 0.1f, 1f)
                : new Color(0.23f, 0.67f, 0.98f, 1f);
        }
    }

    private void Update()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, 1f - Mathf.Exp(-PositionLerpSpeed * Time.deltaTime));
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-RotationLerpSpeed * Time.deltaTime));

        if (nameplateTransform != null && Camera.main != null)
        {
            Vector3 dir = nameplateTransform.position - Camera.main.transform.position;
            if (dir.sqrMagnitude > 0.001f)
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
                bodyRenderer.material.color = new Color(0.23f, 0.67f, 0.98f, 1f);
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
            nameplateText.color = new Color(0.92f, 0.95f, 1f, 1f);
            nameplateText.text = "Remote Player";
        }

        RefreshHealthVisuals();
    }
}
