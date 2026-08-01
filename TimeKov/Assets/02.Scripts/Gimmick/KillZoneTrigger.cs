using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// ── 레이저 결계: 전 영역 "동기화 웨이브" → 마지막 웨이브 전멸 시 결계 해제 ──────────────
// GimmickTrigger 구현체 + 자체 경량 웨이브 스포너.
//   • 플레이어가 activateDistance 안에 들어오면 웨이브0 스폰(접근 시 시작).
//   • 한 웨이브 = 여러 영역(그룹)에 몹을 동시에 뿌린다. 영역마다 다른 몹/마리수 지정 가능.
//       예) 웨이브0 = [북쪽:거미3, 남쪽:늑대2]  →  웨이브1 = [북쪽:록몬2, 남쪽:곰1]  →  웨이브2 = [중앙:거미여왕1]
//   • ★웨이브는 전 영역 동기 진행: '그 웨이브의 모든 영역 몹'이 다 죽어야 다음 웨이브로 같이 넘어간다.
//   • 마지막 웨이브까지 전멸시키면 결계 해제(latch). 웨이브 0은 접근 즉시(텀 무시).
//   • 리스폰 없음. 진행 중 멀어지면 정리하고 웨이브0부터 리셋.
//   각 가드의 EnemyHealth.OnDeath 를 구독 → 현재 웨이브 남은 수 0 → 다음 웨이브 or 해제.
[DisallowMultipleComponent]
public class KillZoneTrigger : GimmickTrigger
{
    // 종류별 마리수. prefab 다르게 여러 항목 = 여러 종류 혼합.
    [System.Serializable]
    public class GuardSpawn
    {
        [Tooltip("스폰할 몹 프리팹(EnemyHealth 필요).")]
        public GameObject prefab;
        [Tooltip("이 종류를 몇 마리 스폰할지.")]
        [Min(1)] public int count = 1;
    }

    // 한 웨이브 안에서 "이 영역에 이 몹들"을 정의하는 그룹. 영역별로 다른 몹을 주려고 웨이브를 그룹으로 나눈다.
    [System.Serializable]
    public class AreaGroup
    {
        [Tooltip("에디터 구분용 이름(선택). 예: '북쪽', '남쪽 다리목'")]
        public string label;
        [Tooltip("이 그룹이 스폰될 영역(BoxCollider). ★펜스 '바깥'에 두고 크기를 맞춰라(씬에서 크기가 보임).")]
        public BoxCollider spawnArea;
        [Tooltip("이 영역에 이번 웨이브에 나올 몹들(종류별 마리수).")]
        public List<GuardSpawn> guards = new();
    }

    // 한 웨이브 = 여러 영역 그룹을 동시에 스폰 + 이전 웨이브 전멸 후 이 웨이브까지 텀.
    [System.Serializable]
    public class Wave
    {
        [Tooltip("에디터 구분용 이름(선택). 예: '1페이즈', '보스'")]
        public string label;
        [Tooltip("이번 웨이브에 동시에 뿌릴 영역별 그룹들. 영역마다 다른 몹을 넣는다.")]
        public List<AreaGroup> groups = new();
        [Tooltip("이전 웨이브 전멸 후 이 웨이브 스폰까지 텀(초). ★첫 웨이브(0번)는 접근 즉시라 무시된다.")]
        [Min(0f)] public float spawnDelay = 2f;
    }

    [Header("웨이브 (전 영역 동기 진행)")]
    [Tooltip("웨이브 하나를 전 영역 통째로 전멸시키면 다음 웨이브로 같이 넘어간다. 마지막까지 잡으면 결계 해제.\n" +
             "웨이브 1개 + 그룹 1개 = 가장 단순한 '한 번에 다 잡기'.")]
    [SerializeField] private List<Wave> waves = new();

