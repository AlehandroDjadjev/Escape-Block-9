using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class PickupPromptUI : MonoBehaviour
{
    [SerializeField] private float fadeSpeed = 8f;

    private static Sprite circleSprite;
    private CanvasGroup canvasGroup;
    private Text promptText;
    private Button pickupButton;
    private Image progressBackgroundImage;
    private Image progressFillImage;
    private Action onPickupClicked;
    private bool visibleTarget;

    public void Show(string keyLabel, string pickupText, Action onClick)
    {
        EnsureUiCreated();
        onPickupClicked = onClick;
        promptText.text = $"[{keyLabel}] {pickupText}";
        visibleTarget = true;
        pickupButton.interactable = true;
        SetProgressVisible(false);
    }

    public void ShowStatus(string keyLabel, string pickupText)
    {
        EnsureUiCreated();
        onPickupClicked = null;
        promptText.text = $"[{keyLabel}] {pickupText}";
        visibleTarget = true;
        pickupButton.interactable = false;
        SetProgressVisible(false);
    }

    public void ShowProgress(string keyLabel, string pickupText, float progress01)
    {
        EnsureUiCreated();
        onPickupClicked = null;
        promptText.text = $"[{keyLabel}] {pickupText}";
        visibleTarget = true;
        pickupButton.interactable = false;
        SetProgressVisible(true);
        SetProgress(progress01);
    }

    public void Hide()
    {
        if (canvasGroup == null)
        {
            return;
        }

        visibleTarget = false;
        pickupButton.interactable = false;
        onPickupClicked = null;
        SetProgressVisible(false);
    }

    private void Update()
    {
        if (canvasGroup == null)
        {
            return;
        }

        float target = visibleTarget ? 1f : 0f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, fadeSpeed * Time.deltaTime);
        canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.95f;
        canvasGroup.interactable = canvasGroup.blocksRaycasts;
    }

    private void EnsureUiCreated()
    {
        if (canvasGroup != null)
        {
            return;
        }

        EnsureEventSystem();

        GameObject canvasObject = new GameObject("PickupCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3000;
        canvasObject.AddComponent<GraphicRaycaster>();

        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject buttonObject = new GameObject("PickupButton");
        buttonObject.transform.SetParent(canvasObject.transform, false);
        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.18f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.18f);
        buttonRect.sizeDelta = new Vector2(420f, 64f);
        buttonRect.anchoredPosition = Vector2.zero;

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0f, 0f, 0f, 0.72f);

        pickupButton = buttonObject.AddComponent<Button>();
        pickupButton.targetGraphic = buttonImage;
        pickupButton.onClick.AddListener(() => onPickupClicked?.Invoke());

        GameObject textObject = new GameObject("PickupText");
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 8f);
        textRect.offsetMax = new Vector2(-16f, -8f);

        promptText = textObject.AddComponent<Text>();
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.color = Color.white;
        promptText.fontSize = 24;
        promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        promptText.text = string.Empty;

        Sprite sprite = GetOrCreateCircleSprite();

        GameObject progressBackgroundObject = new GameObject("ProgressBackground");
        progressBackgroundObject.transform.SetParent(buttonObject.transform, false);
        RectTransform progressBackgroundRect = progressBackgroundObject.AddComponent<RectTransform>();
        progressBackgroundRect.anchorMin = new Vector2(0f, 0.5f);
        progressBackgroundRect.anchorMax = new Vector2(0f, 0.5f);
        progressBackgroundRect.pivot = new Vector2(0.5f, 0.5f);
        progressBackgroundRect.anchoredPosition = new Vector2(34f, 0f);
        progressBackgroundRect.sizeDelta = new Vector2(34f, 34f);
        progressBackgroundImage = progressBackgroundObject.AddComponent<Image>();
        progressBackgroundImage.sprite = sprite;
        progressBackgroundImage.color = new Color(1f, 1f, 1f, 0.12f);

        GameObject progressFillObject = new GameObject("ProgressFill");
        progressFillObject.transform.SetParent(progressBackgroundObject.transform, false);
        RectTransform progressFillRect = progressFillObject.AddComponent<RectTransform>();
        progressFillRect.anchorMin = Vector2.zero;
        progressFillRect.anchorMax = Vector2.one;
        progressFillRect.offsetMin = Vector2.zero;
        progressFillRect.offsetMax = Vector2.zero;
        progressFillImage = progressFillObject.AddComponent<Image>();
        progressFillImage.sprite = sprite;
        progressFillImage.color = new Color(0.22f, 0.78f, 1f, 0.95f);
        progressFillImage.type = Image.Type.Filled;
        progressFillImage.fillMethod = Image.FillMethod.Radial360;
        progressFillImage.fillOrigin = (int)Image.Origin360.Top;
        progressFillImage.fillClockwise = false;
        progressFillImage.fillAmount = 0f;
        SetProgressVisible(false);
    }

    private void SetProgress(float progress01)
    {
        if (progressFillImage == null)
        {
            return;
        }

        progressFillImage.fillAmount = Mathf.Clamp01(progress01);
    }

    private void SetProgressVisible(bool visible)
    {
        if (progressBackgroundImage != null)
        {
            progressBackgroundImage.enabled = visible;
        }

        if (progressFillImage != null)
        {
            progressFillImage.enabled = visible;
        }
    }

    private static Sprite GetOrCreateCircleSprite()
    {
        if (circleSprite != null)
        {
            return circleSprite;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        texture.name = "PickupPromptCircle";
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;
        Color clear = new Color(1f, 1f, 1f, 0f);
        Color solid = Color.white;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? solid : clear);
            }
        }

        texture.Apply();
        circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return circleSprite;
    }

    private static void EnsureEventSystem()
    {
        EventSystem existing = FindAnyObjectByType<EventSystem>();
        if (existing != null)
        {
            return;
        }

        GameObject eventSystemObj = new GameObject("EventSystem");
        eventSystemObj.AddComponent<EventSystem>();
        eventSystemObj.AddComponent<InputSystemUIInputModule>();
    }
}
