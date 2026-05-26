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
    public float HurtDuration = 0.3f;  // ���� ���� �ð�
    public float InvincibleDuration = 0.5f;  // ���� �� ���� �ð�

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

        // Hurt ���� ����
        if (_hurtRoutine != null) StopCoroutine(_hurtRoutine);
        _hurtRoutine = StartCoroutine(HurtRoutine(attackerPos));
    }

    IEnumerator HurtRoutine(Vector3 attackerPos)
    {
        IsHurt = true;
        IsInvincible = true;

        // Skill3 ���� ���̸� Interrupt ȣ��
        var skillComp = GetComponent<PlayerSkillComponent>();
        if (skillComp != null && skillComp.CurrentSkillIsInterruptible)
            skillComp.Interrupt();

        // �ǰ� ���� �Ǻ� �� Hit L / Hit R ���
        bool isLeft = attackerPos != Vector3.zero && IsAttackerOnLeft(attackerPos);
        _player.Anim.PlayHit(isLeft);

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

        bool isIdle = _player.Input.MoveInput.magnitude < 0.1f
                   && _player.Movement.IsGrounded;
        if (!isIdle) return;

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

    // ������ ���� (�÷��̾� -> ��)
    public float CalculateAttackDamage(float baseDamage, float enemyDef)
    {
        return Mathf.Max(1f, baseDamage + ATK - enemyDef);
    }

    public bool IsDead => CurrentHp <= 0;
}