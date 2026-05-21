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
    [Header("스폰할 적 프리팹 (랜덤 선택)")]
    [SerializeField] private List<GameObject> enemyPrefabs = new();

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
    private readonly List<GameObject> aliveEnemies = new();
    private readonly Dictionary<GameObject, List<GameObject>> enemyWaypoints = new();
    private int pendingRespawns = 0;

    // TryGetRandomNavPos 마지막 호출에서 raycast로 찾은 ground 정보 (SpawnOne에서 baseOffset 보정용)
    private Vector3 _lastGroundPos;
    private bool _lastHasGround;

    private void Awake()
    {
        area = GetComponent<BoxCollider>();
        area.isTrigger = true;
    }

    private IEnumerator Start()
    {
        if (!spawnOnStart) yield break;
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
            if (hp != null) hp.OnDeath -= () => OnEnemyDied(e); // 캡처 ref 안 맞아서 사실상 noop이지만 GC 도움
        }
    }

    private void SpawnOne()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0) return;

        var prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Count)];
        if (prefab == null) return;

        if (!TryGetRandomNavPos(out Vector3 pos))
        {
            Debug.LogWarning($"[EnemySpawnPoint] {name}: 영역 안 NavMesh 점 못 찾음. NavMesh Bake 확인.", this);
            return;
        }

        var rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        var enemy = Instantiate(prefab, pos, rot);
        aliveEnemies.Add(enemy);

        // SO enemyName으로 GameObject 이름 정리 ("Enemy_X(Clone)" 제거)
        var brain = enemy.GetComponent<EnemyBrain>();
        if (brain != null && brain.Data != null && !string.IsNullOrEmpty(brain.Data.enemyName))
            enemy.name = brain.Data.enemyName;

        // Ground 정렬 보정: 적 발이 ground에 닿도록 NavMeshAgent.baseOffset 강제 보정
        // 원인 1: prefab의 baseOffset이 양수 (예: 0.86) → transform이 NavMesh 위로 그만큼 들림
        // 원인 2: NavMesh 자체가 ground보다 위에 깔림 → baseOffset 음수로 보정 필요
        if (snapToGround && _lastHasGround)
        {
            var agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                float prevBaseOffset = agent.baseOffset;
                // NavMesh가 ground보다 얼마나 위에 있는지
                float navMeshLiftFromGround = pos.y - _lastGroundPos.y;
                // 적 transform이 ground에 박히려면: transform.y = NavMesh.y + baseOffset = ground.y
                // → baseOffset = ground.y - NavMesh.y = -navMeshLiftFromGround
                agent.baseOffset = -navMeshLiftFromGround;
                agent.Warp(_lastGroundPos);

                if (verboseLog)
                    Debug.Log($"[SpawnPoint] {enemy.name}: navY={pos.y:F3}, groundY={_lastGroundPos.y:F3}, prevBaseOffset={prevBaseOffset:F3}, newBaseOffset={agent.baseOffset:F3}, finalY={enemy.transform.position.y:F3}", enemy);
            }
        }

        // 웨이포인트 생성 + 주입
        var waypoints = CreatePatrolPoints(enemy);
        enemyWaypoints[enemy] = waypoints;

        if (brain != null)
            brain.SetPatrolPoints(waypoints);

        // 죽음 구독 (캡처 변수 안전하게 로컬 변수로)
        var hp = enemy.GetComponent<EnemyHealth>();
        if (hp != null)
        {
            GameObject captured = enemy;
            hp.OnDeath += () => OnEnemyDied(captured);
        }
    }

    private void OnEnemyDied(GameObject enemy)
    {
        if (enemy == null) return;

        aliveEnemies.Remove(enemy);

        // 웨이포인트 정리
        if (enemyWaypoints.TryGetValue(enemy, out var points))
        {
            foreach (var p in points)
                if (p != null) Destroy(p);
            enemyWaypoints.Remove(enemy);
        }

        pendingRespawns++;
        StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        if (aliveEnemies.Count + pendingRespawns - 1 < maxAlive)
            SpawnOne();
        pendingRespawns--;
    }

    private List<GameObject> CreatePatrolPoints(GameObject owner)
    {
        var list = new List<GameObject>(patrolPointsPerEnemy);
        for (int i = 0; i < patrolPointsPerEnemy; i++)
        {
            if (!TryGetRandomNavPos(out Vector3 pos)) continue;
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
                result = navHit.position;
                return true;
            }
        }
        result = transform.position;
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
