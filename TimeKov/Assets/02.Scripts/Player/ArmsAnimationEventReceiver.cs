using UnityEngine;

/// <summary>
/// 에셋 애니메이션 클립에 박혀있는 AnimationEvent(PlayWeaponSound 등) 때문에
/// "has no receiver" 에러가 뜨는걸 막기 위한 더미 리시버.
/// </summary>
public class ArmsAnimationEventReceiver : MonoBehaviour
{
    public void PlayWeaponSound() { }
    public void PlaySound() { }
    public void PlayStepSound() { }
    public void PlayEquipSound() { }
}
