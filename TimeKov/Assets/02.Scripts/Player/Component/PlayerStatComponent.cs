using System;
using System.Collections;
using UnityEngine;

public class PlayerStatComponent : MonoBehaviour, ISaveable
{
    // 인스펙터에서 숨긴다 = 여기서 바꿔도 소용없기 때문.
    // 최대 체력(=시간)은 코어 강화 전용이고 값의 권위는 CoreLevelData 시트다.
    // CoreUpgradeManager.Start() -> ApplyCoreStats(data.maxTime) 가 무조건 덮어쓴다.
    // 조절하려면 시트의 maxTime 컬럼을 고쳐라.
    [HideInInspector] public float MaxHp = 300f;
    public float HpDrainRate = 1f;
    [Tooltip("시간(HP) 드레인 배수 - 보스 포효 등 디버프로 일시 가속(1=기본). 결계 밖에서만 적용.")]
    public float HpDrainMultiplier = 1f;

    // ATK/DEF/MaxStamina = 앰플로 영구 누적되는 값이라 인스펙터에서 조절할 수 없다.
    // 세이브가 있으면 저장값이, 없으면 PlayerBaseStats 가 값을 정한다.
    // 그래서 아예 직렬화를 끊었다([NonSerialized]) - 씬에 박혀 있던 사본이 죽어서
    // 같은 숫자를 씬/코드/GameSaveData 세 군데에 손으로 맞추던 이중관리가 사라진다.
    // 시작값을 바꾸려면 PlayerBaseStats 를 고쳐라.
    [NonSerialized] public float ATK = PlayerBaseStats.ATK;
    [NonSerialized] public float DEF = PlayerBaseStats.DEF;
    [NonSerialized] public float MaxStamina = PlayerBaseStats.MaxStamina;

    [Header("Stamina")]
    public float StaminaDrain = 10f;
    [Tooltip("기본 최대치 기준 초당 회복량. 앰플로 최대치가 커지면 회복량도 같은 비율로 커져 0->풀 완충 시간이 일정하게 유지된다. 예) 기본 100을 10초에 채우면(=10/s) 500으로 늘어도 10초에 채워진다.")]
    public float StaminaRegen = 5f;
    [Tooltip("탈진 후 달리기 재허용 기준 비율 (0~1). 스테미나가 0이 되면 탈진, 이 비율 이상 회복되면 재허용")]
    public float ExhaustedThreshold = 0.3f;
    [Tooltip("대쉬로 스태미나를 쓴 직후 이 시간(초)만큼 회복을 막는다. 걸으면서 대쉬 스팸으로 스태미나가 사실상 무한 회복되던 문제를 막는 핵심. (달리기에는 안 걸림 - 달리다 멈추면 바로 회복)")]
    public float StaminaRegenDelay = 1.5f;

    [Header("Hurt")]
    public float HurtDuration = 0.3f;  // 피격 경직(움직임 잠금) 시간. 무적과 분리 - 경직은 첫 히트만.
    [Tooltip("피격 후 무적 시간(초). '같은 순간 중복 히트'만 막는 최소값이라 짧게 둔다. 이 값보다 간격이 긴 다단/볼리/틱 공격(보스 3연발 등)은 전부 들어간다. 0.5처럼 크게 두면 3연발이 1발로 씹힌다.")]
    public float InvincibleDuration = 0.1f;  // 피격 후 무적 시간(짧게 - 다단 히트 씹힘 방지)
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
    public bool IsHurt { get; private set; }
    public bool IsInvincible => _iframeTimer > 0f;

    private Player _player;
    private Coroutine _hurtRoutine;
    private float _regenLockTimer;   // >0 동안 스태미나 회복 정지(대쉬 소모 직후만. 달리기는 안 검)
    private float _iframeTimer;       // >0 동안 피격 무적. '같은 순간 중복 히트'만 차단(다단/볼리/틱은 간격이 있어 통과).
    private float _baseMaxStamina = PlayerBaseStats.MaxStamina;   // 앰플 누적 전 기본 최대 스태미나. 회복량을 이 값 대비 현재 MaxStamina 비율로 스케일 -> 최대치를 늘려도 완충 시간 일정.

    // 로드 직후 ApplyCoreStats가 MaxHp를 확정하기 전까지 대기 중인 복원 비율(없으면 null).
    // CoreUpgradeManager.Start()가 ApplyCoreStats를 호출하는 시점에 1회만 소비된다.
    private float? _pendingHpPercent;