    [Header("스폰 배치 공통")]
    [Tooltip("박스 안 임의 점에서 NavMesh 위로 붙일 때 허용 반경(m).")]
    [SerializeField] private float navSampleRadius = 3f;
    [Tooltip("지면 인식 Layer. 박스가 지면보다 떠 있어도 아래로 레이 쏴서 지면에 맞춘다.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("접근 시 스폰")]
    [Tooltip("플레이어가 이 거리(m) 안에 들어오면 웨이브 시작. 밖으로 멀어지면(약간 더 바깥) 정리하고 처음부터 리셋.")]
    [SerializeField] private float activateDistance = 30f;
    [Tooltip("거리 체크 주기(초). 매 프레임 안 재도 됨(가벼운 폴링).")]
    [SerializeField] private float checkInterval = 0.25f;

    // 인스펙터/디버그용
    public int EditorWave => _waveIndex;
    public int EditorRemaining => _remaining;

    private readonly List<EnemyHealth> _alive = new();
    private int _remaining;
    private int _waveIndex = -1;   // 현재 진행 중 웨이브(-1=미시작)
    private bool _started;
    private bool _cleared;
    private Transform _player;
    private float _nextCheck;
    private Coroutine _waveCo;
    private bool _musicEngaged;    // 웨이브 진행 내내 전투 BGM 유지(텀에도 안 끊기게)

    private void Start()
    {
        var p = FindFirstObjectByType<Player>();
        _player = p != null ? p.transform : null;

        if (waves == null || waves.Count == 0)
            Debug.LogWarning($"[KillZoneTrigger] {name}: waves 미설정 — 스폰할 웨이브가 없다.", this);
    }

    private void Update()
    {
        if (_cleared) return;
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + checkInterval;

        if (_player == null)
        {
            var p = FindFirstObjectByType<Player>();
            _player = p != null ? p.transform : null;
            if (_player == null) return;
        }

        float sqr = (_player.position - transform.position).sqrMagnitude;
        float on = activateDistance;
        float off = activateDistance * 1.2f;        // 히스테리시스(경계 떨림 방지)

        if (!_started)
        {
            if (sqr <= on * on) StartWaves();
        }
        else if (sqr > off * off)
        {
            DespawnReset();                         // 진행 중 이탈 → 정리 후 처음부터 리셋
        }
    }

    private void StartWaves()
    {
        _started = true;
        EngageMusic();                              // 웨이브 시퀀스 동안 전투 BGM 유지(텀에도 안 끊김)
        SpawnWave(0);                               // 첫 웨이브는 접근 즉시(텀 무시)
    }

    // 트리거가 자기 id로 교전 1개를 잡아둔다 → 웨이브 사이 텀에 몹이 0이어도 카운트가 0으로 안 떨어져 BGM 유지.
    private void EngageMusic()
    {
        if (_musicEngaged) return;
        _musicEngaged = true;
        BattleMusicTracker.Engage(GetInstanceID());
    }

    private void DisengageMusic()
    {
        if (!_musicEngaged) return;
        _musicEngaged = false;
        BattleMusicTracker.Disengage(GetInstanceID());
    }

    private void SpawnWave(int index)
    {
        _waveIndex = index;
        _alive.Clear();
        _remaining = 0;

        if (waves == null || index < 0 || index >= waves.Count) { AdvanceOrClear(); return; }

        var wave = waves[index];
        if (wave != null && wave.groups != null)
        {
            foreach (var group in wave.groups)
            {
                if (group == null || group.spawnArea == null || group.guards == null) continue;
                foreach (var g in group.guards)
                {
                    if (g == null || g.prefab == null) continue;
                    int n = Mathf.Max(1, g.count);
                    for (int k = 0; k < n; k++)
                    {
                        if (!TryGetSpawnPos(group.spawnArea, out Vector3 pos)) continue;
                        var rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                        var go = Instantiate(g.prefab, pos, rot);
                        var hp = go.GetComponent<EnemyHealth>();
                        if (hp == null) hp = go.GetComponentInChildren<EnemyHealth>();
                        if (hp != null)
                        {
                            _alive.Add(hp);
                            _remaining++;
                            var captured = hp;                    // per-iteration 캡처(안전)
                            hp.OnDeath += () => OnGuardDied(captured);
                        }
                    }
                }
            }
        }

        // 이 웨이브가 비었으면(설정 누락) 바로 다음으로.
        if (_remaining == 0) AdvanceOrClear();
    }

    private void OnGuardDied(EnemyHealth hp)
    {
        _alive.Remove(hp);
        _remaining = Mathf.Max(0, _remaining - 1);
        if (_remaining == 0) AdvanceOrClear();      // 현재 웨이브 전 영역 전멸
    }

    // 다음 웨이브가 있으면 텀 후 스폰, 없으면 결계 해제.
    private void AdvanceOrClear()
    {
        int next = _waveIndex + 1;
        if (waves != null && next < waves.Count)
        {
            float delay = Mathf.Max(0f, waves[next].spawnDelay);
            StopWaveCo();
            _waveCo = StartCoroutine(SpawnAfterDelay(next, delay));
        }
        else
        {
            _cleared = true;
            DisengageMusic();                       // 전투 종료 → BGM 놓아줌(마지막 몹 사망과 함께 필드 BGM 복귀)
            SetSatisfied(true);                     // 마지막 웨이브까지 전멸 → 결계 해제(latch 라 영구)
        }
    }

    private IEnumerator SpawnAfterDelay(int index, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        _waveCo = null;
        SpawnWave(index);
    }

    private void StopWaveCo()
    {
        if (_waveCo != null) { StopCoroutine(_waveCo); _waveCo = null; }
    }

    // 특정 BoxCollider 영역 안 NavMesh 위 랜덤 지점 획득.
    private bool TryGetSpawnPos(BoxCollider box, out Vector3 result)
    {
        result = transform.position;
        if (box == null) return false;

        for (int attempt = 0; attempt < 15; attempt++)   // 박스/NavMesh 사정으로 실패할 수 있어 여러 번
        {
            Vector3 local = new Vector3(
                Random.Range(-box.size.x * 0.5f, box.size.x * 0.5f),
                box.size.y * 0.5f,
                Random.Range(-box.size.z * 0.5f, box.size.z * 0.5f));
            Vector3 world = box.transform.TransformPoint(box.center + local);

            // 위에서 아래로 레이 → 지면 높이 보정(박스가 지면보다 떠 있어도 안전)
            Vector3 candidate = world;
            if (Physics.Raycast(world + Vector3.up * 5f, Vector3.down, out RaycastHit hit,
                                50f + box.size.y * 2f, groundMask, QueryTriggerInteraction.Ignore))
                candidate = hit.point;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit nav, navSampleRadius, NavMesh.AllAreas))
            {
                result = nav.position;
                return true;
            }
        }
        return false;
    }

