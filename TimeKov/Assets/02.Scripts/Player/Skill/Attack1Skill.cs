using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Attack1Skill", menuName = "TIMEKOV/스킬/평타 1타")]
public class Attack1Skill : ComboAttackBase
{
    protected override float GetAnimDuration() => AnimDuration;
    public float AnimDuration = 0.8f;   // 애니메이션 길이에 맞게 조정
}