    public event Action OnDead;
    public event Action OnHurt;
    public event Action<float> OnDamaged;  // 시간(HP) 감소량 — 플로팅 텍스트용
    public event Action<float> OnHealed;   // 시간(HP) 회복량 — 플로팅 텍스트용

    void Awake()
    {
        _player = GetComponent<Player>();

        // 앰플 누적 전 기본 최대치(= PlayerBaseStats.MaxStamina). 회복량을 이 값 대비 비율로
        // 스케일해서, 앰플로 최대치를 늘려도 0->풀 완충 시간이 일정하게 유지된다.
        _baseMaxStamina = PlayerBaseStats.MaxStamina;

        SaveSlotManager.Instance?.Register(this);
        if (SaveSlotManager.Instance != null && SaveSlotManager.Instance.HasActiveSlot)
        {
            GameSaveData data = SaveSlotManager.Instance.Data;

            // ATK/DEF/MaxStamina는 앰플로 영구 누적되는 값 — CurrentStamina=MaxStamina 적용
            // 전에 먼저 복원해야 새로 늘어난 최대치 기준으로 풀스태미나가 채워진다.
            ATK = data.playerATK;
            DEF = data.playerDEF;
            MaxStamina = data.playerMaxStamina;

            _pendingHpPercent = data.playerHpPercent;

            // 위치 복원은 저장한 씬과 현재 씬이 같을 때만. 프롤로그(Tutorial)에서 저장된 좌표가
            // World로 새어들어와 바다 위에 스폰돼 즉사하던 것 방지. 씬이 다르거나 구버전 세이브면
            // 복원을 건너뛰고 World 씬에 배치된 기본 스폰 위치를 그대로 쓴다.
            if (data.hasPlayerPosition && data.playerPositionScene == gameObject.scene.name)
            {
                transform.position = data.playerPosition;
                transform.rotation = Quaternion.Euler(0f, data.playerRotationY, 0f);
            }
        }

        CurrentHp = MaxHp;
        CurrentStamina = MaxStamina;
    }

    void OnDestroy()
    {
        SaveSlotManager.Instance?.Unregister(this);
    }

    /// <summary>SaveSlotManager.SaveActive()가 호출. 비율로 저장(코어 강화로 MaxHp가 바뀌어도 안전) + 현재 위치.</summary>
    public void Capture(GameSaveData data)
    {
        data.playerHpPercent = MaxHp > 0f ? Mathf.Clamp01(CurrentHp / MaxHp) : 1f;
        data.playerATK = ATK;
        data.playerDEF = DEF;
        data.playerMaxStamina = MaxStamina;
        data.hasPlayerPosition = true;
        data.playerPosition = transform.position;
        data.playerRotationY = transform.eulerAngles.y;
        data.playerPositionScene = gameObject.scene.name;   // 이 위치를 저장한 씬(복원은 같은 씬일 때만)
    }

    void Update()
    {
        HandleHpDrain();
        if (_regenLockTimer > 0f) _regenLockTimer -= Time.deltaTime;
        if (_iframeTimer > 0f) _iframeTimer -= Time.deltaTime;
        HandleStaminaRegen();
        UpdateExhaustedState();
    }

    void HandleHpDrain()
    {
        if (IsInBase) return;
        if (IsDead) return;

        CurrentHp -= HpDrainRate * HpDrainMultiplier * Time.deltaTime;

        if (CurrentHp <= 0)
        {
            CurrentHp = 0;
            OnDead?.Invoke();
        }
    }

    public void TakeDamage(float amount, Vector3 attackerPos = default)
    {
        if (IsDead) return;
        // 무적은 '같은 순간 중복 히트'만 막는 짧은 창(InvincibleDuration, 기본 0.1s).
        // 보스 3연발/배러지/유성/틱처럼 간격(0.15~0.25s)이 있는 다단 공격은 이 창을 지나 전부 들어간다.
        if (_iframeTimer > 0f) return;
        _iframeTimer = InvincibleDuration;

        float finalDamage = Mathf.Max(1f, amount - DEF);
        CurrentHp = Mathf.Max(0, CurrentHp - finalDamage);
        OnDamaged?.Invoke(finalDamage);

        if (CurrentHp <= 0) { OnDead?.Invoke(); return; }

        _player?.Audio?.PlayHurt();   // 피격음(비치명타). 클립 2종 랜덤(번갈아) = GameSfxConfig PlayerHurt clips.

        // 피드백(셰이크/VFX/이벤트)은 들어간 모든 히트에 준다 -> 다단이면 연타 피드백.
        ThirdPersonCamera.Shake(HurtShakeDuration, HurtShakeMagnitude);
        VfxUtils.SpawnAtCaster(HurtVfxPrefab, gameObject, HurtVfxOffset, HurtVfxLifeTime, false);
        OnHurt?.Invoke();

        // [학살 플레이] 공격/스킬/대시 등 행동 중 피격은 경직 없이 데미지만(콤보 유지).
        if (IsInAction()) return;

        // 경직(움직임 잠금)은 첫 히트만 — 이미 경직 중이면 다시 걸지 않는다(배러지 스턴락 방지).
        if (IsHurt) return;
        if (_hurtRoutine != null) StopCoroutine(_hurtRoutine);   // 캔슬로 IsHurt만 내려간 잔존 루틴 정리
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
        // 무적(_iframeTimer)은 짧은 중복차단 창이라 건드리지 않는다 - 경직만 즉시 푼다.
        // 잔존 _hurtRoutine 은 다음 TakeDamage 가 StopCoroutine 으로 정리한다.
    }

