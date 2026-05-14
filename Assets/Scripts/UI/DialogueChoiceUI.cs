using System;
using UnityEngine;
using UnityEngine.UI;

public class DialogueChoiceUI : MonoBehaviour
{
    [SerializeField] private float fadeSpeed = 10f;

    private CanvasGroup canvasGroup;
    private RectTransform choicesRoot;
    private Action<int> onChoiceSelected;
    private bool visibleTarget;

    public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.95f;

    public void Show(DialogueChoiceData[] choices, Action<int> onSelected)
    {
        EnsureUi();
        ClearChoices();

        onChoiceSelected = onSelected;
        if (choices == null || choices.Length == 0)
        {
            Hide();
            return;
        }

        for (int i = 0; i < choices.Length; i++)
        {
            DialogueChoiceData choice = choices[i];
            if (choice == null || string.IsNullOrWhiteSpace(choice.text))
            {
                continue;
            }

            CreateChoiceButton(i, choice.text.Trim());
        }

        visibleTarget = true;
    }

    public void Hide()
    {
        if (canvasGroup == null)
        {
            return;
        }

        visibleTarget = false;
        onChoiceSelected = null;
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

    private void EnsureUi()
    {
        if (canvasGroup != null)
        {
            return;
        }

        GameObject canvasObj = new GameObject("DialogueChoiceCanvas");
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3200;
        canvasObj.AddComponent<GraphicRaycaster>();

        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject panelObj = new GameObject("ChoicePanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.7f);

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.2f);
        panelRect.anchorMax = new Vector2(0.5f, 0.2f);
        panelRect.sizeDelta = new Vector2(1000f, 130f);
        panelRect.anchoredPosition = Vector2.zero;

        GameObject rootObj = new GameObject("ChoicesRoot");
        rootObj.transform.SetParent(panelObj.transform, false);
        choicesRoot = rootObj.AddComponent<RectTransform>();
        choicesRoot.anchorMin = new Vector2(0f, 0f);
        choicesRoot.anchorMax = new Vector2(1f, 1f);
        choicesRoot.offsetMin = new Vector2(16f, 16f);
        choicesRoot.offsetMax = new Vector2(-16f, -16f);

        HorizontalLayoutGroup layout = rootObj.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 12f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = rootObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    private void CreateChoiceButton(int index, string text)
    {
        GameObject buttonObj = new GameObject($"Choice_{index + 1}");
        buttonObj.transform.SetParent(choicesRoot, false);

        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(240f, 84f);

        LayoutElement layoutElement = buttonObj.AddComponent<LayoutElement>();
        layoutElement.minHeight = 84f;
        layoutElement.preferredHeight = 84f;
        layoutElement.preferredWidth = 240f;
        layoutElement.flexibleWidth = 1f;

        Image image = buttonObj.AddComponent<Image>();
        image.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onChoiceSelected?.Invoke(index));
        buttonObj.AddComponent<ChoiceHoverAnimation>();

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.1f, 0.1f, 0.1f, 0.92f);
        colors.highlightedColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(buttonObj.transform, false);
        RectTransform txtRect = txtObj.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(14f, 8f);
        txtRect.offsetMax = new Vector2(-14f, -8f);

        Text label = txtObj.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 24;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.text = text;
    }

    private void ClearChoices()
    {
        if (choicesRoot == null)
        {
            return;
        }

        for (int i = choicesRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(choicesRoot.GetChild(i).gameObject);
        }
    }
}
