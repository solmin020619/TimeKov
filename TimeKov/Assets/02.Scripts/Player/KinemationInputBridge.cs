using UnityEngine;
using UnityEngine.InputSystem;
using KINEMATION.FPSAnimationPack.Scripts.Player;

public class KinemationInputBridge : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("같은 오브젝트(SK_Arms_Mono)에 붙은 FPSPlayer")]
    public FPSPlayer fpsPlayer;

    [Tooltip("Player 루트(혹은 너가 둔 곳)에 있는 PlayerWeaponController")]
    public PlayerWeaponController weaponController;

    [Header("Options")]
    public bool driveAssetFire = true;     // fpsPlayer.FirePressed/Released 호출
    public bool driveGameplayFire = true;  // weaponController.RequestFire 호출

    private void Reset()
    {
        if (fpsPlayer == null) fpsPlayer = GetComponent<FPSPlayer>();
    }

    private void Awake()
    {
        if (fpsPlayer == null) fpsPlayer = GetComponent<FPSPlayer>();

        if (weaponController == null)
            weaponController = GetComponentInParent<PlayerWeaponController>();

        if (fpsPlayer == null)
            Debug.LogWarning("[KinemationInputBridge] fpsPlayer is null. Assign FPSPlayer.");
        if (weaponController == null)
            Debug.LogWarning("[KinemationInputBridge] weaponController is null. Assign PlayerWeaponController.");
    }

    // =========================
    // PlayerInput (Send Messages) receivers
    // =========================

    // Fire: Send Messages는 보통 InputValue로 들어옴 (버튼)
    public void OnFire(InputValue value)
    {
        bool pressed = value != null && value.isPressed;

        if (driveGameplayFire && pressed)
        {
            // 우리 쪽: 애니 이벤트 타이밍으로 발사되게 "요청"만 세팅
            weaponController?.RequestFire();
        }

        if (driveAssetFire)
        {
            // 에셋 쪽: 총 모션/사운드/카메라쉐이크/탄 소모
            if (pressed) fpsPlayer?.FirePressed();
            else fpsPlayer?.FireReleased();
        }
    }

    public void OnReload()
    {
        fpsPlayer?.OnReload();
        // 우리 쪽은 기본 구현이 "R키 코루틴"이었으니 일단 에셋만
        // 필요하면 weaponController 쪽에 Reload 요청 함수 만들어서 여기서 호출하면 됨.
    }

    // Aim: 버튼/토글 둘 다 대응 (InputValue.isPressed)
    public void OnAim(InputValue value)
    {
        bool aiming = value != null && value.isPressed;
        fpsPlayer?.SetAiming(aiming);
    }

    public void OnChangeWeapon()
    {
        fpsPlayer?.OnChangeWeapon();
    }

    public void OnChangeFireMode()
    {
        fpsPlayer?.OnChangeFireMode();
    }

    public void OnThrowGrenade()
    {
        fpsPlayer?.OnThrowGrenade();
    }

    // Move/Look는 지금 “뷰모델만 유지” 구조라 FPSPlayer 쪽 SetMoveInput/AddLookPitchDelta로 넣고 싶으면
    // 너 PlayerController 입력 구조 정리 후에 연결하자.
    // 지금 단계(발사/히트)에서는 굳이 안 건드려도 됨.
}
