using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationEat : StateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool("IsEating", false);
    }
}
