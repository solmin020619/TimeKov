// =====================================================================
// PlayerStatComponent.cs
// 플레이어 스탯 관리 : HP(시간), ATK, DEF, MP, 스태미나
// =====================================================================

using System;
using UnityEngine;

public class PlayerStatComponent : MonoBehaviour
{
    [Header("HP (= 시간)")]
    public float MaxHp = 300f;
    public float HpDrainRate = 1f;

    [Header("ATK / DEF")]
    public float ATK = 0f;
    public float DEF = 0f;

    [Header("MP")]
    public float MaxMp = 100f;

    [Header("Stamina")]
    public float MaxStamina = 100f;
    public float StaminaDrain = 10f;
    public float StaminaRegen = 5f;
    public float ExhaustedThreshold = 0.3f;

    public float CurrentHp { get; private set; }
    public float CurrentMp { get; private set; }
    public float CurrentStamina { get; private set; }
    public bool IsExhausted { get; private set; }
    public bool IsInBase { get; private set; }

    private Player _player;

    public event Action OnDead;

    void Awake()
    {
        _player = GetComponent<Player>();
        CurrentHp = MaxHp;
        CurrentMp = MaxMp;
        CurrentStamina = MaxStamina;
    }

    void Update()
    {
        HandleHpDrain();
        HandleStaminaRegen();
        UpdateExhaustedState();
    }

    // ── HP(시간) 감소 ─────────────────────────────────────────

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

    // 적 공격 등 외부 데미지
    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        float finalDamage = Mathf.Max(1f, amount - DEF);
        CurrentHp = Mathf.Max(0, CurrentHp - finalDamage);

        if (CurrentHp <= 0) OnDead?.Invoke();
    }

    // ── 회복 메서드 (인벤토리 소모품 연동) ───────────────────

    // Flat HP 회복
    public void Heal(float amount)
    {
        CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
    }

    // 최대HP 기준 비율 회복 (percent: 0.0 ~ 1.0)
    public void HealPercent(float percent)
    {
        CurrentHp = Mathf.Min(MaxHp, CurrentHp + MaxHp * percent);
    }

    // 스태미나 즉시 회복
    public void RecoverStamina(float amount)
    {
        CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + amount);
    }

    // ── 기존 메서드 ───────────────────────────────────────────

    // BaseZone 에서 호출
    public void SetInBase(bool inBase) => IsInBase = inBase;

    // 리스폰 시 호출
    public void Respawn()
    {
        CurrentHp = MaxHp;
        CurrentMp = MaxMp;
        CurrentStamina = MaxStamina;
        IsExhausted = false;
        IsInBase = true;
    }

    // 달리기 중 매 프레임 호출 : 달릴 수 있으면 true
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

    public void UseMp(float amount) => CurrentMp = Mathf.Max(0, CurrentMp - amount);
    public void RecoverMp(float amount) => CurrentMp = Mathf.Min(MaxMp, CurrentMp + amount);

    // 데미지 공식 (플레이어 -> 적)
    public float CalculateAttackDamage(float baseDamage, float enemyDef)
    {
        return Mathf.Max(1f, baseDamage + ATK - enemyDef);
    }

    public bool IsDead => CurrentHp <= 0;
}