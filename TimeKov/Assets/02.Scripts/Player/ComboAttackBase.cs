using System.Collections;
using UnityEngine;

public abstract class ComboAttackBase : ScriptableObject
{
    [Header("Combo")]
    public int ComboIndex = 0;
    public float ComboWindow = 1.2f;
    public float Damage = 10f;

    public virtual IEnumerator ExecuteRoutine(GameObject caster)
    {
        var anim = caster.GetComponent<PlayerAnimatorComponent>();
        anim?.PlayAttack(ComboIndex);

        yield return new WaitForSeconds(GetAnimDuration());

        OnAttackHit(caster);
    }

    protected virtual void OnAttackHit(GameObject caster) { }
    protected abstract float GetAnimDuration();
    public virtual void OnInterrupt(GameObject caster) { }
}