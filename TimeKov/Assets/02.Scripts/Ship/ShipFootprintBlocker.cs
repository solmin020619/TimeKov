using System.Collections.Generic;
using UnityEngine;

// 폐우주선이 차지하는 건축 그리드 칸을 '점유'로 예약해 그 위에 설비 설치를 막는다.
// 우주선은 정중앙 고정(약 4x4~5x5). 오브젝트 월드 위치 기준으로 footprint 칸을 계산해
// BuildManager 에 예약한다. (해제는 없음 — 우주선은 영구 고정)
public class ShipFootprintBlocker : MonoBehaviour
{
    [Tooltip("우주선이 차지하는 건축 칸 크기 (가로 x 세로).")]
    [SerializeField] private Vector2Int footprintSize = new Vector2Int(5, 5);
    [Tooltip("건축 매니저. 비우면 씬에서 자동 탐색.")]
    [SerializeField] private BuildManager buildManager;

    private void Start()
    {
        if (buildManager == null) buildManager = FindAnyObjectByType<BuildManager>();
        if (buildManager == null) return;

        // BuildManager 의 그리드 origin 등 초기화 이후 예약하도록 한 프레임 뒤 실행.
        Invoke(nameof(Reserve), 0f);
    }

    private void Reserve()
    {
        if (buildManager == null) return;

        int sx = Mathf.Max(1, footprintSize.x);
        int sy = Mathf.Max(1, footprintSize.y);
        var size = new Vector2Int(sx, sy);

        Vector2Int origin = GridMath.WorldToStartCellCentered(
            transform.position, size, buildManager.GridOriginPos, buildManager.cellSize);

        var cells = new List<Vector2Int>(sx * sy);
        for (int x = 0; x < sx; x++)
            for (int y = 0; y < sy; y++)
                cells.Add(new Vector2Int(origin.x + x, origin.y + y));

        buildManager.ReserveCells(cells);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var bm = buildManager != null ? buildManager : FindAnyObjectByType<BuildManager>();
        float s = bm != null ? bm.cellSize : 1f;
        Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.28f);
        Gizmos.DrawCube(transform.position, new Vector3(footprintSize.x * s, 0.3f, footprintSize.y * s));
    }
#endif
}
