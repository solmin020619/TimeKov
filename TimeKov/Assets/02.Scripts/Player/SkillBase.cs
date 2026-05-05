using System.Collections;
using UnityEngine;

public abstract class SkillBase : ScriptableObject
{
    public SkillSheetId SkillSheetId;
    public Sprite SkillIcon;
    public float CoolTime;

    public abstract IEnumerator ExecuteRoutine(GameObject caster);
    public virtual void OnInterrupt(GameObject caster) { }
}