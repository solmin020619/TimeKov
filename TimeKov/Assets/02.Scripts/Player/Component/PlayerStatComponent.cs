using System;
using UnityEngine;

public class PlayerStatComponent : MonoBehaviour
{
    [Header("HP (= 시간)")]
    public float MaxHp = 300f;    // 최대 생존 시간 (초)
    public float HpDrainRate = 1f;      // 기지 밖에서 초당 감소량

    [Header("ATK / DEF")]
    public float ATK = 0f;              // 공격 수치, 코어 강화로만 증가, 최대 100
    public float DEF = 0f;              // 방어 수치, 코어 강화로만 증가, 최대 100

    [Header("MP")]
    public float MaxMp = 100f;          // 최대 MP

    [Header("Stamina")]
    public float MaxStamina = 100f;  // 최대 스태미나
    public float StaminaDrain = 10f;   // 달리기 중 초당 소모량
    public float StaminaRegen = 5f;    // Idle 상태에서 초당 회복량
    public float ExhaustedThreshold = 0.3f;  // 이 비율 이하면 Exhausted (0.3 = 30%)

    public float CurrentHp { get; private set; }   // 현재 HP (시간)
    public float CurrentMp { get; private set; }   // 현재 MP
    public float CurrentStamina { get; private set; }   // 현재 스태미나
    public bool IsExhausted { get; private set; }   // 스태미나 30% 이하 상태
    public bool IsInBase { get; private set; }   // 기지 내부 여부

    private Player _player;

    public event Action OnDead;  // 사망 시 외부에 알림

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

    // HP (시간)
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

    // 적 공격 등 외부에서 직접 데미지
    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        // 최솟값 1 보장
        float finalDamage = Mathf.Max(1f, amount - DEF);
        CurrentHp = Mathf.Max(0, CurrentHp - finalDamage);

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

    // Stamina
    // 달리기 중 매 프레임 호출, 달릴 수 있으면 true 반환
    public bool TryDrainSprintStamina()
    {
        if (IsExhausted) return false;

        CurrentStamina = Mathf.Max(0, CurrentStamina - StaminaDrain * Time.deltaTime);
        return true;
    }

    // 스태미나 직접 소모, 대시 등 즉시 소모에 사용
    public void UseStamina(float amount)
    {
        CurrentStamina = Mathf.Max(0, CurrentStamina - amount);
    }

    void HandleStaminaRegen()
    {
        if (CurrentStamina >= MaxStamina) return;

        // Idle 상태에서만 회복 (이동 입력 없고 지상일 때)
        bool isIdle = _player.Input.MoveInput.magnitude < 0.1f
                   && _player.Movement.IsGrounded;

        if (!isIdle) return;

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

    // MP
    public void UseMp(float amount) => CurrentMp = Mathf.Max(0, CurrentMp - amount);
    public void RecoverMp(float amount) => CurrentMp = Mathf.Min(MaxMp, CurrentMp + amount);

    // 데미지 공식 (플레이어 -> 적)
    // 최종 데미지 = 기본 데미지 + 플레이어 ATK - 적 DEF, 최솟값 1
    public float CalculateAttackDamage(float baseDamage, float enemyDef)
    {
        return Mathf.Max(1f, baseDamage + ATK - enemyDef);
    }

    public bool IsDead => CurrentHp <= 0;   // 사망 여부
}