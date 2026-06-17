using UnityEngine;

[RequireComponent(typeof(ItemPickup))]
public class FlashlightItem : MonoBehaviour
{
    [SerializeField] private float batteryLifeSeconds = 60f;
    [SerializeField] private bool startsEnabled = true;
    [SerializeField] private Color flashlightColor = new Color(1f, 0.96f, 0.88f, 1f);
    [SerializeField] private float flashlightIntensity = 1.35f;
    [SerializeField] private float flashlightRange = 18f;
    [SerializeField] private float flashlightSpotAngle = 72f;
    [SerializeField] private float flashlightInnerSpotAngle = 44f;
    [SerializeField] private LightShadows flashlightShadows = LightShadows.None;

    private Light flashlight;
    private float batterySecondsRemaining;
    private bool isHeld;
    private bool isOn;

    public bool IsHeld => isHeld;
    public bool IsOn => isOn;
    public float BatteryNormalized => batteryLifeSeconds <= 0.001f ? 0f : Mathf.Clamp01(batterySecondsRemaining / batteryLifeSeconds);

    private void Awake()
    {
        batterySecondsRemaining = Mathf.Max(0f, batteryLifeSeconds);
        EnsureVisualLight();
        ApplyLightState();
    }

    private void Update()
    {
        if (!isHeld || !isOn)
        {
            return;
        }

        if (batterySecondsRemaining <= 0f)
        {
            isOn = false;
            ApplyLightState();
            return;
        }

        batterySecondsRemaining = Mathf.Max(0f, batterySecondsRemaining - Time.deltaTime);
        if (batterySecondsRemaining <= 0f)
        {
            isOn = false;
            ApplyLightState();
        }
    }

    public void ConfigureLight(
        float batteryLife,
        Color color,
        float intensity,
        float range,
        float spotAngle,
        float innerSpotAngle,
        LightShadows shadows)
    {
        batteryLifeSeconds = Mathf.Max(1f, batteryLife);
        batterySecondsRemaining = Mathf.Clamp(batterySecondsRemaining <= 0f ? batteryLifeSeconds : batterySecondsRemaining, 0f, batteryLifeSeconds);
        flashlightColor = color;
        flashlightIntensity = intensity;
        flashlightRange = range;
        flashlightSpotAngle = spotAngle;
        flashlightInnerSpotAngle = innerSpotAngle;
        flashlightShadows = shadows;
        EnsureVisualLight();
        ApplyLightState();
    }

    public void SetHeldState(bool held)
    {
        isHeld = held;
        if (!held)
        {
            isOn = false;
        }
        else if (batterySecondsRemaining > 0f)
        {
            isOn = startsEnabled;
        }

        EnsureVisualLight();
        ApplyLightState();
    }

    public void Toggle()
    {
        if (!isHeld || batterySecondsRemaining <= 0f)
        {
            return;
        }

        isOn = !isOn;
        ApplyLightState();
    }

    private void EnsureVisualLight()
    {
        Transform lightTransform = transform.Find("FlashlightBeam");
        if (lightTransform == null)
        {
            GameObject lightObject = new GameObject("FlashlightBeam");
            lightTransform = lightObject.transform;
            lightTransform.SetParent(transform, false);
            lightTransform.localPosition = new Vector3(0f, 0f, 0.34f);
            lightTransform.localEulerAngles = Vector3.zero;
        }

        flashlight = lightTransform.GetComponent<Light>();
        if (flashlight == null)
        {
            flashlight = lightTransform.gameObject.AddComponent<Light>();
        }

        flashlight.type = LightType.Spot;
        flashlight.color = flashlightColor;
        flashlight.intensity = Mathf.Max(0f, flashlightIntensity);
        flashlight.range = Mathf.Max(0.1f, flashlightRange);
        flashlight.spotAngle = Mathf.Clamp(flashlightSpotAngle, 1f, 179f);
        flashlight.innerSpotAngle = Mathf.Clamp(flashlightInnerSpotAngle, 0f, flashlight.spotAngle);
        flashlight.shadows = flashlightShadows;
    }

    private void ApplyLightState()
    {
        if (flashlight == null)
        {
            return;
        }

        flashlight.enabled = isHeld && isOn && batterySecondsRemaining > 0f;
    }
}
