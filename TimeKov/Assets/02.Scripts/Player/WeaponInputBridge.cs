// WeaponInputBridge.cs
using UnityEngine;

/// <summary>
/// Player Input(SendMessage) -> PlayerWeaponController 로 전달
/// (KINEMATION FPSPlayer는 여기서 건드리지 않는다: 연출은 KinemationWeaponDriver가 처리)
/// </summary>
public class WeaponInputBridge : MonoBehaviour
{
    [SerializeField] private PlayerWeaponController weapon;
    [SerializeField] private bool logCalls = false;

    private void Reset()
    {
        if (weapon == null) weapon = FindFirstObjectByType<PlayerWeaponController>();
    }

    public void OnFire()
    {
        if (logCalls) Debug.Log("[WeaponInputBridge] OnFire()");
        if (weapon == null) return;
        weapon.Fire();
    }
    public void Fire() => OnFire();

    public void OnFireUp()
    {
        if (logCalls) Debug.Log("[WeaponInputBridge] OnFireUp()");
        if (weapon == null) return;
        weapon.FireUp();
    }
    public void FireUp() => OnFireUp();

    public void OnReload()
    {
        if (logCalls) Debug.Log("[WeaponInputBridge] OnReload()");
        if (weapon == null) return;
        weapon.Reload();
    }
    public void Reload() => OnReload();
    public void OnAim() { if (weapon != null) weapon.SetADS(true); }
    public void OnAimDown() { if (weapon != null) weapon.SetADS(true); }
    public void OnAimUp() { if (weapon != null) weapon.SetADS(false); }
    public void OnAim(bool isAiming) { if (weapon != null) weapon.SetADS(isAiming); }
    public void OnAim(float value) { if (weapon != null) weapon.SetADS(value > 0.5f); }
}
