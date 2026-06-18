using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using EscapeBlock9.ProcGen.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Networking;

[DefaultExecutionOrder(-1200)]
public sealed class EscapeBlock9MultiplayerRuntime : MonoBehaviour
{
    private const string DefaultServerUrl = "https://cavegame-production.up.railway.app";
    private const string DefaultMapId = "escape_block_9_facility";
    private const string SavedServerUrlKey = "eb9_multiplayer_server_url";
    private const string LobbyMusicPath = @"C:\Users\sashk\Documents\codenes";
    private const float StateSendInterval = 1f / 20f;
    private const float SharedObjectStateSendInterval = 1f / 10f;
    private const float ForcedStateSendInterval = 0.5f;
    private const float MinPositionDeltaSqr = 0.0004f;
    private const float MinRotationDelta = 1.5f;
    private const float LobbyPingInterval = 2f;
    private const float GamePingInterval = 1f;
    private const float LobbyHeartbeatTimeout = 8f;
    private const float GameHeartbeatTimeout = 5f;
    private const int MaxPlayers = 2;

    private static EscapeBlock9MultiplayerRuntime instance;

    private EscapeBlock9MultiplayerApiClient api;
    private EscapeBlock9MultiplayerSocketClient lobbySocket;
    private EscapeBlock9MultiplayerSocketClient gameSocket;

    private string authToken;
    private MultiplayerUserDto currentUser;
    private MultiplayerLobbyDto currentLobby;
    private MultiplayerLobbyMemberDto localMember;
    private MultiplayerGameStartedDto currentGameStart;

    private bool guestAuthenticated;
    private bool gameStarted;
    private bool generationReady;
    private bool generationRequested;
    private bool suppressBuiltInUi = true;

    // Setup phase: after the facility generates, both clients independently roll
    // the same role assignment (deterministic from lobbyId + mapId) and a reveal
    // panel is shown for a few seconds before gameplay is enabled. Chunk 1 covers
    // role pick + reveal; placement UI (drag-drop teachers, click key spot) is the
    // next chunk and will gate on setupPhaseComplete the same way.
    private bool setupPhaseComplete = true;
    private bool localPlayerIsKeyHider;
    private int localStateSeq;
    private float nextLobbyPingTime;
    private float nextGamePingTime;
    private float lastLobbyPingSentAt = -1f;
    private float lastLobbyPongAt = -1f;
    private float lastLobbyHeartbeatAt = -1f;
    private float lastGamePingSentAt = -1f;
    private float lastGamePongAt = -1f;
    private float lastGameHeartbeatAt = -1f;
    private float nextStateSendTime;
    private float lastStateSendTime;
    private Vector3 lastSentPosition;
    private Vector3 lastSentEulerAngles;
    private int lastSentHealth = -1;
    private int lastSentMaxHealth = -1;
    private bool lastSentDead;
    private float nextSharedObjectStateSendTime;
    private int localKeyStateSeq;
    private Vector3 lastSentKeyPosition;
    private Vector3 lastSentKeyEulerAngles;
    private bool lastSentKeyHeld;
    private string lastSentKeyHolderPlayerId;
    private readonly Dictionary<string, int> localTeacherStateSeq = new Dictionary<string, int>();

    private Canvas rootCanvas;
    private GameObject authPanel;
    private GameObject lobbyPanel;
    private GameObject overlayPanel;
    private InputField serverUrlInput;
    private InputField joinCodeInput;
    private Text authStatusText;
    private Text lobbyTitleText;
    private Text lobbyCodeText;
    private Text lobbyStatusText;
    private Text overlayStatusText;
    private GameObject roleRevealPanel;
    private Text roleRevealTitle;
    private Text roleRevealSubtitle;
    private Button roleRevealContinueButton;

    // Placement phase (chunk 2a) — Key Hider clicks on the top-down map to choose
    // where the exit key spawns. Teacher Placer just sees a waiting message in this
    // chunk; chunk 2b will add the drag-drop teacher list.
    private GameObject placementPanel;
    private RawImage placementMapImage;
    private RectTransform placementMapRect;
    private Image placementMapMarker;
    private Text placementStatusText;
    private Button placementConfirmButton;
    private Camera placementMapCamera;
    private Light placementMapLight;
    private RenderTexture placementMapTexture;
    private Vector3? localChosenKeyWorldPos;
    private Vector3? peerChosenKeyWorldPos;
    private bool localPlacementConfirmed;
    private bool peerPlacementConfirmed;
    private Bounds placementMapWorldBounds;

    // Teacher placement (chunk 2b). Items are kept in a stable alphabetical order
    // so both clients can use a positional array over the wire without sending names.
    private static readonly (string slug, string displayName)[] TeacherSlots =
    {
        ("basheva",      "Basheva"),
        ("bojkata",      "Bojkata"),
        ("direktorka",   "Direktorka"),
        ("frenski",      "Frenski"),
        ("hristov",      "Hristov"),
        ("ivanzaprqnov", "Ivan Zaprqnov"),
        ("ivazaharieva", "Iva Zaharieva"),
        ("milaneikova",  "Milaneikova"),
        ("milenSpasov",  "Milen Spasov"),
        ("tancheto",     "Tancheto"),
    };

    private GameObject placementTeacherListContent;
    private readonly GameObject[] placementTeacherListItems = new GameObject[TeacherSlots.Length];
    private readonly RawImage[] placementTeacherPortraits = new RawImage[TeacherSlots.Length];
    private readonly Vector3?[] localTeacherWorldPos = new Vector3?[TeacherSlots.Length];
    private readonly Vector3?[] peerTeacherWorldPos = new Vector3?[TeacherSlots.Length];
    private readonly Image[] placementTeacherMapMarkers = new Image[TeacherSlots.Length];
    private GameObject placementDragGhost;
    private Image placementDragGhostImage;
    private Button connectGuestButton;
    private Button createLobbyButton;
    private Button joinLobbyButton;
    private Button readyButton;
    private Button startButton;
    private Button resetButton;
    private readonly List<PlayerSlotView> slotViews = new List<PlayerSlotView>();

    private FirstPersonController localController;
    private PlayerHealth localHealth;
    private SingleItemInventory localInventory;
    private PlayerItemInteractor localItemInteractor;
    private PlayerEntityInteractor localEntityInteractor;
    private FacilityRuntimeGenerator runtimeGenerator;
    private AudioSource lobbyMusicSource;
    private Coroutine lobbyMusicRoutine;
    private readonly Dictionary<string, EscapeBlock9RemotePlayerProxy> remotePlayers = new Dictionary<string, EscapeBlock9RemotePlayerProxy>();
    private readonly Dictionary<int, MultiplayerLobbyMemberDto> lobbyMembersByUserId = new Dictionary<int, MultiplayerLobbyMemberDto>();
    private GameObject spawnedKeyObject;
    private ItemPickup spawnedKeyPickup;

    public static bool ShouldSuppressBuiltInUi => instance != null && instance.suppressBuiltInUi;
    public static bool ControlsProceduralGeneration => instance != null && instance.suppressBuiltInUi;

    public static bool NotifyPlayerEscaped()
    {
        if (instance == null)
        {
            return false;
        }

        instance.HandlePlayerEscaped();
        return true;
    }

    public static bool TryGetDesiredSpawnSlot(out int slot)
    {
        if (instance != null && instance.gameStarted && instance.localMember != null)
        {
            slot = instance.localMember.slot;
            return true;
        }

        slot = 0;
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<EscapeBlock9MultiplayerRuntime>() != null)
        {
            return;
        }

