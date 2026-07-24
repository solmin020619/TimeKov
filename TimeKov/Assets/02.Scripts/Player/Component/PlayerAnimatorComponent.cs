using UnityEngine;

public class PlayerAnimatorComponent : MonoBehaviour
{
    private Player _player;
    private Animator _anim;

    private static readonly int NormalHash = Animator.StringToHash("----normal");
    private static readonly int Attack1Hash = Animator.StringToHash("ATTACK1");
    private static readonly int Attack2Hash = Animator.StringToHash("ATTACK2");
    private static readonly int Attack3Hash = Animator.StringToHash("ATTACK3");
    private static readonly int Skill1Hash = Animator.StringToHash("SP SKILL 1");
    private static readonly int Skill2Hash = Animator.StringToHash("SP SKILL 2");
    private static readonly int Skill3Hash = Animator.StringToHash("SP SKILL 3");
    private static readonly int DashFHash = Animator.StringToHash("QUICK SHIFT F");
    private static readonly int DashBHash = Animator.StringToHash("QUICK SHIFT B");
    private static readonly int DashRHash = Animator.StringToHash("QUICK SHIFT R");
    private static readonly int DashLHash = Animator.StringToHash("QUICK SHIFT L");
    private static readonly int HitLHash = Animator.StringToHash("Hit L");
    private static readonly int HitRHash = Animator.StringToHash("Hit R");
    private static readonly int DieHash = Animator.StringToHash("Die");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    // 피격 회복 직후 애니메이션 즉시 동기화용 타이머
    private bool  _prevIsHurt;
    private float _hurtRecoveryTimer;
    private const float HURT_RECOVERY_ANIM = 0.15f;

    void Awake()
    {
        _player = GetComponent<Player>();
        _anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        UpdateMovement();
    }

    void UpdateMovement()
    {
        // 피격 상태가 끝난 직후 감지 → 회복 타이머 시작
        bool nowHurt = _player.Stat.IsHurt;
        if (_prevIsHurt && !nowHurt)
        {
            _hurtRecoveryTimer = HURT_RECOVERY_ANIM;
            // 피격 종료 순간 이동 입력이 있으면 Blend Tree 즉시 강제 전환 (끌림 방지)
            if (_player.Input.MoveInput.magnitude > 0.1f)
                _anim.Play("Blend Tree", 0, 0f);
        }
        _prevIsHurt = nowHurt;

        if (_hurtRecoveryTimer > 0f)
            _hurtRecoveryTimer -= Time.deltaTime;

        // damping = 0 적용 조건:
        // 1) 공격·스킬 잠금 해제 직후 (IsPostLockTransition)
        // 2) 피격 중 (IsHurt) — 속도 0 즉시 반영
        // 3) 피격 회복 직후 0.15s — 이동 시작 시 애니메이션 즉시 전환 (끌림 방지)
        bool snapSync = _player.Movement.IsPostLockTransition
                     || _player.Stat.IsHurt
                     || _hurtRecoveryTimer > 0f;

        float damp = snapSync ? 0f : 0.15f;
        _anim.SetFloat(NormalHash, _player.Movement.CurrentSpeed, damp, Time.deltaTime);
    }

    public void PlayAttack(int comboIndex)
    {
        _anim.ResetTrigger(Attack1Hash);
        _anim.ResetTrigger(Attack2Hash);
        _anim.ResetTrigger(Attack3Hash);

        switch (comboIndex)
        {
            case 0: _anim.SetTrigger(Attack1Hash); break;
            case 1: _anim.SetTrigger(Attack2Hash); break;
            case 2: _anim.SetTrigger(Attack3Hash); break;
        }

        // 기본 공격 스윙 사운드
        _player.Audio?.PlayAttackSwing(comboIndex);
    }

    public void PlaySkill(int skillIndex)
    {
        switch (skillIndex)
        {
            case 0: _anim.SetTrigger(Skill1Hash); break;
            case 1: _anim.SetTrigger(Skill2Hash); break;
            case 2: _anim.SetTrigger(Skill3Hash); break;
        }
    }

    public void PlayDash(Vector3 dashDir)
    {
        Vector3 localDir = transform.InverseTransformDirection(dashDir);
        float forward = localDir.z;
        float right = localDir.x;

        if (Mathf.Abs(forward) >= Mathf.Abs(right))
        {
            if (forward >= 0) _anim.SetTrigger(DashFHash);
            else _anim.SetTrigger(DashBHash);
        }
        else
        {
            if (right >= 0) _anim.SetTrigger(DashRHash);
            else _anim.SetTrigger(DashLHash);
        }
    }

    public void PlayJump()
    {
        _anim.SetTrigger(JumpHash);
        _player.Audio?.PlayJump();
    }

    /// <summary>개발용(비행 등): 애니메이션 정지/재개. speed 토글이라 이동 파라미터가 바뀌어도 현재 포즈가 고정된다.</summary>
    public void SetFrozen(bool frozen)
    {
        if (_anim != null) _anim.speed = frozen ? 0f : 1f;
    }

    public void ResetToIdle()
    {
        // GS_Die는 Action Layer(레이어 1)에 있음 → 레이어 1도 Empty로 리셋해야 함
        // 레이어 1이 Override(weight=1)이라 리셋 안 하면 죽는 애니메이션이 계속 재생됨
        _anim.ResetTrigger(DieHash);
        _anim.Play("Blend Tree", 0, 0f);  // Base Layer → Idle/Walk/Run
        _anim.Play("Empty",      1, 0f);  // Action Layer → Empty (GS_Die 종료)
    }

