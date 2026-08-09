using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Attack2Skill", menuName = "TIMEKOV/스킬/평타 2타")]
public class Attack2Skill : ComboAttackBase
{
    protected override float GetAnimDuration() => AnimDuration;
    public float AnimDuration = 0.8f;
}