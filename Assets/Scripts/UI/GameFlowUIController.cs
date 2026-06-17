using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;

public class GameFlowUIController : MonoBehaviour
{
    private static GameFlowUIController instance;

    private enum FlowState
    {
        Playing = 0,
        Paused = 1,
        GameOver = 2,
        Victory = 3
    }

    [Header("References")]
    [SerializeField] private FirstPersonController firstPersonController;
    [SerializeField] private PlayerItemInteractor itemInteractor;
    [SerializeField] private PlayerEntityInteractor entityInteractor;

    private FlowState currentState = FlowState.Playing;
    private Canvas rootCanvas;
    private GameObject pausePanel;
    private GameObject gameOverPanel;
    private GameObject victoryPanel;
    private GameObject hudPanel;
    private Text runTimerText;
    private bool initialized;
    private bool trackRunTimer;
    private float currentRunElapsedSeconds;

    public static bool NotifyPlayerEscaped()
    {
        if (instance == null)
        {
            Debug.Log("Player escaped.");
            return false;
        }

        instance.HandleEscapeSuccess();
        return true;
    }

    private void Awake()
    {
        if (EscapeBlock9MultiplayerRuntime.ShouldSuppressBuiltInUi)
        {
            enabled = false;
            return;
        }

        EnsureInitialized();
        EnterPlaying(true);
    }

    public void BeginSingleplayerTestSession()
    {
        enabled = true;
        EnsureInitialized();
        EnterPlaying(true);
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        instance = this;

        if (firstPersonController == null)
        {
            firstPersonController = GetComponent<FirstPersonController>();
        }

        if (itemInteractor == null)
        {
            itemInteractor = GetComponent<PlayerItemInteractor>();
        }

        if (entityInteractor == null)
        {
            entityInteractor = GetComponent<PlayerEntityInteractor>();
        }

        BuildUi();
        EnsureEventSystem();
        initialized = true;
    }

    private void OnEnable()
    {
        EntityCinemachineDollyFollower.PlayerCaught += OnPlayerCaught;
    }

    private void OnDisable()
    {
        EntityCinemachineDollyFollower.PlayerCaught -= OnPlayerCaught;
    }

    private void Update()
    {
        EnforceCursorForState();

        bool escapePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (currentState == FlowState.Playing && escapePressed)
        {
            EnterPaused();
        }
        else if (currentState == FlowState.Paused && escapePressed)
        {
            EnterPlaying();
        }
    }

    private void OnPlayerCaught(EntityCinemachineDollyFollower _)
    {
        if (currentState == FlowState.Playing)
        {
            EnterGameOver();
        }
    }

