using UnityEngine;
using UnityEngine.AI;

// 보스 공용 모터.
// 보스는 BT(BehaviorGraph)/EnemyBrain 을 안 쓰기 때문에 EnemyBrain 이 해주던 글루
// (SO 동기화 / 플레이어 획득 / 이동 / 회전 / Animator Speed 기록)를 각 보스가 직접 복제해야 한다.
// 그 복제분이 보스마다 글자 하나 안 바뀌므로 여기로 모았다. 공격/페이즈 로직은 각 보스 컨트롤러에 남는다.
//
// MonoBehaviour 가 아니라 순수 클래스인 이유: 프리팹에 컴포넌트를 추가할 필요가 없어
// 기존 보스(와이번) 프리팹을 안 건드리고 코드만으로 태울 수 있다. 되돌리기도 쉽다.
//
// 사용법:
//   Awake:      _motor = new BossMotor(this, speedParam); _motor.ApplyData(data);
//   Start:      _motor.AcquirePlayer();
//   Update:     _motor.TickSpeedParam();
//   LateUpdate: _motor.TickFacing(_dead || _attacking, ResolveTarget(), data);
public class BossMotor
{
    public NavMeshAgent Agent { get; private set; }
    public EnemyHealth Health { get; private set; }
    public EnemyFeedback Feedback { get; private set; }
    public Animator Animator { get; private set; }
    public Transform Player { get; private set; }
    public PlayerStatComponent PlayerStat { get; private set; }

    private readonly Transform _tr;
    private readonly int _speedHash;

    public BossMotor(MonoBehaviour owner, string speedParam)
    {
        _tr = owner.transform;
        Agent = owner.GetComponent<NavMeshAgent>();
        Health = owner.GetComponent<EnemyHealth>();
        Feedback = owner.GetComponent<EnemyFeedback>();
        Animator = owner.GetComponent<Animator>();
        if (Animator == null) Animator = owner.GetComponentInChildren<Animator>();
        // UnityEngine. 명시 필수: 프로퍼티명 Animator 가 클래스명을 가려서 static 호출이 안 잡힌다.
        _speedHash = UnityEngine.Animator.StringToHash(speedParam);

        // 루트모션 끄기: 이동은 NavMeshAgent 가 한다.
        // 주의: applyRootMotion=false 는 위치/회전 커브만 막고 스케일 커브는 안 막는다.
        // 스케일이 튀는 클립(비행 등)을 쓰는 보스는 LateUpdate 에서 localScale 을 직접 고정해야 한다.
        if (Animator != null) Animator.applyRootMotion = false;
        if (Agent != null) Agent.updateRotation = false;   // 회전은 TickFacing 이 처리
    }

    // SO -> 컴포넌트 동기화 (EnemyBrain 역할 대체)
    public void ApplyData(MeleeEnemyData data)
    {
        if (data == null) return;
        if (Agent != null)
        {
            Agent.speed = data.moveSpeed;
            Agent.acceleration = data.acceleration;
            Agent.angularSpeed = data.angularSpeed;
            Agent.stoppingDistance = Mathf.Max(0f, data.attackRange * data.attackApproachRatio);
        }
        if (Health != null) { Health.maxHP = data.maxHP; Health.currentHP = data.maxHP; }
        if (Feedback != null) Feedback.SetData(data);
    }

    public void AcquirePlayer()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;
        Player = p.transform;
        PlayerStat = p.GetComponent<PlayerStatComponent>();
    }

    public bool AgentReady() => Agent != null && Agent.enabled && Agent.isOnNavMesh;

    public void Chase(Vector3 dest)
    {
        if (!AgentReady()) return;
        Agent.isStopped = false;
        Agent.SetDestination(dest);
    }

    public void StopMove()
    {
        if (!AgentReady()) return;
        Agent.isStopped = true;
        Agent.velocity = Vector3.zero;
    }

    public void PlayState(string stateName)
    {
        if (Animator != null && !string.IsNullOrEmpty(stateName))
            Animator.CrossFadeInFixedTime(stateName, 0.1f, 0);
    }

    // 수평 거리(Y 무시). 보스는 덩치가 커서 Y 차이로 사거리 판정이 틀어지면 안 된다.
    public float PlanarDistance(Vector3 worldPos)
    {
        Vector3 a = _tr.position; a.y = 0f;
        Vector3 b = worldPos; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // 정면 호(arc) 안에 플레이어가 있는지 (range 이내 + 정면 halfAngle 이내). 회피 판정의 핵심.
    public bool PlayerInArc(float range, float halfAngleDeg)
    {
        if (Player == null) return false;
        Vector3 to = Player.position - _tr.position; to.y = 0f;
        if (to.sqrMagnitude > range * range) return false;
        if (halfAngleDeg >= 180f) return true;
        Vector3 fwd = _tr.forward; fwd.y = 0f;
        return Vector3.Angle(fwd, to) <= halfAngleDeg;
    }

    public void FaceInstant(Vector3 pos)
    {
        Vector3 to = pos - _tr.position; to.y = 0f;
        if (to.sqrMagnitude > 0.0001f) _tr.rotation = Quaternion.LookRotation(to);
    }

    // Animator 의 이동속도 파라미터 기록. Update 에서 호출.
    // 에이전트를 끄는 연출(공중 다이브 등) 중엔 조용히 스킵된다.
    public void TickSpeedParam()
    {
        if (Animator != null && Agent != null && Agent.isActiveAndEnabled)
            Animator.SetFloat(_speedHash, Agent.velocity.magnitude);
    }

    // 평소엔 이동방향(없으면 타깃)을 향해 회전. LateUpdate 에서 호출.
    // suppressed=true 면 회전 정지 = 공격 중 방향 커밋(플레이어가 옆/뒤로 회피할 수 있게 하는 핵심).
    public void TickFacing(bool suppressed, Transform target, MeleeEnemyData data)
    {
        if (suppressed) return;

        Vector3 faceDir = Vector3.zero;
        if (Agent != null)
        {
            Vector3 vel = Agent.velocity; vel.y = 0f;
            if (vel.sqrMagnitude > 0.01f) faceDir = vel;
        }
        if (faceDir.sqrMagnitude < 0.0001f && target != null)
        {
            Vector3 to = target.position - _tr.position; to.y = 0f;
            faceDir = to;
        }
        if (faceDir.sqrMagnitude < 0.0001f) return;

        Quaternion rot = Quaternion.LookRotation(faceDir);
        float ang = data != null ? data.angularSpeed : 480f;
        _tr.rotation = Quaternion.RotateTowards(_tr.rotation, rot, ang * Time.deltaTime);
    }

    // 스폰 자리로 되돌리기(리쉬 리셋 공통분). 페이즈/쿨 초기화는 각 보스가 알아서.
    public void ResetToSpawn(Vector3 pos, Quaternion rot, MeleeEnemyData data)
    {
        if (Health != null) { Health.Invulnerable = false; Health.ResetToFull(); }
        if (Agent != null && data != null) Agent.speed = data.moveSpeed;

        // 네비메시 위면 Warp, 아니면 직접 이동
        if (Agent != null && Agent.enabled && Agent.isOnNavMesh) Agent.Warp(pos);
        else _tr.position = pos;
        _tr.rotation = rot;

        if (AgentReady()) { Agent.isStopped = true; Agent.ResetPath(); }
    }
}
