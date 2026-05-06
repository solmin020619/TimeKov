using System;
using UnityEngine;

public class PlayerStatComponent : MonoBehaviour
{
    [Header("HP (= 시간)")]
    public float MaxHp = 100f;
    public float HpDrainRate = 1f;      // 기지 밖에서 초당 감소량

    [Header("MP")]
    public float MaxMp = 100f;

    [Header("Stamina")]
    public float MaxStamina = 100f;
    public float StaminaDrain = 15f;   // 달리기 중 초당 소모량
    public float StaminaRegen = 20f;   // 초당 회복량
    public float StaminaRegenDelay = 1.5f;  // 달리기 멈춘 후 회복 시작까지 딜레이
    public float ExhaustedThreshold = 0.3f;  // 이 비율 이하면 Exhausted (0.3 = 30%)

    public float CurrentHp { get; private set; }
    public float CurrentMp { get; private set; }
    public float CurrentStamina { get; private set; }
    public bool IsExhausted { get; private set; }
    public bool IsInBase { get; private set; }

    private float _staminaRegenTimer;

    // 사망 시 외부에 알림 (RespawnManager 등에서 구독)
    public event Action OnDead;

    void Awake()
    {
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

    // ── HP (시간) ────────────────────────────────────────────
    void HandleHpDrain()
    {
        if (IsInBase) return;   // 기지 안이면 시간 정지
        if (IsDead) return;

        CurrentHp -= HpDrainRate * Time.deltaTime;

        if (CurrentHp <= 0)
        {
            CurrentHp = 0;
            OnDead?.Invoke();
        }
    }

    // 전투 등 외부에서 직접 데미지
    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        CurrentHp = Mathf.Max(0, CurrentHp - amount);
        if (CurrentHp <= 0) OnDead?.Invoke();
    }

    // BaseZone에서 호출
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

    // ── Stamina ─────────────────────────────────────────────
    // 달리기 중 매 프레임 호출. 달릴 수 있으면 true 반환
    public bool TryDrainSprintStamina()
    {
        if (IsExhausted) return false;

        CurrentStamina = Mathf.Max(0, CurrentStamina - StaminaDrain * Time.deltaTime);
        _staminaRegenTimer = StaminaRegenDelay;
        return true;
    }

    void HandleStaminaRegen()
    {
        if (_staminaRegenTimer > 0)
        {
            _staminaRegenTimer -= Time.deltaTime;
            return;
        }

        if (CurrentStamina < MaxStamina)
            CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + StaminaRegen * Time.deltaTime);
    }

    void UpdateExhaustedState()
    {
        // 30% 이하 진입 시 Exhausted
        if (!IsExhausted && CurrentStamina <= MaxStamina * ExhaustedThreshold)
            IsExhausted = true;

        // 100% 완전 회복 시에만 해제
        if (IsExhausted && CurrentStamina >= MaxStamina)
            IsExhausted = false;
    }

    // ── MP ──────────────────────────────────────────────────
    public void UseMp(float amount) => CurrentMp = Mathf.Max(0, CurrentMp - amount);
    public void RecoverMp(float amount) => CurrentMp = Mathf.Min(MaxMp, CurrentMp + amount);

    // ── 상태 ────────────────────────────────────────────────
    public bool IsDead => CurrentHp <= 0;
}