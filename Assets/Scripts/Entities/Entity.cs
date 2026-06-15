using UnityEngine;
using UnityEngine.Audio;
using System;
using UnityEngine.Video;

[RequireComponent(typeof(EntitySerializer))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(AudioLowPassFilter))]
[RequireComponent(typeof(AudioReverbFilter))]
[RequireComponent(typeof(VideoPlayer))]
public class Entity : MonoBehaviour
{
    [SerializeField] private string entityId = "entity_default";
    [SerializeField] private string entityName = "Entity";
    [Header("Dialogue Source")]
    [SerializeField] private bool fetchDialogueFromBackend = true;
    [SerializeField] private string backendBaseUrl = "http://127.0.0.1:8000";
    [SerializeField] private string dialogueCharacterSlug = "basheva";
    [SerializeField] private string dialogueApiUrl;
    [TextArea(5, 20)]
    [SerializeField] private string dialogueJson;
    [SerializeField] private TextAsset dialogueTreeJson;
    [Header("Interaction")]
    [SerializeField] private string talkKeyLabel = "F";
    [SerializeField] private string talkPromptText = "Talk";
    [SerializeField] private float talkRadius = 2.6f;
    [SerializeField] private Transform talkPoint;
    [Header("Voice Audio (3D)")]
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioMixerGroup voiceMixerGroup;
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
    [SerializeField] private float minDistance = 1.2f;
    [SerializeField] private float maxDistance = 30f;
    [SerializeField] private float reverbZoneMix = 1f;
    [SerializeField] private float spread = 0f;
    [SerializeField] private float dopplerLevel = 0f;
    [SerializeField] private VideoPlayer voiceVideoPlayer;
    [Header("Voice Filters")]
    [SerializeField] private AudioLowPassFilter voiceLowPassFilter;
    [SerializeField] private bool enableLowPass = true;
    [SerializeField] private float lowPassCutoff = 22000f;
    [SerializeField] private float lowPassResonanceQ = 1f;
    [SerializeField] private AudioReverbFilter voiceReverbFilter;
    [SerializeField] private bool enableReverbFilter = true;
    [SerializeField] private AudioReverbPreset reverbPreset = AudioReverbPreset.Off;

    private EntitySerializer serializer;

    public string EntityId => entityId;
    public string EntityName => entityName;
    public bool FetchDialogueFromBackend => fetchDialogueFromBackend;
    public string BackendBaseUrl => backendBaseUrl;
    public string DialogueCharacterSlug => dialogueCharacterSlug;
    public string DialogueApiUrl => dialogueApiUrl;
    public string TalkKeyLabel => talkKeyLabel;
    public string TalkPromptText => talkPromptText;
    public float TalkRadius => talkRadius;
    public Transform TalkPoint => talkPoint != null ? talkPoint : transform;

    private void Awake()
    {
        serializer = GetComponent<EntitySerializer>();
    }

    public bool TryGetDialogueTree(out DialogueTreeData tree)
    {
        tree = null;
        string sourceJson = dialogueTreeJson != null ? dialogueTreeJson.text : dialogueJson;
        if (string.IsNullOrWhiteSpace(sourceJson))
        {
            return false;
        }

        tree = JsonUtility.FromJson<DialogueTreeData>(sourceJson);
        return tree != null && tree.nodes != null && tree.nodes.Length > 0;
    }

    public void MarkTalked(string lastNodeId, int talksCount)
    {
        SerializedEntityData data = new SerializedEntityData
        {
            entityId = entityId,
            entityName = entityName,
            talksCount = talksCount,
            lastNodeId = lastNodeId
        };

        serializer.Save(data);
    }

    public AudioSource GetOrCreateVoiceSource()
    {
        if (voiceSource == null)
        {
            voiceSource = GetComponent<AudioSource>();
            if (voiceSource == null)
            {
                voiceSource = gameObject.AddComponent<AudioSource>();
            }
        }

        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.volume = 1f;
        voiceSource.spatialBlend = Mathf.Clamp01(spatialBlend);
        voiceSource.rolloffMode = AudioRolloffMode.Logarithmic;
        voiceSource.minDistance = Mathf.Max(0.01f, minDistance);
        voiceSource.maxDistance = Mathf.Max(voiceSource.minDistance + 0.01f, maxDistance);
        voiceSource.reverbZoneMix = reverbZoneMix;
        voiceSource.spread = spread;
        voiceSource.dopplerLevel = dopplerLevel;
        voiceSource.outputAudioMixerGroup = voiceMixerGroup;
        EnsureVoiceFilters();
        return voiceSource;
    }

    public VideoPlayer GetOrCreateVoiceVideoPlayer()
    {
        if (voiceVideoPlayer == null)
        {
            voiceVideoPlayer = GetComponent<VideoPlayer>();
            if (voiceVideoPlayer == null)
            {
                voiceVideoPlayer = gameObject.AddComponent<VideoPlayer>();
            }
        }

        AudioSource source = GetOrCreateVoiceSource();

        voiceVideoPlayer.playOnAwake = false;
        voiceVideoPlayer.waitForFirstFrame = true;
        voiceVideoPlayer.source = VideoSource.Url;
        voiceVideoPlayer.renderMode = VideoRenderMode.APIOnly;
        voiceVideoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        voiceVideoPlayer.EnableAudioTrack(0, true);
        voiceVideoPlayer.SetTargetAudioSource(0, source);
        return voiceVideoPlayer;
    }

    public AudioLowPassFilter GetOrCreateVoiceLowPassFilter()
    {
        if (voiceLowPassFilter == null)
        {
            voiceLowPassFilter = GetComponent<AudioLowPassFilter>();
            if (voiceLowPassFilter == null)
            {
                voiceLowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
            }
        }

        voiceLowPassFilter.enabled = enableLowPass;
        voiceLowPassFilter.cutoffFrequency = Mathf.Clamp(lowPassCutoff, 10f, 22000f);
        voiceLowPassFilter.lowpassResonanceQ = Mathf.Clamp(lowPassResonanceQ, 1f, 10f);
        return voiceLowPassFilter;
    }

    public AudioReverbFilter GetOrCreateVoiceReverbFilter()
    {
        if (voiceReverbFilter == null)
        {
            voiceReverbFilter = GetComponent<AudioReverbFilter>();
            if (voiceReverbFilter == null)
            {
                voiceReverbFilter = gameObject.AddComponent<AudioReverbFilter>();
            }
        }

        voiceReverbFilter.enabled = enableReverbFilter;
        voiceReverbFilter.reverbPreset = reverbPreset;
        return voiceReverbFilter;
    }

    private void EnsureVoiceFilters()
    {
        GetOrCreateVoiceLowPassFilter();
        GetOrCreateVoiceReverbFilter();
    }

    public string GetResolvedDialogueApiUrl()
    {
        if (!string.IsNullOrWhiteSpace(dialogueApiUrl))
        {
            return dialogueApiUrl.Trim();
        }

        if (string.IsNullOrWhiteSpace(dialogueCharacterSlug))
        {
            return string.Empty;
        }

        string baseUrl = string.IsNullOrWhiteSpace(backendBaseUrl) ? "http://127.0.0.1:8000" : backendBaseUrl.Trim();
        if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            baseUrl += "/";
        }

        return $"{baseUrl}api/dialogue/{dialogueCharacterSlug.Trim()}/";
    }
}