        GameObject bootstrap = new GameObject("EscapeBlock9MultiplayerRuntime");
        bootstrap.AddComponent<EscapeBlock9MultiplayerRuntime>();
        DontDestroyOnLoad(bootstrap);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        api = new EscapeBlock9MultiplayerApiClient(PlayerPrefs.GetString(SavedServerUrlKey, DefaultServerUrl), () => authToken);
        BuildUi();
        EnsureLobbyMusicSource();
        EnsureEventSystem();
        ShowAuthPanel("Connect to the hosted CaveGame backend as a guest.");
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (runtimeGenerator != null)
        {
            runtimeGenerator.GenerationCompleted -= OnRuntimeGenerationCompleted;
        }
    }

    private void Update()
    {
        lobbySocket?.Pump();
        gameSocket?.Pump();
        RefreshLocalReferences();
        EnforceLobbyAndGameState();
        PumpLobbySocketKeepAlive();
        PumpGameSocketKeepAlive();
        PumpLocalStateSync();
        PumpSharedObjectStateSync();
        PumpPlacementInput();

        if (Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame)
        {
            ResetToAuth();
        }
    }

    // While the placement panel is up and the local player is the Key Hider, a
    // left-click on the map records the world position and shows a marker on the
    // map. The Confirm button picks it up from there.
    private void PumpPlacementInput()
    {
        if (placementPanel == null || !placementPanel.activeInHierarchy) return;
        if (!localPlayerIsKeyHider || localPlacementConfirmed) return;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        if (!TryMapClickToWorld(screenPos, out Vector3 worldPos)) return;

        localChosenKeyWorldPos = worldPos;
        ShowPlacementMarkerAtScreen(screenPos);
        UpdatePlacementUiForRole();
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("MultiplayerRuntimeCanvas");
        rootCanvas = canvasObject.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 8000;
        canvasObject.AddComponent<GraphicRaycaster>();
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        DontDestroyOnLoad(canvasObject);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Color ink = new Color(0.012f, 0.01f, 0.012f, 1f);
        Color panel = new Color(0.045f, 0.05f, 0.055f, 1f);
        Color soft = new Color(0.12f, 0.12f, 0.105f, 1f);
        Color accent = new Color(0.62f, 0.035f, 0.045f, 1f);
        Color gold = new Color(0.86f, 0.66f, 0.16f, 1f);

        authPanel = CreateFullScreenPanel(rootCanvas.transform, "AuthPanel", ink);
        CreateMenuDecor(authPanel.transform);
        Text authTitle = CreateText(authPanel.transform, "AuthTitle", "ESCAPE BLOCK 9", font, 68, new Vector2(0.5f, 0.82f), new Vector2(980f, 92f), new Color(1f, 0.95f, 0.78f, 1f));
        AddTextOutline(authTitle, new Color(0.55f, 0f, 0.02f, 1f), new Vector2(3f, -3f));
        GameObject authCard = CreatePanel(authPanel.transform, "AuthCard", panel, new Vector2(0.5f, 0.47f), new Vector2(780f, 430f));
        CreateText(authCard.transform, "ServerLabel", "Backend Address", font, 21, new Vector2(0.5f, 0.8f), new Vector2(640f, 34f), new Color(0.88f, 0.8f, 0.58f, 1f));
        serverUrlInput = CreateInputField(authCard.transform, "ServerInput", font, new Vector2(0.5f, 0.63f), new Vector2(640f, 54f), api.BaseHttpUrl);
        connectGuestButton = CreateButton(authCard.transform, "ConnectGuestButton", "Claim Guest Badge", font, new Vector2(0.5f, 0.38f), new Vector2(340f, 60f), accent, OnConnectGuestPressed);
        CreateButton(authCard.transform, "SingleplayerTestButton", "Start Singleplayer Test", font, new Vector2(0.5f, 0.2f), new Vector2(340f, 54f), gold, OnSingleplayerTestPressed);
        authStatusText = CreateText(authCard.transform, "AuthStatus", string.Empty, font, 18, new Vector2(0.5f, 0.06f), new Vector2(660f, 76f), new Color(0.9f, 0.9f, 0.82f, 1f));

        lobbyPanel = CreateFullScreenPanel(rootCanvas.transform, "LobbyPanel", ink);
        CreateMenuDecor(lobbyPanel.transform);
        Text lobbyTitle = CreateText(lobbyPanel.transform, "LobbyTitle", "MULTIPLAYER", font, 60, new Vector2(0.5f, 0.88f), new Vector2(780f, 78f), new Color(1f, 0.95f, 0.78f, 1f));
        AddTextOutline(lobbyTitle, new Color(0.55f, 0f, 0.02f, 1f), new Vector2(3f, -3f));
        GameObject lobbyCard = CreatePanel(lobbyPanel.transform, "LobbyCard", panel, new Vector2(0.5f, 0.5f), new Vector2(900f, 560f));
        lobbyTitleText = CreateText(lobbyCard.transform, "LobbyHeader", "Not Connected", font, 32, new Vector2(0.5f, 0.9f), new Vector2(660f, 44f), new Color(1f, 0.95f, 0.78f, 1f));
        lobbyCodeText = CreateText(lobbyCard.transform, "LobbyCode", string.Empty, font, 21, new Vector2(0.5f, 0.82f), new Vector2(560f, 34f), new Color(0.82f, 0.85f, 0.78f, 1f));

        createLobbyButton = CreateButton(lobbyCard.transform, "CreateLobbyButton", "Create Lobby", font, new Vector2(0.27f, 0.7f), new Vector2(250f, 56f), accent, OnCreateLobbyPressed);
        joinCodeInput = CreateInputField(lobbyCard.transform, "JoinCodeInput", font, new Vector2(0.66f, 0.71f), new Vector2(190f, 52f), string.Empty);
        joinCodeInput.characterLimit = 6;
        joinLobbyButton = CreateButton(lobbyCard.transform, "JoinLobbyButton", "Join", font, new Vector2(0.86f, 0.71f), new Vector2(120f, 52f), soft, OnJoinLobbyPressed);

        CreateText(lobbyCard.transform, "PlayersHeader", "Escape Committee", font, 24, new Vector2(0.5f, 0.57f), new Vector2(280f, 34f), new Color(0.88f, 0.8f, 0.58f, 1f));
        slotViews.Add(CreateSlotView(lobbyCard.transform, font, new Vector2(0.5f, 0.43f), 1));
        slotViews.Add(CreateSlotView(lobbyCard.transform, font, new Vector2(0.5f, 0.28f), 2));

        readyButton = CreateButton(lobbyCard.transform, "ReadyButton", "Ready Up", font, new Vector2(0.32f, 0.1f), new Vector2(220f, 54f), new Color(0.15f, 0.46f, 0.25f, 1f), OnReadyPressed);
        startButton = CreateButton(lobbyCard.transform, "StartButton", "Start Run", font, new Vector2(0.68f, 0.1f), new Vector2(220f, 54f), gold, OnStartPressed);
        resetButton = CreateButton(lobbyCard.transform, "ResetButton", "Back", font, new Vector2(0.5f, -0.04f), new Vector2(140f, 42f), soft, ResetToAuth);
        lobbyStatusText = CreateText(lobbyCard.transform, "LobbyStatus", string.Empty, font, 18, new Vector2(0.5f, -0.13f), new Vector2(650f, 68f), new Color(0.9f, 0.9f, 0.82f, 1f));

        overlayPanel = CreateFullScreenPanel(rootCanvas.transform, "OverlayPanel", new Color(0f, 0f, 0f, 0.78f));
        GameObject overlayCard = CreatePanel(overlayPanel.transform, "OverlayCard", panel, new Vector2(0.5f, 0.12f), new Vector2(520f, 70f));
        overlayStatusText = CreateText(overlayCard.transform, "OverlayStatus", string.Empty, font, 18, new Vector2(0.5f, 0.5f), new Vector2(480f, 40f), Color.white);

        // Role reveal panel — shown after facility generation. Sits above the overlay
        // because it's built later (later sibling renders on top).
        roleRevealPanel = CreateFullScreenPanel(rootCanvas.transform, "RoleRevealPanel", new Color(0.04f, 0.05f, 0.08f, 0.96f));
        GameObject revealCard = CreatePanel(roleRevealPanel.transform, "RoleRevealCard", panel, new Vector2(0.5f, 0.5f), new Vector2(760f, 320f));
        CreateText(revealCard.transform, "RoleRevealHeader", "YOUR ROLE", font, 24, new Vector2(0.5f, 0.86f), new Vector2(680f, 36f), new Color(0.88f, 0.8f, 0.58f, 1f));
        roleRevealTitle    = CreateText(revealCard.transform, "RoleRevealTitle",    string.Empty, font, 60, new Vector2(0.5f, 0.60f), new Vector2(700f, 90f), new Color(1f, 0.95f, 0.78f, 1f));
        roleRevealSubtitle = CreateText(revealCard.transform, "RoleRevealSubtitle", string.Empty, font, 20, new Vector2(0.5f, 0.30f), new Vector2(680f, 80f), new Color(0.85f, 0.9f, 0.82f, 1f));
        roleRevealContinueButton = CreateButton(revealCard.transform, "RoleRevealContinue", "Continue", font, new Vector2(0.5f, 0.08f), new Vector2(260f, 56f), gold, OnRoleRevealContinuePressed);

        // Placement panel — full-screen top-down map. Built after the reveal so it
        // renders on top. Contains the live RenderTexture of the school, a marker
        // dot that follows the cursor click, a confirm button, and a status line.
        placementPanel = CreateFullScreenPanel(rootCanvas.transform, "PlacementPanel", new Color(0.02f, 0.03f, 0.05f, 0.95f));
        placementStatusText = CreateText(placementPanel.transform, "PlacementStatus", string.Empty, font, 24, new Vector2(0.5f, 0.95f), new Vector2(1200f, 50f), new Color(1f, 0.95f, 0.78f, 1f));

        GameObject mapHolder = CreatePanel(placementPanel.transform, "PlacementMapHolder", panel, new Vector2(0.5f, 0.5f), new Vector2(900f, 720f));
        GameObject mapImageGo = new GameObject("PlacementMapImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        mapImageGo.transform.SetParent(mapHolder.transform, false);
        placementMapImage = mapImageGo.GetComponent<RawImage>();
        placementMapImage.color = Color.white;
        placementMapRect = mapImageGo.GetComponent<RectTransform>();
        placementMapRect.anchorMin = new Vector2(0f, 0f);
        placementMapRect.anchorMax = new Vector2(1f, 1f);
        placementMapRect.offsetMin = new Vector2(12f, 12f);
        placementMapRect.offsetMax = new Vector2(-12f, -12f);

        // Marker dot — child of the map image so its anchored position maps directly
        // to map coordinates. Hidden until the player has clicked once.
        GameObject markerGo = new GameObject("PlacementMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        markerGo.transform.SetParent(mapImageGo.transform, false);
        placementMapMarker = markerGo.GetComponent<Image>();
        placementMapMarker.color = new Color(1f, 0.85f, 0.25f, 1f);
        RectTransform markerRect = markerGo.GetComponent<RectTransform>();
        markerRect.sizeDelta = new Vector2(24f, 24f);
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerGo.SetActive(false);

        placementConfirmButton = CreateButton(placementPanel.transform, "PlacementConfirm", "Confirm", font, new Vector2(0.5f, 0.06f), new Vector2(280f, 60f), gold, OnPlacementConfirmPressed);

        BuildPlacementTeacherList(panel, font);

        // Ghost icon that follows the cursor while a teacher is being dragged. Owned
        // by the placement panel so it auto-hides with the rest of the placement UI.
        GameObject ghostGo = new GameObject("PlacementDragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        ghostGo.transform.SetParent(placementPanel.transform, false);
        placementDragGhost = ghostGo;
        placementDragGhostImage = ghostGo.GetComponent<Image>();
        placementDragGhostImage.raycastTarget = false;
        placementDragGhostImage.color = new Color(1f, 1f, 1f, 0.85f);
        RectTransform ghostRect = ghostGo.GetComponent<RectTransform>();
        ghostRect.sizeDelta = new Vector2(80f, 80f);
        ghostGo.SetActive(false);

        authPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        overlayPanel.SetActive(false);
        roleRevealPanel.SetActive(false);
        placementPanel.SetActive(false);
    }

    private void RefreshLocalReferences()
    {
        if (localController == null)
        {
            localController = FindAnyObjectByType<FirstPersonController>();
        }

        if (localHealth == null && localController != null)
        {
            localHealth = localController.GetComponent<PlayerHealth>();
        }

        if (localInventory == null && localController != null)
        {
            localInventory = localController.GetComponent<SingleItemInventory>();
        }

        if (localItemInteractor == null)
        {
            localItemInteractor = FindAnyObjectByType<PlayerItemInteractor>();
        }

        if (localEntityInteractor == null)
        {
            localEntityInteractor = FindAnyObjectByType<PlayerEntityInteractor>();
        }
    }

    private void EnforceLobbyAndGameState()
    {
        if (!suppressBuiltInUi)
        {
            return;
        }

        bool enableGameplay = gameStarted && generationReady && setupPhaseComplete;
        SetLocalGameplayEnabled(enableGameplay);

        if (!enableGameplay)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void SetLocalGameplayEnabled(bool enabled)
    {
        if (localController != null && localController.enabled != enabled)
        {
            localController.enabled = enabled;
        }

        if (localItemInteractor != null && localItemInteractor.enabled != enabled)
        {
            localItemInteractor.enabled = enabled;
        }

        if (localEntityInteractor != null && localEntityInteractor.enabled != enabled)
        {
            localEntityInteractor.enabled = enabled;
        }
    }

    private void PumpLobbySocketKeepAlive()
    {
        if (lobbySocket == null || !lobbySocket.IsOpen || currentLobby == null || gameStarted)
        {
            return;
        }

        if (Time.unscaledTime >= nextLobbyPingTime)
        {
            nextLobbyPingTime = Time.unscaledTime + LobbyPingInterval;
            lastLobbyPingSentAt = Time.unscaledTime;
            lobbySocket.SendJson(JsonUtility.ToJson(new MultiplayerPingDto { clientTime = Time.realtimeSinceStartupAsDouble }));
            lobbySocket.SendJson(JsonUtility.ToJson(new MultiplayerHeartbeatDto { clientTime = Time.realtimeSinceStartupAsDouble }));
        }

        if (IsHeartbeatTimedOut(lastLobbyPingSentAt, lastLobbyPongAt, lastLobbyHeartbeatAt, LobbyHeartbeatTimeout))
        {
            SetLobbyStatus("Lobby connection timed out. Reconnecting...");
            ConnectLobbySocket(currentLobby.id);
        }
    }

    private void PumpGameSocketKeepAlive()
    {
        if (gameSocket == null || !gameSocket.IsOpen || !gameStarted)
        {
            return;
        }

        if (Time.unscaledTime >= nextGamePingTime)
        {
            nextGamePingTime = Time.unscaledTime + GamePingInterval;
            lastGamePingSentAt = Time.unscaledTime;
            gameSocket.SendJson(JsonUtility.ToJson(new MultiplayerPingDto { clientTime = Time.realtimeSinceStartupAsDouble }));
            gameSocket.SendJson(JsonUtility.ToJson(new MultiplayerHeartbeatDto { clientTime = Time.realtimeSinceStartupAsDouble }));
        }

        if (IsHeartbeatTimedOut(lastGamePingSentAt, lastGamePongAt, lastGameHeartbeatAt, GameHeartbeatTimeout))
        {
            SetOverlayStatus("Game connection timed out. Reconnecting...");
            ConnectGameSocket(currentGameStart.lobbyId);
        }
    }

    private void PumpLocalStateSync()
    {
        if (!gameStarted || !generationReady || localController == null || gameSocket == null || !gameSocket.IsOpen || localMember == null)
        {
            return;
        }

        if (Time.unscaledTime < nextStateSendTime)
        {
            return;
        }

        Transform target = localController.transform;
        Vector3 position = target.position;
        Vector3 eulerAngles = target.eulerAngles;
        int currentHealth = localHealth != null ? localHealth.CurrentHealth : 100;
        int maxHealth = localHealth != null ? localHealth.MaxHealth : 100;
        bool isDead = localHealth != null && localHealth.IsDead;
        bool shouldSend = localStateSeq == 0
            || (position - lastSentPosition).sqrMagnitude >= MinPositionDeltaSqr
            || Quaternion.Angle(Quaternion.Euler(lastSentEulerAngles), Quaternion.Euler(eulerAngles)) >= MinRotationDelta
            || currentHealth != lastSentHealth
            || maxHealth != lastSentMaxHealth
            || isDead != lastSentDead
            || Time.unscaledTime - lastStateSendTime >= ForcedStateSendInterval;
        if (!shouldSend)
        {
            nextStateSendTime = Time.unscaledTime + StateSendInterval;
            return;
        }

        nextStateSendTime = Time.unscaledTime + StateSendInterval;
        MultiplayerPlayerStateDto state = MultiplayerPlayerStateDto.FromTransform(
            localMember.playerId,
            localMember.userId,
            ++localStateSeq,
            target,
            localController.CurrentVelocity,
            localHealth);
        gameSocket.SendJson(JsonUtility.ToJson(state));
        lastSentPosition = position;
        lastSentEulerAngles = eulerAngles;
        lastSentHealth = currentHealth;
        lastSentMaxHealth = maxHealth;
        lastSentDead = isDead;
        lastStateSendTime = Time.unscaledTime;
    }

    private void PumpSharedObjectStateSync()
    {
        if (!gameStarted || !generationReady || !setupPhaseComplete || gameSocket == null || !gameSocket.IsOpen || localMember == null)
        {
            return;
        }

        if (Time.unscaledTime < nextSharedObjectStateSendTime)
        {
            return;
        }

        nextSharedObjectStateSendTime = Time.unscaledTime + SharedObjectStateSendInterval;

        if (!localPlayerIsKeyHider)
        {
            SendTeacherStates();
        }

        SendKeyStateIfAuthoritative();
    }

    private void SendTeacherStates()
    {
        foreach (SimpleTeacherWander teacher in FindObjectsByType<SimpleTeacherWander>(FindObjectsSortMode.None))
        {
            string teacherId = ExtractTeacherId(teacher.gameObject.name);
            if (string.IsNullOrWhiteSpace(teacherId))
            {
                continue;
            }

            teacher.SetNetworkControlled(false);
            if (!localTeacherStateSeq.TryGetValue(teacherId, out int seq))
            {
                seq = 0;
            }

            localTeacherStateSeq[teacherId] = ++seq;
            MultiplayerTeacherStateDto dto = new MultiplayerTeacherStateDto
            {
                type = "teacher_state",
                lobbyId = currentGameStart != null ? currentGameStart.lobbyId : 0,
                teacherId = teacherId,
                authoritativeUserId = localMember.userId,
                seq = seq,
                position = MultiplayerJson.VectorToArray(teacher.transform.position),
                rotation = MultiplayerJson.VectorToArray(teacher.transform.eulerAngles),
                aiState = teacher.NetworkStateName,
                canSeePlayer = teacher.CanSeePlayer,
                lastKnownPlayerPosition = MultiplayerJson.VectorToArray(teacher.LastKnownPlayerPosition)
            };
            gameSocket.SendJson(JsonUtility.ToJson(dto));
        }
    }

    private void SendKeyStateIfAuthoritative()
    {
        ItemPickup key = ResolveCurrentKeyPickup(out bool heldByLocalPlayer);
        if (key == null)
        {
            return;
        }

        bool canAuthorKey = heldByLocalPlayer || localPlayerIsKeyHider || lastSentKeyHeld;
        if (!canAuthorKey)
        {
            return;
        }

        Transform keyTransform = key.transform;
        string holderPlayerId = heldByLocalPlayer && localMember != null ? localMember.playerId : string.Empty;
        Vector3 position = keyTransform.position;
        Vector3 eulerAngles = keyTransform.eulerAngles;
        bool shouldSend = localKeyStateSeq == 0
            || (position - lastSentKeyPosition).sqrMagnitude >= MinPositionDeltaSqr
            || Quaternion.Angle(Quaternion.Euler(lastSentKeyEulerAngles), Quaternion.Euler(eulerAngles)) >= MinRotationDelta
            || heldByLocalPlayer != lastSentKeyHeld
            || !string.Equals(holderPlayerId, lastSentKeyHolderPlayerId, StringComparison.Ordinal);

        if (!shouldSend)
        {
            return;
        }

        MultiplayerKeyStateDto dto = new MultiplayerKeyStateDto
        {
            type = "key_state",
            lobbyId = currentGameStart != null ? currentGameStart.lobbyId : 0,
            keyId = key.ItemId,
            authoritativeUserId = localMember.userId,
            seq = ++localKeyStateSeq,
            position = MultiplayerJson.VectorToArray(position),
            rotation = MultiplayerJson.VectorToArray(eulerAngles),
            isHeld = heldByLocalPlayer,
            holderPlayerId = holderPlayerId
        };
        gameSocket.SendJson(JsonUtility.ToJson(dto));
        lastSentKeyPosition = position;
        lastSentKeyEulerAngles = eulerAngles;
        lastSentKeyHeld = heldByLocalPlayer;
        lastSentKeyHolderPlayerId = holderPlayerId;
    }

    private void OnConnectGuestPressed()
    {
        string serverUrl = string.IsNullOrWhiteSpace(serverUrlInput.text) ? DefaultServerUrl : serverUrlInput.text.Trim();
        api.BaseHttpUrl = serverUrl;
        PlayerPrefs.SetString(SavedServerUrlKey, api.BaseHttpUrl);
        PlayerPrefs.Save();
        SetAuthStatus("Connecting to backend...");
        StartCoroutine(api.CreateGuest(result =>
        {
            if (!result.IsSuccess)
            {
                SetAuthStatus(result.Error);
                return;
            }

            authToken = result.Value.token;
            currentUser = result.Value.user;
            guestAuthenticated = true;
            SetAuthStatus($"Connected as {currentUser.username}.");
            ShowLobbyPanel();
        }));
    }

    private void OnCreateLobbyPressed()
    {
        if (!EnsureAuthenticated())
        {
            return;
        }

        SetLobbyStatus("Creating lobby...");
        StartCoroutine(api.CreateLobby(MaxPlayers, result =>
        {
            if (!result.IsSuccess)
            {
                SetLobbyStatus(result.Error);
                return;
            }

            EnterLobby(result.Value);
        }));
    }

    private void OnJoinLobbyPressed()
    {
        if (!EnsureAuthenticated())
        {
            return;
        }

        string code = (joinCodeInput.text ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            SetLobbyStatus("Enter a lobby code first.");
            return;
        }

        SetLobbyStatus($"Joining {code}...");
        StartCoroutine(api.JoinLobby(code, result =>
        {
            if (!result.IsSuccess)
            {
                SetLobbyStatus(result.Error);
                return;
            }

            localMember = result.Value.member;
            EnterLobby(result.Value.lobby);
        }));
    }

    private void OnSingleplayerTestPressed()
    {
        StopLobbyMusic();
        suppressBuiltInUi = false;
        gameStarted = false;
        generationReady = false;
        generationRequested = false;
        authPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        overlayPanel.SetActive(false);

        runtimeGenerator = FindAnyObjectByType<FacilityRuntimeGenerator>();
        if (runtimeGenerator == null)
        {
            GameObject generatorObject = new GameObject("FacilityRuntimeGenerator");
            runtimeGenerator = generatorObject.AddComponent<FacilityRuntimeGenerator>();
        }

        runtimeGenerator.ConfigurePreviewConnectedRoomLayout(12);
        runtimeGenerator.RandomizeSeedAndGenerate();

        GameFlowUIController flowController = FindAnyObjectByType<GameFlowUIController>();
        if (flowController != null)
        {
            flowController.BeginSingleplayerTestSession();
        }
    }

    private void OnReadyPressed()
    {
        if (currentLobby == null || localMember == null)
        {
            return;
        }

        bool nextReady = !localMember.isReady;
        StartCoroutine(api.SetReady(currentLobby.id, nextReady, result =>
        {
            if (!result.IsSuccess)
            {
                SetLobbyStatus(result.Error);
                return;
            }

            localMember.isReady = nextReady;
            SetLobbyStatus(nextReady ? "Ready set. Waiting for the other player..." : "Ready removed.");
            StartCoroutine(RefreshLobbyFromServer());
        }));
    }

    private void OnStartPressed()
    {
        if (currentLobby == null)
        {
            return;
        }

        StartCoroutine(api.StartLobby(currentLobby.id, DefaultMapId, result =>
        {
            if (!result.IsSuccess)
            {
                SetLobbyStatus(result.Error);
                return;
            }

            BeginGame(result.Value);
        }));
    }

    private void EnterLobby(MultiplayerLobbyDto lobby)
    {
        currentLobby = lobby;
        RebuildLobbyMemberLookup();
        if (currentUser != null)
        {
            localMember = FindMember(currentUser.id);
        }

        lobbyPanel.SetActive(true);
        authPanel.SetActive(false);
        overlayPanel.SetActive(false);
        lobbyTitleText.text = "Two-Player Lobby";
        lobbyCodeText.text = string.IsNullOrWhiteSpace(lobby.code) ? string.Empty : $"Code: {lobby.code}";
        SetLobbyStatus("Lobby connected.");
        UpdateLobbyUi();
        ConnectLobbySocket(lobby.id);
    }

    private void ConnectLobbySocket(int lobbyId)
    {
        if (lobbySocket != null)
        {
            lobbySocket.Opened -= OnLobbySocketOpened;
            lobbySocket.MessageReceived -= OnLobbySocketMessage;
            lobbySocket.Closed -= OnLobbySocketClosed;
            lobbySocket.ErrorReceived -= OnLobbySocketError;
            lobbySocket.Close();
        }

        lobbySocket = new EscapeBlock9MultiplayerSocketClient();
        lobbySocket.Opened += OnLobbySocketOpened;
        lobbySocket.MessageReceived += OnLobbySocketMessage;
        lobbySocket.Closed += OnLobbySocketClosed;
        lobbySocket.ErrorReceived += OnLobbySocketError;
        lobbySocket.Connect(api.BuildWebSocketUrl($"/ws/lobby/{lobbyId}/"));
    }

    private void ConnectGameSocket(int lobbyId)
    {
        if (gameSocket != null)
        {
            gameSocket.Opened -= OnGameSocketOpened;
            gameSocket.MessageReceived -= OnGameSocketMessage;
            gameSocket.Closed -= OnGameSocketClosed;
            gameSocket.ErrorReceived -= OnGameSocketError;
            gameSocket.Close();
        }

        gameSocket = new EscapeBlock9MultiplayerSocketClient();
        gameSocket.Opened += OnGameSocketOpened;
        gameSocket.MessageReceived += OnGameSocketMessage;
        gameSocket.Closed += OnGameSocketClosed;
        gameSocket.ErrorReceived += OnGameSocketError;
        gameSocket.Connect(api.BuildWebSocketUrl($"/ws/game/{lobbyId}/"));
    }

    private void OnLobbySocketOpened()
    {
        nextLobbyPingTime = Time.unscaledTime + 0.1f;
        lastLobbyHeartbeatAt = Time.unscaledTime;
        SetLobbyStatus("Realtime lobby connected.");
    }

    private void OnLobbySocketMessage(string json)
    {
        MultiplayerSocketTypeEnvelopeDto envelope = JsonUtility.FromJson<MultiplayerSocketTypeEnvelopeDto>(json);
        switch (envelope.type)
        {
            case "lobby_snapshot":
            {
                MultiplayerLobbySnapshotDto snapshot = JsonUtility.FromJson<MultiplayerLobbySnapshotDto>(json);
                ApplyLobbySnapshot(snapshot);
                break;
            }
            case "player_joined":
            case "player_left":
            case "player_ready_changed":
                StartCoroutine(RefreshLobbyFromServer());
                break;
            case "game_started":
            {
                MultiplayerGameStartedDto started = JsonUtility.FromJson<MultiplayerGameStartedDto>(json);
                BeginGame(started);
                break;
            }
            case "pong":
                lastLobbyPongAt = Time.unscaledTime;
                break;
            case "heartbeat":
                lastLobbyHeartbeatAt = Time.unscaledTime;
                break;
        }
    }

    private void OnLobbySocketClosed(string closeCode)
    {
        if (!gameStarted && currentLobby != null)
        {
            SetLobbyStatus($"Lobby socket closed ({closeCode}). Reconnecting...");
            ConnectLobbySocket(currentLobby.id);
        }
    }

    private void OnLobbySocketError(string error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            SetLobbyStatus(error);
        }
    }

    private void OnGameSocketOpened()
    {
        nextGamePingTime = Time.unscaledTime + 0.1f;
        lastGameHeartbeatAt = Time.unscaledTime;
        SetOverlayStatus("Realtime game connected.");
    }

    private void OnGameSocketMessage(string json)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        MultiplayerSocketTypeEnvelopeDto envelope = JsonUtility.FromJson<MultiplayerSocketTypeEnvelopeDto>(json);
        switch (envelope.type)
        {
            case "room_snapshot":
            {
                MultiplayerRoomSnapshotDto snapshot = JsonUtility.FromJson<MultiplayerRoomSnapshotDto>(json);
                HandleSetupSnapshot(snapshot.setup);
                HandleSetupSnapshot(snapshot.setupFinalized);
                if (snapshot.players != null)
                {
                    for (int i = 0; i < snapshot.players.Length; i++)
                    {
                        ApplyRemotePlayerState(snapshot.players[i]);
                    }
                }
                if (snapshot.teachers != null)
                {
                    for (int i = 0; i < snapshot.teachers.Length; i++)
                    {
                        ApplyTeacherState(snapshot.teachers[i]);
                    }
                }
                ApplyKeyState(snapshot.keyState);
                break;
            }
            case "player_state":
                ApplyRemotePlayerState(JsonUtility.FromJson<MultiplayerPlayerStateDto>(json));
                break;
            case "setup_placement":
                HandleRemotePlacement(JsonUtility.FromJson<MultiplayerSetupPlacementDto>(json));
                break;
            case "setup_snapshot":
                HandleSetupSnapshot(JsonUtility.FromJson<MultiplayerSetupSnapshotDto>(json));
                break;
            case "setup_finalized":
                HandleSetupFinalized(JsonUtility.FromJson<MultiplayerSetupSnapshotDto>(json));
                break;
            case "teacher_state":
                ApplyTeacherState(JsonUtility.FromJson<MultiplayerTeacherStateDto>(json));
                break;
            case "key_state":
                ApplyKeyState(JsonUtility.FromJson<MultiplayerKeyStateDto>(json));
                break;
            case "player_left":
            {
                MultiplayerLobbyEventDto left = JsonUtility.FromJson<MultiplayerLobbyEventDto>(json);
                RemoveRemotePlayer(left.playerId);
                break;
            }
            case "pong":
                lastGamePongAt = Time.unscaledTime;
                break;
            case "heartbeat":
                lastGameHeartbeatAt = Time.unscaledTime;
                break;
        }
    }

    private void OnGameSocketClosed(string closeCode)
    {
        if (gameStarted && currentGameStart != null)
        {
            SetOverlayStatus($"Game socket closed ({closeCode}). Reconnecting...");
            ConnectGameSocket(currentGameStart.lobbyId);
        }
    }

    private void OnGameSocketError(string error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            SetOverlayStatus(error);
        }
    }

    private void ApplyLobbySnapshot(MultiplayerLobbySnapshotDto snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        currentLobby = new MultiplayerLobbyDto
        {
            id = snapshot.lobbyId,
            code = snapshot.code,
            hostId = snapshot.hostId,
            isStarted = snapshot.isStarted,
            maxPlayers = MaxPlayers,
            members = snapshot.players
        };
        RebuildLobbyMemberLookup();
        if (currentUser != null)
        {
            localMember = FindMember(currentUser.id);
        }

        UpdateLobbyUi();
    }

    private IEnumerator RefreshLobbyFromServer()
    {
        if (currentLobby == null)
        {
            yield break;
        }

        yield return api.GetLobby(currentLobby.id, result =>
        {
            if (!result.IsSuccess)
            {
                SetLobbyStatus(result.Error);
                return;
            }

            currentLobby = result.Value;
            RebuildLobbyMemberLookup();
            if (currentUser != null)
            {
                localMember = FindMember(currentUser.id);
            }

            UpdateLobbyUi();
        });
    }

    private void BeginGame(MultiplayerGameStartedDto start)
    {
        if (start == null)
        {
            return;
        }

        currentGameStart = start;
        gameStarted = true;
        generationReady = false;
        generationRequested = false;
        setupPhaseComplete = false;
        localPlacementConfirmed = false;
        peerPlacementConfirmed = false;
        localKeyStateSeq = 0;
        localTeacherStateSeq.Clear();
        lastSentHealth = -1;
        lastSentMaxHealth = -1;
        lastSentDead = false;
        lastSentKeyPosition = Vector3.positiveInfinity;
        lastSentKeyEulerAngles = Vector3.positiveInfinity;
        lastSentKeyHeld = false;
        lastSentKeyHolderPlayerId = string.Empty;
        nextSharedObjectStateSendTime = 0f;
        StopLobbyMusic();
        lobbyPanel.SetActive(false);
        overlayPanel.SetActive(true);
        SetOverlayStatus("Generating synchronized facility...");
        currentLobby ??= new MultiplayerLobbyDto { id = start.lobbyId, maxPlayers = MaxPlayers };
        if (currentUser != null)
        {
            localMember = FindLocalMemberFromGameStart(start);
        }

        ConnectGameSocket(start.lobbyId);
        StartCoroutine(BeginMultiplayerGeneration(start));
    }

    private IEnumerator BeginMultiplayerGeneration(MultiplayerGameStartedDto start)
    {
        if (generationRequested)
        {
            yield break;
        }

        generationRequested = true;
        yield return null;
        RefreshLocalReferences();
        runtimeGenerator = FindAnyObjectByType<FacilityRuntimeGenerator>();
        if (runtimeGenerator == null)
        {
            GameObject generatorObject = new GameObject("FacilityRuntimeGenerator");
            runtimeGenerator = generatorObject.AddComponent<FacilityRuntimeGenerator>();
        }

        runtimeGenerator.ConfigurePreviewConnectedRoomLayout(12);
        runtimeGenerator.GenerationCompleted -= OnRuntimeGenerationCompleted;
        runtimeGenerator.GenerationCompleted += OnRuntimeGenerationCompleted;
        int seed = MultiplayerJson.DeterministicHash($"{start.lobbyId}:{start.mapId}");
        runtimeGenerator.GenerateWithSeed(seed);
    }

    private void OnRuntimeGenerationCompleted(FacilityRuntimeGenerator generator, bool success)
    {
        if (!success)
        {
            SetOverlayStatus("Facility generation failed.");
            return;
        }

        generationReady = true;
        SetOverlayStatus("Facility ready. Waiting for player states...");
        PreSpawnRemotePlayers(generator);

        // After the facility is built, run the setup phase: roll roles, show the
        // reveal, then unlock gameplay. Setting setupPhaseComplete = false
        // synchronously here (not inside the coroutine) closes a one-frame race
        // where EnforceLobbyAndGameState would otherwise enable gameplay before
        // the coroutine's first instruction runs.
        setupPhaseComplete = false;
        Debug.Log("[SetupPhase] Generation complete — entering setup (role pick + placement).");
        ShowRoleReveal();
    }

    // Decide the role and show the reveal panel. The player clicks Continue to move
    // on (no auto-timer — that was getting missed while juggling two windows).
    private void ShowRoleReveal()
    {
        localPlayerIsKeyHider = DetermineLocalRoleIsKeyHider();
        Debug.Log($"[SetupPhase] Role decided — localPlayerIsKeyHider={localPlayerIsKeyHider}. " +
                  $"roleRevealPanel={(roleRevealPanel != null ? "ok" : "NULL")}, " +
                  $"placementPanel={(placementPanel != null ? "ok" : "NULL")}");

        if (overlayPanel != null) overlayPanel.SetActive(false);
        if (placementPanel != null) placementPanel.SetActive(false);

        if (roleRevealPanel != null)
        {
            if (roleRevealTitle != null)
                roleRevealTitle.text = localPlayerIsKeyHider ? "KEY HIDER" : "TEACHER PLACER";
            if (roleRevealSubtitle != null)
                roleRevealSubtitle.text = localPlayerIsKeyHider
                    ? "You hide the exit key. Click Continue, then click on the map to choose its spot."
                    : "You place the teachers. Click Continue, then drag each teacher onto the map.";
            roleRevealPanel.transform.SetAsLastSibling(); // ensure it's on top
            roleRevealPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("[SetupPhase] roleRevealPanel is null — UI not built. Jumping to placement.");
            BeginPlacementPhase();
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnRoleRevealContinuePressed()
    {
        Debug.Log("[SetupPhase] Continue pressed — opening placement panel.");
        if (roleRevealPanel != null) roleRevealPanel.SetActive(false);
        BeginPlacementPhase();
    }

    // Both clients compute the role from a deterministic hash of the lobby+map, so
    // they always agree without an extra round-trip. We DON'T rely on `slot` being a
    // clean 1/2 (it wasn't — both players were coming out as Teacher Placer). Instead
    // we sort the players' userIds (identical list on both clients), pick one by the
    // hash, and the local player is the Key Hider iff its userId is the chosen one.
    // Exactly one player gets it. Single-player falls back to Key Hider.
    private bool DetermineLocalRoleIsKeyHider()
    {
        if (currentGameStart == null || currentGameStart.players == null) return true;
        var players = currentGameStart.players;
        if (players.Length < 2) return true;

        int localUserId = currentUser != null ? currentUser.id
                        : (localMember != null ? localMember.userId : int.MinValue);

        var ids = new List<int>(players.Length);
        foreach (var p in players) ids.Add(p.userId);
        ids.Sort();

        int hash = MultiplayerJson.DeterministicHash($"role:{currentGameStart.lobbyId}:{currentGameStart.mapId}");
        int hiderIndex = Mathf.Abs(hash) % ids.Count;
        int hiderUserId = ids[hiderIndex];

        bool isHider = localUserId == hiderUserId;
        Debug.Log($"[SetupPhase] Role calc — localUserId={localUserId}, hiderUserId={hiderUserId}, " +
                  $"ids=[{string.Join(",", ids)}], isHider={isHider}");
        return isHider;
    }

    // Wire up the top-down camera + RT and show the placement panel. Both players
    // see the map; only the Key Hider can click. Once each side has confirmed and
    // the peer's placement message arrives, the key spawns and gameplay unlocks.
    private void BeginPlacementPhase()
    {
        localChosenKeyWorldPos = null;
        peerChosenKeyWorldPos = null;
        localPlacementConfirmed = false;
        peerPlacementConfirmed = false;
        for (int i = 0; i < TeacherSlots.Length; i++)
        {
            localTeacherWorldPos[i] = null;
            peerTeacherWorldPos[i] = null;
            // Teachers now exist in the scene, so (re)load each portrait — at Awake
            // they may not have spawned yet and came back null.
            if (placementTeacherListItems[i] != null) placementTeacherListItems[i].SetActive(true);
            if (placementTeacherPortraits[i] != null)
            {
                Texture2D tex = LoadTeacherPortrait(TeacherSlots[i].slug);
                placementTeacherPortraits[i].texture = tex;
                placementTeacherPortraits[i].color = tex != null ? Color.white : new Color(0.3f, 0.3f, 0.35f, 1f);
            }
        }

        CreatePlacementMapCamera();
        if (placementMapMarker != null) placementMapMarker.gameObject.SetActive(false);

        if (placementPanel != null)
        {
            placementPanel.transform.SetAsLastSibling(); // render above everything
            placementPanel.SetActive(true);
            Debug.Log("[SetupPhase] Placement panel activated.");
        }
        else
        {
            Debug.LogError("[SetupPhase] placementPanel is null — cannot show map UI.");
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UpdatePlacementUiForRole();
    }

    private void UpdatePlacementUiForRole()
    {
        // Only the Teacher Placer sees the teacher list column.
        if (placementTeacherListContent != null)
        {
            placementTeacherListContent.transform.parent.parent.gameObject.SetActive(!localPlayerIsKeyHider);
        }

        int placed = CountPlacedTeachers();
        if (placementStatusText != null)
        {
            if (localPlayerIsKeyHider)
            {
                placementStatusText.text = localPlacementConfirmed
                    ? "Key spot locked in. Waiting for the Teacher Placer..."
                    : "Click on the map to hide the exit key. Press Confirm when ready.";
            }
            else
            {
                placementStatusText.text = localPlacementConfirmed
                    ? "Waiting for the Key Hider to finish..."
                    : $"Drag each teacher onto the map. Placed {placed}/{TeacherSlots.Length}.";
            }
        }
        if (placementConfirmButton != null)
        {
            bool canConfirm;
            if (localPlacementConfirmed) canConfirm = false;
            else if (localPlayerIsKeyHider) canConfirm = localChosenKeyWorldPos.HasValue;
            else canConfirm = placed == TeacherSlots.Length;
            placementConfirmButton.interactable = canConfirm;
        }
    }

    // Build a child ortho camera over the school that renders into the UI image.
    // Camera sits high above the world bounds and looks straight down.
    private void CreatePlacementMapCamera()
    {
        ComputeSchoolBounds();

        if (placementMapTexture != null)
        {
            placementMapTexture.Release();
            DestroyUnityObject(placementMapTexture);
            placementMapTexture = null;
        }
        placementMapTexture = new RenderTexture(1024, 1024, 16, RenderTextureFormat.ARGB32);
        placementMapTexture.name = "PlacementMapRT";
        placementMapTexture.Create();

        if (placementMapCamera == null)
        {
            GameObject camGo = new GameObject("PlacementMapCamera");
            placementMapCamera = camGo.AddComponent<Camera>();
        }

        Bounds b = placementMapWorldBounds;
        Vector3 center = b.center;
        float camY = b.max.y + 10f;
        placementMapCamera.transform.position = new Vector3(center.x, camY, center.z);
        placementMapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        placementMapCamera.orthographic = true;
        // Pick the larger horizontal extent so the whole school fits.
        float halfExtent = Mathf.Max(b.extents.x, b.extents.z) + 1f;
        placementMapCamera.orthographicSize = halfExtent;

        // "See through the ceiling": clip away the top slice of the building so the
        // camera looks past the roof/ceiling slab down into the rooms. The near plane
        // starts just below the highest point; clipDown controls how much of the top
        // is removed (enough to clear the ceiling, not so much we lose the walls).
        float clipDown = Mathf.Clamp(b.size.y * 0.35f, 1.0f, 4.0f);
        placementMapCamera.nearClipPlane = (camY - b.max.y) + clipDown;
        placementMapCamera.farClipPlane = (camY - b.min.y) + 5f;
        placementMapCamera.clearFlags = CameraClearFlags.SolidColor;
        placementMapCamera.backgroundColor = new Color(0.10f, 0.11f, 0.14f, 1f);
        placementMapCamera.cullingMask = ~0;
        placementMapCamera.targetTexture = placementMapTexture;

        // A dedicated bright light pointing straight down so the interiors aren't a
        // dark blob — the level lighting alone is a night/horror palette and reads as
        // black from above. Parented to the camera, only alive during placement.
        if (placementMapLight == null)
        {
            GameObject lightGo = new GameObject("PlacementMapLight");
            placementMapLight = lightGo.AddComponent<Light>();
            placementMapLight.type = LightType.Directional;
        }
        placementMapLight.transform.position = new Vector3(center.x, camY, center.z);
        placementMapLight.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        placementMapLight.intensity = 1.6f;
        placementMapLight.color = Color.white;
        placementMapLight.shadows = LightShadows.None;
        placementMapLight.cullingMask = ~0;
        placementMapLight.gameObject.SetActive(true);

        if (placementMapImage != null)
        {
            placementMapImage.texture = placementMapTexture;
        }
    }

    // Find the generated school's bounding box so the camera can frame it. Tries
    // the procedural generator's root first, then the static Block9Building, then
    // falls back to a fixed 60m square at origin.
    private void ComputeSchoolBounds()
    {
        Transform target = null;
        if (runtimeGenerator != null && runtimeGenerator.GeneratedRoot != null)
        {
            target = runtimeGenerator.GeneratedRoot.transform;
        }
        if (target == null)
        {
            GameObject block = GameObject.Find("Block9Building");
            if (block != null) target = block.transform;
        }

        if (target == null)
        {
            placementMapWorldBounds = new Bounds(Vector3.zero, new Vector3(60f, 10f, 60f));
            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            placementMapWorldBounds = new Bounds(target.position, new Vector3(60f, 10f, 60f));
            return;
        }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        placementMapWorldBounds = b;
    }

    // Convert a click in the map's RectTransform into a world-space position using
    // the ortho camera. We hit the ground plane at the school's average Y. Returns
    // false if the click was outside the map rect.
    private bool TryMapClickToWorld(Vector2 screenPos, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        if (placementMapRect == null || placementMapCamera == null) return false;
        if (!RectTransformUtility.RectangleContainsScreenPoint(placementMapRect, screenPos, null)) return false;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(placementMapRect, screenPos, null, out localPoint);
        Rect r = placementMapRect.rect;
        float u = Mathf.Clamp01((localPoint.x - r.xMin) / r.width);
        float v = Mathf.Clamp01((localPoint.y - r.yMin) / r.height);

        Bounds b = placementMapWorldBounds;
        float worldX = b.center.x + (u - 0.5f) * b.size.x;
        float worldZ = b.center.z + (v - 0.5f) * b.size.z;
        float worldY = b.center.y - b.extents.y * 0.5f; // a bit below center, closer to floor
        worldPos = new Vector3(worldX, worldY, worldZ);
        return true;
    }

    // Drop the marker dot at the clicked map position (so the Hider sees where
    // they've chosen). Position is in the map rect's local space.
    private void ShowPlacementMarkerAtScreen(Vector2 screenPos)
    {
        if (placementMapMarker == null || placementMapRect == null) return;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(placementMapRect, screenPos, null, out localPoint);
        RectTransform markerRect = placementMapMarker.GetComponent<RectTransform>();
        markerRect.anchoredPosition = localPoint;
        placementMapMarker.gameObject.SetActive(true);
    }

    private void OnPlacementConfirmPressed()
    {
        if (localPlacementConfirmed) return;
        if (localPlayerIsKeyHider && !localChosenKeyWorldPos.HasValue) return;
        if (!localPlayerIsKeyHider && CountPlacedTeachers() != TeacherSlots.Length) return;

        localPlacementConfirmed = true;
        SendLocalPlacement();
        UpdatePlacementUiForRole();
    }

    private void SendLocalPlacement()
    {
        if (gameSocket == null) return;
        var dto = new MultiplayerSetupPlacementDto
        {
            type = "setup_placement",
            playerId = localMember != null ? localMember.playerId : string.Empty,
            isKeyHider = localPlayerIsKeyHider,
            keyPosition = (localPlayerIsKeyHider && localChosenKeyWorldPos.HasValue)
                ? MultiplayerJson.VectorToArray(localChosenKeyWorldPos.Value)
                : null,
        };

        // For the Teacher Placer, ship per-axis arrays sized to the slot count.
        if (!localPlayerIsKeyHider)
        {
            int n = TeacherSlots.Length;
            dto.teacherPositionsX = new float[n];
            dto.teacherPositionsY = new float[n];
            dto.teacherPositionsZ = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 p = localTeacherWorldPos[i] ?? Vector3.zero;
                dto.teacherPositionsX[i] = p.x;
                dto.teacherPositionsY[i] = p.y;
                dto.teacherPositionsZ[i] = p.z;
            }
        }

        gameSocket.SendJson(JsonUtility.ToJson(dto));
    }

    private void HandleSetupSnapshot(MultiplayerSetupSnapshotDto dto)
    {
        if (dto == null)
        {
            return;
        }

        if (dto.isFinalized || string.Equals(dto.type, "setup_finalized", StringComparison.Ordinal))
        {
            HandleSetupFinalized(dto);
            return;
        }

        if (dto.keyPosition != null && dto.keyPosition.Length == 3)
        {
            if (localPlayerIsKeyHider)
            {
                localChosenKeyWorldPos ??= MultiplayerJson.ArrayToVector(dto.keyPosition);
            }
            else
            {
                peerChosenKeyWorldPos = MultiplayerJson.ArrayToVector(dto.keyPosition);
            }
        }

        if (dto.teacherPositionsX != null && dto.teacherPositionsY != null && dto.teacherPositionsZ != null)
        {
            Vector3?[] target = localPlayerIsKeyHider ? peerTeacherWorldPos : localTeacherWorldPos;
            CopyTeacherPositionsFromArrays(dto.teacherPositionsX, dto.teacherPositionsY, dto.teacherPositionsZ, target);
        }

        UpdatePlacementUiForRole();
    }

    private void HandleSetupFinalized(MultiplayerSetupSnapshotDto dto)
    {
        if (dto == null)
        {
            return;
        }

        if (dto.keyPosition != null && dto.keyPosition.Length == 3)
        {
            peerChosenKeyWorldPos = MultiplayerJson.ArrayToVector(dto.keyPosition);
            localChosenKeyWorldPos ??= peerChosenKeyWorldPos;
        }

        Vector3?[] finalizedTeachers = localPlayerIsKeyHider ? peerTeacherWorldPos : localTeacherWorldPos;
        if (dto.teacherPositionsX != null && dto.teacherPositionsY != null && dto.teacherPositionsZ != null)
        {
            CopyTeacherPositionsFromArrays(dto.teacherPositionsX, dto.teacherPositionsY, dto.teacherPositionsZ, finalizedTeachers);
        }

        localPlacementConfirmed = true;
        peerPlacementConfirmed = true;
        TryFinalizePlacement();
    }

    private static void CopyTeacherPositionsFromArrays(float[] x, float[] y, float[] z, Vector3?[] target)
    {
        if (x == null || y == null || z == null || target == null)
        {
            return;
        }

        int count = Mathf.Min(Mathf.Min(target.Length, x.Length), Mathf.Min(y.Length, z.Length));
        for (int i = 0; i < count; i++)
        {
            target[i] = new Vector3(x[i], y[i], z[i]);
        }
    }

    private void HandleRemotePlacement(MultiplayerSetupPlacementDto dto)
    {
        if (dto == null) return;
        peerPlacementConfirmed = true;

        if (dto.isKeyHider && dto.keyPosition != null && dto.keyPosition.Length == 3)
        {
            peerChosenKeyWorldPos = new Vector3(dto.keyPosition[0], dto.keyPosition[1], dto.keyPosition[2]);
        }

        if (!dto.isKeyHider
            && dto.teacherPositionsX != null && dto.teacherPositionsY != null && dto.teacherPositionsZ != null
            && dto.teacherPositionsX.Length == TeacherSlots.Length)
        {
            for (int i = 0; i < TeacherSlots.Length; i++)
            {
                peerTeacherWorldPos[i] = new Vector3(
                    dto.teacherPositionsX[i],
                    dto.teacherPositionsY[i],
                    dto.teacherPositionsZ[i]);
            }
        }

        UpdatePlacementUiForRole();
    }

    private void TryFinalizePlacement()
    {
        if (!localPlacementConfirmed || !peerPlacementConfirmed) return;

        Vector3? keyPos = localPlayerIsKeyHider ? localChosenKeyWorldPos : peerChosenKeyWorldPos;
        if (keyPos.HasValue) SpawnKeyAt(keyPos.Value);

        // The Teacher Placer's array is authoritative — both clients use it.
        Vector3?[] teachers = localPlayerIsKeyHider ? peerTeacherWorldPos : localTeacherWorldPos;
        ApplyTeacherPlacements(teachers);

        if (placementPanel != null) placementPanel.SetActive(false);
        if (placementMapCamera != null) placementMapCamera.targetTexture = null;
        // Kill the map-only light so it doesn't blow out the actual gameplay lighting.
        if (placementMapLight != null) placementMapLight.gameObject.SetActive(false);
        setupPhaseComplete = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Left-side scrollable column with the 10 teachers as draggable portraits.
    // Only visible to the Teacher Placer (hidden otherwise in UpdatePlacementUiForRole).
    private void BuildPlacementTeacherList(Color panel, Font font)
    {
        GameObject column = CreatePanel(placementPanel.transform, "PlacementTeacherColumn", panel, new Vector2(0.13f, 0.5f), new Vector2(220f, 720f));
        CreateText(column.transform, "TeacherListHeader", "TEACHERS", font, 22, new Vector2(0.5f, 0.96f), new Vector2(200f, 30f), new Color(1f, 0.95f, 0.78f, 1f));

        // Scroll view containing the items.
        GameObject scrollGo = new GameObject("TeacherScroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        scrollGo.transform.SetParent(column.transform, false);
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        RectTransform scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0.05f, 0.04f);
        scrollRect.anchorMax = new Vector2(0.95f, 0.92f);
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = Vector2.zero;

        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(scrollGo.transform, false);
        placementTeacherListContent = contentGo;
        RectTransform contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, TeacherSlots.Length * 78f);

        for (int i = 0; i < TeacherSlots.Length; i++)
        {
            placementTeacherListItems[i] = BuildTeacherListItem(contentGo.transform, i, font);
        }
    }

    private GameObject BuildTeacherListItem(Transform parent, int index, Font font)
    {
        GameObject item = new GameObject($"TeacherItem_{TeacherSlots[index].slug}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(PlacementTeacherDragHandler));
        item.transform.SetParent(parent, false);

        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 1f);
        itemRect.anchorMax = new Vector2(1f, 1f);
        itemRect.pivot = new Vector2(0.5f, 1f);
        itemRect.sizeDelta = new Vector2(0f, 70f);
        itemRect.anchoredPosition = new Vector2(0f, -(index * 78f) - 4f);

        Image bg = item.GetComponent<Image>();
        bg.color = new Color(0.16f, 0.18f, 0.22f, 1f);
        bg.raycastTarget = true;

        // Portrait — Texture2D loaded from the photos folder by slug.
        Texture2D portrait = LoadTeacherPortrait(TeacherSlots[index].slug);
        GameObject portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        portraitGo.transform.SetParent(item.transform, false);
        RawImage portraitImage = portraitGo.GetComponent<RawImage>();
        portraitImage.texture = portrait;
        portraitImage.color = portrait != null ? Color.white : new Color(0.3f, 0.3f, 0.35f, 1f);
        portraitImage.raycastTarget = false;
        placementTeacherPortraits[index] = portraitImage;
        RectTransform portraitRect = portraitGo.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0f, 0.5f);
        portraitRect.anchorMax = new Vector2(0f, 0.5f);
        portraitRect.pivot = new Vector2(0f, 0.5f);
        portraitRect.sizeDelta = new Vector2(60f, 60f);
        portraitRect.anchoredPosition = new Vector2(6f, 0f);

        Text label = CreateText(item.transform, "Name", TeacherSlots[index].displayName, font, 16,
            new Vector2(0.62f, 0.5f), new Vector2(140f, 60f), new Color(0.92f, 0.92f, 0.85f, 1f));
        label.alignment = TextAnchor.MiddleLeft;
        label.raycastTarget = false;

        var handler = item.GetComponent<PlacementTeacherDragHandler>();
        handler.slotIndex = index;
        handler.onBeginDrag = OnTeacherDragBegin;
        handler.onDrag = OnTeacherDragMove;
        handler.onEndDrag = OnTeacherDragEnd;

        return item;
    }

    private static Texture2D LoadTeacherPortrait(string slug)
    {
        // Primary path (works in builds too): the teacher GameObjects are already in
        // the scene, each with a PlaneHeadImage holding its face photo. Reuse it.
        GameObject teacherGo = GameObject.Find($"Teacher_{slug}");
        if (teacherGo != null)
        {
            var headImage = teacherGo.GetComponentInChildren<PlaneHeadImage>(true);
            if (headImage != null && headImage.HeadImage != null) return headImage.HeadImage;
        }

#if UNITY_EDITOR
        foreach (string ext in new[] { ".png", ".jpg", ".jpeg" })
        {
            string path = $"Assets/snimki na uchitelite/{slug}{ext}";
            Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return tex;
        }
#endif
        return Resources.Load<Texture2D>($"TeacherPortraits/{slug}");
    }

    private void OnTeacherDragBegin(int slotIndex, Vector2 screenPos)
    {
        if (!IsTeacherListVisible() || !CanModifyTeacherSlot(slotIndex)) return;
        Texture2D portrait = LoadTeacherPortrait(TeacherSlots[slotIndex].slug);
        if (placementDragGhostImage != null && portrait != null)
        {
            placementDragGhostImage.sprite = Sprite.Create(portrait,
                new Rect(0, 0, portrait.width, portrait.height), new Vector2(0.5f, 0.5f));
        }
        if (placementDragGhost != null)
        {
            placementDragGhost.SetActive(true);
            ((RectTransform)placementDragGhost.transform).position = screenPos;
        }
    }

    private void OnTeacherDragMove(int slotIndex, Vector2 screenPos)
    {
        if (placementDragGhost == null || !placementDragGhost.activeSelf) return;
        ((RectTransform)placementDragGhost.transform).position = screenPos;
    }

    private void OnTeacherDragEnd(int slotIndex, Vector2 screenPos)
    {
        if (placementDragGhost != null) placementDragGhost.SetActive(false);
        if (!IsTeacherListVisible() || !CanModifyTeacherSlot(slotIndex)) return;

        if (!TryMapClickToWorld(screenPos, out Vector3 worldPos)) return;

        localTeacherWorldPos[slotIndex] = worldPos;
        PlaceTeacherMarkerOnMap(slotIndex, screenPos);

        // Remove the dragged item from the list so the player can see what's left.
        if (placementTeacherListItems[slotIndex] != null)
        {
            placementTeacherListItems[slotIndex].SetActive(false);
        }

        UpdatePlacementUiForRole();
    }

    private bool IsTeacherListVisible()
    {
        return placementPanel != null && placementPanel.activeInHierarchy
               && !localPlayerIsKeyHider && !localPlacementConfirmed;
    }

    private bool CanModifyTeacherSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < TeacherSlots.Length && !localTeacherWorldPos[slotIndex].HasValue;
    }

    // Drop a small numbered dot on the map at the placed teacher's screen position so
    // the placer can see what they've staked out.
    private void PlaceTeacherMarkerOnMap(int slotIndex, Vector2 screenPos)
    {
        if (placementMapRect == null) return;

        Image marker = placementTeacherMapMarkers[slotIndex];
        if (marker == null)
        {
            GameObject markerGo = new GameObject($"TeacherMarker_{TeacherSlots[slotIndex].slug}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            markerGo.transform.SetParent(placementMapRect.transform, false);
            marker = markerGo.GetComponent<Image>();
            marker.color = new Color(0.95f, 0.4f, 0.4f, 0.95f);
            marker.raycastTarget = false;
            placementTeacherMapMarkers[slotIndex] = marker;
        }
        RectTransform markerRect = marker.rectTransform;
        markerRect.sizeDelta = new Vector2(20f, 20f);
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(placementMapRect, screenPos, null, out Vector2 local);
        markerRect.anchoredPosition = local;
    }

    private int CountPlacedTeachers()
    {
        int n = 0;
        for (int i = 0; i < localTeacherWorldPos.Length; i++)
            if (localTeacherWorldPos[i].HasValue) n++;
        return n;
    }

    private static string ExtractTeacherId(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return string.Empty;
        }

        string cleanName = objectName.Replace("(Clone)", string.Empty).Trim();
        const string prefix = "Teacher_";
        if (!cleanName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return cleanName.Substring(prefix.Length);
    }

    private ItemPickup ResolveCurrentKeyPickup(out bool heldByLocalPlayer)
    {
        heldByLocalPlayer = false;
        if (localInventory != null && localInventory.HeldPickup != null &&
            string.Equals(localInventory.HeldPickup.ItemId, "objective_exit_key", StringComparison.OrdinalIgnoreCase))
        {
            heldByLocalPlayer = true;
            return localInventory.HeldPickup;
        }

        if (spawnedKeyPickup != null)
        {
            return spawnedKeyPickup;
        }

        ItemPickup[] pickups = FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < pickups.Length; i++)
        {
            if (pickups[i] != null &&
                string.Equals(pickups[i].ItemId, "objective_exit_key", StringComparison.OrdinalIgnoreCase))
            {
                spawnedKeyPickup = pickups[i];
                spawnedKeyObject = pickups[i].gameObject;
                return spawnedKeyPickup;
            }
        }

        return null;
    }

    // After both confirms arrive, move each teacher GameObject in the scene to the
    // placement chosen for it. Uses the slug suffix on the GameObject name to bind
    // slot index → teacher; the order matches the array on the wire.
    private void ApplyTeacherPlacements(Vector3?[] positionsByIndex)
    {
        if (positionsByIndex == null) return;

        // Map slug → SimpleTeacherWander by walking the scene once.
        var teacherBySlug = new Dictionary<string, SimpleTeacherWander>(TeacherSlots.Length);
        foreach (var teacher in FindObjectsByType<SimpleTeacherWander>(FindObjectsSortMode.None))
        {
            string slug = ExtractTeacherId(teacher.gameObject.name);
            if (string.IsNullOrWhiteSpace(slug)) continue;
            teacherBySlug[slug] = teacher;
        }

        int moved = 0;
        for (int i = 0; i < TeacherSlots.Length; i++)
        {
            if (!positionsByIndex[i].HasValue) continue;
            if (!teacherBySlug.TryGetValue(TeacherSlots[i].slug, out var teacher)) continue;
            teacher.transform.position = positionsByIndex[i].Value;
            teacher.SetNetworkControlled(localPlayerIsKeyHider);
            moved++;
        }
        Debug.Log($"[SetupPhase] Teachers moved to placed positions: {moved}/{TeacherSlots.Length}.");
    }

    private GameObject SpawnKeyAt(Vector3 worldPos)
    {
        if (spawnedKeyObject != null)
        {
            DestroyUnityObject(spawnedKeyObject);
            spawnedKeyObject = null;
            spawnedKeyPickup = null;
        }

        // Remove any key the procedural generator already dropped, so there's exactly
        // one key — at the spot the Key Hider chose.
        foreach (var go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && go.name.StartsWith("KeyItem", StringComparison.Ordinal))
            {
                DestroyUnityObject(go);
            }
        }

        GameObject keyPrefab = Resources.Load<GameObject>("KeyItem");
#if UNITY_EDITOR
        if (keyPrefab == null)
            keyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/arhitektura/KeyItem.prefab");
#endif
        if (keyPrefab == null)
        {
            Debug.LogWarning("[SetupPhase] KeyItem prefab not found; key will not spawn.");
            return null;
        }

        spawnedKeyObject = Instantiate(keyPrefab, worldPos, Quaternion.identity);
        spawnedKeyObject.name = "KeyItem";
        spawnedKeyPickup = spawnedKeyObject.GetComponent<ItemPickup>();
        Debug.Log($"[SetupPhase] Key spawned at {worldPos}.");
        return spawnedKeyObject;
    }

    private void ApplyTeacherState(MultiplayerTeacherStateDto state)
    {
        if (state == null || state.seq <= 0 || string.IsNullOrWhiteSpace(state.teacherId))
        {
            return;
        }

        if (!localPlayerIsKeyHider && localMember != null && state.authoritativeUserId == localMember.userId)
        {
            return;
        }

        foreach (SimpleTeacherWander teacher in FindObjectsByType<SimpleTeacherWander>(FindObjectsSortMode.None))
        {
            string teacherId = ExtractTeacherId(teacher.gameObject.name);
            if (!string.Equals(teacherId, state.teacherId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            teacher.SetNetworkControlled(true);
            teacher.ApplyNetworkState(
                MultiplayerJson.ArrayToVector(state.position),
                MultiplayerJson.ArrayToVector(state.rotation),
                state.aiState,
                state.canSeePlayer,
                MultiplayerJson.ArrayToVector(state.lastKnownPlayerPosition));
            return;
        }
    }

    private void ApplyKeyState(MultiplayerKeyStateDto state)
    {
        if (state == null || state.seq <= 0)
        {
            return;
        }

        bool heldLocally = localInventory != null &&
            localInventory.HeldPickup != null &&
            string.Equals(localInventory.HeldPickup.ItemId, "objective_exit_key", StringComparison.OrdinalIgnoreCase);

        if (heldLocally && string.Equals(state.holderPlayerId, localMember?.playerId, StringComparison.Ordinal))
        {
            return;
        }

        ItemPickup key = ResolveCurrentKeyPickup(out _);
        if (key == null && !state.isHeld)
        {
            GameObject keyObject = SpawnKeyAt(MultiplayerJson.ArrayToVector(state.position));
            key = keyObject != null ? keyObject.GetComponent<ItemPickup>() : null;
        }

        if (key == null)
        {
            return;
        }

        bool heldByRemote = state.isHeld && !string.Equals(state.holderPlayerId, localMember?.playerId, StringComparison.Ordinal);
        key.gameObject.SetActive(!heldByRemote);
        if (!heldByRemote)
        {
            key.transform.position = MultiplayerJson.ArrayToVector(state.position);
            key.transform.rotation = Quaternion.Euler(MultiplayerJson.ArrayToVector(state.rotation));
        }
    }

    private void HandlePlayerEscaped()
    {
        Time.timeScale = 1f;
        gameStarted = false;
        generationReady = false;
        generationRequested = false;
        currentGameStart = null;
        localStateSeq = 0;

        setupPhaseComplete = true;
        if (roleRevealPanel != null) roleRevealPanel.SetActive(false);
        if (placementPanel != null) placementPanel.SetActive(false);
        localChosenKeyWorldPos = null;
        peerChosenKeyWorldPos = null;
        localPlacementConfirmed = false;
        peerPlacementConfirmed = false;
        for (int i = 0; i < TeacherSlots.Length; i++)
        {
            localTeacherWorldPos[i] = null;
            peerTeacherWorldPos[i] = null;
            if (placementTeacherListItems[i] != null) placementTeacherListItems[i].SetActive(true);
            if (placementTeacherMapMarkers[i] != null) DestroyUnityObject(placementTeacherMapMarkers[i].gameObject);
            placementTeacherMapMarkers[i] = null;
        }
        if (placementMapCamera != null) placementMapCamera.targetTexture = null;
        if (placementMapLight != null) placementMapLight.gameObject.SetActive(false);

        if (gameSocket != null)
        {
            gameSocket.Opened -= OnGameSocketOpened;
            gameSocket.MessageReceived -= OnGameSocketMessage;
            gameSocket.Closed -= OnGameSocketClosed;
            gameSocket.ErrorReceived -= OnGameSocketError;
            gameSocket.Close();
            gameSocket = null;
        }

        ClearRemotePlayers();
        if (runtimeGenerator != null && runtimeGenerator.GeneratedRoot != null)
        {
            DestroyUnityObject(runtimeGenerator.GeneratedRoot);
        }

        RefreshLocalReferences();
        SetLocalGameplayEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (guestAuthenticated && currentLobby != null)
        {
            ShowLobbyPanel();
            SetLobbyStatus("Run complete. You escaped. The paperwork did not.");
            ConnectLobbySocket(currentLobby.id);
            StartCoroutine(RefreshLobbyFromServer());
            return;
        }

        ShowAuthPanel("Run complete. Connect to the multiplayer desk for the next mistake.");
    }

    private void PreSpawnRemotePlayers(FacilityRuntimeGenerator generator)
    {
        if (currentLobby?.members == null || generator == null)
        {
            return;
        }

        for (int i = 0; i < currentLobby.members.Length; i++)
        {
            MultiplayerLobbyMemberDto member = currentLobby.members[i];
            if (member == null || localMember == null || member.userId == localMember.userId)
            {
                continue;
            }

            Vector3 position = localController != null ? localController.transform.position + new Vector3(1.5f + i, 0f, 0f) : Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            if (generator.TryGetSpawnPointForSlot(member.slot, out Vector3 spawnPosition, out Quaternion spawnRotation))
            {
                position = spawnPosition;
                rotation = spawnRotation;
            }

            GetOrCreateRemotePlayer(member, position, rotation);
        }
    }

    private void ApplyRemotePlayerState(MultiplayerPlayerStateDto state)
    {
        if (state == null || localMember == null || string.IsNullOrWhiteSpace(state.playerId))
        {
            return;
        }

        if (state.userId == localMember.userId || string.Equals(state.playerId, localMember.playerId, StringComparison.Ordinal))
        {
            return;
        }

        MultiplayerLobbyMemberDto member = FindMember(state.userId);
        Vector3 position = MultiplayerJson.ArrayToVector(state.position);
        Quaternion rotation = Quaternion.Euler(MultiplayerJson.ArrayToVector(state.rotation));
        EscapeBlock9RemotePlayerProxy proxy = GetOrCreateRemotePlayer(member, position, rotation, state.playerId, state.userId);
        proxy.ApplyNetworkState(state);
    }

    private EscapeBlock9RemotePlayerProxy GetOrCreateRemotePlayer(
        MultiplayerLobbyMemberDto member,
        Vector3 startPosition,
        Quaternion startRotation,
        string fallbackPlayerId = null,
        int fallbackUserId = 0)
    {
        string playerId = member != null && !string.IsNullOrWhiteSpace(member.playerId) ? member.playerId : fallbackPlayerId ?? string.Empty;
        if (remotePlayers.TryGetValue(playerId, out EscapeBlock9RemotePlayerProxy existing) && existing != null)
        {
            if (member != null)
            {
                existing.SetDisplayName(member.username);
            }

            return existing;
        }

        GameObject root = new GameObject($"RemotePlayer_{playerId}");
        EscapeBlock9RemotePlayerProxy proxy = root.AddComponent<EscapeBlock9RemotePlayerProxy>();
        proxy.Initialize(
            playerId,
            member != null ? member.userId : fallbackUserId,
            member != null ? member.slot : 0,
            member != null ? member.username : $"Player {fallbackUserId}",
            startPosition,
            startRotation);
        remotePlayers[playerId] = proxy;
        return proxy;
    }

    private void RemoveRemotePlayer(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        if (remotePlayers.TryGetValue(playerId, out EscapeBlock9RemotePlayerProxy proxy))
        {
            remotePlayers.Remove(playerId);
            if (proxy != null)
            {
                DestroyUnityObject(proxy.gameObject);
            }
        }
    }

    private void RebuildLobbyMemberLookup()
    {
        lobbyMembersByUserId.Clear();
        if (currentLobby?.members == null)
        {
            return;
        }

        for (int i = 0; i < currentLobby.members.Length; i++)
        {
            MultiplayerLobbyMemberDto member = currentLobby.members[i];
            if (member != null)
            {
                lobbyMembersByUserId[member.userId] = member;
            }
        }
    }

    private MultiplayerLobbyMemberDto FindMember(int userId)
    {
        return lobbyMembersByUserId.TryGetValue(userId, out MultiplayerLobbyMemberDto member) ? member : null;
    }

    private MultiplayerLobbyMemberDto FindLocalMemberFromGameStart(MultiplayerGameStartedDto start)
    {
        if (start?.players == null || currentUser == null)
        {
            return localMember;
        }

        for (int i = 0; i < start.players.Length; i++)
        {
            MultiplayerGameStartedPlayerDto player = start.players[i];
            if (player != null && player.userId == currentUser.id)
            {
                return new MultiplayerLobbyMemberDto
                {
                    userId = player.userId,
                    playerId = player.playerId,
                    slot = player.slot,
                    username = currentUser.username,
                    isReady = true
                };
            }
        }

        return localMember;
    }

    private void UpdateLobbyUi()
    {
        if (currentLobby == null)
        {
            return;
        }

        lobbyCodeText.text = $"Code: {currentLobby.code}";
        lobbyTitleText.text = $"Lobby Host: {(currentLobby.hostId == currentUser?.id ? "You" : $"User {currentLobby.hostId}")}";
        for (int i = 0; i < slotViews.Count; i++)
        {
            MultiplayerLobbyMemberDto member = FindMemberInSlot(i);
            slotViews[i].SetMember(member, member != null && currentUser != null && member.userId == currentUser.id);
        }

        bool isHost = currentUser != null && currentLobby.hostId == currentUser.id;
        bool allReady = AreAllMembersReady();
        readyButton.GetComponentInChildren<Text>().text = localMember != null && localMember.isReady ? "Unready" : "Ready Up";
        readyButton.interactable = localMember != null;
        startButton.interactable = isHost && allReady && CountMembers() == MaxPlayers;
        startButton.GetComponentInChildren<Text>().text = isHost ? "Start Run" : "Waiting For Host";
    }

    private MultiplayerLobbyMemberDto FindMemberInSlot(int slot)
    {
        if (currentLobby?.members == null)
        {
            return null;
        }

        for (int i = 0; i < currentLobby.members.Length; i++)
        {
            MultiplayerLobbyMemberDto member = currentLobby.members[i];
            if (member != null && member.slot == slot)
            {
                return member;
            }
        }

        return null;
    }

    private bool AreAllMembersReady()
    {
        if (currentLobby?.members == null || currentLobby.members.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < currentLobby.members.Length; i++)
        {
            if (currentLobby.members[i] == null || !currentLobby.members[i].isReady)
            {
                return false;
            }
        }

        return true;
    }

    private int CountMembers()
    {
        return currentLobby?.members?.Length ?? 0;
    }

    private bool EnsureAuthenticated()
    {
        if (guestAuthenticated)
        {
            return true;
        }

        SetLobbyStatus("Connect as a guest first.");
        return false;
    }

    private void ShowAuthPanel(string status)
    {
        authPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        overlayPanel.SetActive(false);
        SetAuthStatus(status);
        PlayLobbyMusic();
    }

    private void ShowLobbyPanel()
    {
        authPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        overlayPanel.SetActive(false);
        SetLobbyStatus("Create or join a 2-player lobby.");
        PlayLobbyMusic();
    }

    private void SetAuthStatus(string status)
    {
        if (authStatusText != null)
        {
            authStatusText.text = status ?? string.Empty;
        }
    }

    private void SetLobbyStatus(string status)
    {
        if (lobbyStatusText != null)
        {
            lobbyStatusText.text = status ?? string.Empty;
        }
    }

    private void SetOverlayStatus(string status)
    {
        if (overlayStatusText != null)
        {
            overlayStatusText.text = status ?? string.Empty;
        }
    }

    private void ResetToAuth()
    {
        suppressBuiltInUi = true;
        gameStarted = false;
        generationReady = false;
        generationRequested = false;
        currentLobby = null;
        localMember = null;
        currentGameStart = null;
        if (lobbySocket != null)
        {
            lobbySocket.Close();
            lobbySocket = null;
        }

        if (gameSocket != null)
        {
            gameSocket.Close();
            gameSocket = null;
        }

        ClearRemotePlayers();
        ShowAuthPanel(guestAuthenticated ? $"Connected as {currentUser?.username}. You can reconnect to a lobby." : "Connect to the hosted backend as a guest.");
    }

    private void ClearRemotePlayers()
    {
        foreach (KeyValuePair<string, EscapeBlock9RemotePlayerProxy> pair in remotePlayers)
        {
            if (pair.Value != null)
            {
                DestroyUnityObject(pair.Value.gameObject);
            }
        }

        remotePlayers.Clear();
    }

    private void EnsureLobbyMusicSource()
    {
        if (lobbyMusicSource != null)
        {
            return;
        }

        lobbyMusicSource = gameObject.GetComponent<AudioSource>();
        if (lobbyMusicSource == null)
        {
            lobbyMusicSource = gameObject.AddComponent<AudioSource>();
        }

        lobbyMusicSource.playOnAwake = false;
        lobbyMusicSource.loop = true;
        lobbyMusicSource.spatialBlend = 0f;
        lobbyMusicSource.volume = 0.45f;
    }

    private void PlayLobbyMusic()
    {
        EnsureLobbyMusicSource();
        if (lobbyMusicSource == null)
        {
            return;
        }

        if (lobbyMusicSource.clip != null)
        {
            if (!lobbyMusicSource.isPlaying)
            {
                lobbyMusicSource.Play();
            }

            return;
        }

        if (lobbyMusicRoutine == null)
        {
            lobbyMusicRoutine = StartCoroutine(LoadLobbyMusicClip());
        }
    }

    private void StopLobbyMusic()
    {
        if (lobbyMusicSource != null && lobbyMusicSource.isPlaying)
        {
            lobbyMusicSource.Stop();
        }
    }

    private IEnumerator LoadLobbyMusicClip()
    {
        string resolvedPath = ResolveLobbyMusicFilePath();
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            Debug.LogWarning($"No supported lobby music file found at '{LobbyMusicPath}'.");
            lobbyMusicRoutine = null;
            yield break;
        }

        string url = new Uri(resolvedPath).AbsoluteUri;
        AudioType audioType = ResolveAudioTypeFromPath(resolvedPath);
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Failed to load lobby music '{resolvedPath}': {request.error}");
                lobbyMusicRoutine = null;
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null)
            {
                Debug.LogWarning($"Lobby music loaded but returned no clip for '{resolvedPath}'.");
                lobbyMusicRoutine = null;
                yield break;
            }

            EnsureLobbyMusicSource();
            lobbyMusicSource.clip = clip;
            lobbyMusicSource.Play();
        }

        lobbyMusicRoutine = null;
    }

    private static string ResolveLobbyMusicFilePath()
    {
        if (string.IsNullOrWhiteSpace(LobbyMusicPath))
        {
            return null;
        }

        if (File.Exists(LobbyMusicPath))
        {
            return LobbyMusicPath;
        }

        if (!Directory.Exists(LobbyMusicPath))
        {
            return null;
        }

        string[] extensions = { "*.mp3", "*.wav", "*.ogg", "*.flac", "*.aiff", "*.m4a" };
        for (int i = 0; i < extensions.Length; i++)
        {
            string[] files = Directory.GetFiles(LobbyMusicPath, extensions[i], SearchOption.AllDirectories);
            if (files != null && files.Length > 0)
            {
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                return files[0];
            }
        }

        return null;
    }

    private static AudioType ResolveAudioTypeFromPath(string path)
    {
        string extension = Path.GetExtension(path)?.ToLowerInvariant();
        switch (extension)
        {
            case ".mp3":
                return AudioType.MPEG;
            case ".ogg":
                return AudioType.OGGVORBIS;
            case ".aiff":
                return AudioType.AIFF;
            case ".flac":
            case ".m4a":
                return AudioType.UNKNOWN;
            default:
                return AudioType.WAV;
        }
    }

    private static void DestroyUnityObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
            return;
        }

        DestroyImmediate(target);
    }

    private static bool IsHeartbeatTimedOut(float lastPingSentAt, float lastPongAt, float lastHeartbeatAt, float timeoutSeconds)
    {
        if (lastPingSentAt < 0f)
        {
            return false;
        }

        float latestInbound = Mathf.Max(lastPongAt, lastHeartbeatAt);
        return latestInbound >= 0f && Time.unscaledTime - latestInbound > timeoutSeconds;
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

    private static void CreateMenuDecor(Transform parent)
    {
        CreatePanel(parent, "TopBloodBand", new Color(0.27f, 0.015f, 0.018f, 1f), new Vector2(0.5f, 0.93f), new Vector2(2200f, 150f));
        CreatePanel(parent, "WarningStripe", new Color(0.86f, 0.66f, 0.16f, 0.95f), new Vector2(0.5f, 0.68f), new Vector2(1040f, 16f));
        CreatePanel(parent, "LowInkBand", new Color(0.025f, 0.028f, 0.028f, 1f), new Vector2(0.5f, 0.08f), new Vector2(2200f, 150f));
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchor, Vector2 size)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        Image image = panel.AddComponent<Image>();
        image.color = color;
        return panel;
    }

    private static Text CreateText(Transform parent, string name, string value, Font font, int fontSize, Vector2 anchor, Vector2 size, Color color)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
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

    private static InputField CreateInputField(Transform parent, string name, Font font, Vector2 anchor, Vector2 size, string defaultText)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        RectTransform rect = root.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        Image image = root.AddComponent<Image>();
        image.color = new Color(0.018f, 0.02f, 0.022f, 1f);
        InputField input = root.AddComponent<InputField>();

        GameObject placeholderObject = new GameObject("Placeholder");
        placeholderObject.transform.SetParent(root.transform, false);
        RectTransform placeholderRect = placeholderObject.AddComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(14f, 10f);
        placeholderRect.offsetMax = new Vector2(-14f, -10f);
        Text placeholder = placeholderObject.AddComponent<Text>();
        placeholder.font = font;
        placeholder.fontSize = 18;
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.color = new Color(0.7f, 0.64f, 0.48f, 0.75f);
        placeholder.text = "Type here before the lights notice";

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(root.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 10f);
        textRect.offsetMax = new Vector2(-14f, -10f);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = 18;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = new Color(0.98f, 0.93f, 0.78f, 1f);
        text.text = defaultText;

        input.textComponent = text;
        input.placeholder = placeholder;
        input.text = defaultText;
        return input;
    }

    private static Button CreateButton(Transform parent, string name, string label, Font font, Vector2 anchor, Vector2 size, Color color, UnityEngine.Events.UnityAction callback)
    {
        GameObject buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        Image image = buttonObject.AddComponent<Image>();
        image.color = color;
        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(callback);
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, new Color(1f, 0.85f, 0.25f, 1f), 0.22f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.28f);
        button.colors = colors;
        Text labelText = CreateText(buttonObject.transform, "Label", label, font, 20, new Vector2(0.5f, 0.5f), size - new Vector2(18f, 14f), new Color(1f, 0.96f, 0.82f, 1f));
        AddTextOutline(labelText, new Color(0f, 0f, 0f, 0.65f), new Vector2(1.2f, -1.2f));
        return button;
    }

    private static PlayerSlotView CreateSlotView(Transform parent, Font font, Vector2 anchor, int slotNumber)
    {
        GameObject root = CreatePanel(parent, $"Slot{slotNumber}", new Color(0.13f, 0.16f, 0.24f, 0.96f), anchor, new Vector2(620f, 74f));
        Text title = CreateText(root.transform, "Title", $"Player {slotNumber}", font, 20, new Vector2(0.18f, 0.5f), new Vector2(180f, 32f), Color.white);
        title.alignment = TextAnchor.MiddleLeft;
        Text state = CreateText(root.transform, "State", "Open", font, 18, new Vector2(0.76f, 0.5f), new Vector2(180f, 30f), new Color(0.74f, 0.8f, 0.9f, 1f));
        state.alignment = TextAnchor.MiddleRight;
        return new PlayerSlotView(root, title, state);
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
        DontDestroyOnLoad(eventSystemObject);
    }

    private readonly struct PlayerSlotView
    {
        private readonly GameObject root;
        private readonly Text title;
        private readonly Text state;

        public PlayerSlotView(GameObject root, Text title, Text state)
        {
            this.root = root;
            this.title = title;
            this.state = state;
        }

        public void SetMember(MultiplayerLobbyMemberDto member, bool isLocal)
        {
            Image image = root.GetComponent<Image>();
            if (member == null)
            {
                title.text = "Open Slot";
                state.text = "Waiting...";
                state.color = new Color(0.74f, 0.8f, 0.9f, 1f);
                image.color = new Color(0.13f, 0.16f, 0.24f, 0.96f);
                return;
            }

            title.text = isLocal ? $"{member.username} (You)" : member.username;
            state.text = member.isReady ? "Ready" : "Not Ready";
            state.color = member.isReady ? new Color(0.3f, 0.92f, 0.5f, 1f) : new Color(0.94f, 0.74f, 0.3f, 1f);
            image.color = isLocal ? new Color(0.17f, 0.24f, 0.34f, 0.98f) : new Color(0.13f, 0.16f, 0.24f, 0.96f);
        }
    }
}
