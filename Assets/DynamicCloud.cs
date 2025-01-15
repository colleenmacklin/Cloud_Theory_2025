using UnityEditor;
using UnityEngine;
using UnityEngine.AzureSky;

public class DynamicCloud : MonoBehaviour
{
    [SerializeField] private AzureTimeController timeController;
    [SerializeField] private float currentTimeline;
    [SerializeField] private Light directionalLight;

    [Header("Summer Solstice Light Colors (June 21st)")]
    [Tooltip("Night/Early Twilight (00:00-03:00)")]
    [SerializeField] private Color nightColor = new Color(0.05f, 0.05f, 0.15f);    // Deep blue night

    [Tooltip("Dawn Twilight (03:00-04:43)")]
    [SerializeField] private Color dawnTwilightColor = new Color(0.2f, 0.2f, 0.4f); // Purple dawn

    [Tooltip("Sunrise (04:43-06:00)")]
    [SerializeField] private Color sunriseColor = new Color(1f, 0.7f, 0.4f);       // Warm sunrise

    [Tooltip("Morning (06:00-10:00)")]
    [SerializeField] private Color morningColor = new Color(1f, 0.98f, 0.9f);      // Bright morning

    [Tooltip("Midday (10:00-15:00)")]
    [SerializeField] private Color middayColor = new Color(1f, 1f, 0.95f);         // Peak daylight

    [Tooltip("Afternoon (15:00-19:00)")]
    [SerializeField] private Color afternoonColor = new Color(1f, 0.95f, 0.8f);    // Warm afternoon

    [Tooltip("Sunset (19:00-21:21)")]
    [SerializeField] private Color sunsetColor = new Color(1f, 0.6f, 0.3f);        // Orange sunset

    [Tooltip("Dusk Twilight (21:21-23:00)")]
    [SerializeField] private Color duskColor = new Color(0.2f, 0.2f, 0.4f);        // Purple dusk

    [Header("Transition Settings")]
    [SerializeField] private float transitionSpeed = 1.0f;

    private void Start()
    {
        if (timeController == null)
            timeController = FindObjectOfType<AzureTimeController>();

        if (directionalLight == null)
            directionalLight = FindObjectOfType<Light>();

        // Set initial light intensity to 1
        if (directionalLight != null)
            directionalLight.intensity = 1f;
    }

    private void Update()
    {
        if (timeController != null && directionalLight != null)
        {
            currentTimeline = timeController.GetTimeline();
            UpdateLightColor();
        }
    }

    private void UpdateLightColor()
    {
        Color targetColor;
        float lerpValue;

        // Summer solstice specific time periods
        if (currentTimeline <= 3f) // Night to early twilight (00:00-03:00)
        {
            lerpValue = currentTimeline / 3f;
            targetColor = Color.Lerp(nightColor, dawnTwilightColor, lerpValue);
        }
        else if (currentTimeline <= 4.72f) // Dawn twilight (03:00-04:43)
        {
            lerpValue = (currentTimeline - 3f) / 1.72f;
            targetColor = Color.Lerp(dawnTwilightColor, sunriseColor, lerpValue);
        }
        else if (currentTimeline <= 6f) // Sunrise (04:43-06:00)
        {
            lerpValue = (currentTimeline - 4.72f) / 1.28f;
            targetColor = Color.Lerp(sunriseColor, morningColor, lerpValue);
        }
        else if (currentTimeline <= 10f) // Morning (06:00-10:00)
        {
            lerpValue = (currentTimeline - 6f) / 4f;
            targetColor = Color.Lerp(morningColor, middayColor, lerpValue);
        }
        else if (currentTimeline <= 15f) // Midday (10:00-15:00)
        {
            lerpValue = (currentTimeline - 10f) / 5f;
            targetColor = Color.Lerp(middayColor, afternoonColor, lerpValue);
        }
        else if (currentTimeline <= 19f) // Afternoon (15:00-19:00)
        {
            lerpValue = (currentTimeline - 15f) / 4f;
            targetColor = Color.Lerp(afternoonColor, sunsetColor, lerpValue);
        }
        else if (currentTimeline <= 21.35f) // Sunset (19:00-21:21)
        {
            lerpValue = (currentTimeline - 19f) / 2.35f;
            targetColor = Color.Lerp(sunsetColor, duskColor, lerpValue);
        }
        else if (currentTimeline <= 23f) // Dusk (21:21-23:00)
        {
            lerpValue = (currentTimeline - 21.35f) / 1.65f;
            targetColor = Color.Lerp(duskColor, nightColor, lerpValue);
        }
        else // Late night (23:00-24:00)
        {
            lerpValue = (currentTimeline - 23f) / 1f;
            targetColor = nightColor;
        }

        // Smooth transition for color only
        directionalLight.color = Color.Lerp(directionalLight.color, targetColor, Time.deltaTime * transitionSpeed);
    }
}