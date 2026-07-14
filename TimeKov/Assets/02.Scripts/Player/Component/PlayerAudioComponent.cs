// PlayerAudioComponent.cs
// 플레이어 전체 사운드 관리
// ─────────────────────────────────────────────────────────────────────
// 이동      : 걷기/달리기 발소리 (랜덤 클립, 속도별 인터벌)
// 대시/스태미나 : 대시 발동음, 스태미나 부족음 (탈진 전환 시 1회)
// 기본 공격  : 휘두르기 1/2/3 (스윙 시작), 공격 적중음
// 스킬      : Q/E/R 발동음, 스킬 적중음, 사용 불가음
// 피격      : 플레이어 피격음 (OnHurt 이벤트 구독)
// ─────────────────────────────────────────────────────────────────────

using UnityEngine;

[RequireComponent(typeof(Player))]
public class PlayerAudioComponent : MonoBehaviour
{
    // 모든 플레이어 효과음 클립은 GameSfx(SfxId.Player*)로 통합 — GameSfxConfig 에서 관리.
    // 재생은 아래 3개 로컬 소스에서 유지(공격 스윙 중단·발소리 분리 때문). 발소리는 SfxId 배열(랜덤).

    // ─── 이동 ────────────────────────────────────────────────────────
    [Header("이동")]
    [Tooltip("걷기 발소리 재생 간격 (초)")]
    public float WalkStepInterval = 0.50f;

    [Tooltip("달리기 발소리 재생 간격 (초)")]
    public float RunStepInterval  = 0.32f;

    // ─── AudioSource ─────────────────────────────────────────────────
    [Header("Audio Sources (비워두면 자동 생성)")]
    [Tooltip("효과음 전용 AudioSource (피격·스킬·점프 등)")]
    [SerializeField] private AudioSource _sfxSource;

    [Tooltip("공격 스윙 전용 AudioSource — 피격 시 독립적으로 Stop 가능")]
    [SerializeField] private AudioSource _attackSource;

    [Tooltip("발소리 전용 AudioSource")]
    [SerializeField] private AudioSource _footstepSource;

    // ─── 런타임 ──────────────────────────────────────────────────────
    private Player _player;
    private float  _stepTimer;
    private bool   _wasExhausted;

    // ═════════════════════════════════════════════════════════════════

    void Awake()
    {
        _player = GetComponent<Player>();

        // AudioSource 자동 생성
        if (_sfxSource == null)
        {
            _sfxSource              = gameObject.AddComponent<AudioSource>();
            _sfxSource.spatialBlend = 0f;
            _sfxSource.playOnAwake  = false;
        }

        if (_attackSource == null)
        {
            _attackSource              = gameObject.AddComponent<AudioSource>();
            _attackSource.spatialBlend = 0f;
            _attackSource.playOnAwake  = false;
        }

        if (_footstepSource == null)
        {
            _footstepSource              = gameObject.AddComponent<AudioSource>();
            _footstepSource.spatialBlend = 0f;
            _footstepSource.playOnAwake  = false;
        }
    }

    void Start()
    {
        // 피격 이벤트 구독
        if (_player.Stat != null)
            _player.Stat.OnHurt += PlayHurt;
    }

    void OnDestroy()
    {
        if (_player?.Stat != null)
            _player.Stat.OnHurt -= PlayHurt;
    }

    void Update()
    {
        HandleFootstep();
        HandleStaminaWarning();
    }

    // ═════════════════════════════════════════════════════════════════
    // 내부 로직
    // ═════════════════════════════════════════════════════════════════

