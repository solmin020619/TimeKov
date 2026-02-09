using UnityEngine;

public class WeaponAnimEventBridge : MonoBehaviour
{
    public PlayerWeaponController weapon;

    // Fire 애니 클립에서 이벤트로 호출
    public void OnFireGameplay()
    {
        Debug.Log(" OnFireGameplay CALLED (Bridge)");
        if (weapon != null) weapon.OnFireGameplay();
        else Debug.LogWarning("[Bridge] weapon is NULL");
    }

    // Reload 애니를 이벤트 기반으로 바꿀 때 사용(선택)
    public void OnReloadStart()
    {
        weapon?.OnReloadStart();
    }

    public void OnReloadApply()
    {
        weapon?.OnReloadApply();
    }
}
