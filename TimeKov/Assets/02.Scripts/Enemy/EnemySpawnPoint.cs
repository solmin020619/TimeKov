using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// BoxCollider 영역 기반 적 자동 스폰/리스폰 + 영역 내 순찰 시스템.
/// - spawnEntries(종류별 마리수/리스폰/엘리트 조건)대로 스폰. 항목별 maxCount 까지 유지.
/// - 적이 죽으면 OnDeath 구독으로 카운트 감소 -> 그 항목 respawnDelay 후 재스폰
/// - 엘리트는 일반몹 N킬 달성 시 해금되어 곧장 등장(첫 등장만 짧게), 이후 죽으면 항목 Respawn Delay로 재등장
/// - 적별로 영역 안 NavMesh 점 N개 자동 생성 -> EnemyBrain.SetPatrolPoints 주입
/// - Gizmo로 영역 시각화
/// 사용:
/// 1. 빈 GameObject 생성 + BoxCollider(영역) + EnemySpawnPoint 컴포넌트
/// 2. Spawn Entries 에 항목 추가: prefab / maxCount / respawnDelay / isElite / unlockAfterNormalKills
///    (단일몹/보스도 항목 1개로: maxCount=1, isElite=false, unlockAfterNormalKills=0)
/// 3. NavMesh 위에 영역 깔기
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class EnemySpawnPoint : MonoBehaviour
{
    // 종류별 마리수/리스폰/엘리트 조건을 지정하는 스폰 항목.
    [System.Serializable]
    public class SpawnEntry
    {
        [Tooltip("스폰할 적 프리팹")]
        public GameObject prefab;
        [Tooltip("이 종류가 동시에 살아있을 최대 수 (엘리트=1, 쫄=4 식. 단일몹/보스=1)")]
        [Min(1)] public int maxCount = 1;
        [Tooltip("이 종류 사망 후 재스폰까지 대기(초). 엘리트/보스는 길게.")]
        public float respawnDelay = 5f;
        [Tooltip("엘리트 여부 (해금 조건/리스폰을 따로 적용)")]
        public bool isElite = false;
        [Tooltip("0=처음부터 스폰. >0이면 이 구역에서 '일반몹'을 이만큼 처치해야 엘리트 등장")]
        public int unlockAfterNormalKills = 0;
    }

    [Header("스폰 항목 (종류별 마리수/리스폰/엘리트 조건)")]
    [Tooltip("여기에 적을 추가한다. 단일몹/보스도 항목 1개(maxCount=1)로 넣으면 됨. 비우면 아무것도 안 나옴.")]
    [SerializeField] private List<SpawnEntry> spawnEntries = new();

    [Header("구역 전체 제한")]
    [Tooltip("이 구역에 동시에 살아있을 수 있는 총 마리수. 0 = 무제한.\n" +
             "항목별 최대수를 다 더한 값보다 작게 주면, 종류는 섞이되 총량은 안 넘는다.\n" +
             "큰 박스에 여러 종을 넣고 '뿌리는' 배치를 할 때 이걸로 부하를 잡는다.")]
    [Min(0)] [SerializeField] private int totalMaxAlive = 0;

    [Tooltip("스폰할 때 이미 살아있는 적과 이만큼은 떨어뜨린다(m). 0 = 안 씀.\n" +
             "넓은 구역에 랜덤으로 뿌리면 몹이 한 곳에 뭉치는데, 그걸 막는다.")]
    [Min(0f)] [SerializeField] private float minSpawnSpacing = 0f;

    [Tooltip("플레이어가 이 거리 안에 있으면 리스폰을 미룬다(m). 0 = 거리 무시하고 바로 리스폰.\n" +
             "눈앞에서 몹이 튀어나오는 걸 막는다. 구역이 넓을수록 유용.")]
    [Min(0f)] [SerializeField] private float respawnPlayerDistance = 0f;

    [Header("성능 (원거리 슬립)")]
    [Tooltip("플레이어가 이 거리 밖이면 이 구역 몹을 despawn 하고 스폰도 멈춘다(m). 0 = 항상 유지(기존 동작).\n" +
             "안 간/떠난 구역의 몹이 CPU를 안 먹게 한다. 다가갈 때 팝인이 안 보이게 시야보다 넉넉히 크게 줘라.\n" +
             "재방문하면 몹은 새로 스폰된다(엘리트 해금 진행도는 유지).")]
    [Min(0f)] [SerializeField] private float activateDistance = 100f;

    [Header("초기 스폰")]
    [Tooltip("시작 시 항목별 maxCount까지 자동 스폰(해금형 엘리트 제외 = 일반몹 처치 후 등장)")]
    [SerializeField] private bool spawnOnStart = true;
    [Tooltip("초기 스폰 시 한 마리당 간격(초). 동시 폭주 방지")]
    [SerializeField] private float initialSpawnInterval = 0.3f;

    [Header("순찰")]
    [Tooltip("적 한 마리당 자동 생성할 웨이포인트 수")]
    [SerializeField] private int patrolPointsPerEnemy = 4;
    [Tooltip("랜덤 점 -> NavMesh sample 시 허용 반경")]
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
    [Tooltip("Ground로 인식할 Layer. Everything이면 다른 적/콜라이더도 hit할 수 있음 -> Ground/Terrain만 켜는 게 안전.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("디버그")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color areaColor = new Color(1f, 0.5f, 0f, 0.25f);
    [SerializeField] private Color waypointColor = new Color(0.2f, 0.8f, 1f, 1f);

    // 인스펙터(EnemySpawnPointEditor)에서 플레이 중 현황을 보여주기 위한 읽기 전용 창구
    public int EditorAliveCount => aliveEnemies != null ? aliveEnemies.Count : 0;
    public int EditorTotalMaxAlive => totalMaxAlive;

    private BoxCollider area;
    private readonly List<BoxCollider> _excluders = new();
    private readonly List<GameObject> aliveEnemies = new();
    private readonly Dictionary<GameObject, List<GameObject>> enemyWaypoints = new();

    // 스폰 런타임 추적 (항목별)
    private int[] _aliveByEntry;
    private int[] _pendingByEntry;
    private int _normalKills;
    private bool _zoneAsleep;   // activateDistance 슬립 상태(몹 despawn 됨). 플레이어 접근 시 깨어나 재스폰.
    private readonly Dictionary<GameObject, int> _entryOf = new();

    // TryGetRandomNavPos 마지막 호출에서 raycast로 찾은 ground 정보 (SpawnPrefab에서 baseOffset 보정용)
    private Vector3 _lastGroundPos;
    private bool _lastHasGround;

    private void Awake()
    {
        area = GetComponent<BoxCollider>();
        area.isTrigger = true;
        BuildExcluders();

        int n = spawnEntries != null ? spawnEntries.Count : 0;
        _aliveByEntry = new int[n];
        _pendingByEntry = new int[n];
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

        if (spawnEntries == null || spawnEntries.Count == 0)
        {
            Debug.LogWarning($"[EnemySpawnPoint] {name}: Spawn Entries 비어있음 -> 아무것도 안 나옴. 적을 항목으로 추가하세요(단일몹/보스도 항목 1개).", this);
            yield break;
        }

        // 원거리 슬립 사용 시: 시작부터 다 스폰하지 않고, 플레이어가 접근할 때만 스폰/멀면 despawn.
        if (activateDistance > 0f)
        {
            StartCoroutine(ActivationLoop());
            yield break;
        }

        yield return InitialSpawnAll();
    }

    // 항목별 maxCount 까지 스폰(초기 스폰 / 슬립에서 깨어날 때 공통).
    //   해금형 엘리트는 아직 조건 미달이면 제외(일반몹 처치 후 등장). 이미 해금됐으면 재방문 시 같이 나온다.
    private IEnumerator InitialSpawnAll()
    {
        for (int i = 0; i < spawnEntries.Count; i++)
        {
            var e = spawnEntries[i];
            if (e == null || e.prefab == null) continue;
            if (e.isElite && e.unlockAfterNormalKills > 0 && _normalKills < e.unlockAfterNormalKills) continue;
            int want = Mathf.Max(1, e.maxCount);
            for (int k = 0; k < want; k++)
            {
                if (!CanSpawnEntry(i)) break;
                SpawnEntryOne(i);
                if (initialSpawnInterval > 0f)
                    yield return new WaitForSeconds(initialSpawnInterval);
            }
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

    // 스폰 항목 1마리 스폰
    private void SpawnEntryOne(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= spawnEntries.Count) return;
        var e = spawnEntries[entryIndex];
        if (e == null || e.prefab == null) return;
        if (SpawnPrefab(e.prefab, entryIndex) != null)
            _aliveByEntry[entryIndex]++;
    }

    // 공통 스폰 코어: 인스턴스화 + 이름정리 + 지면정렬 + 순찰주입 + 사망구독.
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
        // IEnemyDataSource 로 받는 이유 = 보스는 EnemyBrain 이 없다(전용 컨트롤러 사용).
        var src = enemy.GetComponent<IEnemyDataSource>();
        if (src != null && src.Data != null && !string.IsNullOrEmpty(src.Data.enemyName))
            enemy.name = src.Data.enemyName;

        // Ground 정렬 보정: 적 발이 ground에 닿도록 NavMeshAgent.baseOffset 강제 보정
        // 원인 1: prefab의 baseOffset이 양수 (예: 0.86) -> transform이 NavMesh 위로 그만큼 들림
        // 원인 2: NavMesh 자체가 ground보다 위에 깔림 -> baseOffset 음수로 보정 필요
        if (snapToGround && _lastHasGround)
        {
            var agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                float navMeshLiftFromGround = pos.y - _lastGroundPos.y;
                agent.baseOffset = -navMeshLiftFromGround;
                agent.Warp(_lastGroundPos);
            }
        }

        // 웨이포인트 생성 + 주입
        // 순찰은 BT(EnemyBrain) 쓰는 일반몹만 해당. 보스는 전용 컨트롤러라 여기 안 걸린다.
        var waypoints = CreatePatrolPoints(enemy);
        enemyWaypoints[enemy] = waypoints;
        var brain = enemy.GetComponent<EnemyBrain>();
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

        if (entryIndex < 0 || entryIndex >= spawnEntries.Count) return;   // 안전 가드(정리 콜백 등)

        // 항목별 카운트 감소 + (일반몹이면) 킬 누적 -> 엘리트 해금 시도 + 항목별 리스폰
        _aliveByEntry[entryIndex] = Mathf.Max(0, _aliveByEntry[entryIndex] - 1);
        var e = spawnEntries[entryIndex];
        if (e != null && !e.isElite)
        {
            _normalKills++;
            TryUnlockElites();
        }
        float delay = e != null ? e.respawnDelay : 5f;
        StartCoroutine(RespawnEntryAfterDelay(entryIndex, delay));
    }

    // 구역 전체 현황 — totalMaxAlive 판정용
    private int TotalAlive()
    {
        int n = 0;
        for (int i = 0; i < _aliveByEntry.Length; i++) n += _aliveByEntry[i];
        return n;
    }

    private int TotalPending()
    {
        int n = 0;
        for (int i = 0; i < _pendingByEntry.Length; i++) n += _pendingByEntry[i];
        return n;
    }

    // 지금 즉시 스폰 가능한지(살아있는 수 < 최대 + 엘리트 해금 충족 + 구역 총량)
    private bool CanSpawnEntry(int i)
    {
        if (_zoneAsleep) return false;   // 원거리 슬립 중엔 스폰 금지(깨어날 때 InitialSpawnAll 이 채운다)
        if (i < 0 || i >= spawnEntries.Count) return false;
        var e = spawnEntries[i];
        if (e == null || e.prefab == null) return false;
        if (_aliveByEntry[i] >= Mathf.Max(1, e.maxCount)) return false;
        if (e.isElite && e.unlockAfterNormalKills > 0 && _normalKills < e.unlockAfterNormalKills) return false;
        if (totalMaxAlive > 0 && TotalAlive() >= totalMaxAlive) return false;
        return true;
    }

    // 예약(코루틴)까지 고려해 자리 남았는지 — 중복 예약으로 maxCount 초과 방지
    private bool CanScheduleEntry(int i)
    {
        if (_zoneAsleep) return false;
        if (i < 0 || i >= spawnEntries.Count) return false;
        var e = spawnEntries[i];
        if (e == null || e.prefab == null) return false;
        if (_aliveByEntry[i] + _pendingByEntry[i] >= Mathf.Max(1, e.maxCount)) return false;
        if (e.isElite && e.unlockAfterNormalKills > 0 && _normalKills < e.unlockAfterNormalKills) return false;
        if (totalMaxAlive > 0 && TotalAlive() + TotalPending() >= totalMaxAlive) return false;
        return true;
    }

    private IEnumerator RespawnEntryAfterDelay(int i, float delay)
    {
        _pendingByEntry[i]++;
        yield return new WaitForSeconds(delay + Random.Range(0f, 1f));

        // 플레이어가 가까우면 멀어질 때까지 대기 — 눈앞에서 튀어나오는 걸 막는다.
        // 여기서 계속 pending 을 물고 있으므로 그 사이 다른 놈이 자리를 채가지 않는다.
        if (respawnPlayerDistance > 0f)
        {
            float sqr = respawnPlayerDistance * respawnPlayerDistance;
            while (true)
            {
                var p = GetPlayerTransform();
                if (p == null) break;                                   // 플레이어 없으면(사망/씬전환) 그냥 진행
                if ((p.position - transform.position).sqrMagnitude > sqr) break;
                yield return new WaitForSeconds(1f);
            }
        }

        _pendingByEntry[i] = Mathf.Max(0, _pendingByEntry[i] - 1);
        if (CanSpawnEntry(i)) SpawnEntryOne(i);
    }

    // 플레이어는 씬에 하나 — 매번 찾지 않게 캐시
    private static Transform _playerT;
    private static Transform GetPlayerTransform()
    {
        if (_playerT != null) return _playerT;
        var p = FindFirstObjectByType<Player>();
        _playerT = p != null ? p.transform : null;
        return _playerT;
    }

    // 원거리 슬립 (activateDistance)
    // 플레이어가 구역에서 멀면 몹을 despawn 해 CPU 부하를 없애고, 가까워지면 다시 스폰한다.
    //   0.5초마다 구역 박스까지 거리만 재는 아주 가벼운 폴링. 히스테리시스로 경계 깜빡임 방지.
    private IEnumerator ActivationLoop()
    {
        _zoneAsleep = true;   // 시작은 잠든 상태 - 플레이어가 사거리에 들어와야 스폰
        float on  = activateDistance;
        float off = activateDistance * 1.15f;   // 깨는 거리보다 재우는 거리를 멀게(경계 떨림 방지)
        float onSqr = on * on, offSqr = off * off;
        var wait = new WaitForSeconds(0.5f);

        while (true)
        {
            var p = GetPlayerTransform();
            if (p != null && area != null)
            {
                // 구역 박스까지의 거리(안에 있으면 0) - 큰 박스도 안전하게 판정.
                float d = (p.position - area.bounds.ClosestPoint(p.position)).sqrMagnitude;
                if (_zoneAsleep) { if (d < onSqr) yield return WakeZone(); }
                else             { if (d > offSqr) SleepZone(); }
            }
            yield return wait;
        }
    }

    private IEnumerator WakeZone()
    {
        _zoneAsleep = false;
        yield return InitialSpawnAll();   // 항목별 maxCount 까지 새로 스폰
    }

    // 구역의 살아있는 몹 전부 제거(처치 아님 - OnDeath 를 안 거쳐 킬 집계/퀘스트에 안 잡힌다) + 카운트/웨이포인트 정리.
    private void SleepZone()
    {
        _zoneAsleep = true;
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            var e = aliveEnemies[i];
            if (e == null) continue;
            if (enemyWaypoints.TryGetValue(e, out var pts))
            {
                foreach (var wp in pts) if (wp != null) Destroy(wp);
                enemyWaypoints.Remove(e);
            }
            _entryOf.Remove(e);
            Destroy(e);
        }
        aliveEnemies.Clear();
        for (int i = 0; i < _aliveByEntry.Length; i++) _aliveByEntry[i] = 0;
        // _pendingByEntry: 진행 중 리스폰 코루틴은 _zoneAsleep 게이트로 스폰 안 함(자연 정리).
        // _normalKills: 엘리트 해금 진행도라 유지(재방문 시 이어짐).
    }

    // 해금되는 순간(일반몹 N킬 달성) 엘리트가 곧장 나오게 하는 첫 등장 지연(초).
    // 죽은 뒤 재등장은 항목별 Respawn Delay(길다)를 그대로 쓴다 -> 첫 등장만 빠르게.
    private const float EliteUnlockAppearDelay = 3f;

    // 일반몹 처치로 해금 조건을 넘긴 엘리트가 있으면 스폰 예약
    private void TryUnlockElites()
    {
        for (int i = 0; i < spawnEntries.Count; i++)
        {
            var e = spawnEntries[i];
            if (e == null || !e.isElite || e.unlockAfterNormalKills <= 0) continue;
            if (_normalKills < e.unlockAfterNormalKills) continue;
            // 첫 등장은 Respawn Delay(재등장용, 50~100초로 길다)가 아니라 짧은 지연으로 곧장.
            if (CanScheduleEntry(i)) StartCoroutine(RespawnEntryAfterDelay(i, EliteUnlockAppearDelay));
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

    // 이미 살아있는 적과 너무 붙었는지 (minSpawnSpacing)
    private bool IsTooCloseToAlive(Vector3 pos)
    {
        if (minSpawnSpacing <= 0f) return false;
        float sqr = minSpawnSpacing * minSpawnSpacing;
        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            var e = aliveEnemies[i];
            if (e == null) continue;
            if ((e.transform.position - pos).sqrMagnitude < sqr) return true;
        }
        return false;
    }

    private bool TryGetRandomNavPos(out Vector3 result)
    {
        if (area == null) area = GetComponent<BoxCollider>();
        // ★간격 조건이 켜져 있으면 후보를 더 많이 뽑아본다.
        //   10번만 뽑으면 좁은 구역에서 전부 탈락해 스폰이 조용히 누락된다.
        int tries = minSpawnSpacing > 0f ? 30 : 10;
        for (int i = 0; i < tries; i++)
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
                // 마지막 몇 번은 간격 조건을 포기한다. 안 그러면 자리가 빡빡할 때 아예 못 나온다.
                if (i < tries - 5 && IsTooCloseToAlive(navHit.position)) continue;
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
