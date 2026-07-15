using UnityEngine;

/// <summary>
/// Copies the color of the Azure Sky directional light to a second directional light
/// every frame, with an optional warm tint blended on top.
///
/// Warmth = 0  →  exact copy of source color
/// Warmth = 1  →  fully WarmColor, ignoring source
/// Values in between blend proportionally.
/// </summary>
public class SkyLightColorCopy : MonoBehaviour
{
    [Header("Lights")]
    [Tooltip("The Azure Sky directional light (source).")]
    public Light sourceLight;
    [Tooltip("The directional light that will receive the copied + tinted color.")]
    public Light targetLight;

    [Header("Warm Tint")]
    [Tooltip("Color to blend toward when Warmth > 0. Default is a late-afternoon orange.")]
    public Color warmColor = new Color(1f, 0.55f, 0.15f, 1f);
    [Range(0f, 1f)]
    [Tooltip("0 = exact copy of source light color, 1 = fully WarmColor.")]
    public float warmth = 0.2f;

    [Header("Intensity")]
    [Tooltip("Also copy the source light's intensity to the target.")]
    public bool copyIntensity = false;
    [Tooltip("Multiplier applied to the copied intensity.")]
    public float intensityMultiplier = 1f;

    private void LateUpdate()
    {
        if (sourceLight == null || targetLight == null) return;

        targetLight.color = Color.Lerp(sourceLight.color, warmColor, warmth);

        if (copyIntensity)
            targetLight.intensity = sourceLight.intensity * intensityMultiplier;
    }
}
