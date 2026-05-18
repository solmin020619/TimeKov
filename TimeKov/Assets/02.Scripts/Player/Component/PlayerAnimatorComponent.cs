using UnityEngine;

public class PlayerAnimatorComponent : MonoBehaviour
{
    private Player _player;
    private Animator _anim;

    private static readonly int NormalHash = Animator.StringToHash("----normal");     // 이동 속도 (Blend Tree)
    private static readonly int Attack1Hash = Animator.StringToHash("ATTACK1");        // 1타 트리거
    private static readonly int Attack2Hash = Animator.StringToHash("ATTACK2");        // 2타 트리거
    private static readonly int Attack3Hash = Animator.StringToHash("ATTACK3");        // 3타 트리거
    private static readonly int Skill1Hash = Animator.StringToHash("SP SKILL 1");     // 스킬1 트리거
    private static readonly int Skill2Hash = Animator.StringToHash("SP SKILL 2");     // 스킬2 트리거
    private static readonly int Skill3Hash = Animator.StringToHash("SP SKILL 3");     // 스킬3 트리거
    private static readonly int DashFHash = Animator.StringToHash("QUICK SHIFT F");  // 앞 대시 트리거
    private static readonly int DashBHash = Animator.StringToHash("QUICK SHIFT B");  // 뒤 대시 트리거
    private static readonly int DashRHash = Animator.StringToHash("QUICK SHIFT R");  // 우 대시 트리거
    private static readonly int DashLHash = Animator.StringToHash("QUICK SHIFT L");  // 좌 대시 트리거
    private static readonly int HitLHash = Animator.StringToHash("Hit L");          // 피격 좌 트리거
    private static readonly int HitRHash = Animator.StringToHash("Hit R");          // 피격 우 트리거
    private static readonly int DieHash = Animator.StringToHash("Die");            // 사망 트리거
    private static readonly int JumpHash = Animator.StringToHash("Jump");           // 점프 트리거

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
        // Blend Tree가 Float 하나로 Idle/Walk/Run/Sprint 자동 블렌딩
        _anim.SetFloat(NormalHash, _player.Movement.CurrentSpeed, 0.15f, Time.deltaTime);
    }

    public void PlayAttack(int comboIndex)
    {
        // 기존 트리거 초기화 후 세팅
        _anim.ResetTrigger(Attack1Hash);
        _anim.ResetTrigger(Attack2Hash);
        _anim.ResetTrigger(Attack3Hash);

        switch (comboIndex)
        {
            case 0: _anim.SetTrigger(Attack1Hash); break;
            case 1: _anim.SetTrigger(Attack2Hash); break;
            case 2: _anim.SetTrigger(Attack3Hash); break;
        }
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

    // 대시 방향을 캐릭터 로컬 기준으로 판단해서 방향별 애니메이션 재생
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

    public void PlayJump() => _anim.SetTrigger(JumpHash);

    public void ResetToIdle()
    {
        _anim.ResetTrigger(DieHash);
        _anim.Play("Blend Tree", 0);
    }

    public void PlayHit(bool isLeft) => _anim.SetTrigger(isLeft ? HitLHash : HitRHash);
    public void PlayDie() => _anim.SetTrigger(DieHash);
}