    /// <summary>
    /// 대시·공격·스킬 등 액션 애니메이션이 재생 중이면 true.
    /// 전환(Transition) 중이거나 Blend Tree 상태가 아닐 때 true를 반환.
    /// DashRoutine / SkillFlow / ComboAttackBase 에서 이동 잠금 해제 타이밍 판단에 사용.
    /// </summary>
    public bool IsInActionAnim()
    {
        if (_anim == null) return false;
        if (_anim.IsInTransition(0)) return true;   // 전환 중 = 아직 액션 중
        return !_anim.GetCurrentAnimatorStateInfo(0).IsName("Blend Tree");
    }

    /// <summary>
    /// 액션(스킬/공격) 동작이 끝났을 때 이동(Blend Tree)으로 즉시 복귀.
    /// 스킬 클립이 딜 모션보다 훨씬 길어(후속 마무리 폼 포함), 딜 끝난 뒤 그 잔여 클립이
    /// Blend Tree로 천천히 블렌딩되며 미끄러지듯 보이던 문제 해결 — 즉시 컷으로 잘라낸다.
    /// </summary>
    public void ReturnToLocomotion()
    {
        if (_anim == null) return;
        _anim.ResetTrigger(Skill1Hash);
        _anim.ResetTrigger(Skill2Hash);
        _anim.ResetTrigger(Skill3Hash);
        _anim.ResetTrigger(Attack1Hash);
        _anim.ResetTrigger(Attack2Hash);
        _anim.ResetTrigger(Attack3Hash);

        // 공격/스킬 클립은 Action Layer(레이어 1)에서 재생된다.
        // 레이어 0 만 Play 하면 스킬 모션이 안 끊기므로 두 레이어 모두 처리:
        //   레이어 0(Base) -> Blend Tree(이동),  레이어 1(Action) -> Empty(동작 종료)
        _anim.Play("Blend Tree", 0, 0f);
        _anim.Play("Empty",      1, 0f);
    }

    /// <summary>
    /// 대시 물리 이동이 끝났을 때 이동(Blend Tree)으로 "부드럽게" 복귀.
    /// 스킬과 달리 대시는 이동 흐름이라 즉시 컷하면 뚝 끊겨 부자연스럽다.
    /// 대시 클립(QUICK SHIFT)은 Action Layer(1)에서 재생되므로, 그 레이어를 짧게
    /// CrossFade 로 빼서 "자세 낮춘 끝부분"이 길게 미끄러지는 것만 잘라낸다.
    /// </summary>
    public void EndDash(float fade = 0.2f)
    {
        if (_anim == null) return;
        _anim.ResetTrigger(DashFHash);
        _anim.ResetTrigger(DashBHash);
        _anim.ResetTrigger(DashRHash);
        _anim.ResetTrigger(DashLHash);
        // 레이어 1(Action)을 Empty 로 짧게 블렌딩 → 대시 끝자세에서 이동으로 매끄럽게 전환
        _anim.CrossFade("Empty", fade, 1);
    }

    public void PlayHit(bool isLeft) => _anim.SetTrigger(isLeft ? HitLHash : HitRHash);

    /// <summary>귀환석 채널링 모션 시작 — Action Layer(1)의 "Channel" 상태(GS_CrouchIdle, 루프)를 강제 재생.
    /// 진행 중이던 액션/대기 트리거를 먼저 제거해, 다른 모션 도중 눌러도 채널 상태가 튕기지 않게 한다.</summary>
    public void PlayChannel()
    {
        if (_anim == null) return;
        ResetActionTriggers();
        _anim.SetLayerWeight(1, 1f);          // 크라우치가 전신으로 보이게 Action 레이어 강제
        _anim.Play("Channel", 1, 0f);         // 트랜지션에 밀리지 않게 강제 진입(PlayDie와 동일 방식)
    }

    // 채널 진입/사망이 다른 액션 트리거에 밀려 안 나오는 것 방지 — 대기 중 트리거 일괄 제거.
    private void ResetActionTriggers()
    {
        _anim.ResetTrigger(Attack1Hash); _anim.ResetTrigger(Attack2Hash); _anim.ResetTrigger(Attack3Hash);
        _anim.ResetTrigger(Skill1Hash);  _anim.ResetTrigger(Skill2Hash);  _anim.ResetTrigger(Skill3Hash);
        _anim.ResetTrigger(DashFHash);   _anim.ResetTrigger(DashBHash);
        _anim.ResetTrigger(DashRHash);   _anim.ResetTrigger(DashLHash);
        _anim.ResetTrigger(HitLHash);    _anim.ResetTrigger(HitRHash);
        _anim.ResetTrigger(JumpHash);
    }

    /// <summary>채널링 종료 — Action Layer를 Empty 로 빼 이동/대기로 복귀.</summary>
    public void StopChannel(float fade = 0.15f)
    {
        if (_anim == null) return;
        _anim.CrossFade("Empty", fade, 1);
    }

    public void PlayDie()
    {
        // 죽는 애니가 확실히, 전신으로 나오게:
        //  - Action 레이어(1) weight=1 강제 (점프/낙하 등 베이스 레이어 포즈가 비쳐 스완다이브처럼 보이던 것 방지)
        //  - 트리거 + Play 둘 다 (전환 누락 시에도 강제 재생)
        _anim.ResetTrigger(DieHash);
        _anim.SetLayerWeight(1, 1f);
        _anim.SetTrigger(DieHash);
        _anim.Play("GS_Die", 1, 0f);
        _player.Audio?.PlayDie();
    }
}