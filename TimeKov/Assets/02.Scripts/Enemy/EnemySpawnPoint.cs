using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// BoxCollider 영역 기반 적 자동 스폰/리스폰 + 영역 내 순찰 시스템.
/// - enemyPrefabs 중 랜덤 선택해서 maxAlive 명까지 스폰
/// - 적이 죽으면 OnDeath 구독으로 카운트 감소 → respawnDelay 후 재스폰
/// - 적별로 영역 안 NavMesh 점 N개 자동 생성 → EnemyBrain.SetPatrolPoints로 Blackboard 주입
/// - Gizmo로 영역 시각화
/// 사용:
/// 1. 빈 GameObject 생성, BoxCollider 추가 (영역으로 사용)
/// 2. EnemySpawnPoint 컴포넌트 추가
/// 3. enemyPrefabs에 Enemy_*.prefab 드래그 (여러 개 OK, 랜덤 선택)
/// 4. NavMesh 위에 영역 깔기
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class EnemySpawnPoint : MonoBehaviour
{
    [Header("스폰할 적 프리팹 (랜덤 선택) - 레거시")]
    [Tooltip("아래 '그룹 스폰'을 채우면 이 리스트는 무시된다.")]
    [SerializeField] private List<GameObject> enemyPrefabs = new();

    // 종류별 마리수/리스폰/엘리트 조건을 지정하는 그룹 스폰 항목.
    [System.Serializable]
    public class SpawnEntry
    {
        [Tooltip("스폰할 적 프리팹")]
        public GameObject prefab;
        [Tooltip("이 종류가 동시에 살아있을 최대 수 (엘리트=1, 쫄=4 식)")]
        [Min(1)] public int maxCount = 1;
        [Tooltip("이 종류 사망 후 재스폰까지 대기(초). 엘리트는 길게.")]
        public float respawnDelay = 5f;
        [Tooltip("엘리트 여부 (해금 조건/리스폰을 따로 적용)")]
        public bool isElite = false;
        [Tooltip("0=처음부터 스폰. >0이면 이 구역에서 '일반몹'을 이만큼 처치해야 엘리트 등장")]
        public int unlockAfterNormalKills = 0;
    }

    [Header("그룹 스폰 (엘리트+쫄: 종류별 마리수/리스폰/조건)")]
    [Tooltip("비우면 위 enemyPrefabs(랜덤) 방식 그대로. 채우면 항목별로 동작(이때 enemyPrefabs/maxAlive 무시, respawnDelay는 항목값 사용).")]
    [SerializeField] private List<SpawnEntry> spawnEntries = new();

    [Header("동시 생존 / 리스폰")]
    [Tooltip("동시에 살아있을 수 있는 최대 적 수")]
    [SerializeField] private int maxAlive = 5;
    [Tooltip("적 사망 후 다음 스폰까지 대기 시간(초)")]
    [SerializeField] private float respawnDelay = 5f;
    [Tooltip("시작 시 maxAlive까지 자동 스폰")]
    [SerializeField] private bool spawnOnStart = true;
    [Tooltip("초기 스폰 시 한 마리당 간격(초). 동시 폭주 방지")]
    [SerializeField] private float initialSpawnInterval = 0.3f;

    [Header("순찰")]
    [Tooltip("적 한 마리당 자동 생성할 웨이포인트 수")]
    [SerializeField] private int patrolPointsPerEnemy = 4;
    [Tooltip("랜덤 점 → NavMesh sample 시 허용 반경")]
    [SerializeField] private float navMeshSampleRadius = 5f;
    [Tooltip("0이면 영역 전체에서 순찰. >0이면 각 적의 스폰 지점 반경 내에서만 순찰. 맵 전체 같은 큰 박스에서 적이 멀리 안 가게 할 때 사용.")]
    [SerializeField] private float patrolRadius = 0f;

    [Header("스폰 제외 영역 (갈색존 등)")]
    [Tooltip("이 BoxCollider들 안에는 스폰/순찰 안 함. 갈색존 위에 둔 박스(예: 다른 몹 스폰존)를 드래그하면 그 부분만 빠짐.")]
    [SerializeField] private List<BoxCollider> excludeZones = new();
    [Tooltip("excludeZones에 더해, 씬에 있는 다른 모든 EnemySpawnPoint 영역도 자동으로 제외(=다른 몹 스폰존엔 안 겹치게).")]
    [SerializeField] private bool autoExcludeOtherSpawnPoints = false;

    [Header("지면 정렬 (Ground Snap)")]
    [Tooltip("영역 위에서 아래로 raycast해서 ground에 정확히 박기. NavMesh가 지면보다 살짝 떠있을 때 사용.")]
    [SerializeField] private bool snapToGround = true;
    [Tooltip("Ground로 인식할 Layer. Everything이면 다른 적/콜라이더도 hit할 수 있음 → Ground/Terrain만 켜는 게 안전.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("디버그")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color areaColor = new Color(1f, 0.5f, 0f, 0.25f);
    [SerializeField] private Color waypointColor = new Color(0.2f, 0.8f, 1f, 1f);
    [Tooltip("Console에 스폰 위치/지면 보정 값 출력 (부유 디버깅용)")]
    [SerializeField] private bool verboseLog = false;

    private BoxCollider area;
    private readonly List<BoxCollider> _excluders = new();
    private readonly List<GameObject> aliveEnemies = new();
    private readonly Dictionary<GameObject, List<GameObject>> enemyWaypoints = new();
    private int pendingRespawns = 0;

    // 그룹 스폰 런타임 추적
    private int[] _aliveByEntry;
    private int[] _pendingByEntry;
    private int _normalKills;
    private readonly Dictionary<GameObject, int> _entryOf = new();
    private bool UseEntries => spawnEntries != null && spawnEntries.Count > 0;

    // TryGetRandomNavPos 마지막 호출에서 raycast로 찾은 ground 정보 (SpawnOne에서 baseOffset 보정용)
    private Vector3 _lastGroundPos;
    private bool _lastHasGround;

    private void Awake()
    {
        area = GetComponent<BoxCollider>();
        area.isTrigger = true;
        BuildExcluders();

        if (UseEntries)
        {
            _aliveByEntry = new int[spawnEntries.Count];
            _pendingByEntry = new int[spawnEntries.Count];
        }
    }

    private void BuildExcluders()
    {
        _excluders.Clear();
        foreach (var bc in excludeZones)
            if (bc != null && !_excluders.Contains(bc)) _excluders.Add(bc);

        if (autoExcludeOtherSpawnPoints)
        {
            var others = FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
            foreach (var sp in others)
            {
                if (sp == this) continue;
                var bc = sp.GetComponent<BoxCollider>();
                if (bc != null && !_excluders.Contains(bc)) _excluders.Add(bc);
            }
        }
    }

    // candidate가 제외 영역(갈색존 등) 안에 들어가는지
    private bool IsInExcludedZone(Vector3 world)
    {
        for (int i = 0; i < _excluders.Count; i++)
        {
            var bc = _excluders[i];
            if (bc == null) continue;
            if ((bc.ClosestPoint(world) - world).sqrMagnitude < 0.01f) return true;
        }
        return false;
    }

    private IEnumerator Start()
    {
        if (!spawnOnStart) yield break;

        // 그룹 스폰: 항목별로 maxCount 까지 초기 스폰(해금형 엘리트는 제외 = 일반몹 처치 후 등장)
        if (UseEntries)
        {
            for (int i = 0; i < spawnEntries.Count; i++)
            {
                var e = spawnEntries[i];
                if (e == null || e.prefab == null) continue;
                if (e.isElite && e.unlockAfterNormalKills > 0) continue;
                int want = Mathf.Max(1, e.maxCount);
                for (int k = 0; k < want; k++)
                {
                    if (!CanSpawnEntry(i)) break;
                    SpawnEntryOne(i);
                    if (initialSpawnInterval > 0f)
                        yield return new WaitForSeconds(initialSpawnInterval);
                }
            }
            yield break;
        }

        // 레거시: enemyPrefabs 랜덤으로 maxAlive 까지
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            Debug.LogWarning($"[EnemySpawnPoint] {name}: enemyPrefabs 비어있음. 스폰 안 함.", this);
            yield break;
        }

        for (int i = 0; i < maxAlive; i++)
        {
            SpawnOne();
            if (initialSpawnInterval > 0f)
                yield return new WaitForSeconds(initialSpawnInterval);
        }
    }

    private void OnDestroy()
    {
        // 영역 자체 파괴되면 살아있는 적은 그대로 두지만 콜백은 정리
        foreach (var e in aliveEnemies)
        {
            if (e == null) continue;
            var hp = e.GetComponent<EnemyHealth>();
            if (hp != null) hp.OnDeath -= () => OnEnemyDied(e, -1); // 캡처 ref 안 맞아서 사실상 noop이지만 GC 도움
        }
    }

    // 레거시(랜덤) 1마리 스폰
    private void SpawnOne()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0) return;
        var prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Count)];
        if (prefab == null) return;
        SpawnPrefab(prefab, -1);
    }

    // 그룹 스폰 항목 1마리 스폰
    private void SpawnEntryOne(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= spawnEntries.Count) return;
        var e = spawnEntries[entryIndex];
        if (e == null || e.prefab == null) return;
        if (SpawnPrefab(e.prefab, entryIndex) != null)
            _aliveByEntry[entryIndex]++;
    }

    // 공통 스폰 코어: 인스턴스화 + 이름정리 + 지면정렬 + 순찰주입 + 사망구독. entryIndex<0 = 레거시(랜덤).
    private GameObject SpawnPrefab(GameObject prefab, int entryIndex)
    {
        if (prefab == null) return null;

        if (!TryGetRandomNavPos(out Vector3 pos))
        {
            Debug.LogWarning($"[EnemySpawnPoint] {name}: 영역 안 NavMesh 점 못 찾음. NavMesh Bake 확인.", this);
            return null;
        }

        var rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        var enemy = Instantiate(prefab, pos, rot);
        aliveEnemies.Add(enemy);
        if (entryIndex >= 0) _entryOf[enemy] = entryIndex;

        // SO enemyName으로 GameObject 이름 정리 ("Enemy_X(Clone)" 제거)
        var brain = enemy.GetComponent<EnemyBrain>();
        if (brain != null && brain.Data != null && !string.IsNullOrEmpty(brain.Data.enemyName))
            enemy.name = brain.Data.enemyName;

        // Ground 정렬 보정: 적 발이 ground에 닿도록 NavMeshAgent.baseOffset 강제 보정
        // 원인 1: prefab의 baseOffset이 양수 (예: 0.86) -> transform이 NavMesh 위로 그만큼 들림
        // 원인 2: NavMesh 자체가 ground보다 위에 깔림 -> baseOffset 음수로 보정 필요
        if (snapToGround && _lastHasGround)
        {
            var agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                float prevBaseOffset = agent.baseOffset;
                float navMeshLiftFromGround = pos.y - _lastGroundPos.y;
                agent.baseOffset = -navMeshLiftFromGround;
                agent.Warp(_lastGroundPos);

                if (verboseLog)
                    Debug.Log($"[SpawnPoint] {enemy.name}: navY={pos.y:F3}, groundY={_lastGroundPos.y:F3}, prevBaseOffset={prevBaseOffset:F3}, newBaseOffset={agent.baseOffset:F3}, finalY={enemy.transform.position.y:F3}", enemy);
            }
        }

        // 웨이포인트 생성 + 주입
        var waypoints = CreatePatrolPoints(enemy);
        enemyWaypoints[enemy] = waypoints;
        if (brain != null) brain.SetPatrolPoints(waypoints);

        // 죽음 구독 (캡처 변수 안전하게 로컬 변수로)
        var hp = enemy.GetComponent<EnemyHealth>();
        if (hp != null)
        {
            GameObject captured = enemy;
            int capIdx = entryIndex;
            hp.OnDeath += () => OnEnemyDied(captured, capIdx);
        }
        return enemy;
    }

    private void OnEnemyDied(GameObject enemy, int entryIndex)
    {
        if (enemy == null) return;

        aliveEnemies.Remove(enemy);
        _entryOf.Remove(enemy);

        // 웨이포인트 정리
        if (enemyWaypoints.TryGetValue(enemy, out var points))
        {
            foreach (var p in points)
                if (p != null) Destroy(p);
            enemyWaypoints.Remove(enemy);
        }

        // 레거시(랜덤) 경로
        if (!UseEntries || entryIndex < 0 || entryIndex >= spawnEntries.Count)
        {
            pendingRespawns++;
            StartCoroutine(RespawnAfterDelay());
            return;
        }

        // 그룹 스폰 경로: 종류별 카운트 감소 + (일반몹이면) 킬 누적 -> 엘리트 해금 시도 + 종류별 리스폰
        _aliveByEntry[entryIndex] = Mathf.Max(0, _aliveByEntry[entryIndex] - 1);
        var e = spawnEntries[entryIndex];
        if (e != null && !e.isElite)
        {
            _normalKills++;
            TryUnlockElites();
        }
        float delay = e != null ? e.respawnDelay : respawnDelay;
        StartCoroutine(RespawnEntryAfterDelay(entryIndex, delay));
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay + Random.Range(0f, 2f));
        if (aliveEnemies.Count + pendingRespawns - 1 < maxAlive)
            SpawnOne();
        pendingRespawns--;
    }

    // 그룹 스폰: 지금 즉시 스폰 가능한지(살아있는 수 < 최대 + 엘리트 해금 충족)
    private bool CanSpawnEntry(int i)
    {
        if (!UseEntries || i < 0 || i >= spawnEntries.Count) return false;
        var e = spawnEntries[i];
        if (e == null || e.prefab == null) return false;
        if (_aliveByEntry[i] >= Mathf.Max(1, e.maxCount)) return false;
        if (e.isElite && e.unlockAfterNormalKills > 0 && _normalKills < e.unlockAfterNormalKills) return false;
        return true;
    }

    // 예약(코루틴)까지 고려해 자리 남았는지 — 중복 예약으로 maxCount 초과 방지
    private bool CanScheduleEntry(int i)
    {
        if (!UseEntries || i < 0 || i >= spawnEntries.Count) return false;
        var e = spawnEntries[i];
        if (e == null || e.prefab == null) return false;
        if (_aliveByEntry[i] + _pendingByEntry[i] >= Mathf.Max(1, e.maxCount)) return false;
        if (e.isElite && e.unlockAfterNormalKills > 0 && _normalKills < e.unlockAfterNormalKills) return false;
        return true;
    }

    private IEnumerator RespawnEntryAfterDelay(int i, float delay)
    {
        _pendingByEntry[i]++;
        yield return new WaitForSeconds(delay + Random.Range(0f, 1f));
        _pendingByEntry[i] = Mathf.Max(0, _pendingByEntry[i] - 1);
        if (CanSpawnEntry(i)) SpawnEntryOne(i);
    }

    // 일반몹 처치로 해금 조건을 넘긴 엘리트가 있으면 스폰 예약
    private void TryUnlockElites()
    {
        for (int i = 0; i < spawnEntries.Count; i++)
        {
            var e = spawnEntries[i];
            if (e == null || !e.isElite || e.unlockAfterNormalKills <= 0) continue;
            if (_normalKills < e.unlockAfterNormalKills) continue;
            if (CanScheduleEntry(i)) StartCoroutine(RespawnEntryAfterDelay(i, e.respawnDelay));
        }
    }

    private List<GameObject> CreatePatrolPoints(GameObject owner)
    {
        var list = new List<GameObject>(patrolPointsPerEnemy);
        for (int i = 0; i < patrolPointsPerEnemy; i++)
        {
            bool ok = patrolRadius > 0f
                ? TryGetRandomNavPosNear(owner.transform.position, patrolRadius, out Vector3 pos)
                : TryGetRandomNavPos(out pos);
            if (!ok) continue;
            var wp = new GameObject($"WP_{owner.name}_{i}");
            wp.transform.SetParent(transform);
            wp.transform.position = pos;
            list.Add(wp);
        }
        return list;
    }

    private bool TryGetRandomNavPos(out Vector3 result)
    {
        if (area == null) area = GetComponent<BoxCollider>();
        // 최대 10번 시도
        for (int i = 0; i < 10; i++)
        {
            // 영역 내 XZ 랜덤 + Y는 영역 상단 (위에서 raycast down)
            Vector3 local = new Vector3(
                Random.Range(-area.size.x * 0.5f, area.size.x * 0.5f),
                area.size.y * 0.5f,
                Random.Range(-area.size.z * 0.5f, area.size.z * 0.5f)
            );
            Vector3 world = transform.TransformPoint(area.center + local);

            Vector3 candidate = world;
            _lastHasGround = false;
            if (snapToGround)
            {
                float rayLength = area.size.y * 2f + 50f;
                if (Physics.Raycast(world, Vector3.down, out RaycastHit groundHit, rayLength, groundMask, QueryTriggerInteraction.Ignore))
                {
                    candidate = groundHit.point;
                    _lastGroundPos = groundHit.point;
                    _lastHasGround = true;
                }
                // ground 못 찾으면 world 그대로 사용 (NavMesh sample이 대신 보정)
            }

            if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                if (IsInExcludedZone(navHit.position)) continue;
                result = navHit.position;
                return true;
            }
        }
        result = transform.position;
        return false;
    }

    // 특정 지점 반경 내에서 NavMesh 위 랜덤 점 (스폰 위치 중심 로컬 순찰용)
    private bool TryGetRandomNavPosNear(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 c = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(c.x, 0f, c.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, Mathf.Max(navMeshSampleRadius, radius), NavMesh.AllAreas))
            {
                if (IsInExcludedZone(navHit.position)) continue;
                result = navHit.position;
                return true;
            }
        }
        result = center;
        return false;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        var bc = GetComponent<BoxCollider>();
        if (bc == null) return;

        // 영역 박스 (filled)
        Gizmos.color = areaColor;
        Matrix4x4 m = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.matrix = m;
        Gizmos.DrawCube(bc.center, bc.size);

        // 영역 박스 (wire)
        Color edge = areaColor;
        edge.a = 1f;
        Gizmos.color = edge;
        Gizmos.DrawWireCube(bc.center, bc.size);

        // 살아있는 적의 웨이포인트 표시 (런타임)
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = waypointColor;
        foreach (var kv in enemyWaypoints)
        {
            foreach (var p in kv.Value)
            {
                if (p == null) continue;
                Gizmos.DrawSphere(p.transform.position, 0.25f);
            }
        }
    }
}