    // 발소리: 속도·지면 여부에 따라 인터벌 재생
    void HandleFootstep()
    {
        if (!_player.Movement.IsGrounded)
        {
            _stepTimer = 0f;
            return;
        }

        float speed       = _player.Movement.CurrentSpeed;
        float sprintThres = _player.Movement.SprintSpeed * 0.8f;
        float walkThres   = _player.Movement.MoveSpeed   * 0.3f;

        if (speed < walkThres)
        {
            _stepTimer = 0f;
            return;
        }

        bool isSprinting = speed >= sprintThres;
        float interval   = isSprinting ? RunStepInterval : WalkStepInterval;

        _stepTimer -= Time.deltaTime;
        if (_stepTimer <= 0f)
        {
            _stepTimer = interval;
            PlayOn(_footstepSource, isSprinting ? SfxId.PlayerFootstepRun : SfxId.PlayerFootstepWalk);
        }
    }

    // 스태미나 부족음: IsExhausted 가 false → true 로 전환되는 순간 1회
    void HandleStaminaWarning()
    {
        bool isExhausted = _player.Stat.IsExhausted;
        if (!_wasExhausted && isExhausted)
            PlayOn(_sfxSource, SfxId.PlayerStaminaWarning);
        _wasExhausted = isExhausted;
    }

    // ═════════════════════════════════════════════════════════════════
    // 공개 재생 메서드 (다른 컴포넌트에서 호출)
    // ═════════════════════════════════════════════════════════════════

    /// <summary>대시 발동음</summary>
    public void PlayDash() => PlayOn(_sfxSource, SfxId.PlayerDash);

    /// <summary>기본 공격 스윙음 (comboIndex: 0=1타, 1=2타, 2=3타)</summary>
    public void PlayAttackSwing(int comboIndex)
    {
        var id = comboIndex == 0 ? SfxId.PlayerAttack1 :
                 comboIndex == 1 ? SfxId.PlayerAttack2 :
                                   SfxId.PlayerAttack3;
        PlayOn(_attackSource, id);
    }

    /// <summary>
    /// 공격 스윙음 즉시 중단 — 피격 인터럽트 시 호출
    /// _attackSource만 Stop → 피격음·스킬음 등 다른 효과음은 영향 없음
    /// </summary>
    public void StopAttackSwing()
    {
        if (_attackSource == null) return;
        _attackSource.Stop();
    }

    /// <summary>기본 공격 적중음</summary>
    public void PlayAttackHit() => PlayOn(_sfxSource, SfxId.PlayerAttackHit);

    /// <summary>스킬 발동음 (SkillSheetId 기준)</summary>
    public void PlaySkill(SkillSheetId id)
    {
        var sfx = id == SkillSheetId.Skill1 ? SfxId.PlayerSkill1 :
                  id == SkillSheetId.Skill2 ? SfxId.PlayerSkill2 :
                                              SfxId.PlayerSkill3;
        PlayOn(_sfxSource, sfx);
    }

    /// <summary>스킬 적중음</summary>
    public void PlaySkillHit() => PlayOn(_sfxSource, SfxId.PlayerSkillHit);

    /// <summary>스킬 사용 불가음 (게이지 부족 / 쿨다운)</summary>
    public void PlaySkillUnavailable() => PlayOn(_sfxSource, SfxId.PlayerSkillUnavailable);

    /// <summary>피격음 (OnHurt 이벤트로 자동 호출)</summary>
    public void PlayHurt() => PlayOn(_sfxSource, SfxId.PlayerHurt);

    /// <summary>점프음</summary>
    public void PlayJump() => PlayOn(_sfxSource, SfxId.PlayerJump);

    /// <summary>사망음</summary>
    public void PlayDie() => PlayOn(_sfxSource, SfxId.PlayerDie);

    // ═════════════════════════════════════════════════════════════════
    // 헬퍼
    // ═════════════════════════════════════════════════════════════════

    // 지정 소스에 GameSfxConfig 의 클립을 재생(배열 ID는 랜덤). 클립 미지정이면 무음.
    void PlayOn(AudioSource source, SfxId id)
    {
        if (source == null) return;
        if (GameSfx.TryGet(id, out var clip, out var volume)) source.PlayOneShot(clip, volume);
    }
}
