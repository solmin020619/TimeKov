using UnityEngine;

[CreateAssetMenu(fileName = "WeaponAnimSettings", menuName = "TimeKov/WeaponAnimSettings")]
public class WeaponAnimSettings : ScriptableObject
{
    [Header("Animator Controller (per weapon)")]
    public RuntimeAnimatorController characterController;

    [Header("Options")]
    public bool useFireClip = true;
    public bool hasEquipOverride = false;
    public bool hasFireOut = false;
}
