using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ReticuleGlow : MonoBehaviour
{
    [Range(0.05f, 2f)] public float fadeSpeed = 0.3f;

    private Material _mat;
    private Coroutine _anim;
    private Coroutine _opacityAnim;

    private void Awake()
    {
        _mat = GetComponent<Renderer>().material;
    }

    private void OnEnable()
    {
        Actions.OnHoverOverTargetCloud += OnHover;
        Actions.OnHoverExit            += OnExit;
        Actions.GetClickedCloud        += OnClicked;
        Actions.Speak                  += OnNarratorSpeak;
        Actions.ConversationEnded      += OnConversationEnded;
    }

    private void OnDisable()
    {
        Actions.OnHoverOverTargetCloud -= OnHover;
        Actions.OnHoverExit            -= OnExit;
        Actions.GetClickedCloud        -= OnClicked;
        Actions.Speak                  -= OnNarratorSpeak;
        Actions.ConversationEnded      -= OnConversationEnded;
    }

    private void OnHover(GameObject _)   => SetGlow(1f);
    private void OnExit()                => SetGlow(0f);
    private void OnClicked(GameObject _) => SetGlow(0f);
    private void OnNarratorSpeak()       => SetOpacity(0f);
    private void OnConversationEnded()   => SetOpacity(1f);

    private void SetGlow(float target)
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateGlow(target));
    }

    private IEnumerator AnimateGlow(float target)
    {
        float current = _mat.GetFloat("_GlowAmount");
        while (!Mathf.Approximately(current, target))
        {
            current = Mathf.MoveTowards(current, target, Time.deltaTime / fadeSpeed);
            _mat.SetFloat("_GlowAmount", current);
            yield return null;
        }
        _mat.SetFloat("_GlowAmount", target);
    }

    private void SetOpacity(float target)
    {
        if (_opacityAnim != null) StopCoroutine(_opacityAnim);
        _opacityAnim = StartCoroutine(AnimateOpacity(target));
    }

    private IEnumerator AnimateOpacity(float target)
    {
        float current = _mat.GetFloat("_Opacity");
        while (!Mathf.Approximately(current, target))
        {
            current = Mathf.MoveTowards(current, target, Time.deltaTime / fadeSpeed);
            _mat.SetFloat("_Opacity", current);
            yield return null;
        }
        _mat.SetFloat("_Opacity", target);
    }
}
