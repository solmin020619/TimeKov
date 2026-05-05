using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Attack1Skill", menuName = "Skills/Attack1")]
public class Attack1Skill : ComboAttackBase
{
    protected override float GetAnimDuration() => AnimDuration;
    public float AnimDuration = 0.8f;   // 애니메이션 길이에 맞게 조정
}