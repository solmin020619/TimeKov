using UnityEngine;

public class PlayerAnimatorComponent : MonoBehaviour
{
    private Player _player;
    private Animator _anim;

    private static readonly int NormalHash = Animator.StringToHash("----normal");     // �̵� �ӵ� (Blend Tree)
    private static readonly int Attack1Hash = Animator.StringToHash("ATTACK1");        // 1Ÿ Ʈ����
    private static readonly int Attack2Hash = Animator.StringToHash("ATTACK2");        // 2Ÿ Ʈ����
    private static readonly int Attack3Hash = Animator.StringToHash("ATTACK3");        // 3Ÿ Ʈ����
    private static readonly int Skill1Hash = Animator.StringToHash("SP SKILL 1");     // ��ų1 Ʈ����
    private static readonly int Skill2Hash = Animator.StringToHash("SP SKILL 2");     // ��ų2 Ʈ����
    private static readonly int Skill3Hash = Animator.StringToHash("SP SKILL 3");     // ��ų3 Ʈ����
    private static readonly int DashFHash = Animator.StringToHash("QUICK SHIFT F");  // �� ��� Ʈ����
    private static readonly int DashBHash = Animator.StringToHash("QUICK SHIFT B");  // �� ��� Ʈ����
    private static readonly int DashRHash = Animator.StringToHash("QUICK SHIFT R");  // �� ��� Ʈ����
    private static readonly int DashLHash = Animator.StringToHash("QUICK SHIFT L");  // �� ��� Ʈ����
    private static readonly int HitLHash = Animator.StringToHash("Hit L");          // �ǰ� �� Ʈ����
    private static readonly int HitRHash = Animator.StringToHash("Hit R");          // �ǰ� �� Ʈ����
    private static readonly int DieHash = Animator.StringToHash("Die");            // ��� Ʈ����
    private static readonly int JumpHash = Animator.StringToHash("Jump");           // ���� Ʈ����

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
        // ���� Ʈ���� �ʱ�ȭ �� ����
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

    // ��� ������ ĳ���� ���� �������� �Ǵ��ؼ� ���⺰ �ִϸ��̼� ���
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

    public void ResetToIdle()
    {
        // GS_Die는 Action Layer(레이어 1)에 있음 → 레이어 1도 Empty로 리셋해야 함
        // 레이어 1이 Override(weight=1)이라 리셋 안 하면 죽는 애니메이션이 계속 재생됨
        _anim.ResetTrigger(DieHash);
        _anim.Play("Blend Tree", 0, 0f);  // Base Layer → Idle/Walk/Run
        _anim.Play("Empty",      1, 0f);  // Action Layer → Empty (GS_Die 종료)
    }

    public void PlayHit(bool isLeft) => _anim.SetTrigger(isLeft ? HitLHash : HitRHash);
    public void PlayDie()
    {
        _anim.SetTrigger(DieHash);
        _player.Audio?.PlayDie();
    }
}