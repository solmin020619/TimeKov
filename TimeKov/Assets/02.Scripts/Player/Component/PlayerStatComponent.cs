using System;
using System.Collections;
using UnityEngine;

public class PlayerStatComponent : MonoBehaviour
{
    [Header("HP (= �ð�)")]
    public float MaxHp = 300f;
    public float HpDrainRate = 1f;

    [Header("ATK / DEF")]
    public float ATK = 0f;
    public float DEF = 0f;

    [Header("Stamina")]
    public float MaxStamina = 100f;
    public float StaminaDrain = 10f;
    public float StaminaRegen = 5f;
    [Tooltip("탈진 후 달리기 재허용 기준 비율 (0~1). 스테미나가 0이 되면 탈진, 이 비율 이상 회복되면 재허용")]
    public float ExhaustedThreshold = 0.3f;

    [Header("Hurt")]
    public float HurtDuration = 0.3f;  // 피격 경직 시간
    public float InvincibleDuration = 0.5f;  // 피격 후 무적 시간
    [Tooltip("피격 시 Hit 애니메이션 재생 여부. 일반 몹=false, 보스=true로 설정")]
    public bool EnableHitAnimation = false;

    [Header("Camera Shake")]
    public float HurtShakeDuration  = 0.18f;
    public float HurtShakeMagnitude = 0.10f;

    [Header("Hit VFX")]
    public GameObject HurtVfxPrefab;
    public Vector3 HurtVfxOffset = new Vector3(0f, 1f, 0f);
    public float HurtVfxLifeTime = 1.5f;

    public float CurrentHp { get; private set; }
    public float CurrentStamina { get; private set; }
    public bool IsExhausted { get; private set; }
    public bool IsInBase { get; private set; }
    public bool IsHurt { get; private set; }  // �ǰ� ���� ��
    public bool IsInvincible { get; private set; }  // ���� ��

    private Player _player;
    private Coroutine _hurtRoutine;

    public event Action OnDead;
    public event Action OnHurt;  // UI �ǰ� �ǵ���

    void Awake()
    {
        _player = GetComponent<Player>();
        CurrentHp = MaxHp;
        CurrentStamina = MaxStamina;
    }

    void Update()
    {
        HandleHpDrain();
        HandleStaminaRegen();
        UpdateExhaustedState();
    }

    // HP(�ð�) �ڵ� ����
    void HandleHpDrain()
    {
        if (IsInBase) return;
        if (IsDead) return;

        CurrentHp -= HpDrainRate * Time.deltaTime;

        if (CurrentHp <= 0)
        {
            CurrentHp = 0;
            OnDead?.Invoke();
        }
    }

    // �ܺ� ������ (attackerPos: �ǰ� ���� �Ǻ���)
    public void TakeDamage(float amount, Vector3 attackerPos = default)
    {
        if (IsDead) return;
        if (IsInvincible) return;  // ���� �� ����

        float finalDamage = Mathf.Max(1f, amount - DEF);
        CurrentHp = Mathf.Max(0, CurrentHp - finalDamage);

        if (CurrentHp <= 0) { OnDead?.Invoke(); return; }

        // [학살 플레이] 공격/스킬/대시 등 행동 중 피격은 데미지만 받고 행동을 끊지 않는다.
        // 잡몹에 둘러싸여도 콤보가 뚝뚝 끊기지 않도록 — 가만히/이동 중일 때만 경직.
        if (IsInAction())
        {
            // 피드백(셰이크/VFX)만 주고 경직·인터럽트 없음
            ThirdPersonCamera.Shake(HurtShakeDuration, HurtShakeMagnitude);
            VfxUtils.SpawnAtCaster(HurtVfxPrefab, gameObject, HurtVfxOffset, HurtVfxLifeTime, false);
            OnHurt?.Invoke();
            return;
        }

        // Hurt ���� ����
        if (_hurtRoutine != null) StopCoroutine(_hurtRoutine);
        _hurtRoutine = StartCoroutine(HurtRoutine(attackerPos));
    }

    // 공격/스킬/대시 등 "행동 중"인지 — 행동 중 피격은 끊지 않기 위한 판정
    bool IsInAction()
    {
        if (_player == null) return false;
        bool skillExecuting = _player.Skill != null && _player.Skill.IsExecuting;
        bool dashing        = _player.Dash != null && _player.Dash.IsDashing;
        return skillExecuting || dashing;
    }

    // 피격 경직을 즉시 해제한다 (무적은 유지). 공격 입력으로 경직을 캔슬할 때 사용.
    // 정지 상태에서 1타를 누르는 순간 직전 피격 경직과 겹쳐 콤보 첫 타가 씹히는 문제 방지.
    public void CancelHurtStun()
    {
        if (!IsHurt) return;
        IsHurt = false;
        // 무적(IsInvincible)은 그대로 둬서 같은 타격 도배만 막고, 경직만 푼다.
        // _hurtRoutine 은 무적 해제까지 계속 돌게 두되 IsHurt 만 내려 행동 가능 상태로.
    }

