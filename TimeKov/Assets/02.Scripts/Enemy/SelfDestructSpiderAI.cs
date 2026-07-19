using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 자폭거미(SpiderBot) 전용 AI. 난파 우주선 오작동 드론 컨셉의 순수 자폭 러셔.
// 기존 EnemyBrain/BehaviorGraph 미사용(보스처럼 전용 컨트롤러). IEnemyDataSource 로 EnemyHealth 에 ID/이름 제공.
//
// [정체성] 근접 물기 없음. 오직 자폭 단일 기믹 = "한 자리 버티기(터틀링) 처벌".
//   추격 -> armRange 진입 시 arming(발광 램프업 + 스케일 펄스 + 지면 링 = 회피할 시간) -> 폭발(범위 데미지).
//   ★arming 중 플레이어가 먼저 죽이면 폭발 안 함 = 리스크/리워드 카운터플레이.
// [사망] 폭발 순간 self-kill = health.TakeDamage(maxHP) -> EnemyHealth.Die 가 드롭/도감/시간흡수 정상 집계.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class SelfDestructSpiderAI : MonoBehaviour, IEnemyDataSource
{
    [Header("데이터 (HP/속도/시야는 SO에서)")]
    [SerializeField] private MeleeEnemyData data;
    public MeleeEnemyData Data => data;

    [Header("자폭")]
    [Tooltip("이 거리 안에 들면 점화(arming) 시작.")]
    [SerializeField] private float armRange = 2.5f;
    [Tooltip("점화~폭발까지 시간(초). 이 동안 플레이어는 반경 밖으로 피하거나 거미를 죽일 수 있다.")]
    [SerializeField] private float armTime = 1.1f;
    [SerializeField] private float explodeRadius = 3f;
    [Tooltip("폭발 데미지 = attackDamage x 이 값.")]
    [SerializeField] private float explodeDmgMul = 1.6f;
    [SerializeField] private GameObject explodeVfx;

    [Header("전조 (점화 연출)")]
    [Tooltip("지면 폭발범위 링(회피용 예고). 비우면 안 나옴.")]
    [SerializeField] private GameObject telegraphVfx;
    [SerializeField] private float telegraphScaleMul = 2.5f;
    [Tooltip("점화 중 발광색(HDR). 폭발 직전 경고등.")]
    [ColorUsage(true, true)] [SerializeField] private Color armEmission = new Color(6f, 0.5f, 0.1f, 1f);
    [Tooltip("점화 중 몸 크기 펄스 진폭(0=없음).")]
    [SerializeField] private float armScalePulse = 0.14f;
    [Tooltip("점화 펄스 최대 주파수(폭발 임박할수록 빨라짐).")]
    [SerializeField] private float armPulseHz = 7f;
    [SerializeField] private AudioClip armSound;

    [Header("애니메이터")]
    [SerializeField] private string speedParam = "Speed";

    private NavMeshAgent _agent;
    private EnemyHealth _health;
    private EnemyFeedback _feedback;
    private Animator _animator;
    private AudioSource _audio;
    private Transform _player;
    private PlayerStatComponent _playerStat;

    private int _speedHash;
    private bool _dead;
    private bool _arming;
    private Vector3 _baseScale;
    private readonly List<Material> _emissionMats = new List<Material>();
    private Color _baseEmission;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _health = GetComponent<EnemyHealth>();
        _feedback = GetComponent<EnemyFeedback>();
        _animator = GetComponentInChildren<Animator>();
        _audio = GetComponent<AudioSource>();
        _speedHash = Animator.StringToHash(speedParam);
        _baseScale = transform.localScale;

        if (_animator != null) _animator.applyRootMotion = false;

        // 발광 재질 인스턴스 캐시(폭발 직전 경고등용). Renderer.materials = 인스턴스라 이 거미만 영향.
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.materials)
                if (m != null && m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    _emissionMats.Add(m);
                }
        if (_emissionMats.Count > 0) _baseEmission = _emissionMats[0].GetColor("_EmissionColor");

        ApplyData();
    }

    private void ApplyData()
    {
        if (data == null) return;
        if (_agent != null)
        {
            _agent.speed = data.moveSpeed;
            _agent.acceleration = data.acceleration;
            _agent.angularSpeed = data.angularSpeed;
            _agent.stoppingDistance = 0f;
            _agent.updateRotation = true;   // 단순 추격이라 에이전트가 진행방향으로 자동 회전
        }
        if (_health != null) { _health.maxHP = data.maxHP; _health.currentHP = data.maxHP; }
        if (_feedback != null) _feedback.SetData(data);
    }

    private void Start()
    {
        AcquirePlayer();
        if (_health != null) _health.OnDeath += HandleDeath;
        _feedback?.PlaySpawn();
    }

    private void OnDestroy()
    {
        if (_health != null) _health.OnDeath -= HandleDeath;
    }

    private void AcquirePlayer()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;
        _player = p.transform;
        _playerStat = p.GetComponent<PlayerStatComponent>();
    }

    // 플레이어가 arming 중 거미를 죽이면 폭발 안 함(카운터플레이). 여기서 코루틴 정지.
    private void HandleDeath()
    {
        _dead = true;
        StopAllCoroutines();
        _arming = false;
        transform.localScale = _baseScale;
        SetEmission(_baseEmission);   // arming 중 처치되면 발광이 켜진 채 남으므로 원복
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh) { _agent.isStopped = true; _agent.ResetPath(); }
    }

    private void Update()
    {
        if (_dead || _arming) return;
        if (_player == null) { AcquirePlayer(); if (_player == null) return; }
        if (data == null) return;

        bool valid = _playerStat == null || (!_playerStat.IsDead && !_playerStat.IsInBase);
        float dist = PlanarDistance(_player.position);

        if (!valid || dist > data.visionRange)
        {
            StopMove();
            TickSpeed();
            return;
        }

        if (dist <= armRange)
        {
            StartCoroutine(ArmAndExplode());
            return;
        }

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = false;
            _agent.SetDestination(_player.position);
        }
        TickSpeed();
    }

    // 점화 -> 폭발. arming 중엔 제자리 정지(전조 보여줌).
    private IEnumerator ArmAndExplode()
    {
        _arming = true;
        StopMove();
        if (_player != null) FaceInstant(_player.position);
        _feedback?.PlayAttack();
        if (_audio != null && armSound != null) _audio.PlayOneShot(armSound);

        // 지면 폭발범위 예고 링(회피용)
        if (telegraphVfx != null)
        {
            var ring = Instantiate(telegraphVfx, GroundSpot(transform.position) + Vector3.up * 0.05f, Quaternion.identity);
            ring.transform.localScale = Vector3.one * explodeRadius * telegraphScaleMul;
            Destroy(ring, armTime + 0.2f);
        }

        float t = 0f;
        while (t < armTime && !_dead)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / armTime);
            // 폭발 임박할수록 점멸 빨라짐 + 크기 펄스 커짐
            float hz = Mathf.Lerp(armPulseHz * 0.4f, armPulseHz, k);
            float pulse = Mathf.Abs(Mathf.Sin(t * Mathf.PI * hz));
            SetEmission(armEmission * Mathf.Lerp(0.3f, 1f, k) * pulse);
            transform.localScale = _baseScale * (1f + armScalePulse * pulse * k);
            yield return null;
        }
        transform.localScale = _baseScale;
        if (_dead) yield break;   // arming 중 처치됨 = 폭발 안 함(카운터플레이)

        // 폭발
        Vector3 center = transform.position;
        if (explodeVfx != null) { var v = Instantiate(explodeVfx, center, Quaternion.identity); Destroy(v, 4f); }
        if (_playerStat != null && !_playerStat.IsDead && _player != null)
        {
            Vector3 a = center; a.y = 0f;
            Vector3 b = _player.position; b.y = 0f;
            if (Vector3.Distance(a, b) <= explodeRadius)
                _playerStat.TakeDamage(data.attackDamage * explodeDmgMul, center);
        }

        // 몸을 폭발에 숨기고(렌더 끔) EnemyHealth 경유 사망 -> 드롭/도감/시간흡수 정상 집계
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        if (_health != null) _health.TakeDamage(_health.maxHP);
    }

    private void TickSpeed()
    {
        if (_animator != null && _agent != null && _agent.isActiveAndEnabled)
            _animator.SetFloat(_speedHash, _agent.velocity.magnitude);
    }

    private void StopMove()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
        _agent.ResetPath();
    }

    private void FaceInstant(Vector3 pos)
    {
        Vector3 to = pos - transform.position; to.y = 0f;
        if (to.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(to);
    }

    private float PlanarDistance(Vector3 worldPos)
    {
        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = worldPos; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private Vector3 GroundSpot(Vector3 pos)
    {
        if (NavMesh.SamplePosition(pos, out var nav, 3f, NavMesh.AllAreas)) return nav.position;
        return pos;
    }

    private void SetEmission(Color c)
    {
        for (int i = 0; i < _emissionMats.Count; i++)
            _emissionMats[i].SetColor("_EmissionColor", c);
    }
}