    private void BuildUi()
    {
        GameObject canvasObj = new GameObject("GameFlowCanvas");
        rootCanvas = canvasObj.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 5000;
        canvasObj.AddComponent<GraphicRaycaster>();
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        gameOverPanel = CreateMenuPanel(canvasObj.transform, "GameOverPanel", font, "DETENTION WON", "You were caught. HR has filed you under: educational casualty.");
        CreateButton(gameOverPanel.transform, "RetryButton", "Try Again", font, new Vector2(0.5f, 0.45f), ReloadScene);
        CreateButton(gameOverPanel.transform, "GameOverExitButton", "Quit", font, new Vector2(0.5f, 0.34f), OnExitPressed);

        victoryPanel = CreateMenuPanel(canvasObj.transform, "VictoryPanel", font, "YOU ESCAPED", "Congratulations. The school is disappointed in your lack of team spirit.");
        CreateButton(victoryPanel.transform, "VictoryLobbyButton", "Back To Multiplayer", font, new Vector2(0.5f, 0.45f), OnBackToMultiplayerPressed);
        CreateButton(victoryPanel.transform, "VictoryExitButton", "Quit", font, new Vector2(0.5f, 0.34f), OnExitPressed);

        pausePanel = CreateMenuPanel(canvasObj.transform, "PausePanel", font, "PAUSED", "A short break before the corridor starts making eye contact again.");
        CreateButton(pausePanel.transform, "ResumeButton", "Resume", font, new Vector2(0.5f, 0.48f), () => EnterPlaying());
        CreateButton(pausePanel.transform, "PauseRetryButton", "Restart Run", font, new Vector2(0.5f, 0.37f), ReloadScene);
        CreateButton(pausePanel.transform, "PauseExitButton", "Quit", font, new Vector2(0.5f, 0.26f), OnExitPressed);

        hudPanel = new GameObject("HudPanel");
        hudPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform hudRect = hudPanel.AddComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0f, 1f);
        hudRect.anchorMax = new Vector2(0f, 1f);
        hudRect.pivot = new Vector2(0f, 1f);
        hudRect.anchoredPosition = new Vector2(18f, -18f);
        hudRect.sizeDelta = new Vector2(280f, 100f);
        runTimerText = CreateText(hudPanel.transform, "RunTimer", "Time 00:00.0", font, 22, new Vector2(0f, 1f), new Vector2(220f, 32f), Color.white);
        RectTransform timerRect = runTimerText.rectTransform;
        timerRect.pivot = new Vector2(0f, 1f);
        timerRect.anchoredPosition = new Vector2(0f, -10f);
    }

    private static GameObject CreateMenuPanel(Transform parent, string name, Font font, string title, string subtitle)
    {
        GameObject panel = CreateFullScreenPanel(parent, name, new Color(0.015f, 0.012f, 0.012f, 1f));
        CreateAnchoredPanel(panel.transform, "InkBandTop", new Color(0.28f, 0.015f, 0.018f, 1f), new Vector2(0.5f, 0.88f), new Vector2(0f, 0f), new Vector2(2100f, 150f));
        CreateAnchoredPanel(panel.transform, "SicklyNotice", new Color(0.88f, 0.72f, 0.16f, 0.95f), new Vector2(0.5f, 0.16f), new Vector2(0f, 0f), new Vector2(980f, 16f));
        CreateAnchoredPanel(panel.transform, "MainCardShadow", new Color(0f, 0f, 0f, 0.78f), new Vector2(0.5f, 0.52f), new Vector2(14f, -16f), new Vector2(860f, 430f));
        CreateAnchoredPanel(panel.transform, "MainCard", new Color(0.05f, 0.055f, 0.062f, 1f), new Vector2(0.5f, 0.53f), Vector2.zero, new Vector2(860f, 430f));
        CreateAnchoredPanel(panel.transform, "CardStripe", new Color(0.58f, 0.025f, 0.035f, 1f), new Vector2(0.5f, 0.69f), Vector2.zero, new Vector2(860f, 18f));

        Text titleText = CreateText(panel.transform, "Title", title, font, 64, new Vector2(0.5f, 0.6f), new Vector2(820f, 96f), new Color(1f, 0.96f, 0.82f, 1f));
        AddTextOutline(titleText, new Color(0.55f, 0f, 0.02f, 1f), new Vector2(3f, -3f));
        CreateText(panel.transform, "Subtitle", subtitle, font, 23, new Vector2(0.5f, 0.53f), new Vector2(760f, 70f), new Color(0.78f, 0.84f, 0.82f, 1f));
        CreateText(panel.transform, "FooterJoke", "Block 9 thanks you for your cooperation. It has misplaced the paperwork.", font, 18, new Vector2(0.5f, 0.18f), new Vector2(920f, 36f), new Color(0.72f, 0.68f, 0.58f, 1f));
        return panel;
    }

    private static GameObject CreateFullScreenPanel(Transform parent, string name, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = panel.AddComponent<Image>();
        image.color = color;
        return panel;
    }

    private static GameObject CreateAnchoredPanel(Transform parent, string name, Color color, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = panel.AddComponent<Image>();
        image.color = color;
        return panel;
    }

    private static void EnsureEventSystem()
    {
        EventSystem existing = FindAnyObjectByType<EventSystem>();
        if (existing != null)
        {
            if (existing.GetComponent<InputSystemUIInputModule>() == null)
            {
                existing.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private static Text CreateText(Transform parent, string name, string value, Font font, int size, Vector2 anchor, Vector2 dims)
    {
        return CreateText(parent, name, value, font, size, anchor, dims, Color.white);
    }

    private static Text CreateText(Transform parent, string name, string value, Font font, int size, Vector2 anchor, Vector2 dims, Color color)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = dims;
        Text text = textObj.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = value;
        return text;
    }

    private static void AddTextOutline(Text text, Color color, Vector2 distance)
    {
        if (text == null)
        {
            return;
        }

        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    private static void CreateButton(Transform parent, string name, string label, Font font, Vector2 anchor, UnityEngine.Events.UnityAction callback)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent, false);
        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300f, 62f);

        Image image = buttonObj.AddComponent<Image>();
        image.color = new Color(0.13f, 0.025f, 0.03f, 1f);

        Button button = buttonObj.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.13f, 0.025f, 0.03f, 1f);
        colors.highlightedColor = new Color(0.58f, 0.06f, 0.07f, 1f);
        colors.pressedColor = new Color(0.78f, 0.46f, 0.08f, 1f);
        button.colors = colors;
        button.onClick.AddListener(callback);

        Text labelText = CreateText(buttonObj.transform, "Label", label, font, 28, new Vector2(0.5f, 0.5f), new Vector2(260f, 44f), new Color(1f, 0.96f, 0.82f, 1f));
        AddTextOutline(labelText, new Color(0f, 0f, 0f, 0.7f), new Vector2(1.5f, -1.5f));
    }

    private void SetPanels(bool pause, bool gameOver, bool hud)
    {
        if (pausePanel != null) pausePanel.SetActive(pause);
        if (gameOverPanel != null) gameOverPanel.SetActive(gameOver);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(hud);
    }

    private void SetGameplayScriptsEnabled(bool enabled)
    {
        if (firstPersonController != null) firstPersonController.enabled = enabled;
        if (itemInteractor != null) itemInteractor.enabled = enabled;
        if (entityInteractor != null) entityInteractor.enabled = enabled;
    }

    private static void SetCursorUiMode(bool uiMode)
    {
        Cursor.lockState = uiMode ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = uiMode;
    }

    private void EnforceCursorForState()
    {
        bool dialogueChoiceUiMode =
            currentState == FlowState.Playing &&
            entityInteractor != null &&
            entityInteractor.IsChoiceModeActive;

        bool uiMode = currentState != FlowState.Playing || dialogueChoiceUiMode;
        CursorLockMode targetLock = uiMode ? CursorLockMode.None : CursorLockMode.Locked;
        bool targetVisible = uiMode;

        if (Cursor.lockState != targetLock)
        {
            Cursor.lockState = targetLock;
        }

        if (Cursor.visible != targetVisible)
        {
            Cursor.visible = targetVisible;
        }
    }

    private void EnterPlaying(bool resetTimer = false)
    {
        if (resetTimer)
        {
            currentRunElapsedSeconds = 0f;
            UpdateRunTimerLabel();
        }

        currentState = FlowState.Playing;
        Time.timeScale = 1f;
        trackRunTimer = true;
        SetPanels(pause: false, gameOver: false, hud: true);
        SetGameplayScriptsEnabled(true);
        SetCursorUiMode(false);
    }

    private void EnterPaused()
    {
        if (currentState != FlowState.Playing)
        {
            return;
        }

        currentState = FlowState.Paused;
        Time.timeScale = 0f;
        trackRunTimer = false;
        SetPanels(pause: true, gameOver: false, hud: false);
        SetGameplayScriptsEnabled(false);
        SetCursorUiMode(true);
    }

    private void EnterGameOver()
    {
        currentState = FlowState.GameOver;
        Time.timeScale = 0f;
        trackRunTimer = false;
        SetPanels(pause: false, gameOver: true, hud: false);
        SetGameplayScriptsEnabled(false);
        SetCursorUiMode(true);
    }

    private void EnterVictory()
    {
        currentState = FlowState.Victory;
        Time.timeScale = 0f;
        trackRunTimer = false;
        SetPanels(pause: false, gameOver: false, hud: false);
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }
        SetGameplayScriptsEnabled(false);
        SetCursorUiMode(true);
    }

    private static void OnExitPressed()
    {
        Application.Quit();
    }

    private void ReloadScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void HandleEscapeSuccess()
    {
        if (ReturnToMultiplayerMenu())
        {
            return;
        }

        EnterVictory();
    }

    private static bool ReturnToMultiplayerMenu()
    {
        return EscapeBlock9MultiplayerRuntime.NotifyPlayerEscaped();
    }

    private static void OnBackToMultiplayerPressed()
    {
        ReturnToMultiplayerMenu();
    }

    private void UpdateRunTimerLabel()
    {
        if (runTimerText != null)
        {
            runTimerText.text = $"Time {FormatElapsedTime(currentRunElapsedSeconds)}";
        }
    }

    private static string FormatElapsedTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float remainingSeconds = seconds - (minutes * 60f);
        return $"{minutes:00}:{remainingSeconds:00.0}";
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void LateUpdate()
    {
        if (!trackRunTimer || currentState != FlowState.Playing)
        {
            return;
        }

        currentRunElapsedSeconds += Time.unscaledDeltaTime;
        UpdateRunTimerLabel();
    }
}
