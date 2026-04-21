using UnityEngine;
using TMPro;
using System.Collections;
using System.Runtime.CompilerServices;

public class Subtitle_Handler : MonoBehaviour
{
    //public TextMeshProUGUI textField;
    public TMP_Text textfield;
    public CanvasGroup textCanvasGroup;
    public float fadeDuration = 1f;
    private bool conditionMet = false; // The condition to wait for

        // Call this method from another script or event to start the sequence
        //could set up Actions...
    public void StartTextSequence(string text)
    {
        if (textCanvasGroup != null)
        {
            speakStart();
            gameObject.SetActive(true);
            textfield.text = text;
            StartCoroutine(SequenceRoutine());
        }
        else
        {
            Debug.LogError("CanvasGroup reference is missing!");
        }
    }

    public void EndTextSequence()
    {
        if (textCanvasGroup != null)
        {
            speakEnd();
            textfield.text = "";
        }
        else
        {
            Debug.LogError("CanvasGroup reference is missing!");
        }
    }


    private IEnumerator SequenceRoutine()
    {
        // Fade in
        yield return StartCoroutine(FadeCanvasGroup(textCanvasGroup, 0f, 1f, fadeDuration));

        // Wait for condition to be true
        yield return new WaitUntil(() => conditionMet);

        // Fade out
        yield return StartCoroutine(FadeCanvasGroup(textCanvasGroup, 1f, 0f, fadeDuration));

        // Optional: Deactivate the GameObject after fading out
        gameObject.SetActive(false);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float startAlpha, float endAlpha, float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            cg.alpha = Mathf.Lerp(startAlpha, endAlpha, timer / duration);
            timer += Time.deltaTime;
            yield return null; // Wait until the next frame
        }
        cg.alpha = endAlpha; // Ensure the final alpha value is set
    }

    // Public method to be called when the condition is met by another system
    public void speakEnd()
    {
        conditionMet = true;
    }

    public void speakStart()
    {
        conditionMet = false;
    }
}


