using UnityEngine;

public class FlickeringFluorescent : MonoBehaviour
{
    [Header("Fixture")]
    [SerializeField] private Light fixtureLight;
    [SerializeField] private Renderer tubeRenderer;
    [SerializeField] private ParticleSystem sparks;

    [Header("Light")]
    [SerializeField] private float baseIntensity = 1.1f;
    [SerializeField] private float lowIntensity = 0.12f;
    [SerializeField] private float randomHum = 0.06f;
    [SerializeField] private Color litColor = new Color(0.62f, 0.78f, 0.9f, 1f);

    [Header("Timing")]
    [SerializeField] private Vector2 timeBetweenFlickers = new Vector2(7f, 18f);
    [SerializeField] private Vector2 flickerDuration = new Vector2(0.12f, 0.45f);
    [SerializeField] private float sparkChance = 0.45f;

    private MaterialPropertyBlock propertyBlock;
    private float nextFlickerTime;
    private float flickerEndTime;
    private bool flickering;
    private int emissionColorId;

    private void Awake()
    {
        if (fixtureLight == null)
        {
            fixtureLight = GetComponentInChildren<Light>();
        }

        if (tubeRenderer == null)
        {
            tubeRenderer = GetComponentInChildren<Renderer>();
        }

        if (sparks == null)
        {
            sparks = GetComponentInChildren<ParticleSystem>();
        }

        propertyBlock = new MaterialPropertyBlock();
        emissionColorId = Shader.PropertyToID("_EmissionColor");
        ScheduleNextFlicker();
        SetOutput(baseIntensity);
    }

    private void Update()
    {
        if (fixtureLight == null)
        {
            return;
        }

        if (!flickering && Time.time >= nextFlickerTime)
        {
            flickering = true;
            flickerEndTime = Time.time + Random.Range(flickerDuration.x, flickerDuration.y);

            if (sparks != null && Random.value <= sparkChance)
            {
                sparks.Emit(Random.Range(5, 14));
            }
        }

        if (flickering)
        {
            float pulse = Random.value > 0.52f ? baseIntensity : lowIntensity;
            SetOutput(pulse);

            if (Time.time >= flickerEndTime)
            {
                flickering = false;
                ScheduleNextFlicker();
            }

            return;
        }

        SetOutput(baseIntensity + Random.Range(-randomHum, randomHum));
    }

    private void ScheduleNextFlicker()
    {
        nextFlickerTime = Time.time + Random.Range(timeBetweenFlickers.x, timeBetweenFlickers.y);
    }

    private void SetOutput(float intensity)
    {
        intensity = Mathf.Max(0f, intensity);
        fixtureLight.intensity = intensity;
        fixtureLight.color = litColor;

        if (tubeRenderer == null)
        {
            return;
        }

        tubeRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(emissionColorId, litColor * Mathf.Lerp(0.15f, 1.8f, Mathf.InverseLerp(lowIntensity, baseIntensity, intensity)));
        tubeRenderer.SetPropertyBlock(propertyBlock);
    }
}