    // 경직(움직임 잠금)만 담당. 무적은 TakeDamage의 _iframeTimer로 분리됐고(다단 히트 씹힘 방지),
    // 피드백(셰이크/VFX/OnHurt)도 TakeDamage에서 히트마다 직접 준다 -> 여기선 피격 애니메이션만.
    IEnumerator HurtRoutine(Vector3 attackerPos)
    {
        IsHurt = true;

        // 피격 애니메이션 — EnableHitAnimation이 true이고 정지 상태일 때만 재생
        // 이동 중 피격 시엔 경직만 주고 애니메이션은 생략 (어색한 끌림 방지)
        if (EnableHitAnimation && _player.Movement.CurrentSpeed < 0.1f)
        {
            bool isLeft = attackerPos != Vector3.zero && IsAttackerOnLeft(attackerPos);
            _player.Anim.PlayHit(isLeft);
        }

        // 피격 경직 시간(첫 히트만 — 재진입은 TakeDamage의 IsHurt 가드가 막아 배러지 스턴락 방지)
        yield return new WaitForSeconds(HurtDuration);
        IsHurt = false;
        _hurtRoutine = null;
    }

    bool IsAttackerOnLeft(Vector3 attackerPos)
    {
        Vector3 toAttacker = (attackerPos - transform.position).normalized;
        return Vector3.Dot(transform.right, toAttacker) < 0;
    }

    // 플랫 HP 회복
    public void Heal(float amount)
    {
        if (IsDead) return; // 사망 후 힐로 IsDead=false가 되어 좀비 상태가 되는 버그 방지
        float before = CurrentHp;
        CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
        float healed = CurrentHp - before;
        if (healed > 0f) OnHealed?.Invoke(healed);
    }

    // 최대 HP 비율 회복 (0.0 ~ 1.0)
    public void HealPercent(float percent)
    {
        if (IsDead) return; // 동일 사유
        float before = CurrentHp;
        CurrentHp = Mathf.Min(MaxHp, CurrentHp + MaxHp * percent);
        float healed = CurrentHp - before;
        if (healed > 0f) OnHealed?.Invoke(healed);
    }

    // 시간(HP)을 비용으로 소모 (상자 즉시열기 등). DEF/무적/경직 무시하는 순수 차감.
    public void SpendHp(float amount)
    {
        if (IsDead || amount <= 0f) return;
        float before = CurrentHp;
        CurrentHp = Mathf.Max(0f, CurrentHp - amount);
        float spent = before - CurrentHp;
        if (spent > 0f) OnDamaged?.Invoke(spent);
        if (CurrentHp <= 0f) OnDead?.Invoke();
    }

    // [07-29] 환경 시간 감소(서리장판 등 지속 장판). 결계 밖 자연 감소(HandleHpDrain)와 "같은 채널" =
    // 경직/카메라셰이크/피격VFX/플로팅텍스트 전부 없이 HP(시간)만 스르륵 깎인다. TakeDamage(공격)와 구분하는 게 핵심.
    // 매 프레임 amount(= 초당rate * deltaTime) 호출을 전제로 한다. 결계 안/사망 시엔 자연 감소처럼 무시.
    public void DrainTime(float amount)
    {
        if (IsDead || IsInBase || amount <= 0f) return;
        CurrentHp = Mathf.Max(0f, CurrentHp - amount);
        if (CurrentHp <= 0f) OnDead?.Invoke();
    }

    // 즉사 (물 빠짐 등). 무적/DEF/경직 전부 무시하고 바로 사망 흐름(OnDead) 발동.
    public void Kill()
    {
        if (IsDead) return;
        CurrentHp = 0f;
        OnDead?.Invoke();
    }

