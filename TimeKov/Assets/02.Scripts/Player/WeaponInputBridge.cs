// WeaponInputBridge.cs
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// PlayerInput(SendMessage) -> PlayerWeaponController 전달
/// - 연사(hold) 지원: OnFire(InputValue)로 pressed 상태를 받아서 Fire/FireUp 호출
/// </summary>
public class WeaponInputBridge : MonoBehaviour
{
    [SerializeField] private PlayerWeaponController weapon;
    [SerializeField] private bool logCalls = false;

    private void Reset()
    {
        if (weapon == null) weapon = FindFirstObjectByType<PlayerWeaponController>();
    }

    // ---- New Input System (SendMessage) ----
#if ENABLE_INPUT_SYSTEM
    public void OnFire(InputValue value)
    {
        if (weapon == null) return;

        float v = value.Get<float>();              // 보통 0/1
        bool pressed = value.isPressed;            // hold 상태
        if (logCalls) Debug.Log($"[WeaponInputBridge] OnFire(InputValue) v={v} pressed={pressed}");

        if (pressed) weapon.Fire();
        else weapon.FireUp();
    }

    public void OnReload(InputValue value)
    {
        if (weapon == null) return;
        if (value.isPressed) weapon.Reload();
    }

    public void OnAim(InputValue value)
    {
        if (weapon == null) return;
        if (!weapon.HasWeaponEquipped())
        {
            weapon.SetADS(false);
            return;
        }

        weapon.SetADS(value.isPressed || value.Get<float>() > 0.5f);
    }
#endif

    // ---- Fallback (혹시 SendMessage가 파라미터 없이 호출되는 경우) ----
    public void OnFire()
    {
        if (logCalls) Debug.Log("[WeaponInputBridge] OnFire()");
        if (weapon == null) return;
        weapon.Fire();
    }

    public void OnFireUp()
    {
        if (logCalls) Debug.Log("[WeaponInputBridge] OnFireUp()");
        if (weapon == null) return;
        weapon.FireUp();
    }

    public void OnReload()
    {
        if (logCalls) Debug.Log("[WeaponInputBridge] OnReload()");
        if (weapon == null) return;
        weapon.Reload();
    }

    public void OnAimDown()
    {
        if (weapon != null && weapon.HasWeaponEquipped())
            weapon.SetADS(true);
    }

    public void OnAimUp()
    {
        if (weapon != null)
            weapon.SetADS(false);
    }
}