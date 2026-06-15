using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.Video;

public class PlayerEntityInteractor : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PickupPromptUI talkPromptUi;
    [SerializeField] private DialogueChoiceUI dialogueChoiceUi;
    [SerializeField] private bool verboseLogging;
    [SerializeField] private float maxClipLoadSeconds = 12f;
    [Header("Voice Runtime Audio")]
    [SerializeField] private bool enableOcclusion = true;
    [SerializeField] private LayerMask occlusionMask = ~0;
    [SerializeField] private float unobstructedCutoff = 22000f;
    [SerializeField] private float obstructedCutoff = 1400f;

    private Entity currentTarget;
    private bool isPromptCursorMode;
    private FirstPersonController firstPersonController;
    private int talksCount;
    private bool isInConversation;
    private int selectedChoiceIndex;
    private bool choiceSelected;
    private readonly HashSet<string> conversationFlags = new HashSet<string>();
    private static readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

    private class RuntimeDialogueTree
    {
        public AudioDialogueTreeData tree;
        public string audioBaseUrl;
        public string sourceApiUrl;
    }

    public bool IsChoiceModeActive => isInConversation && dialogueChoiceUi != null && dialogueChoiceUi.IsVisible;

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (talkPromptUi == null)
        {
            talkPromptUi = FindAnyObjectByType<PickupPromptUI>();
            if (talkPromptUi == null)
            {
                GameObject uiObj = new GameObject("TalkPromptUI");
                talkPromptUi = uiObj.AddComponent<PickupPromptUI>();
            }
        }

        if (dialogueChoiceUi == null)
        {
            dialogueChoiceUi = FindAnyObjectByType<DialogueChoiceUI>();
            if (dialogueChoiceUi == null)
            {
                GameObject uiObj = new GameObject("DialogueChoiceUI");
                dialogueChoiceUi = uiObj.AddComponent<DialogueChoiceUI>();
            }
        }

        firstPersonController = GetComponent<FirstPersonController>();
        EnsureAudioListener();
    }

    private void Update()
    {
        if (isInConversation)
        {
            talkPromptUi.Hide();
            if (isPromptCursorMode)
            {
                EnforcePromptCursorMode();
            }
            return;
        }

        currentTarget = FindNearestEntity();
        if (currentTarget == null)
        {
            talkPromptUi.Hide();
            SetPromptCursorMode(false);
            return;
        }

        string prompt = $"{currentTarget.TalkPromptText} {currentTarget.EntityName}";
        talkPromptUi.Show(currentTarget.TalkKeyLabel, prompt, TryTalkCurrentTarget);
        SetPromptCursorMode(false);

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            TryTalkCurrentTarget();
        }
    }

    private void TryTalkCurrentTarget()
    {
        if (currentTarget == null)
        {
            return;
        }

        StartCoroutine(RunConversation(currentTarget));
        talkPromptUi.Hide();
        SetPromptCursorMode(false);
    }

    private IEnumerator RunConversation(Entity entity)
    {
        isInConversation = true;
        dialogueChoiceUi.Hide();
        SetPromptCursorMode(false);
        conversationFlags.Clear();

        RuntimeDialogueTree runtimeTree = null;
        yield return LoadDialogueTreeForEntity(entity, result => runtimeTree = result);
        if (runtimeTree == null || runtimeTree.tree == null || runtimeTree.tree.nodes == null || runtimeTree.tree.nodes.Length == 0)
        {
            if (verboseLogging)
            {
                Debug.LogWarning($"No dialogue tree available for entity '{entity.EntityName}'.");
            }
            isInConversation = false;
            SetPromptCursorMode(false);
            yield break;
        }

        Dictionary<string, AudioDialogueNodeData> lookup = AudioDialogueInterpreter.BuildLookup(runtimeTree.tree);
        AudioDialogueNodeData currentNode = AudioDialogueInterpreter.GetStartNode(runtimeTree.tree, lookup);
        string lastNodeId = string.Empty;
        HashSet<string> visited = new HashSet<string>();

        while (currentNode != null)
        {
            if (!string.IsNullOrWhiteSpace(currentNode.id) && visited.Contains(currentNode.id))
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(currentNode.id))
            {
                visited.Add(currentNode.id);
                lastNodeId = currentNode.id;
            }

            yield return PlayNodeAudio(entity, currentNode, runtimeTree);

            int chosen = 0;
            if (currentNode.choices != null && currentNode.choices.Length > 0)
            {
                // Ensure options appear only after all prior node audio has fully passed.
                yield return null;
                choiceSelected = false;
                selectedChoiceIndex = 0;
                SetPromptCursorMode(true);
                dialogueChoiceUi.Show(AudioDialogueInterpreter.BuildUiChoices(currentNode), OnChoiceSelected);

                while (!choiceSelected)
                {
                    yield return null;
                }

                dialogueChoiceUi.Hide();
                SetPromptCursorMode(false);
                chosen = selectedChoiceIndex;
                ApplyChoiceFlags(currentNode, chosen);
            }

            string nextNodeId = AudioDialogueInterpreter.ResolveNextNodeId(currentNode, chosen);
            if (!AudioDialogueInterpreter.TryGetNode(nextNodeId, lookup, out currentNode))
            {
                break;
            }
        }

        talksCount++;
        entity.MarkTalked(lastNodeId, talksCount);
        isInConversation = false;
        SetPromptCursorMode(false);
    }

    private void OnChoiceSelected(int index)
    {
        selectedChoiceIndex = index;
        choiceSelected = true;
    }

    private void ApplyChoiceFlags(AudioDialogueNodeData node, int chosenIndex)
    {
        if (node == null || node.choices == null || node.choices.Length == 0)
        {
            return;
        }

        int index = Mathf.Clamp(chosenIndex, 0, node.choices.Length - 1);
        AudioDialogueChoiceData choice = node.choices[index];
        if (choice == null || choice.setFlags == null)
        {
            return;
        }

        foreach (string flag in choice.setFlags)
        {
            if (!string.IsNullOrWhiteSpace(flag))
            {
                conversationFlags.Add(flag.Trim());
            }
        }
    }

    private IEnumerator PlayNodeAudio(Entity entity, AudioDialogueNodeData node, RuntimeDialogueTree runtimeTree)
    {
        if (entity == null || node == null || node.lines == null || node.lines.Length == 0)
        {
            yield break;
        }

        AudioSource source = entity.GetOrCreateVoiceSource();
        if (source == null)
        {
            yield break;
        }

        foreach (AudioDialogueLineData line in node.lines)
        {
            AudioDialogueVariantData variant = SelectVariant(line);
            if (variant == null)
            {
                continue;
            }

            string clipUrl = ResolveVariantUrl(variant, runtimeTree);
            if (string.IsNullOrWhiteSpace(clipUrl))
            {
                if (verboseLogging)
                {
                    Debug.LogWarning($"Skipping line '{line.lineId}' because clip URL is missing.");
                }
                continue;
            }

            AudioClip clip = null;
            yield return GetAudioClip(clipUrl, result => clip = result);
            if (clip == null)
            {
                bool playedByVideoFallback = false;
                yield return PlayViaVideoFallback(entity, clipUrl, result => playedByVideoFallback = result);
                if (playedByVideoFallback)
                {
                    continue;
                }

                continue;
            }

            source.clip = clip;
            source.Play();

            while (source != null && source.isPlaying)
            {
                ApplyLiveVoiceFilters(entity, source);
                yield return null;
            }
        }
    }

    private void ApplyLiveVoiceFilters(Entity entity, AudioSource source)
    {
        if (entity == null || source == null)
        {
            return;
        }

        AudioLowPassFilter lowPass = entity.GetOrCreateVoiceLowPassFilter();
        if (lowPass == null || !lowPass.enabled)
        {
            return;
        }

        if (!enableOcclusion || playerCamera == null)
        {
            lowPass.cutoffFrequency = Mathf.Clamp(unobstructedCutoff, 10f, 22000f);
            return;
        }

        Vector3 origin = source.transform.position;
        Vector3 target = playerCamera.transform.position;
        Vector3 toTarget = target - origin;
        float distance = toTarget.magnitude;
        if (distance < 0.001f)
        {
            lowPass.cutoffFrequency = Mathf.Clamp(unobstructedCutoff, 10f, 22000f);
            return;
        }

        bool blocked = Physics.Raycast(
            origin,
            toTarget / distance,
            distance,
            occlusionMask,
            QueryTriggerInteraction.Ignore);

        float cutoff = blocked ? obstructedCutoff : unobstructedCutoff;
        lowPass.cutoffFrequency = Mathf.Clamp(cutoff, 10f, 22000f);
    }

    private void EnsureAudioListener()
    {
        Camera cam = playerCamera != null ? playerCamera : Camera.main;
        if (cam == null)
        {
            return;
        }

        AudioListener listener = cam.GetComponent<AudioListener>();
        if (listener == null)
        {
            cam.gameObject.AddComponent<AudioListener>();
        }
    }

    private AudioDialogueVariantData SelectVariant(AudioDialogueLineData line)
    {
        if (line == null || line.variants == null || line.variants.Length == 0)
        {
            return null;
        }

        List<AudioDialogueVariantData> candidates = new List<AudioDialogueVariantData>();
        foreach (AudioDialogueVariantData variant in line.variants)
        {
            if (variant == null || string.IsNullOrWhiteSpace(variant.clip))
            {
                continue;
            }

            if (!PassesFlagFilters(variant))
            {
                continue;
            }

            candidates.Add(variant);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        float totalWeight = 0f;
        foreach (AudioDialogueVariantData candidate in candidates)
        {
            totalWeight += Mathf.Max(0.001f, candidate.weight);
        }

        float pick = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (AudioDialogueVariantData candidate in candidates)
        {
            cumulative += Mathf.Max(0.001f, candidate.weight);
            if (pick <= cumulative)
            {
                return candidate;
            }
        }

        return candidates[candidates.Count - 1];
    }

    private bool PassesFlagFilters(AudioDialogueVariantData variant)
    {
        if (variant.requiredFlags != null)
        {
            foreach (string required in variant.requiredFlags)
            {
                if (!string.IsNullOrWhiteSpace(required) && !conversationFlags.Contains(required.Trim()))
                {
                    return false;
                }
            }
        }

        if (variant.excludedFlags != null)
        {
            foreach (string excluded in variant.excludedFlags)
            {
                if (!string.IsNullOrWhiteSpace(excluded) && conversationFlags.Contains(excluded.Trim()))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static string ResolveVariantUrl(AudioDialogueVariantData variant, RuntimeDialogueTree runtimeTree)
    {
        if (variant == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(variant.resolvedClipUrl))
        {
            return variant.resolvedClipUrl.Trim();
        }

        if (string.IsNullOrWhiteSpace(variant.clip))
        {
            return null;
        }

        string clip = variant.clip.Trim();
        if (clip.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            clip.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return clip;
        }

        if (runtimeTree != null && !string.IsNullOrWhiteSpace(runtimeTree.audioBaseUrl))
        {
            string baseUrl = runtimeTree.audioBaseUrl.Trim();
            if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
            {
                baseUrl += "/";
            }
            return baseUrl + clip.TrimStart('/');
        }

        if (runtimeTree != null && !string.IsNullOrWhiteSpace(runtimeTree.sourceApiUrl))
        {
            Uri apiUri = new Uri(runtimeTree.sourceApiUrl);
            Uri full = new Uri(apiUri, clip);
            return full.AbsoluteUri;
        }

        return clip;
    }

    private IEnumerator GetAudioClip(string clipUrl, System.Action<AudioClip> onDone)
    {
        if (string.IsNullOrWhiteSpace(clipUrl))
        {
            if (verboseLogging)
            {
                Debug.LogWarning("Audio clip URL is empty.");
            }
            onDone?.Invoke(null);
            yield break;
        }

        if (clipCache.TryGetValue(clipUrl, out AudioClip cached) && cached != null)
        {
            onDone?.Invoke(cached);
            yield break;
        }

        AudioType[] candidates = GetAudioTypeCandidates(clipUrl);
        for (int i = 0; i < candidates.Length; i++)
        {
            AudioType candidateType = candidates[i];
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(clipUrl, candidateType))
            {
                request.timeout = Mathf.CeilToInt(maxClipLoadSeconds);
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (verboseLogging)
                    {
                        Debug.LogWarning(
                            $"Audio download failed: {clipUrl} (type {candidateType}). Error: {request.error}");
                    }
                    continue;
                }

                AudioClip clip = null;
                try
                {
                    clip = DownloadHandlerAudioClip.GetContent(request);
                }
                catch (Exception ex)
                {
                    if (verboseLogging)
                    {
                        string contentType = request.GetResponseHeader("Content-Type");
                        Debug.LogWarning(
                            $"Audio decode failed: {clipUrl} (type {candidateType}, response {request.responseCode}, content-type {contentType}). {ex.Message}");
                    }
                    continue;
                }

                if (clip == null || clip.length <= 0f)
                {
                    continue;
                }

                clipCache[clipUrl] = clip;
                onDone?.Invoke(clip);
                yield break;
            }
        }

        if (verboseLogging)
        {
            Debug.LogWarning($"All audio decode attempts failed for: {clipUrl}");
        }

        onDone?.Invoke(null);
    }

    private IEnumerator PlayViaVideoFallback(Entity entity, string clipUrl, Action<bool> onDone)
    {
        onDone?.Invoke(false);
        if (entity == null || string.IsNullOrWhiteSpace(clipUrl))
        {
            yield break;
        }

        if (!clipUrl.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase) &&
            !clipUrl.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        VideoPlayer player = entity.GetOrCreateVoiceVideoPlayer();
        if (player == null)
        {
            yield break;
        }

        bool prepared = false;
        bool prepareFailed = false;

        void HandlePrepared(VideoPlayer _) => prepared = true;
        void HandleError(VideoPlayer _, string __) => prepareFailed = true;

        player.prepareCompleted += HandlePrepared;
        player.errorReceived += HandleError;

        player.url = clipUrl;
        player.Prepare();

        float timeoutAt = Time.time + maxClipLoadSeconds;
        while (!prepared && !prepareFailed && Time.time < timeoutAt)
        {
            yield return null;
        }

        player.prepareCompleted -= HandlePrepared;
        player.errorReceived -= HandleError;

        if (!prepared || prepareFailed)
        {
            if (verboseLogging)
            {
                Debug.LogWarning($"Video fallback prepare failed for: {clipUrl}");
            }
            yield break;
        }

        player.Play();
        onDone?.Invoke(true);

        while (player.isPlaying)
        {
            AudioSource source = entity.GetOrCreateVoiceSource();
            ApplyLiveVoiceFilters(entity, source);
            yield return null;
        }
    }

    private static AudioType InferAudioTypeFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return AudioType.UNKNOWN;
        }

        string path = url;
        int queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
        {
            path = path.Substring(0, queryIndex);
        }

        if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            return AudioType.WAV;
        }
        if (path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            return AudioType.MPEG;
        }
        if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            return AudioType.OGGVORBIS;
        }
        if (path.EndsWith(".aac", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
        {
            return AudioType.ACC;
        }

        return AudioType.UNKNOWN;
    }

    private static AudioType[] GetAudioTypeCandidates(string url)
    {
        AudioType inferred = InferAudioTypeFromUrl(url);
        switch (inferred)
        {
            case AudioType.ACC:
                // m4a/aac endpoints are often mislabeled; try multiple decoders.
                return new[] { AudioType.ACC, AudioType.MPEG };
            case AudioType.MPEG:
                return new[] { AudioType.MPEG, AudioType.ACC };
            case AudioType.WAV:
                return new[] { AudioType.WAV };
            case AudioType.OGGVORBIS:
                return new[] { AudioType.OGGVORBIS };
            default:
                return new[] { AudioType.MPEG, AudioType.ACC, AudioType.WAV, AudioType.OGGVORBIS };
        }
    }

    private IEnumerator LoadDialogueTreeForEntity(Entity entity, System.Action<RuntimeDialogueTree> onDone)
    {
        if (entity != null && entity.FetchDialogueFromBackend)
        {
            string apiUrl = entity.GetResolvedDialogueApiUrl();
            if (!string.IsNullOrWhiteSpace(apiUrl))
            {
                using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
                {
                    request.timeout = Mathf.CeilToInt(maxClipLoadSeconds);
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        BackendDialogueResponseData payload =
                            JsonUtility.FromJson<BackendDialogueResponseData>(request.downloadHandler.text);
                        if (payload != null && payload.tree != null)
                        {
                            onDone?.Invoke(
                                new RuntimeDialogueTree
                                {
                                    tree = payload.tree,
                                    audioBaseUrl = payload.character != null ? payload.character.audioBaseUrl : string.Empty,
                                    sourceApiUrl = apiUrl
                                });
                            yield break;
                        }
                    }
                    else if (verboseLogging)
                    {
                        Debug.LogWarning(
                            $"Dialogue fetch failed for '{entity.EntityName}' from {apiUrl}. Error: {request.error}");
                    }
                }
            }
        }

        if (entity != null && entity.TryGetDialogueTree(out DialogueTreeData fallbackTree))
        {
            onDone?.Invoke(
                new RuntimeDialogueTree
                {
                    tree = ConvertLegacyTree(fallbackTree),
                    audioBaseUrl = string.Empty,
                    sourceApiUrl = string.Empty
                });
            yield break;
        }

        onDone?.Invoke(null);
    }

    private static AudioDialogueTreeData ConvertLegacyTree(DialogueTreeData legacy)
    {
        if (legacy == null || legacy.nodes == null)
        {
            return null;
        }

        List<AudioDialogueNodeData> convertedNodes = new List<AudioDialogueNodeData>(legacy.nodes.Length);
        foreach (DialogueNodeData oldNode in legacy.nodes)
        {
            if (oldNode == null)
            {
                continue;
            }

            List<AudioDialogueLineData> convertedLines = new List<AudioDialogueLineData>();
            if (oldNode.lines != null)
            {
                for (int i = 0; i < oldNode.lines.Length; i++)
                {
                    string clipOrUrl = oldNode.lines[i];
                    if (string.IsNullOrWhiteSpace(clipOrUrl))
                    {
                        continue;
                    }

                    convertedLines.Add(new AudioDialogueLineData
                    {
                        lineId = $"{oldNode.id}_line_{i + 1}",
                        variants = new[]
                        {
                            new AudioDialogueVariantData
                            {
                                clip = clipOrUrl.Trim(),
                                weight = 1f
                            }
                        }
                    });
                }
            }

            AudioDialogueChoiceData[] convertedChoices = null;
            if (oldNode.choices != null && oldNode.choices.Length > 0)
            {
                convertedChoices = new AudioDialogueChoiceData[oldNode.choices.Length];
                for (int i = 0; i < oldNode.choices.Length; i++)
                {
                    DialogueChoiceData oldChoice = oldNode.choices[i];
                    convertedChoices[i] = new AudioDialogueChoiceData
                    {
                        text = oldChoice != null ? oldChoice.text : string.Empty,
                        nextNodeId = oldChoice != null ? oldChoice.nextNodeId : null
                    };
                }
            }

            convertedNodes.Add(new AudioDialogueNodeData
            {
                id = oldNode.id,
                lines = convertedLines.ToArray(),
                nextNodeId = oldNode.nextNodeId,
                choices = convertedChoices
            });
        }

        return new AudioDialogueTreeData
        {
            rootNodeId = legacy.rootNodeId,
            nodes = convertedNodes.ToArray()
        };
    }

    private Entity FindNearestEntity()
    {
        Entity[] entities = FindObjectsByType<Entity>();
        if (entities == null || entities.Length == 0)
        {
            return null;
        }

        Vector3 origin = playerCamera != null ? playerCamera.transform.position : transform.position;
        Entity nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Entity entity in entities)
        {
            if (entity == null)
            {
                continue;
            }

            float distance = Vector3.Distance(origin, entity.TalkPoint.position);
            if (distance <= entity.TalkRadius && distance < nearestDistance)
            {
                nearest = entity;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void SetPromptCursorMode(bool enabled)
    {
        if (isPromptCursorMode == enabled)
        {
            return;
        }

        isPromptCursorMode = enabled;
        if (firstPersonController != null)
        {
            firstPersonController.enabled = !enabled;
        }

        if (enabled)
        {
            EnforcePromptCursorMode();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private static void EnforcePromptCursorMode()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
