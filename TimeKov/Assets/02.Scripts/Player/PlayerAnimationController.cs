using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("이동/달리기 상태 값을 제공하는 플레이어 컨트롤러")]
    public PlayerController playerController;

    [Tooltip("현재 장착 무기(itemId)를 제공하는 무기 컨트롤러")]
    public PlayerWeaponController weaponController;

    [Tooltip("Time(체력) 소진 시 사망 이벤트를 받기 위한 컴포넌트")]
    public PlayerTime playerTime;

    [Header("Animator Overrides (Assign in Inspector)")]
    [Tooltip("무기 없음(Basic) 상태에서 사용할 Animator Override Controller")]
    public AnimatorOverrideController basicOverride;   // AOC_Basic

    [Tooltip("권총(Pistol) 상태에서 사용할 Animator Override Controller")]
    public AnimatorOverrideController pistolOverride;  // AOC_Pistol

    [Tooltip("장총(LongGun) 상태에서 사용할 Animator Override Controller (Rifle/SMG/Shotgun/Sniper 공용)")]
    public AnimatorOverrideController longGunOverride; // AOC_LongGun

    [Header("Debug")]
    [Tooltip("무기 상태(AnimSet)가 바뀔 때만 로그 출력")]
    public bool logWhenAnimSetChanges = false;

    private Animator anim;

    // Animator Parameters
    // MoveX(float), MoveY(float), Speed(int), IsDead(bool)
    private static readonly int HashMoveX = Animator.StringToHash("MoveX");
    private static readonly int HashMoveY = Animator.StringToHash("MoveY");
    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashIsDead = Animator.StringToHash("IsDead");

    private enum AnimSet { Basic, Pistol, LongGun }
    private AnimSet currentSet = (AnimSet)(-1);

    private void Awake()
    {
        // Animator 확보
        anim = GetComponent<Animator>();

        // 인스펙터에서 안 넣었다면 같은 오브젝트에서 자동 탐색
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (weaponController == null) weaponController = GetComponent<PlayerWeaponController>();
        if (playerTime == null) playerTime = GetComponent<PlayerTime>();

        // 게임 시작은 보통 무기 없음이므로 Basic으로 초기 적용
        // (만약 시작부터 무기가 있다면 Update에서 바로 교체됨)
        ApplyAnimSet(AnimSet.Basic);
    }

    private void OnEnable()
    {
        // Time(체력)이 0이 되는 이벤트를 받으면 애니 파라미터 IsDead를 true로 바꿔 Dead 상태로 보냄
        if (playerTime != null)
            playerTime.onTimeDepleted += OnDead;
    }

    private void OnDisable()
    {
        if (playerTime != null)
            playerTime.onTimeDepleted -= OnDead;
    }

    private void OnDead()
    {
        // Dead 전이는 Any State -> Dead로 만들어두는 것을 전제로 함
        if (anim == null) return;
        anim.SetBool(HashIsDead, true);
    }

    private void Update()
    {
        if (anim == null || playerController == null)
            return;

        // 이미 죽었다면(Dead 진입 후) 더 이상 이동 파라미터를 업데이트하지 않음
        if (anim.GetBool(HashIsDead))
            return;

        // 1) 무기 상태에 따라 Animator Override 교체
        UpdateAnimatorOverrideByWeapon();

        // 2) 이동 입력(평면)을 MoveX/MoveY로 변환
        // MoveInput은 Vector3이며 (x,z)를 사용한다고 가정 (Y 고정 구조)
        Vector2 move = new Vector2(playerController.MoveInput.x, playerController.MoveInput.z);

        // 대각선 입력이 (1,1)처럼 들어오면 BlendTree가 과하게 반응하니 Normalize
        if (move.sqrMagnitude > 1f) move.Normalize();

        // 3) Speed 값 계산
        // Speed: 0 Idle, 1 Walk, 2 Run
        int speed = 0;
        if (move.sqrMagnitude > 0.001f)
            speed = playerController.IsRunning ? 2 : 1;

        // 4) 이동 파라미터 업데이트 (Dash 삭제)
        anim.SetInteger(HashSpeed, speed);
        anim.SetFloat(HashMoveX, move.x);
        anim.SetFloat(HashMoveY, move.y);
    }

    // WeaponType -> AnimSet 매핑
    // WeaponData에는 LongGun이라는 타입이 없고,
    // Rifle/SMG/Shotgun/Sniper 같은 여러 타입을 "애니메이션 관점에서" LongGun으로 묶어서 처리한다.
    private AnimSet GetAnimSetFromWeapon(int itemId)
    {
        // 0이면 무기 없음
        if (itemId <= 0)
            return AnimSet.Basic;

        // 권총: 1400~1499
        if (itemId >= 1400 && itemId < 1500)
            return AnimSet.Pistol;

        // 장총: 1100~1399 (SR/AR/SMG/SG 포함)
        if (itemId >= 1100 && itemId < 1400)
            return AnimSet.LongGun;

        // 무기 범위 밖이면 기본으로
        return AnimSet.Basic;
    }

    // 무기 상태가 바뀌었을 때만 runtimeAnimatorController를 교체(성능/안정성)
    private void UpdateAnimatorOverrideByWeapon()
    {
        int equippedId = 0;
        if (weaponController != null)
            equippedId = weaponController.GetEquippedItemId();

        AnimSet target = GetAnimSetFromWeapon(equippedId);

        if (target == currentSet)
            return;

        ApplyAnimSet(target);

        if (logWhenAnimSetChanges)
            Debug.Log($"[Anim] Set -> {target} (equippedItemId={equippedId})");
    }

    // 실제로 AnimatorOverrideController를 Animator에 적용하는 함수
    private void ApplyAnimSet(AnimSet set)
    {
        RuntimeAnimatorController next = null;

        // 각 세트에 맞는 Override 컨트롤러를 선택
        switch (set)
        {
            case AnimSet.Basic:
                next = basicOverride;
                break;
            case AnimSet.Pistol:
                next = pistolOverride;
                break;
            case AnimSet.LongGun:
                next = longGunOverride;
                break;
        }

        // 인스펙터 연결이 빠졌으면(=null) 교체하지 않고 그대로 둠
        if (next != null)
            anim.runtimeAnimatorController = next;

        currentSet = set;
    }
}
