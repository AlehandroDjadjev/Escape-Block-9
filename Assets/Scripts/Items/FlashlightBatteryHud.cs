using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(SingleItemInventory))]
public class FlashlightBatteryHud : MonoBehaviour
{
    private SingleItemInventory inventory;
    private CanvasGroup canvasGroup;
    private RectTransform fillRect;
    private Text labelText;

    private void Awake()
    {
        inventory = GetComponent<SingleItemInventory>();
        EnsureUi();
    }

    private void Update()
    {
        if (inventory == null || inventory.HeldPickup == null)
        {
            SetVisible(false);
            return;
        }

        FlashlightItem flashlight = inventory.HeldPickup.GetComponent<FlashlightItem>();
        if (flashlight == null)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        float normalized = flashlight.BatteryNormalized;
        if (fillRect != null)
        {
            fillRect.anchorMax = new Vector2(normalized, 1f);
        }

        if (labelText != null)
        {
            int percent = Mathf.RoundToInt(normalized * 100f);
            string state = flashlight.IsOn ? "ON" : "OFF";
            labelText.text = $"FLASHLIGHT {state}  {percent}%";
        }
    }

    private void EnsureUi()
    {
        if (canvasGroup != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("FlashlightBatteryCanvas");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3200;
        canvasObject.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasObject.AddComponent<CanvasGroup>();

        GameObject root = new GameObject("BatteryHud");
        root.transform.SetParent(canvasObject.transform, false);
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -18f);
        rootRect.sizeDelta = new Vector2(320f, 52f);

        Image background = root.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.62f);

        GameObject barBg = new GameObject("BarBackground");
        barBg.transform.SetParent(root.transform, false);
        RectTransform barBgRect = barBg.AddComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0.08f, 0.2f);
        barBgRect.anchorMax = new Vector2(0.92f, 0.52f);
        barBgRect.offsetMin = Vector2.zero;
        barBgRect.offsetMax = Vector2.zero;
        Image barBgImage = barBg.AddComponent<Image>();
        barBgImage.color = new Color(0.14f, 0.14f, 0.14f, 0.95f);

        GameObject fill = new GameObject("BarFill");
        fill.transform.SetParent(barBg.transform, false);
        fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.93f, 0.88f, 0.42f, 1f);

        GameObject label = new GameObject("BatteryLabel");
        label.transform.SetParent(root.transform, false);
        RectTransform labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.1f, 0.56f);
        labelRect.anchorMax = new Vector2(0.9f, 0.95f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        labelText = label.AddComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 18;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.text = "FLASHLIGHT";

        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }
}
