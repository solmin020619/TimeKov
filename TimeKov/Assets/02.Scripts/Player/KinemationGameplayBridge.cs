using UnityEngine;

public class KinemationGameplayBridge : MonoBehaviour
{
    [Header("Drag Player object (has PlayerWeaponController)")]
    public PlayerWeaponController gameplay;

    // ---- Animation Events (called by Fire / Reload clips) ----

    // Fire clip Animation Event -> Function: OnFireGameplay
    public void OnFireGameplay()
    {
        if (gameplay == null) return;
        gameplay.OnFireGameplay();
    }

    // (선택) Reload clip Animation Event -> Function: OnReloadApply
    public void OnReloadApply()
    {
        if (gameplay == null) return;
        gameplay.OnReloadApply();
    }

    // (선택) Reload 시작 타이밍이 필요하면
    public void OnReloadStart()
    {
        if (gameplay == null) return;
        gameplay.OnReloadStart();
    }
}
