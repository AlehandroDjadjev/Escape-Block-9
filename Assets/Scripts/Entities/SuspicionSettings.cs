using UnityEngine;

public class SuspicionSettings : MonoBehaviour
{
    public static SuspicionSettings Instance { get; private set; }

    [Header("State Thresholds")]
    [SerializeField] private float suspiciousThreshold = 0.01f;
    [SerializeField] private float aggroThreshold = 2.5f;
    [SerializeField] private float minimumSuspicion = -0.5f;

    [Header("Suspicion Dynamics")]
    [SerializeField] private float suspicionDecayPerSecond = 0.5f;
    [SerializeField] private float centralRaySuspicion = 6f;
    [SerializeField] private float peripheralSuspicionMin = 1f;
    [SerializeField] private float peripheralSuspicionMax = 2f;

    [Header("UI And Vision Behavior")]
    [SerializeField] private bool showDotWhenAggro = false;
    [SerializeField] private bool showVisionRays = true;
    [SerializeField] private bool alwaysShowVisionRays = true;
    [SerializeField] private bool showVisionGizmosInEditMode = true;

    public float SuspiciousThreshold => suspiciousThreshold;
    public float AggroThreshold => aggroThreshold;
    public float MinimumSuspicion => minimumSuspicion;
    public float SuspicionDecayPerSecond => suspicionDecayPerSecond;
    public float CentralRaySuspicion => centralRaySuspicion;
    public float PeripheralSuspicionMin => peripheralSuspicionMin;
    public float PeripheralSuspicionMax => peripheralSuspicionMax;
    public bool ShowDotWhenAggro => showDotWhenAggro;
    public bool ShowVisionRays => showVisionRays;
    public bool AlwaysShowVisionRays => alwaysShowVisionRays;
    public bool ShowVisionGizmosInEditMode => showVisionGizmosInEditMode;

    private void OnEnable()
    {
        Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
