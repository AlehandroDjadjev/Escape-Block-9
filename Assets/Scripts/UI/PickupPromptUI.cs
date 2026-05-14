using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class PickupPromptUI : MonoBehaviour
{
    [SerializeField] private float fadeSpeed = 8f;

    private CanvasGroup canvasGroup;
    private Text promptText;
    private Button pickupButton;
    private Action onPickupClicked;
    private bool visibleTarget;

    public void Show(string keyLabel, string pickupText, Action onClick)
    {
        EnsureUiCreated();
        onPickupClicked = onClick;
        promptText.text = $"[{keyLabel}] {pickupText}";
        visibleTarget = true;
        pickupButton.interactable = true;
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
