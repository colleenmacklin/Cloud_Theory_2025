using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class DanceController : MonoBehaviour
{

    private Animator charAnimator;

    // Start is called before the first frame update
    void Start()
    {
        charAnimator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        int animState;

        if (Input.GetKey(KeyCode.W))
        {
            animState = charAnimator.GetInteger("AnimState");

            if (animState != 1)
            {
                charAnimator.SetInteger("AnimState", 1);
            }
            else if (animState == 1)
            {
                charAnimator.SetInteger("AnimState", 0);
            }
        }

        else if (Input.GetKey(KeyCode.I))
        {
            animState = charAnimator.GetInteger("AnimState");

            if (animState != 0)
            {
                charAnimator.SetInteger("AnimState", 0);
            }
        }


        else if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            animState = charAnimator.GetInteger("AnimState");

            if (animState != 2)
            {
                charAnimator.SetInteger("AnimState", 2);
            }
            else if (animState == 2)
            {
                charAnimator.SetInteger("AnimState", 0);
            }
        }

        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            animState = charAnimator.GetInteger("AnimState");

            if (animState != 3)
            {
                charAnimator.SetInteger("AnimState", 3);
            }
            else if (animState == 3)
            {
                charAnimator.SetInteger("AnimState", 0);
            }
        }

        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            animState = charAnimator.GetInteger("AnimState");

            if (animState != 4)
            {
                charAnimator.SetInteger("AnimState", 4);
            }
            else if (animState == 4)
            {
                charAnimator.SetInteger("AnimState", 0);
            }
        }

        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            animState = charAnimator.GetInteger("AnimState");

            if (animState != 5)
            {
                charAnimator.SetInteger("AnimState", 5);
            }
            else if (animState == 5)
            {
                charAnimator.SetInteger("AnimState", 0);
            }
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            animState = charAnimator.GetInteger("AnimState");

            if (animState != 6)
            {
                charAnimator.SetInteger("AnimState", 6);
            }
            else if (animState == 6)
            {
                charAnimator.SetInteger("AnimState", 0);
            }
        }

        else if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            animState = charAnimator.GetInteger("AnimState");

            if (animState != 7)
            {
                charAnimator.SetInteger("AnimState", 7);
            }
            else if (animState == 7)
            {
                charAnimator.SetInteger("AnimState", 0);
            }
        }
        else if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            animState = charAnimator.GetInteger("AnimState");

            if (animState != 8)
            {
                charAnimator.SetInteger("AnimState", 8);
            }
            else if (animState == 8)
            {
                charAnimator.SetInteger("AnimState", 0);
            }
        }

        else if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            animState = charAnimator.GetInteger("AnimState");

            if (animState != 9)
            {
                charAnimator.SetInteger("AnimState", 9);
            }
            else if (animState == 9)
            {
                charAnimator.SetInteger("AnimState", 0);
            }
        }

        else if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            animState = charAnimator.GetInteger("AnimState");

            if (animState != 10)
            {
                charAnimator.SetInteger("AnimState", 10);
            }
            else if (animState == 10)
            {
                charAnimator.SetInteger("AnimState", 0);
            }
        }

        else if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            animState = charAnimator.GetInteger("AnimState");

            if (animState != 11)
            {
                charAnimator.SetInteger("AnimState", 11);
            }
            else if (animState == 11)
            {
                charAnimator.SetInteger("AnimState", 0);
            }
        }

    }
}
