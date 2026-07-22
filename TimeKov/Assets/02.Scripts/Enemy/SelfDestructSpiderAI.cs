using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 자폭거미(SpiderBot) 전용 AI. 난파 우주선 오작동 드론 컨셉의 순수 자폭 러셔.
// 기존 EnemyBrain/BehaviorGraph 미사용(보스처럼 전용 컨트롤러). IEnemyDataSource 로 EnemyHealth 에 ID/이름 제공.
//
// [정체성] 근접 물기 없음. 오직 자폭 단일 기믹 = "한 자리 버티기(터틀링) 처벌".
//   추격 -> armRange 진입 시 점화(몸이 삑삑 빨갛게 점멸) -> ★점화 중에도 계속 추격 -> 시간 다 되면 그 자리서 폭발.
//   폭발 범위 표시(지면 링) 없음. 경고는 오직 몸통 점멸/비프음. 회피 = 대쉬로 거리 벌리기.
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
    [Tooltip("이 거리 안에 들면 점화(arming) 시작. 점화 후에도 계속 쫓아오므로 회피할 여유를 두고 잡는다.")]
    [SerializeField] private float armRange = 3.5f;
    [Tooltip("점화~폭발까지 시간(초). 이 동안 몸이 서서히 빨개지고 비프가 빨라진다. 도망치거나 죽일 시간.")]
    [SerializeField] private float armTime = 2.2f;
    [SerializeField] private float explodeRadius = 3f;
    [Tooltip("폭발 데미지 = attackDamage x 이 값.")]
    [SerializeField] private float explodeDmgMul = 1.6f;
    [SerializeField] private GameObject explodeVfx;

    [Header("전조 (몸통 경고등)")]
    [Tooltip("점화 중 몸 전체가 물드는 색(알베도).")]
    [SerializeField] private Color armBodyColor = new Color(1f, 0.1f, 0.05f, 1f);
    [Tooltip("작열색(색조만. 세기는 아래 armGlowPeak). 모델이 이미 빨개서 '색'보다 '발광 세기'가 알아채는 신호다.")]
    [SerializeField] private Color armGlowColor = new Color(1f, 0.06f, 0.02f, 1f);
    [Tooltip("폭발 직전 발광 세기(HDR). 몸 전체가 벌겋게 작열한다. 약하면 올리되 너무 올리면 하얗게 타버린다.")]
    [SerializeField] private float armGlowPeak = 8f;
    [Tooltip("점화 시작 시점의 발광 세기.")]
    [SerializeField] private float armGlowStart = 1.5f;
    [Tooltip("점화 중 몸 크기 펄스 진폭(0=없음).")]
    [SerializeField] private float armScalePulse = 0.14f;
    [Tooltip("점화 점멸 최대 주파수(폭발 임박할수록 빨라짐). 너무 올리면 눈 아프다.")]
    [SerializeField] private float armPulseHz = 5f;
    [Tooltip("삑- 비프음. 점멸에 맞춰 반복 재생되고 임박할수록 빨라진다.")]
    [SerializeField] private AudioClip armSound;
    [Tooltip("비프 간격(초): 점화 시작 -> 폭발 직전. 느긋하게 시작해서 삑삑삑삑 으로 몰아친다.")]
    [SerializeField] private float beepIntervalStart = 0.42f;
    [Tooltip("비프 클립이 0.3초쯤이라 이걸 더 줄이면 소리가 겹쳐서 삑삑이 아니라 웅- 하는 잡음이 된다.")]
    [SerializeField] private float beepIntervalEnd = 0.13f;

    [Header("평상시 어슬렁 (플레이어 발견 전)")]
    // ★기존 일반몹 19종은 BT(EnemyBrain)라 EnemySpawnPoint 가 순찰 웨이포인트를 주입해준다.
    //   이 녀석은 보스처럼 전용 컨트롤러라 그 경로를 안 타서, 안 넣으면 발견 전까지 굳어 있다.
    //   웨이포인트 대신 스폰 지점 기준으로 스스로 돈다(씬 세팅 불필요).
    [Tooltip("가만히 서 있지 않고 스폰 지점 주변을 순찰한다. 난파 드론이 구역을 훑는 그림.")]
    [SerializeField] private bool wander = true;
    [Tooltip("순찰 반경(m). 스폰 지점 기준.")]
    [SerializeField] private float wanderRadius = 7f;
    [Tooltip("목적지 사이 대기 시간(초) 최소/최대.")]
    [SerializeField] private Vector2 wanderPauseRange = new Vector2(1f, 3f);
    [Tooltip("순찰 속도 = moveSpeed x 이 값.")]
    [Range(0.1f, 1f)][SerializeField] private float wanderSpeedMul = 0.45f;

    [Header("애니메이터")]
    [SerializeField] private string speedParam = "Speed";

    private Coroutine _wanderCo;
    private Vector3 _homePos;
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

    // 점화 시 되돌릴 원래 색을 재질별로 들고 있는다. 알베도(_BaseColor 또는 _Color) + 발광 둘 다.
    private struct BodyMat
    {
        public Material mat;
        public int albedoId;      // -1 = 알베도 프로퍼티 없음
        public Color albedo;
        public bool hasEmission;
        public Color emission;
        public bool hasEmissionMap;
        public Texture emissionMap;   // 점화 중 잠시 떼었다가 원복
    }
    private readonly List<BodyMat> _bodyMats = new List<BodyMat>();

    private static readonly int BaseColorId   = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId       = Shader.PropertyToID("_Color");
    private static readonly int EmissionId    = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

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

        // 재질 인스턴스 캐시(점화 경고등용). Renderer.materials = 인스턴스라 이 거미만 영향.
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.materials)
            {
                if (m == null) continue;
                var bm = new BodyMat { mat = m, albedoId = -1 };

                if (m.HasProperty(BaseColorId)) { bm.albedoId = BaseColorId; }
                else if (m.HasProperty(ColorId)) { bm.albedoId = ColorId; }
                if (bm.albedoId != -1) bm.albedo = m.GetColor(bm.albedoId);

                if (m.HasProperty(EmissionId))
                {
                    m.EnableKeyword("_EMISSION");
                    // 발광이 검게 저작된 재질은 EmissiveIsBlack 플래그 때문에 런타임 변경이 씹힌다
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    bm.hasEmission = true;
                    bm.emission = m.GetColor(EmissionId);

                    // 발광맵은 코어/램프 같은 일부만 덮는다. 점화 땐 이걸 떼야 온몸이 발광체가 된다.
                    if (m.HasProperty(EmissionMapId))
                    {
                        bm.hasEmissionMap = true;
                        bm.emissionMap = m.GetTexture(EmissionMapId);
                    }
                }

                if (bm.albedoId != -1 || bm.hasEmission) _bodyMats.Add(bm);
            }

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
        // ★순찰 중심. 스포너가 Warp 로 자리를 잡은 뒤여야 하므로 Awake 가 아니라 Start 에서 잡는다.
        _homePos = transform.position;

        AcquirePlayer();
        if (_health != null) _health.OnDeath += HandleDeath;
        _feedback?.PlaySpawn();
    }

    // 나중에 적 풀링이 들어오면 arming 중 비활성화가 생긴다. 그때 발광맵이 떼인 채로 재사용되지 않게 막아둔다.
    private void OnDisable()
    {
        _wanderCo = null;   // 코루틴은 비활성화 시 죽는다. 핸들이 남으면 재사용 때 순찰이 안 돈다
        if (!_arming) return;
        _arming = false;
        transform.localScale = _baseScale;
        ResetTint();
    }

    private void OnDestroy()
    {
        if (_health != null) _health.OnDeath -= HandleDeath;

        // Renderer.materials 는 인스턴스를 뜬다. 리스폰이 잦아서 안 지우면 계속 쌓인다.
        for (int i = 0; i < _bodyMats.Count; i++)
            if (_bodyMats[i].mat != null) Destroy(_bodyMats[i].mat);
        _bodyMats.Clear();
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
        _wanderCo = null;   // 핸들이 남으면 죽은 뒤에도 순찰이 도는 걸로 오해한다
        _arming = false;
        transform.localScale = _baseScale;
        ResetTint();   // arming 중 처치되면 빨간 채로 남으므로 원복
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
            // ★가만히 서 있으면 죽어 있는 것처럼 보인다. 구역을 훑고 다닌다.
            if (wander) StartWander();
            else StopMove();
            TickSpeed();
            return;
        }

        // 플레이어를 봤다. 순찰 중단하고 전투 속도로 되돌린다.
        StopWander();

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

    // ── 평상시 순찰 ──
    // 기존 일반몹은 EnemyBrain(BT) 이라 EnemySpawnPoint 가 웨이포인트를 주입해준다.
    // 이 컨트롤러는 보스와 같은 경로라 그걸 못 받으므로 스폰 지점 기준으로 스스로 돈다.
    private void StartWander()
    {
        if (_wanderCo == null) _wanderCo = StartCoroutine(WanderRoutine());
    }

    // 매 프레임 호출되므로 돌고 있을 때만 일한다.
    private void StopWander()
    {
        if (_wanderCo == null) return;
        StopCoroutine(_wanderCo);
        _wanderCo = null;
        StopMove();
        RestoreMoveSpeed();
    }

    private void RestoreMoveSpeed()
    {
        if (_agent != null && data != null) _agent.speed = data.moveSpeed;
    }

    private bool NavOk() => _agent != null && _agent.enabled && _agent.isOnNavMesh;

    private IEnumerator WanderRoutine()
    {
        while (!_dead && !_arming)
        {
            // ★한 번 실패하면 그대로 쉬어버리면 "가끔 한 번씩만 움직이는" 그림이 된다. 여러 번 다시 뽑는다.
            bool moving = false;
            for (int tries = 0; tries < 6 && !moving; tries++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                Vector3 want = _homePos + new Vector3(dir.x, 0f, dir.y)
                                        * Random.Range(wanderRadius * 0.35f, wanderRadius);

                if (!NavMesh.SamplePosition(want, out var hit, 3f, NavMesh.AllAreas)) continue;
                if (!NavOk()) break;

                _agent.speed = data.moveSpeed * wanderSpeedMul;
                _agent.isStopped = false;
                _agent.SetDestination(hit.position);
                moving = true;

                // ★경로가 안 이어지는 지점이면 즉시 다른 곳을 고른다.
                //   예전엔 타임아웃까지 그대로 기다려서 한참 서 있는 것처럼 보였다.
                while (_agent.pathPending) yield return null;
                if (_agent.pathStatus != NavMeshPathStatus.PathComplete)
                {
                    _agent.ResetPath();
                    moving = false;
                    continue;
                }

                float giveUp = Time.time + 6f;
                while (!_dead && !_arming && Time.time < giveUp)
                {
                    if (_agent.remainingDistance <= 0.5f) break;
                    TickSpeed();
                    yield return null;
                }
            }
            if (_dead || _arming) break;

            StopMove();
            RestoreMoveSpeed();

            // ★0 으로 두면 목적지 뽑기 실패와 겹칠 때 한 프레임 안에서 무한 루프 = 게임 정지.
            float pause = Mathf.Max(0.1f, Random.Range(wanderPauseRange.x,
                                    Mathf.Max(wanderPauseRange.x, wanderPauseRange.y)));
            float end = Time.time + pause;
            while (!_dead && !_arming && Time.time < end)
            {
                TickSpeed();
                yield return null;
            }

            yield return null;   // 안전장치
        }

        RestoreMoveSpeed();
        _wanderCo = null;
    }

    // 점화 -> 폭발. ★멈추지 않는다. 삑삑거리며 계속 쫓아오다가 시간이 다 되면 그 자리서 터진다.
    private IEnumerator ArmAndExplode()
    {
        _arming = true;
        _feedback?.PlayAttack();
        BeginArmGlow();

        float dur = Mathf.Max(0.05f, armTime);   // 인스펙터에서 0 이 들어와도 전조가 사라지지 않게
        float t = 0f;
        float nextBeep = 0f;
        while (t < dur && !_dead)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);

            // 점화 중에도 추격 유지(제자리 폭발 금지). 단 결계 안으로는 따라 들어가지 않는다.
            bool chase = _player != null &&
                         (_playerStat == null || (!_playerStat.IsDead && !_playerStat.IsInBase));
            bool navOk = _agent != null && _agent.enabled && _agent.isOnNavMesh;
            if (chase && navOk)
            {
                _agent.isStopped = false;
                _agent.SetDestination(_player.position);
            }
            else if (navOk && !_agent.isStopped) StopMove();
            TickSpeed();

            // 폭발 임박할수록 점멸 빨라짐 + 작열 세기/크기 펄스 커짐.
            // 바닥(Lerp 하한)을 두는 게 핵심: 점멸 골에서도 평소로 안 돌아가고 벌겋게 달아오른 채 깜빡인다.
            float hz = Mathf.Lerp(armPulseHz * 0.4f, armPulseHz, k);
            float pulse = Mathf.Abs(Mathf.Sin(t * Mathf.PI * hz));
            float tint = Mathf.Lerp(0.45f, 1f, k) * Mathf.Lerp(0.6f, 1f, pulse);
            float glow = Mathf.Lerp(armGlowStart, armGlowPeak, k * k) * Mathf.Lerp(0.4f, 1f, pulse);
            SetArmTint(tint, glow);
            transform.localScale = _baseScale * (1f + armScalePulse * pulse * k);

            // 삑- 비프도 같이 빨라짐
            if (_audio != null && armSound != null && t >= nextBeep)
            {
                _audio.PlayOneShot(armSound);
                nextBeep = t + Mathf.Max(0.03f, Mathf.Lerp(beepIntervalStart, beepIntervalEnd, k));
            }
            yield return null;
        }
        transform.localScale = _baseScale;
        if (_dead) yield break;   // arming 중 처치됨 = 폭발 안 함(카운터플레이)

        StopMove();

        // 폭발
        Vector3 center = transform.position;
        if (explodeVfx != null) { var v = Instantiate(explodeVfx, center, Quaternion.identity); Destroy(v, 4f); }
        // 점화 중엔 추격만 하고 검사를 안 하므로 결계 진입/사망을 폭발 시점에 한 번 더 본다
        if (_playerStat != null && !_playerStat.IsDead && !_playerStat.IsInBase && _player != null)
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

    private float PlanarDistance(Vector3 worldPos)
    {
        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = worldPos; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // 점화 시작 시 1회. 발광맵을 떼어 몸 전체가 발광하도록 만든다(맵이 null 이면 셰이더가 흰색으로 취급).
    private void BeginArmGlow()
    {
        for (int i = 0; i < _bodyMats.Count; i++)
        {
            var bm = _bodyMats[i];
            if (bm.hasEmissionMap) bm.mat.SetTexture(EmissionMapId, null);
        }
    }

    // tint 0 = 평소, 1 = 완전히 달아오른 상태. glow = 발광 세기(HDR).
    private void SetArmTint(float tint, float glow)
    {
        for (int i = 0; i < _bodyMats.Count; i++)
        {
            var bm = _bodyMats[i];
            if (bm.albedoId != -1)
                bm.mat.SetColor(bm.albedoId, Color.Lerp(bm.albedo, armBodyColor, tint));
            if (bm.hasEmission)
                bm.mat.SetColor(EmissionId, armGlowColor * glow);
        }
    }

    private void ResetTint()
    {
        for (int i = 0; i < _bodyMats.Count; i++)
        {
            var bm = _bodyMats[i];
            if (bm.albedoId != -1) bm.mat.SetColor(bm.albedoId, bm.albedo);
            if (bm.hasEmission) bm.mat.SetColor(EmissionId, bm.emission);
            if (bm.hasEmissionMap) bm.mat.SetTexture(EmissionMapId, bm.emissionMap);
        }
    }
}
