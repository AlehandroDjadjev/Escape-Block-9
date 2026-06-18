using UnityEngine;

/// <summary>
/// Plays ambient background music for the whole game, and switches to a tense
/// chase track the moment any teacher starts chasing the player — then fades back
/// to ambient once no teacher is chasing.
///
/// Self-bootstraps via RuntimeInitializeOnLoadMethod, so it works in both the
/// singleplayer test and multiplayer without any scene setup. Clips are loaded
/// from Resources/Audio (downloaded as ambient_music.ogg / chase_music.ogg).
/// The chase clip is already trimmed to begin at the 1:15 mark of its source, and
/// it restarts from that beginning each time a fresh chase starts.
/// </summary>
public class GameMusicController : MonoBehaviour
{
    private const float AmbientVolume = 0.5f;
    private const float ChaseVolume = 0.65f;
    private const float FadePerSecond = 1.5f;
    private const float TeacherRefreshInterval = 2f;

    private AudioSource ambientSource;
    private AudioSource chaseSource;

    private SimpleTeacherWander[] teachers;
    private float teacherRefreshTimer;
    private bool chaseActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<GameMusicController>() != null) return;
        var go = new GameObject("GameMusicController");
        DontDestroyOnLoad(go);
        go.AddComponent<GameMusicController>();
    }

    private void Awake()
    {
        AudioClip ambientClip = Resources.Load<AudioClip>("Audio/ambient_music");
        AudioClip chaseClip = Resources.Load<AudioClip>("Audio/chase_music");

        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.clip = ambientClip;
        ambientSource.loop = true;
        ambientSource.playOnAwake = false;
        ambientSource.volume = AmbientVolume;
        ambientSource.spatialBlend = 0f; // 2D, full game

        chaseSource = gameObject.AddComponent<AudioSource>();
        chaseSource.clip = chaseClip;
        chaseSource.loop = true;
        chaseSource.playOnAwake = false;
        chaseSource.volume = 0f;
        chaseSource.spatialBlend = 0f;

        if (ambientClip != null) ambientSource.Play();
        else Debug.LogWarning("[GameMusic] ambient_music not found in Resources/Audio.");
        if (chaseClip == null) Debug.LogWarning("[GameMusic] chase_music not found in Resources/Audio.");
    }

    private void Update()
    {
        RefreshTeachers();

        // The game world is "active" once teachers exist in the scene (i.e. the
        // school has generated). Before that — on the connect/lobby menus — we stay
        // silent so music doesn't play over the menu.
        bool worldActive = teachers != null && teachers.Length > 0;
        bool anyChasing = worldActive && AnyTeacherChasing();

        if (worldActive && ambientSource != null && ambientSource.clip != null && !ambientSource.isPlaying)
            ambientSource.Play();

        // Chase just started — restart the chase track from its beginning (the 1:15
        // mark of the source video).
        if (anyChasing && !chaseActive)
        {
            chaseActive = true;
            if (chaseSource != null && chaseSource.clip != null)
            {
                chaseSource.time = 0f;
                chaseSource.Play();
            }
        }
        else if (!anyChasing && chaseActive)
        {
            chaseActive = false;
        }

        // Volume targets: silent on menus; ambient in-game; duck ambient under the
        // chase track while a teacher is chasing.
        float ambientTarget = !worldActive ? 0f : (chaseActive ? 0f : AmbientVolume);
        float chaseTarget = (worldActive && chaseActive) ? ChaseVolume : 0f;

        float fade = FadePerSecond * Time.unscaledDeltaTime;
        if (ambientSource != null)
        {
            ambientSource.volume = Mathf.MoveTowards(ambientSource.volume, ambientTarget, fade);
            if (!worldActive && ambientSource.volume <= 0.001f && ambientSource.isPlaying)
                ambientSource.Stop();
        }
        if (chaseSource != null)
        {
            chaseSource.volume = Mathf.MoveTowards(chaseSource.volume, chaseTarget, fade);
            if (chaseTarget == 0f && chaseSource.volume <= 0.001f && chaseSource.isPlaying)
                chaseSource.Stop();
        }
    }

    private void RefreshTeachers()
    {
        teacherRefreshTimer -= Time.unscaledDeltaTime;
        if (teachers == null || teacherRefreshTimer <= 0f)
        {
            teachers = FindObjectsByType<SimpleTeacherWander>(FindObjectsSortMode.None);
            teacherRefreshTimer = TeacherRefreshInterval;
        }
    }

    private bool AnyTeacherChasing()
    {
        if (teachers == null) return false;
        foreach (var t in teachers)
        {
            if (t != null && t.IsChasing) return true;
        }
        return false;
    }
}
