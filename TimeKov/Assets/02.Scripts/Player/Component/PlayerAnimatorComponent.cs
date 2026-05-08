using UnityEngine;

public class PlayerAnimatorComponent : MonoBehaviour
{
    private Player _player;
    private Animator _anim;

    private static readonly int NormalHash = Animator.StringToHash("----normal");
    private static readonly int IdleHash = Animator.StringToHash("IDLE");
    private static readonly int WalkHash = Animator.StringToHash("WALK");
    private static readonly int RunHash = Animator.StringToHash("RUN");
    private static readonly int SprintHash = Animator.StringToHash("SPRINT");
    private static readonly int Attack1Hash = Animator.StringToHash("ATTACK1");
    private static readonly int Attack2Hash = Animator.StringToHash("ATTACK2");
    private static readonly int Attack3Hash = Animator.StringToHash("ATTACK3");
    private static readonly int Skill1Hash = Animator.StringToHash("SP SKILL 1");   
    private static readonly int Skill2Hash = Animator.StringToHash("SP SKILL 2");
    private static readonly int Skill3Hash = Animator.StringToHash("SP SKILL 3");
    private static readonly int EvadeHash = Animator.StringToHash("EVADE");
    private static readonly int HitLHash = Animator.StringToHash("HIT L");
    private static readonly int HitRHash = Animator.StringToHash("HIT R");
    private static readonly int DieHash = Animator.StringToHash("DIE");

    private float _prevSpeed;
    private AnimState _prevState;

    private enum AnimState { Idle, Walk, Run, Sprint }

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
        float speed = _player.Movement.CurrentSpeed;

        // float 블렌딩 -> 0.15f 로 부드럽게
        _anim.SetFloat(NormalHash, speed, 0.15f, Time.deltaTime);

        // 상태 변화 있을 때만 트리거
        AnimState current = GetAnimState(speed);

        if (current != _prevState)
        {
            switch (current)
            {
                case AnimState.Idle: _anim.SetTrigger(IdleHash); break;
                case AnimState.Walk: _anim.SetTrigger(WalkHash); break;
                case AnimState.Run: _anim.SetTrigger(RunHash); break;
                case AnimState.Sprint: _anim.SetTrigger(SprintHash); break;
            }

            _prevState = current;
        }

        _prevSpeed = speed;
    }

    AnimState GetAnimState(float speed)
    {
        if (speed <= 0.1f) return AnimState.Idle;
        if (speed < 5f) return AnimState.Walk;
        if (speed < 9f) return AnimState.Run;
        return AnimState.Sprint;
    }

    public void PlayAttack(int comboIndex)
    {
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

    public void ResetToIdle()
    {
        // 모든 트리거 초기화 후 Idle로 복귀
        _anim.ResetTrigger(DieHash);
        _anim.Play("IDLE");   // Animator State 이름이 "Idle"이어야 해요
    }

    public void PlayEvade() => _anim.SetTrigger(EvadeHash);
    public void PlayHit(bool isLeft) => _anim.SetTrigger(isLeft ? HitLHash : HitRHash);
    public void PlayDie() => _anim.SetTrigger(DieHash);
}