using UnityEngine;

public class AnimEventRelay : MonoBehaviour
{
    [Header("Relay Target")]
    [Tooltip("Player 루트(혹은 너가 둔 곳)에 있는 PlayerWeaponController")]
    public PlayerWeaponController weaponController;

    private void Awake()
    {
        if (weaponController == null)
            weaponController = GetComponentInParent<PlayerWeaponController>();

        if (weaponController == null)
            Debug.LogWarning("[AnimEventRelay] weaponController is null. Assign PlayerWeaponController.");
    }

    // =========================
    // Animation Events (Fire clip etc.)
    // =========================

    /// <summary>
    /// Fire 애니 클립에 Animation Event로 걸어둘 함수명
    /// Function = OnFireGameplay
    /// </summary>
    public void OnFireGameplay()
    {
        weaponController?.OnFireGameplay();
    }

    /// <summary>
    /// 리로드 애니에서 탄 채우는 프레임에 걸고 싶으면
    /// Function = OnReloadApply
    /// </summary>
    public void OnReloadApply()
    {
        weaponController?.OnReloadApply();
    }

    /// <summary>
    /// 필요하면: 리로드 시작 프레임 이벤트
    /// Function = OnReloadStart
    /// </summary>
    public void OnReloadStart()
    {
        weaponController?.OnReloadStart();
    }
}