    IEnumerator HurtRoutine(Vector3 attackerPos)
    {
        IsHurt = true;
        IsInvincible = true;

        // 피격 카메라 셰이크
        ThirdPersonCamera.Shake(HurtShakeDuration, HurtShakeMagnitude);

        // 행동 중 피격은 위 TakeDamage 에서 이미 분기 처리(끊지 않음) → 여기 도달 시점은
        // 가만히/이동 중. 인터럽트는 더 이상 강제하지 않는다. (학살 플레이 — 콤보 유지)

        // 피격 애니메이션 — EnableHitAnimation이 true이고 정지 상태일 때만 재생
        // 이동 중 피격 시엔 경직만 주고 애니메이션은 생략 (어색한 끌림 방지)
        if (EnableHitAnimation && _player.Movement.CurrentSpeed < 0.1f)
        {
            bool isLeft = attackerPos != Vector3.zero && IsAttackerOnLeft(attackerPos);
            _player.Anim.PlayHit(isLeft);
        }

        VfxUtils.SpawnAtCaster(
            HurtVfxPrefab,
            gameObject,
            HurtVfxOffset,
            HurtVfxLifeTime,
            false
            );

        OnHurt?.Invoke();  // UI �ǵ�� �̺�Ʈ

        // 피격 경직 시간
        yield return new WaitForSeconds(HurtDuration);
        IsHurt = false;

        // 무적 잔여 시간 (InvincibleDuration이 HurtDuration보다 작게 설정되면 0으로 보정)
        float remainingInvincible = Mathf.Max(0f, InvincibleDuration - HurtDuration);
        yield return new WaitForSeconds(remainingInvincible);
        IsInvincible = false;
        _hurtRoutine = null;
    }

    // �ǰ� ���� �Ǻ� (�����̸� true)
    bool IsAttackerOnLeft(Vector3 attackerPos)
    {
        Vector3 toAttacker = (attackerPos - transform.position).normalized;
        return Vector3.Dot(transform.right, toAttacker) < 0;
    }

    // 플랫 HP 회복
    public void Heal(float amount)
    {
        if (IsDead) return; // 사망 후 힐로 IsDead=false가 되어 좀비 상태가 되는 버그 방지
        CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
    }

    // 최대 HP 비율 회복 (0.0 ~ 1.0)
    public void HealPercent(float percent)
    {
        if (IsDead) return; // 동일 사유
        CurrentHp = Mathf.Min(MaxHp, CurrentHp + MaxHp * percent);
    }

    // ���¹̳� ��� ȸ��
    public void RecoverStamina(float amount)
    {
        CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + amount);
    }

    // BaseZone���� ȣ��
    public void SetInBase(bool inBase) => IsInBase = inBase;

    // ������ �� ȣ��
    public void Respawn()
    {
        CurrentHp = MaxHp;
        CurrentStamina = MaxStamina;
        IsExhausted = false;
        IsInBase = true;
        IsHurt = false;
        IsInvincible = false;

        if (_hurtRoutine != null)
        {
            StopCoroutine(_hurtRoutine);
            _hurtRoutine = null;
        }

        // 사망 시 Kinematic 으로 전환했던 Rigidbody 복구
        _player.Movement.UnfreezeOnRespawn();
    }

    // �޸��� �� �� ������ ȣ��  �޸� �� ������ true
    public bool TryDrainSprintStamina()
    {
        if (IsExhausted) return false;
        CurrentStamina = Mathf.Max(0, CurrentStamina - StaminaDrain * Time.deltaTime);
        return true;
    }

    // ���¹̳� ��� �Ҹ� (��� ��)
    public void UseStamina(float amount)
    {
        CurrentStamina = Mathf.Max(0, CurrentStamina - amount);
    }

    void HandleStaminaRegen()
    {
        if (CurrentStamina >= MaxStamina) return;

        // 지상에 있고 스프린트 중이 아니면 회복 (idle + 걷기 모두 허용)
        bool canRegen = _player.Movement.IsGrounded
                     && !_player.Movement.IsSprinting;
        if (!canRegen) return;

        CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + StaminaRegen * Time.deltaTime);
    }

    void UpdateExhaustedState()
    {
        // 스테미나 0 → 탈진 (달리기 완전히 소진 후)
        if (!IsExhausted && CurrentStamina <= 0f)
            IsExhausted = true;

        // 탈진 후 ExhaustedThreshold(30%) 이상 회복되면 달리기 재허용
        if (IsExhausted && CurrentStamina >= MaxStamina * ExhaustedThreshold)
            IsExhausted = false;
    }

    // 코어 강화 스탯 일괄 적용 (CoreUpgradeManager에서 호출)
    // 강화 성공 또는 게임 로드 시 해당 레벨 데이터로 갱신
    public void ApplyCoreStats(int maxTime, int stamina, int atk, int def)
    {
        MaxHp       = maxTime;
        MaxStamina  = stamina;
        ATK         = atk;
        DEF         = def;

        // 현재값이 새 최대치를 초과하면 클램프 (자동 회복 없음)
        CurrentHp       = Mathf.Min(CurrentHp, MaxHp);
        CurrentStamina  = Mathf.Min(CurrentStamina, MaxStamina);
    }

    // 플레이어 → 적 데미지 계산
    public float CalculateAttackDamage(float baseDamage, float enemyDef)
    {
        return Mathf.Max(1f, baseDamage + ATK - enemyDef);
    }

    public bool IsDead => CurrentHp <= 0;
}