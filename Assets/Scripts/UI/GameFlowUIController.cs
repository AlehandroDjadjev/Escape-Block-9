using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;

public class GameFlowUIController : MonoBehaviour
{
    private enum FlowState
    {
        MainMenu = 0,
        Playing = 1,
        Paused = 2,
        GameOver = 3
    }

    [Header("Branding")]
    [SerializeField] private string gameTitle = "Escape Block 9";

    [Header("References")]
    [SerializeField] private FirstPersonController firstPersonController;
    [SerializeField] private PlayerItemInteractor itemInteractor;
    [SerializeField] private PlayerEntityInteractor entityInteractor;

    private FlowState currentState = FlowState.MainMenu;
    private Canvas rootCanvas;
    private GameObject mainMenuPanel;
    private GameObject pausePanel;
    private GameObject gameOverPanel;
    private GameObject hudPanel;

    private void Awake()
    {
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
        EnterMainMenu();
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

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        mainMenuPanel = CreateFullScreenPanel(canvasObj.transform, "MainMenuPanel", new Color(0f, 0f, 0f, 0.9f));
        CreateText(mainMenuPanel.transform, "Title", gameTitle, font, 62, new Vector2(0.5f, 0.78f), new Vector2(700f, 100f));
        CreateButton(mainMenuPanel.transform, "PlayButton", "Play", font, new Vector2(0.5f, 0.56f), OnPlayPressed);
        CreateButton(mainMenuPanel.transform, "ExitButton", "Exit", font, new Vector2(0.5f, 0.45f), OnExitPressed);

        gameOverPanel = CreateFullScreenPanel(canvasObj.transform, "GameOverPanel", new Color(0f, 0f, 0f, 0.9f));
        CreateText(gameOverPanel.transform, "GameOverTitle", "Game Over", font, 58, new Vector2(0.5f, 0.75f), new Vector2(700f, 100f));
        CreateButton(gameOverPanel.transform, "RetryButton", "Retry", font, new Vector2(0.5f, 0.56f), ReloadScene);
        CreateButton(gameOverPanel.transform, "GameOverMainMenuButton", "Main Menu", font, new Vector2(0.5f, 0.45f), ReloadScene);
        CreateButton(gameOverPanel.transform, "GameOverExitButton", "Exit", font, new Vector2(0.5f, 0.34f), OnExitPressed);

        pausePanel = CreateFullScreenPanel(canvasObj.transform, "PausePanel", new Color(0f, 0f, 0f, 0.82f));
        CreateText(pausePanel.transform, "PauseTitle", "Paused", font, 56, new Vector2(0.5f, 0.75f), new Vector2(700f, 100f));
        CreateButton(pausePanel.transform, "ResumeButton", "Resume", font, new Vector2(0.5f, 0.56f), EnterPlaying);
        CreateButton(pausePanel.transform, "PauseMainMenuButton", "Main Menu", font, new Vector2(0.5f, 0.45f), ReloadScene);
        CreateButton(pausePanel.transform, "PauseExitButton", "Exit", font, new Vector2(0.5f, 0.34f), OnExitPressed);

        hudPanel = new GameObject("HudPanel");
        hudPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform hudRect = hudPanel.AddComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0f, 1f);
        hudRect.anchorMax = new Vector2(0f, 1f);
        hudRect.pivot = new Vector2(0f, 1f);
        hudRect.anchoredPosition = new Vector2(18f, -18f);
        hudRect.sizeDelta = new Vector2(220f, 60f);
        CreateHudButton(hudPanel.transform, "MainMenuHudButton", "Return To Menu", font, new Vector2(100f, -24f), ReloadScene);
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

    private static void CreateText(Transform parent, string name, string value, Font font, int size, Vector2 anchor, Vector2 dims)
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
        text.color = Color.white;
        text.text = value;
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
        image.color = new Color(0.12f, 0.12f, 0.12f, 0.94f);

        Button button = buttonObj.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.12f, 0.12f, 0.94f);
        colors.highlightedColor = new Color(0.2f, 0.2f, 0.2f, 0.98f);
        colors.pressedColor = new Color(0.28f, 0.28f, 0.28f, 1f);
        button.colors = colors;
        button.onClick.AddListener(callback);

        CreateText(buttonObj.transform, "Label", label, font, 30, new Vector2(0.5f, 0.5f), new Vector2(260f, 44f));
    }

    private static void CreateHudButton(Transform parent, string name, string label, Font font, Vector2 anchoredPos, UnityEngine.Events.UnityAction callback)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent, false);
        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(180f, 46f);

        Image image = buttonObj.AddComponent<Image>();
        image.color = new Color(0.07f, 0.07f, 0.07f, 0.85f);

        Button button = buttonObj.AddComponent<Button>();
        button.onClick.AddListener(callback);
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.14f, 0.14f, 0.14f, 0.95f);
        colors.pressedColor = new Color(0.24f, 0.24f, 0.24f, 0.97f);
        button.colors = colors;

        CreateText(buttonObj.transform, "Label", label, font, 22, new Vector2(0.5f, 0.5f), new Vector2(150f, 36f));
    }

    private void SetPanels(bool mainMenu, bool pause, bool gameOver, bool hud)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(mainMenu);
        if (pausePanel != null) pausePanel.SetActive(pause);
        if (gameOverPanel != null) gameOverPanel.SetActive(gameOver);
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

    private void EnterMainMenu()
    {
        currentState = FlowState.MainMenu;
        Time.timeScale = 0f;
        SetPanels(mainMenu: true, pause: false, gameOver: false, hud: false);
        SetGameplayScriptsEnabled(false);
        SetCursorUiMode(true);
    }

    private void EnterPlaying()
    {
        currentState = FlowState.Playing;
        Time.timeScale = 1f;
        SetPanels(mainMenu: false, pause: false, gameOver: false, hud: true);
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
        SetPanels(mainMenu: false, pause: true, gameOver: false, hud: false);
        SetGameplayScriptsEnabled(false);
        SetCursorUiMode(true);
    }

    private void EnterGameOver()
    {
        currentState = FlowState.GameOver;
        Time.timeScale = 0f;
        SetPanels(mainMenu: false, pause: false, gameOver: true, hud: false);
        SetGameplayScriptsEnabled(false);
        SetCursorUiMode(true);
    }

    private void OnPlayPressed()
    {
        EnterPlaying();
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
}
