using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class AnimationManager : MonoBehaviour
{

    private Animator charAnimator;
    private int _animState;

    [SerializeField]
    private List<AnimationClip> _danceAnimations = new List<AnimationClip>();

    AnimatorClipInfo[] _animatorClipInfo;

    // Start is called before the first frame update
    void Start()
    {
        charAnimator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.W))
        {
            _animState = charAnimator.GetInteger("AnimState");

            if (_animState == 0 || _animState == 2)
            {
                charAnimator.SetInteger("AnimState", 1);
            }
            else if (_animState == 1)
            {
                charAnimator.SetInteger("AnimState", 0);
            }
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            _animState = charAnimator.GetInteger("AnimState");

            if (_animState == 0 || _animState == 1)
            {
                charAnimator.SetInteger("AnimState", 2);
            }
            else if (_animState == 2)
            {
                charAnimator.SetInteger("AnimState", 0);
            }
        }

        if (Input.GetKey(KeyCode.Alpha0))
        {
            charAnimator.SetInteger("AnimState", 2);
            SetDanceMotions();
        }

    }

    private void SetDanceMotions()
    {
        ////Get the animator clip information from the Animator Controller
        //_animatorClipInfo = charAnimator.GetCurrentAnimatorClipInfo(0);
        ////Output the name of the starting clip
        //Debug.Log("Starting clip : " + _animatorClipInfo[0].clip);

    }
}
