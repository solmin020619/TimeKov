using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// PlayerInput(Send Messages) 액션을 받아서
/// - PlayerWeaponController.FireFromExternal()
/// - ArmsAnimBridge.NotifyFire()/PlayReloadAnim()
/// 을 호출하는 브릿지.
/// </summary>
public class ArmsInputBridge : MonoBehaviour
{
    [Header("Refs (비워도 자동 탐색)")]
    [SerializeField] private PlayerWeaponController weapon;
    [SerializeField] private ArmsAnimBridge armsAnim;

    private void Awake()
    {
        if (weapon == null)
            weapon = GetComponentInParent<PlayerWeaponController>();

        if (armsAnim == null)
        {
#if UNITY_6000_0_OR_NEWER
            armsAnim = Object.FindFirstObjectByType<ArmsAnimBridge>();
            if (armsAnim == null) armsAnim = Object.FindAnyObjectByType<ArmsAnimBridge>();
#else
            armsAnim = FindObjectOfType<ArmsAnimBridge>();
#endif
        }
    }

#if ENABLE_INPUT_SYSTEM
    // ===== PlayerInput "Send Messages" 콜백 =====
    public void OnFire(InputValue value)
    {
        if (!value.isPressed) return;

        // 1) 실제 발사(우리 로직)
        if (weapon != null)
            weapon.FireFromExternal();

        // 2) 팔/반동 연출
        if (armsAnim != null)
            armsAnim.NotifyFire();
    }

    public void OnReload(InputValue value)
    {
        if (!value.isPressed) return;

        // (선택) 재장전 키는 기존 PlayerWeaponController Update의 R키로도 되지만,
        // 모바일/입력 통합하려면 여기서도 호출하도록 확장 가능.
        if (armsAnim != null)
            armsAnim.PlayReloadAnim();

        // 무기쪽은 현재 코드에 "외부 Reload" 함수가 없으니,
        // 필요하면 PlayerWeaponController에 Public ReloadFromExternal() 만들어서 여기서 호출하면 됨.
    }
#endif
}
