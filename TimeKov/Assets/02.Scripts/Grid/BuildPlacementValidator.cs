// =====================================================================
// BuildPlacementValidator.cs
// "여기에 지을 수 있는가" 판정 전담 - 건축존 영역 / 물리 충돌 / 레일 겹침.
// 설정값은 BuildManager 인스펙터에 남겨두고 owner 통해 읽는다 (인스펙터 작업 불필요).
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

public class BuildPlacementValidator
{
    private readonly BuildManager owner;

    public BuildPlacementValidator(BuildManager owner)
    {
        this.owner = owner;
    }

    // footprint 의 모든 칸이 BuildZone 영역 안에 완전히 들어오는지.
    // buildZoneCollider 미연결 시 기존 플레이어 위치 기준으로 폴백.
    public bool IsFootprintInBuildZone(List<Vector2Int> footprintCells)
    {
        if (owner.buildZoneCollider == null)
            return owner.zoneChecker != null && owner.zoneChecker.IsInBuildZone;

        if (footprintCells == null || footprintCells.Count == 0)
            return false;

        for (int i = 0; i < footprintCells.Count; i++)
            if (!IsCellInBuildZone(footprintCells[i]))
                return false;
        return true;
    }

    // 단일 칸이 BuildZone 영역 안에 완전히 들어오는지 (레일 등 1칸 단위 판정 공용).
    public bool IsCellInBuildZone(Vector2Int cell)
    {
        if (owner.buildZoneCollider == null)
            return owner.zoneChecker != null && owner.zoneChecker.IsInBuildZone;

        Bounds zb = owner.buildZoneCollider.bounds; // 월드 AABB
        float half = owner.cellSize * 0.5f;
        const float eps = 0.01f; // 경계 부동소수 오차 허용

        // 칸의 월드 중심 (1x1 크기를 주면 칸 중심)
        Vector3 c = GridMath.StartCellToWorldCenter(cell, new Vector2Int(1, 1), owner.GridOriginPos, owner.cellSize, owner.fixedY);

        // 칸의 X/Z 네 모서리가 모두 zone bounds 안에 있어야 함 (Y는 평면이라 무시)
        return c.x - half >= zb.min.x - eps && c.x + half <= zb.max.x + eps
            && c.z - half >= zb.min.z - eps && c.z + half <= zb.max.z + eps;
    }

    // footprint 칸 중 레일이 깔린 칸이 하나라도 있는지.
    public bool IsAnyCellOnRail(List<Vector2Int> cells)
    {
        if (owner.RailManager == null) return false;

        for (int i = 0; i < cells.Count; i++)
            if (owner.RailManager.HasRailAt(cells[i]))
                return true;
        return false;
    }

    // 배치 박스가 blockingMask 물체와 겹치는지 (프리뷰 자신은 제외).
    public bool IsBlocked(Vector3 centerPos, Vector2Int size, Quaternion rotation)
    {
        if (owner.blockingMask.value == 0)
            return false;

        float margin = 0.05f;
        Vector3 halfExtents = new Vector3(
            (size.x * owner.cellSize * 0.5f) - margin,
            owner.checkHeight,
            (size.y * owner.cellSize * 0.5f) - margin
        );

        Collider[] hits = Physics.OverlapBox(centerPos, halfExtents, rotation, owner.blockingMask);

        for (int i = 0; i < hits.Length; i++)
        {
            if (owner.previewMarker != null && hits[i].transform.IsChildOf(owner.previewMarker.transform))
                continue;
            return true;
        }
        return false;
    }
}
