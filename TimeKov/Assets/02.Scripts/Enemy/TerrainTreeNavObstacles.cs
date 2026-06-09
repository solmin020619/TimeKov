using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 터레인에 페인트된 나무를 몬스터(NavMeshAgent)가 통과하는 문제 해결.
///
/// 유니티 한계: NavMeshSurface(새 AI Navigation)는 터레인 트리를 베이크에 반영하지 않는다
/// (Unity Issue Tracker 등록 버그, 전 버전 재현). 콜라이더/Use Geometry를 어떻게 해도 무시됨.
///
/// 해결: 터레인의 트리 인스턴스를 읽어 각 나무 위치에 NavMeshObstacle(carving)을 생성한다.
/// 그러면 이미 구워진 바닥 navmesh에 나무마다 구멍이 파여 몬스터가 나무를 우회한다.
/// 나무 자체는 터레인 트리로 그대로 둬서(렌더링 가벼움) 성능을 유지한다.
///
/// 사용법: 터레인 오브젝트(또는 빈 오브젝트)에 붙이고 terrain 연결 → Play 하면 자동 생성.
/// 인스펙터 우클릭 컨텍스트 메뉴로 수동 생성/삭제도 가능.
/// </summary>
public class TerrainTreeNavObstacles : MonoBehaviour
{
    [Header("대상 터레인 (비우면 같은 오브젝트에서 탐색)")]
    [SerializeField] private Terrain terrain;

    [Header("장애물 크기")]
    [Tooltip("나무 줄기 반경(m). 몬스터가 나무에서 이만큼 떨어져 우회한다.")]
    [SerializeField] private float obstacleRadius = 0.6f;
    [Tooltip("장애물 높이(m). 바닥 navmesh를 덮을 정도면 충분.")]
    [SerializeField] private float obstacleHeight = 4f;
    [Tooltip("true면 나무 widthScale에 비례해 반경 조정(굵은 나무=더 크게).")]
    [SerializeField] private bool scaleByTreeWidth = true;
    [Tooltip("widthScale이 이 값 미만인 나무는 무시(작은 덤불/풀 나무 제외).")]
    [SerializeField] private float minTreeScale = 0.3f;

    private const string ParentName = "Tree_Obstacles";

    private void Start()
    {
        Generate();
    }

    [ContextMenu("Generate Tree Obstacles")]
    public void Generate()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();
        if (terrain == null) { Debug.LogWarning("[TerrainTreeNavObstacles] terrain 미연결."); return; }

        Clear();

        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;   // 터레인은 코너 기준
        Vector3 size   = data.size;

        Transform parent = new GameObject(ParentName).transform;
        parent.SetParent(transform, false);

        int made = 0;
        foreach (TreeInstance inst in data.treeInstances)
        {
            if (inst.widthScale < minTreeScale) continue;

            // inst.position 은 0~1 정규화 좌표 → 월드 좌표로 변환
            Vector3 world = origin + Vector3.Scale(inst.position, size);

            var go = new GameObject("TreeObstacle");
            go.transform.SetParent(parent, false);
            go.transform.position = world;

            var obs = go.AddComponent<NavMeshObstacle>();
            obs.shape  = NavMeshObstacleShape.Capsule;
            obs.radius = scaleByTreeWidth ? obstacleRadius * inst.widthScale : obstacleRadius;
            obs.height = obstacleHeight;
            obs.center = new Vector3(0f, obstacleHeight * 0.5f, 0f);
            obs.carving = true;
            obs.carveOnlyStationary = true;   // 나무는 안 움직임 → 1회만 카빙(런타임 비용 최소)
            made++;
        }

        Debug.Log($"[TerrainTreeNavObstacles] 나무 장애물 {made}개 생성 (총 트리 {data.treeInstances.Length})");
    }

    [ContextMenu("Clear Tree Obstacles")]
    public void Clear()
    {
        var existing = transform.Find(ParentName);
        if (existing == null) return;
        if (Application.isPlaying) Destroy(existing.gameObject);
        else DestroyImmediate(existing.gameObject);
    }
}
