// KinemationWeaponDriver.cs
using System.Reflection;
using UnityEngine;
using KINEMATION.FPSAnimationPack.Scripts.Player;
using KINEMATION.FPSAnimationPack.Scripts.Weapon;

/// <summary>
/// SK_Arms_Mono(=FPSPlayer가 붙은 오브젝트)에 붙인다.
/// - 우리 게임플레이(PWC)에서 "성공 발사/리로드 시작" 이벤트를 받으면
/// - KINEMATION 쪽 FPSPlayer/FPSWeapon을 호출해 발사 반동/사운드/카메라쉐이크/장전 애니를 재생한다.
/// - FPSWeapon 내부 _activeAmmo를 우리 탄 수로 동기화해서, 탄약 때문에 애니가 끊기는 문제를 막는다.
/// </summary>
public class KinemationWeaponDriver : MonoBehaviour
{
    [SerializeField] private FPSPlayer fpsPlayer;
    [SerializeField] private PlayerWeaponController weaponCtrl;

    private static FieldInfo _fiActiveAmmo;

    private void Awake()
    {
        if (fpsPlayer == null) fpsPlayer = GetComponent<FPSPlayer>();
        if (weaponCtrl == null) weaponCtrl = FindFirstObjectByType<PlayerWeaponController>();

        if (_fiActiveAmmo == null)
        {
            _fiActiveAmmo = typeof(FPSWeapon).GetField("_activeAmmo",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }

    private void OnEnable()
    {
        if (weaponCtrl == null) return;
        weaponCtrl.Fired += OnGameplayFired;
        weaponCtrl.ReloadStarted += OnGameplayReloadStarted;
    }

    private void OnDisable()
    {
        if (weaponCtrl == null) return;
        weaponCtrl.Fired -= OnGameplayFired;
        weaponCtrl.ReloadStarted -= OnGameplayReloadStarted;
    }

    private void OnGameplayFired()
    {
        if (fpsPlayer == null) return;

        // FPSWeapon 탄 동기화 (중요)
        SyncActiveAmmoToGameplay();

        // 발사 애니/반동/사운드/카메라쉐이크 트리거
        fpsPlayer.FirePressed();

        // Semi 모드면 바로 Release 해서 재진입/루프 방지
        fpsPlayer.FireReleased();
    }

    private void OnGameplayReloadStarted()
    {
        if (fpsPlayer == null) return;

        // 리로드 애니는 FPSWeapon.OnReload()에서 재생 (Tac/Empty는 activeAmmo로 결정)
        SyncActiveAmmoToGameplay();
        fpsPlayer.OnReload();
    }

    private void SyncActiveAmmoToGameplay()
    {
        if (weaponCtrl == null || fpsPlayer == null) return;

        FPSWeapon w = fpsPlayer.GetActiveWeapon();
        if (w == null) return;

        int curAmmo = weaponCtrl.GetCurrentAmmo();
        int magSize = weaponCtrl.GetMagazineSize();

        // weaponSettings.ammo(탄창 최대)도 맞춰준다
        if (w.weaponSettings != null)
        {
            w.weaponSettings.ammo = Mathf.Max(1, magSize);
        }

        // FPSWeapon._activeAmmo (protected, nonpublic) 강제 세팅
        if (_fiActiveAmmo != null)
        {
            _fiActiveAmmo.SetValue(w, Mathf.Max(0, curAmmo));
        }
    }
}
