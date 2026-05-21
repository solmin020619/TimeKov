using System;
using System.Collections;
using UnityEngine;

public class PlayerStatComponent : MonoBehaviour
{
    [Header("HP (= 시간)")]
    public float MaxHp = 300f;
    public float HpDrainRate = 1f;

    [Header("ATK / DEF")]
    public float ATK = 0f;
    public float DEF = 0f;

    [Header("Stamina")]
    public float MaxStamina = 100f;
    public float StaminaDrain = 10f;
    public float StaminaRegen = 5f;
    public float ExhaustedThreshold = 0.3f;

    [Header("Hurt")]
    public float HurtDuration = 0.3f;  // 경직 지속 시간
    public float InvincibleDuration = 0.5f;  // 무적 총 지속 시간

    [Header("Hit VFX")]
    public GameObject HurtVfxPrefab;
    public Vector3 HurtVfxOffset = new Vector3(0f, 1f, 0f);
    public float HurtVfxLifeTime = 1.5f;

    public float CurrentHp { get; private set; }
    public float CurrentStamina { get; private set; }
    public bool IsExhausted { get; private set; }
    public bool IsInBase { get; private set; }
    public bool IsHurt { get; private set; }  // 피격 경직 중
    public bool IsInvincible { get; private set; }  // 무적 중

    private Player _player;
    private Coroutine _hurtRoutine;

    public event Action OnDead;
    public event Action OnHurt;  // UI 피격 피드백용

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

    // HP(시간) 자동 감소
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

    // 외부 데미지 (attackerPos: 피격 방향 판별용)
    public void TakeDamage(float amount, Vector3 attackerPos = default)
    {
        if (IsDead) return;
        if (IsInvincible) return;  // 무적 중 무시

        float finalDamage = Mathf.Max(1f, amount - DEF);
        CurrentHp = Mathf.Max(0, CurrentHp - finalDamage);

        if (CurrentHp <= 0) { OnDead?.Invoke(); return; }

        // Hurt 상태 진입
        if (_hurtRoutine != null) StopCoroutine(_hurtRoutine);
        _hurtRoutine = StartCoroutine(HurtRoutine(attackerPos));
    }

    IEnumerator HurtRoutine(Vector3 attackerPos)
    {
        IsHurt = true;
        IsInvincible = true;

        // Skill3 선딜 중이면 Interrupt 호출
        var skillComp = GetComponent<PlayerSkillComponent>();
        if (skillComp != null && skillComp.CurrentSkillIsInterruptible)
            skillComp.Interrupt();

        // 피격 방향 판별 후 Hit L / Hit R 재생
        bool isLeft = attackerPos != Vector3.zero && IsAttackerOnLeft(attackerPos);
        _player.Anim.PlayHit(isLeft);

        VfxUtils.SpawnAtCaster(
            HurtVfxPrefab,
            gameObject,
            HurtVfxOffset,
            HurtVfxLifeTime,
            false
            );

        OnHurt?.Invoke();  // UI 피드백 이벤트

        // 경직 0.3초
        yield return new WaitForSeconds(HurtDuration);
        IsHurt = false;

        // 무적 잔여 시간 (총 0.5초에서 경직 0.3초 제외)
        yield return new WaitForSeconds(InvincibleDuration - HurtDuration);
        IsInvincible = false;
        _hurtRoutine = null;
    }

    // 피격 방향 판별 (좌측이면 true)
    bool IsAttackerOnLeft(Vector3 attackerPos)
    {
        Vector3 toAttacker = (attackerPos - transform.position).normalized;
        return Vector3.Dot(transform.right, toAttacker) < 0;
    }

    // 플랫 HP 회복
    public void Heal(float amount)
    {
        CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
    }

    // 최대 HP 비율 회복 (0.0 ~ 1.0)
    public void HealPercent(float percent)
    {
        CurrentHp = Mathf.Min(MaxHp, CurrentHp + MaxHp * percent);
    }

    // 스태미나 즉시 회복
    public void RecoverStamina(float amount)
    {
        CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + amount);
    }

    // BaseZone에서 호출
    public void SetInBase(bool inBase) => IsInBase = inBase;

    // 리스폰 시 호출
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
    }

    // 달리기 중 매 프레임 호출  달릴 수 있으면 true
    public bool TryDrainSprintStamina()
    {
        if (IsExhausted) return false;
        CurrentStamina = Mathf.Max(0, CurrentStamina - StaminaDrain * Time.deltaTime);
        return true;
    }

    // 스태미나 즉시 소모 (대시 등)
    public void UseStamina(float amount)
    {
        CurrentStamina = Mathf.Max(0, CurrentStamina - amount);
    }

    void HandleStaminaRegen()
    {
        if (CurrentStamina >= MaxStamina) return;

        bool isIdle = _player.Input.MoveInput.magnitude < 0.1f
                   && _player.Movement.IsGrounded;
        if (!isIdle) return;

        CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + StaminaRegen * Time.deltaTime);
    }

    void UpdateExhaustedState()
    {
        if (!IsExhausted && CurrentStamina <= MaxStamina * ExhaustedThreshold)
            IsExhausted = true;

        if (IsExhausted && CurrentStamina >= MaxStamina)
            IsExhausted = false;
    }

    // 데미지 공식 (플레이어 -> 적)
    public float CalculateAttackDamage(float baseDamage, float enemyDef)
    {
        return Mathf.Max(1f, baseDamage + ATK - enemyDef);
    }

    public bool IsDead => CurrentHp <= 0;
}