    // 미처치 가드 정리 + 처음(웨이브0)부터 리셋. Destroy 는 OnDeath 를 안 거치므로 카운트 오차 없음.
    private void DespawnReset()
    {
        StopWaveCo();
        DisengageMusic();                           // 이탈로 전투 중단 → BGM 놓아줌
        for (int i = _alive.Count - 1; i >= 0; i--)
            if (_alive[i] != null) Destroy(_alive[i].gameObject);
        _alive.Clear();
        _remaining = 0;
        _waveIndex = -1;
        _started = false;
    }

    private void OnDestroy()
    {
        StopWaveCo();
        DisengageMusic();
        for (int i = _alive.Count - 1; i >= 0; i--)
            if (_alive[i] != null) Destroy(_alive[i].gameObject);
        _alive.Clear();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 스폰 시작 범위
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, activateDistance);

        // 웨이브들의 모든 그룹 영역 박스(채워서 보이게)
        if (waves != null)
        {
            foreach (var wave in waves)
            {
                if (wave == null || wave.groups == null) continue;
                foreach (var group in wave.groups)
                {
                    if (group == null || group.spawnArea == null) continue;
                    var box = group.spawnArea;
                    Gizmos.matrix = box.transform.localToWorldMatrix;
                    Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
                    Gizmos.DrawCube(box.center, box.size);
                    Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
                    Gizmos.DrawWireCube(box.center, box.size);
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
#endif
}
