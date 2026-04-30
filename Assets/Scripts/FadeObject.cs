using System.Collections;
using UnityEngine;

public class FadeObject : MonoBehaviour
{
    public float fadeDuration = 3.0f;
    private Renderer [] rendererObjects;
    private Color [] colors;
    void OnEnable()
    {
        Actions.FadeIn += FadeInGO;
        Actions.FadeIn50 += FadeInGO50;
        Actions.FadeOut += FadeOutGO;
        Actions.FadeOut50 += FadeOutGO50;
    }

    void OnDisable()
    {
        Actions.FadeIn -= FadeInGO;
        Actions.FadeIn50 -= FadeInGO50;
        Actions.FadeOut -= FadeOutGO;
        Actions.FadeOut50 -= FadeOutGO50;

    }
    public void FadeInGO(GameObject _go)
    {
        if(_go == this.gameObject){ StartCoroutine(Fade(0f, 1f)); }
    }

    public void FadeInGO50(GameObject _go)
    {
        if(_go == this.gameObject){ StartCoroutine(Fade(1f, .5f)); }
    }


    public void FadeOutGO(GameObject _go)
    {
        if(_go == this.gameObject){ StartCoroutine(Fade(1f, 0f)); }
    }

    public void FadeOutGO50(GameObject _go)
    {
        if(_go == this.gameObject){ StartCoroutine(Fade(.5f, 0f)); }

    }

private static readonly string[] colorPropertyNames = { "_Color", "_BaseColor", "_GlowColor" };

    private string GetColorProperty(Material mat)
    {
        foreach (string prop in colorPropertyNames)
            if (mat.HasProperty(prop)) return prop;
        return null;
    }

    private string[] colorProperties;

    private IEnumerator Fade(float startAlpha, float endAlpha)
    {
        rendererObjects = GetComponentsInChildren<Renderer>();

        if (rendererObjects == null)
        {
            Debug.LogError("Renderer not found on theis GameObject");
            yield break;
        }

        for (int i=0; i < rendererObjects.Length; i++)
        {
            rendererObjects[i]. enabled = true;
        }

        if (colors == null)
        {
            //create a cache of colors if neccessary
            colors = new Color[rendererObjects.Length];
            colorProperties = new string[rendererObjects.Length];
            //store the original colors for all the child objects
            for(int i=0; i< rendererObjects.Length; i++)
            {
                colorProperties[i] = GetColorProperty(rendererObjects[i].material);
                if (colorProperties[i] != null)
                    colors[i] = rendererObjects[i].material.GetColor(colorProperties[i]);
            }
        }

        for(int i = 0; i<rendererObjects.Length; i++)
        {
            if (colorProperties[i] == null) continue;
            Material material = rendererObjects[i].material;
            Color currentColor = colors[i];
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float newAlpha = Mathf.Lerp(startAlpha, endAlpha, timer / fadeDuration);
                material.SetColor(colorProperties[i], new Color(currentColor.r, currentColor.g, currentColor.b, newAlpha));
                yield return null; //wait for next frame
            }

            //Ensure that the final alpha is set Correctly (need to "snap" it)
            material.SetColor(colorProperties[i], new Color(currentColor.r, currentColor.g, currentColor.b, endAlpha));

        }

    }


}
