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
    private Button connectGuestButton;
    private Button createLobbyButton;
    private Button joinLobbyButton;
    private Button readyButton;
    private Button startButton;
    private Button resetButton;
    private readonly List<PlayerSlotView> slotViews = new List<PlayerSlotView>();

    private FirstPersonController localController;
    private PlayerItemInteractor localItemInteractor;
    private PlayerEntityInteractor localEntityInteractor;
    private FacilityRuntimeGenerator runtimeGenerator;
    private AudioSource lobbyMusicSource;
    private Coroutine lobbyMusicRoutine;
    private readonly Dictionary<string, EscapeBlock9RemotePlayerProxy> remotePlayers = new Dictionary<string, EscapeBlock9RemotePlayerProxy>();
    private readonly Dictionary<int, MultiplayerLobbyMemberDto> lobbyMembersByUserId = new Dictionary<int, MultiplayerLobbyMemberDto>();

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

        if (Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame)
        {
            ResetToAuth();
        }
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
        CreateMenuDecor(authPanel.transform, font, "AUTHORIZED PERSONNEL ONLY");
        Text authTitle = CreateText(authPanel.transform, "AuthTitle", "ESCAPE BLOCK 9", font, 68, new Vector2(0.5f, 0.82f), new Vector2(980f, 92f), new Color(1f, 0.95f, 0.78f, 1f));
        AddTextOutline(authTitle, new Color(0.55f, 0f, 0.02f, 1f), new Vector2(3f, -3f));
        CreateText(authPanel.transform, "AuthSubtitle", "Co-op horror for people who read the evacuation plan and still got lost.", font, 24, new Vector2(0.5f, 0.74f), new Vector2(980f, 44f), new Color(0.78f, 0.84f, 0.78f, 1f));
        GameObject authCard = CreatePanel(authPanel.transform, "AuthCard", panel, new Vector2(0.5f, 0.47f), new Vector2(780f, 370f));
        CreateText(authCard.transform, "ServerLabel", "Backend Address", font, 21, new Vector2(0.5f, 0.8f), new Vector2(640f, 34f), new Color(0.88f, 0.8f, 0.58f, 1f));
        serverUrlInput = CreateInputField(authCard.transform, "ServerInput", font, new Vector2(0.5f, 0.63f), new Vector2(640f, 54f), api.BaseHttpUrl);
        connectGuestButton = CreateButton(authCard.transform, "ConnectGuestButton", "Claim Guest Badge", font, new Vector2(0.5f, 0.38f), new Vector2(340f, 60f), accent, OnConnectGuestPressed);
        authStatusText = CreateText(authCard.transform, "AuthStatus", string.Empty, font, 18, new Vector2(0.5f, 0.16f), new Vector2(660f, 76f), new Color(0.9f, 0.9f, 0.82f, 1f));

        lobbyPanel = CreateFullScreenPanel(rootCanvas.transform, "LobbyPanel", ink);
        CreateMenuDecor(lobbyPanel.transform, font, "LOBBY DESK - ONE FORM PER PANIC");
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

        authPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        overlayPanel.SetActive(false);
    }

    private void RefreshLocalReferences()
    {
        if (localController == null)
        {
            localController = FindAnyObjectByType<FirstPersonController>();
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

        bool enableGameplay = gameStarted && generationReady;
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
        bool shouldSend = localStateSeq == 0
            || (position - lastSentPosition).sqrMagnitude >= MinPositionDeltaSqr
            || Quaternion.Angle(Quaternion.Euler(lastSentEulerAngles), Quaternion.Euler(eulerAngles)) >= MinRotationDelta
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
            localController.CurrentVelocity);
        gameSocket.SendJson(JsonUtility.ToJson(state));
        lastSentPosition = position;
        lastSentEulerAngles = eulerAngles;
        lastStateSendTime = Time.unscaledTime;
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
                if (snapshot.players != null)
                {
                    for (int i = 0; i < snapshot.players.Length; i++)
                    {
                        ApplyRemotePlayerState(snapshot.players[i]);
                    }
                }
                break;
            }
            case "player_state":
                ApplyRemotePlayerState(JsonUtility.FromJson<MultiplayerPlayerStateDto>(json));
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
    }

    private void HandlePlayerEscaped()
    {
        Time.timeScale = 1f;
        gameStarted = false;
        generationReady = false;
        generationRequested = false;
        currentGameStart = null;
        localStateSeq = 0;

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

    private static void CreateMenuDecor(Transform parent, Font font, string bannerText)
    {
        CreatePanel(parent, "TopBloodBand", new Color(0.27f, 0.015f, 0.018f, 1f), new Vector2(0.5f, 0.93f), new Vector2(2200f, 150f));
        CreatePanel(parent, "WarningStripe", new Color(0.86f, 0.66f, 0.16f, 0.95f), new Vector2(0.5f, 0.68f), new Vector2(1040f, 16f));
        CreatePanel(parent, "LowInkBand", new Color(0.025f, 0.028f, 0.028f, 1f), new Vector2(0.5f, 0.08f), new Vector2(2200f, 150f));
        Text banner = CreateText(parent, "BannerText", bannerText, font, 18, new Vector2(0.5f, 0.94f), new Vector2(900f, 34f), new Color(1f, 0.89f, 0.55f, 1f));
        AddTextOutline(banner, new Color(0f, 0f, 0f, 0.75f), new Vector2(1.5f, -1.5f));
        CreateText(parent, "MenuFooter", "Block 9 guarantees two exits: one legal, one theoretical.", font, 18, new Vector2(0.5f, 0.08f), new Vector2(900f, 34f), new Color(0.68f, 0.66f, 0.56f, 1f));
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
