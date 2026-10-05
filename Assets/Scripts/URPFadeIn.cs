using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Fades all Renderer children in using URP's _BaseColor alpha.
// Requirements:
//   - Each material's Surface Type must be set to Transparent in the Inspector.
//   - Attach to the parent GameObject of the spheres.
// Trigger: listens for Actions.StandUp by default; change the event in OnEnable/OnDisable.
[DefaultExecutionOrder(10)]
public class URPFadeIn : MonoBehaviour
{
    [Tooltip("How long the fade-in takes in seconds.")]
    public float fadeTime = 2f;
    [Tooltip("Seconds to wait after the trigger before starting.")]
    public float delay = 0f;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    private struct RendererEntry
    {
        public Renderer renderer;
        public Color    targetColor; // original color at full alpha
    }

    [Header("Debug")]
    [Tooltip("Tick in Play mode to force the fade-in immediately, without waiting for Actions.StandUp or Actions.HasStoodUp.")]
    public bool debugTriggerNow = false;

    private List<RendererEntry> _entries = new List<RendererEntry>();
    private bool _triggered = false;

    private void OnEnable()  => Actions.StandUp += Trigger;
    private void OnDisable() => Actions.StandUp -= Trigger;

    private void Update()
    {
#if UNITY_EDITOR
        if (debugTriggerNow)
        {
            debugTriggerNow = false;
            Trigger();
        }
#endif
    }

    private void Awake()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            Material mat = r.material;
            Color original = mat.HasProperty(BaseColor) ? mat.GetColor(BaseColor) : mat.color;
            _entries.Add(new RendererEntry { renderer = r, targetColor = original });

            Debug.Log($"[URPFadeIn] {name}: found renderer '{r.gameObject.name}' ({r.GetType().Name}), " +
                      $"material '{mat.name}', targetColor={original}, hasBaseColor={mat.HasProperty(BaseColor)}");

            // Hide by disabling the renderer — works regardless of Surface Type.
            r.enabled = false;
        }
        Debug.Log($"[URPFadeIn] {name}: found {_entries.Count} Renderer(s) to fade. " +
                  "If this is 0, this object has nothing for URPFadeIn to act on (e.g. a Light isn't a Renderer).");
    }

    // Actions.StandUp is a transient event fired once in the Cloud scene. If this object
    // instead lives in a scene that loads afterward (e.g. Forest), that event has already
    // fired and gone and will never come again — so also check the persistent flag that's
    // set alongside it, in case standing up already happened before this object existed.
    private void Start()
    {
        if (Actions.HasStoodUp) Trigger();
    }

    public void Trigger()
    {
        Debug.Log($"[URPFadeIn] {name}: Trigger() called (already triggered: {_triggered}).");
        if (_triggered) return;
        _triggered = true;
        StartCoroutine(FadeInCoroutine());
    }

    private IEnumerator FadeInCoroutine()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // Re-enable renderers and start them at alpha 0 before the first visible frame.
        foreach (var entry in _entries)
        {
            if (entry.renderer == null) continue;
            entry.renderer.enabled = true;
            Material mat = entry.renderer.material;
            Color c = entry.targetColor;
            c.a = 0f;
            if (mat.HasProperty(BaseColor)) mat.SetColor(BaseColor, c);
            else                            mat.color = c;
        }

        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeTime);

            foreach (var entry in _entries)
            {
                if (entry.renderer == null) continue;
                Material mat = entry.renderer.material;
                Color c = entry.targetColor;
                c.a = Mathf.Lerp(0f, entry.targetColor.a, t);
                if (mat.HasProperty(BaseColor)) mat.SetColor(BaseColor, c);
                else                            mat.color = c;
            }

            yield return null;
        }

        // Snap to final values.
        foreach (var entry in _entries)
        {
            if (entry.renderer == null) continue;
            Material mat = entry.renderer.material;
            if (mat.HasProperty(BaseColor)) mat.SetColor(BaseColor, entry.targetColor);
            else                            mat.color = entry.targetColor;
        }
    }
}
