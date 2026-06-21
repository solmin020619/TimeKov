using System.Collections;
using UnityEngine;

public class PlayerDashComponent : MonoBehaviour
{
    [Header("Dash")]
    public float DashCost = 40f;
    public float DashCooldown = 0.8f;
    public float DashForce = 15f;
    public float DashDuration = 0.2f;

    [Header("Dash VFX")]
    public GameObject DashVfxPrefab;
    public Vector3 DashVfxOffset = new Vector3(0f, 0.2f, 0f);
    public Vector3 DashVfxRotationOffset = Vector3.zero;
    public float DashVfxLifeTime = 1f;
    public bool DashVfxParentToPlayer = true;

    private Player _player;
    private Rigidbody _rb;
    private float _cooldownTimer;

    public bool IsDashing { get; private set; }
    public bool IsOnCooldown => _cooldownTimer > 0;

    // HUD 대쉬 슬롯용 - 스킬 슬롯처럼 쿨다운 표시(테두리 링 + 남은 초)에 쓴다.
    public float CooldownRemaining => Mathf.Max(0f, _cooldownTimer);
    public float MaxCooldown => DashCooldown;

    // ── 코어 해금 게이트 ──────────────────────────────────────────────
    // 우클릭 대쉬 = 코어 0강이면 잠김, 1강 되면 해금(레벨은 안 내려가므로 한 번 풀리면 유지).
    public const int DashUnlockCoreLevel = 1;
    public static bool IsDashUnlocked
    {
        get { var m = CoreUpgradeManager.Instance; return m == null || m.CurrentCoreLevel >= DashUnlockCoreLevel; }
    }
    /// <summary>대쉬가 해금되는 순간 1회 발생 — HUD 아이콘 연출 트리거(SkillBarUI 구독).</summary>
    public static event System.Action OnDashUnlocked;
    /// <summary>잠긴 대쉬를 시도한 순간 발생(스로틀) — HUD 바를 띄워 잠금 슬롯을 보게(SkillBarUI 구독).</summary>
    public static event System.Action OnDashBlocked;

    // 코어 레벨이 오를수록 대쉬 스태미나 소모가 조금씩 줄어든다(레벨당 -3%, 최대 -30%).
    public float EffectiveDashCost
    {
        get
        {
            var m = CoreUpgradeManager.Instance;
            int lv = m != null ? m.CurrentCoreLevel : 0;
            return DashCost * Mathf.Clamp01(1f - lv * 0.03f);
        }
    }

    private float _lockedToastTimer;
    private bool  _wasUnlocked;

    void Awake()
    {
        _player = GetComponent<Player>();
        _rb = GetComponent<Rigidbody>();
        CoreUpgradeManager.OnLevelChanged += OnCoreLevelChanged;
    }

    void Start() => _wasUnlocked = IsDashUnlocked;   // 매니저 준비된 뒤 초기 해금 상태 기록(로드값 대비)

    void OnDestroy() => CoreUpgradeManager.OnLevelChanged -= OnCoreLevelChanged;

    // 코어 레벨 변동 — 잠김 -> 해금으로 넘어가는 순간 토스트 + 아이콘 연출(1회).
    void OnCoreLevelChanged(int level)
    {
        if (_wasUnlocked || !IsDashUnlocked) return;
        _wasUnlocked = true;
        ToastManager.Success("우클릭 대쉬 해금!");
        OnDashUnlocked?.Invoke();
    }

    void Update()
    {
        TickCooldown();

        if (_player.Input.DashPressed)
            TryDash();
    }

    void TickCooldown()
    {
        if (_cooldownTimer > 0)
            _cooldownTimer -= Time.deltaTime;
        if (_lockedToastTimer > 0)
            _lockedToastTimer -= Time.deltaTime;
    }

    void TryDash()
    {
        // ������Dead��Hurt ���� ����
        // 코어 0강이면 우클릭 대쉬 잠김 — 시도 시 안내(스팸 방지 스로틀).
        if (!IsDashUnlocked)
        {
            if (_lockedToastTimer <= 0f)
            {
                ToastManager.Warning($"우클릭 대쉬는 코어 {DashUnlockCoreLevel}강에 해금됩니다");
                _lockedToastTimer = 2.5f;
                OnDashBlocked?.Invoke();   // HUD 바 띄우고 잠금 슬롯 덜컹(왜 안 되는지 보게)
            }
            return;
        }

        if (_player.Movement.IsJumping) return;
        if (_player.Stat.IsDead) return;
        if (_player.Stat.IsHurt) return;

        // 제자리(이동입력 없음)에서도 우클릭하면 정면으로 대시한다 (#20).
        // GetDashDirection()이 입력 없으면 transform.forward 를 반환하므로 방향은 안전.
        if (IsOnCooldown) return;
        if (_player.Stat.CurrentStamina < EffectiveDashCost) return;
        if (_player.Skill.IsExecuting) return;

        StartCoroutine(DashRoutine());
    }

    IEnumerator DashRoutine()
    {
        IsDashing = true;

        _player.Stat.UseStamina(EffectiveDashCost);
        _cooldownTimer = DashCooldown;
        // LockMovement이 X/Z velocity를 0으로 박지만, 같은 frame 직후 dashForce 적용 → 정상
        // HandleSlopeStabilize는 actualXZ < 0.5f 가드가 있어서 dashForce(15)는 보존됨
        _player.Movement.LockMovement(true);

        Vector3 dashDir = _player.Movement.GetDashDirection();

        _rb.linearVelocity = new Vector3(
            dashDir.x * DashForce,
            _rb.linearVelocity.y,
            dashDir.z * DashForce
        );

        _player.Anim.PlayDash(dashDir);
        _player.Audio?.PlayDash();

        // ��� ����Ʈ ����
        VfxUtils.SpawnAtCaster(
            DashVfxPrefab,
            gameObject,
            DashVfxOffset,
            DashVfxRotationOffset,
            DashVfxLifeTime,
            DashVfxParentToPlayer
        );

        yield return new WaitForSeconds(DashDuration);

        // [미끄러짐 수정] 대시 물리 이동이 끝나면 대시 끝자세(자세 낮춘) 클립이 길게
        // 재생되며 미끄러지던 문제 — 클립 끝까지 기다리지 않고 즉시 이동으로 부드럽게 전환.
        // (기존엔 WaitUntil(!IsInActionAnim) 로 끝자세 클립이 끝날 때까지 잠가서 미끄러짐 발생.)
        _rb.linearVelocity = new Vector3(0, _rb.linearVelocity.y, 0);
        _player.Anim.EndDash();   // 대시 클립 -> 이동(Blend Tree) 짧게 블렌딩 (즉시 컷 아님)
        _player.Movement.LockMovement(false, applyPostUnlockDelay: false);
        IsDashing = false;
    }
}