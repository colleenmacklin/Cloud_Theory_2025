using UnityEngine;
using UnityEngine.Events;
using TMPro; // remove this line if you're using legacy UI Text

/// <summary>
/// Attach this to any GameObject in your scene (e.g. a UI canvas or the CloudManager).
/// 
/// Assign:
///   - grammarFile   → the CloudTheoryGrammar.json TextAsset (drag from Project window)
///   - outputText    → a TextMeshProUGUI component to display the line (optional)
///   - onLineGenerated → a UnityEvent<string> you can hook up to anything else
/// 
/// Then call GenerateLine(cloudShape) from your CloudManager whenever a cloud
/// is identified or clicked.
/// </summary>
public class CloudTextGenerator : MonoBehaviour
{
    [Header("Grammar")]
    [Tooltip("Drag CloudTheoryGrammar.json here from your StreamingAssets or Resources folder")]
    public TextAsset grammarFile;

    [Header("Output (optional)")]
    [Tooltip("TextMeshPro component to write lines into. Leave empty to use the UnityEvent only.")]
    public TextMeshProUGUI outputText;

    [Header("Timing")]
    [Tooltip("Seconds to display a line before fading / clearing")]
    public float displayDuration = 6f;

    [Header("Events")]
    [Tooltip("Fired with the generated string — wire this up to your dialogue / subtitle system")]
    public UnityEvent<string> onLineGenerated;

    // ─────────────────────────────────────────────
    private TraceryEngine _engine;
    private Coroutine _displayCoroutine;

    // ─────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (grammarFile == null)
        {
            Debug.LogError("[CloudTextGenerator] No grammar file assigned!");
            return;
        }

        _engine = TraceryEngine.FromTextAsset(grammarFile);
    }

    // ─────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────

    /// <summary>
    /// Generate and display a line for the given CloudShape.
    /// Call this from CloudManager when a cloud is identified.
    /// </summary>
    public void GenerateLine(CloudShape cloud)
    {
        if (_engine == null) return;

        string shapeName = cloud.CurrentShapeName;

        // If the shape has its own grammar rule, use it as the start rule
        // so you can write shape-specific lines. Falls back to "origin".
        string startRule = _engine.HasRule(shapeName) ? shapeName : "origin";

        string line = _engine.Generate(startRule, shapeName);

        DisplayLine(line);
        onLineGenerated?.Invoke(line);
    }

    /// <summary>
    /// Generate a line for an arbitrary shape name string.
    /// </summary>
    public void GenerateLine(string shapeName)
    {
        if (_engine == null) return;

        string startRule = _engine.HasRule(shapeName) ? shapeName : "origin";
        string line = _engine.Generate(startRule, shapeName);

        DisplayLine(line);
        onLineGenerated?.Invoke(line);
    }

    // ─────────────────────────────────────────────
    //  Display
    // ─────────────────────────────────────────────

    private void DisplayLine(string line)
    {
        Debug.Log($"[CloudText] {line}");

        if (outputText != null)
        {
            if (_displayCoroutine != null)
                StopCoroutine(_displayCoroutine);

            _displayCoroutine = StartCoroutine(ShowAndFade(line));
        }
    }

    private System.Collections.IEnumerator ShowAndFade(string line)
    {
        outputText.text = line;

        // Fade in
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            outputText.alpha = Mathf.Clamp01(t);
            yield return null;
        }

        // Hold
        yield return new WaitForSeconds(displayDuration);

        // Fade out
        t = 1f;
        while (t > 0f)
        {
            t -= Time.deltaTime * 0.8f;
            outputText.alpha = Mathf.Clamp01(t);
            yield return null;
        }

        outputText.text = string.Empty;
    }
}