    public void RecoverStamina(float amount)
    {
        CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + amount);
    }

    public void SetInBase(bool inBase)
    {
        bool wasInBase = IsInBase;
        IsInBase = inBase;
        // 결계 안→밖 전이 시 1회 경고 (경계 들락날락 스팸은 쿨다운으로 억제)
        if (wasInBase && !inBase && !IsDead
            && Time.unscaledTime - _lastOutsideWarnTime > OutsideWarnCooldown)
        {
            _lastOutsideWarnTime = Time.unscaledTime;
            ToastManager.Warning("결계 밖입니다. 시간이 줄어듭니다!");
        }
        // 결계 밖→안 진입 전이: 필드에서 미뤄둔 발견 팝업을 이때 flush (전투/이동 중 방해 방지).
        if (!wasInBase && inBase)
            GameEvents.RaiseBaseEntered();
    }
    private float _lastOutsideWarnTime = -999f;
    private const float OutsideWarnCooldown = 8f;

    // 리스폰 시 호출 (hpPercent: 최대 HP 대비 부활 체력 비율. 1=풀피, 0.5=반피. 코어 강화로 MaxHp가 커져도 비율로 추적)
    public void Respawn(float hpPercent = 1f)
    {
        CurrentHp = Mathf.Clamp(MaxHp * hpPercent, 1f, MaxHp);
        CurrentStamina = MaxStamina;
        IsExhausted = false;
        IsInBase = true;
        IsHurt = false;
        _iframeTimer = 0f;

        if (_hurtRoutine != null)
        {
            StopCoroutine(_hurtRoutine);
            _hurtRoutine = null;
        }

        // 사망 시 Kinematic 으로 전환했던 Rigidbody 복구
        _player.Movement.UnfreezeOnRespawn();
    }

    public bool TryDrainSprintStamina()
    {
        if (IsExhausted) return false;
        CurrentStamina = Mathf.Max(0, CurrentStamina - StaminaDrain * Time.deltaTime);
        // 달리기는 회복 락아웃을 안 건다 -> 멈추면 바로 회복 시작. (달리는 중엔 HandleStaminaRegen의 !IsSprinting 가드가 회복을 막음)
        return true;
    }

    // 스태미나 즉시 소모 (대쉬 등). 소모 직후 회복 락아웃 시작.
    public void UseStamina(float amount)
    {
        CurrentStamina = Mathf.Max(0, CurrentStamina - amount);
        _regenLockTimer = StaminaRegenDelay;   // 대쉬 직후 회복 정지 -> 걸으면서 대쉬 스팸 무한충전 차단
    }

    void HandleStaminaRegen()
    {
        if (CurrentStamina >= MaxStamina) return;
        if (_regenLockTimer > 0f) return;   // 대쉬 소모 직후 회복 락아웃(달리기는 아래 !IsSprinting 가드로 처리)

        // 지상에 있고 스프린트 중이 아니면 회복 (idle + 걷기 모두 허용)
        bool canRegen = _player.Movement.IsGrounded
                     && !_player.Movement.IsSprinting;
        if (!canRegen) return;

        // 회복량 = 기본 최대치 대비 현재 최대치 비율로 스케일. 앰플로 MaxStamina가 커지면
        // 초당 회복량도 같은 배율로 커져서 0->풀 채우는 시간이 항상 일정하다(최대치 비례 X).
        float regen = _baseMaxStamina > 0f ? StaminaRegen * (MaxStamina / _baseMaxStamina) : StaminaRegen;
        CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + regen * Time.deltaTime);
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

    // 코어 강화 스탯 적용 (CoreUpgradeManager에서 호출)
    // 코어는 최대 체력(=시간)만 강화한다. 공격력/방어력/스태미나는 공장 앰플 담당.
    public void ApplyCoreStats(int maxTime)
    {
        MaxHp = maxTime;

        if (_pendingHpPercent.HasValue)
        {
            // 로드 직후 1회 — 저장된 비율로 복원 (MaxHp가 막 확정된 시점)
            CurrentHp = Mathf.Clamp(MaxHp * _pendingHpPercent.Value, 0f, MaxHp);
            _pendingHpPercent = null;
        }
        else
        {
            // 현재 HP가 새 최대치를 초과하면 클램프 (자동 회복 없음)
            CurrentHp = Mathf.Min(CurrentHp, MaxHp);
        }
    }

    // 플레이어 → 적 데미지 계산
    public float CalculateAttackDamage(float baseDamage, float enemyDef)
    {
        return Mathf.Max(1f, baseDamage + ATK - enemyDef);
    }

    public bool IsDead => CurrentHp <= 0;
}