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
    // ─── 이동 ────────────────────────────────────────────────────────
    [Header("이동")]
    [Tooltip("걷기 발소리 클립 목록 (랜덤 재생)")]
    public AudioClip[] WalkClips;

    [Tooltip("달리기 발소리 클립 목록 (랜덤 재생)")]
    public AudioClip[] RunClips;

    [Tooltip("걷기 발소리 재생 간격 (초)")]
    public float WalkStepInterval = 0.50f;

    [Tooltip("달리기 발소리 재생 간격 (초)")]
    public float RunStepInterval  = 0.32f;

    // ─── 대시/스태미나 ───────────────────────────────────────────────
    [Header("대시 / 스태미나")]
    public AudioClip DashClip;
    public AudioClip StaminaWarningClip;

    // ─── 기본 공격 ───────────────────────────────────────────────────
    [Header("기본 공격")]
    public AudioClip Attack1Clip;
    public AudioClip Attack2Clip;
    public AudioClip Attack3Clip;
    public AudioClip AttackHitClip;

    // ─── 스킬 ────────────────────────────────────────────────────────
    [Header("스킬")]
    public AudioClip Skill1Clip;
    public AudioClip Skill2Clip;
    public AudioClip Skill3Clip;
    public AudioClip SkillHitClip;
    public AudioClip SkillUnavailableClip;

    // ─── 피격 / 상태 ─────────────────────────────────────────────────
    [Header("피격 / 상태")]
    public AudioClip HurtClip;
    public AudioClip JumpClip;
    public AudioClip DieClip;

    // ─── AudioSource ─────────────────────────────────────────────────
    [Header("Audio Sources (비워두면 자동 생성)")]
    [Tooltip("효과음 전용 AudioSource")]
    [SerializeField] private AudioSource _sfxSource;

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
            _sfxSource.spatialBlend = 0f; // 2D (플레이어는 항상 화면 중앙)
            _sfxSource.playOnAwake  = false;
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
            PlayRandom(_footstepSource, isSprinting ? RunClips : WalkClips);
        }
    }

    // 스태미나 부족음: IsExhausted 가 false → true 로 전환되는 순간 1회
    void HandleStaminaWarning()
    {
        bool isExhausted = _player.Stat.IsExhausted;
        if (!_wasExhausted && isExhausted)
            PlayOneShot(StaminaWarningClip);
        _wasExhausted = isExhausted;
    }

    // ═════════════════════════════════════════════════════════════════
    // 공개 재생 메서드 (다른 컴포넌트에서 호출)
    // ═════════════════════════════════════════════════════════════════

    /// <summary>대시 발동음</summary>
    public void PlayDash() => PlayOneShot(DashClip);

    /// <summary>기본 공격 스윙음 (comboIndex: 0=1타, 1=2타, 2=3타)</summary>
    public void PlayAttackSwing(int comboIndex)
    {
        var clip = comboIndex == 0 ? Attack1Clip :
                   comboIndex == 1 ? Attack2Clip :
                                     Attack3Clip;
        PlayOneShot(clip);
    }

    /// <summary>기본 공격 적중음</summary>
    public void PlayAttackHit() => PlayOneShot(AttackHitClip);

    /// <summary>스킬 발동음 (SkillSheetId 기준)</summary>
    public void PlaySkill(SkillSheetId id)
    {
        var clip = id == SkillSheetId.Skill1 ? Skill1Clip :
                   id == SkillSheetId.Skill2 ? Skill2Clip :
                                               Skill3Clip;
        PlayOneShot(clip);
    }

    /// <summary>스킬 적중음</summary>
    public void PlaySkillHit() => PlayOneShot(SkillHitClip);

    /// <summary>스킬 사용 불가음 (게이지 부족 / 쿨다운)</summary>
    public void PlaySkillUnavailable() => PlayOneShot(SkillUnavailableClip);

    /// <summary>피격음 (OnHurt 이벤트로 자동 호출)</summary>
    public void PlayHurt() => PlayOneShot(HurtClip);

    /// <summary>점프음</summary>
    public void PlayJump() => PlayOneShot(JumpClip);

    /// <summary>사망음</summary>
    public void PlayDie() => PlayOneShot(DieClip);

    // ═════════════════════════════════════════════════════════════════
    // 헬퍼
    // ═════════════════════════════════════════════════════════════════

    void PlayOneShot(AudioClip clip)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip);
    }

    void PlayRandom(AudioSource source, AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0 || source == null) return;
        var clip = clips[Random.Range(0, clips.Length)];
        if (clip != null) source.PlayOneShot(clip);
    }
}
