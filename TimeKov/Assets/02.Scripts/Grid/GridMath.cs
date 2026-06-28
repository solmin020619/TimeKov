// =====================================================================
// GridMath.cs
// 건축 그리드 좌표 변환 - 순수 함수 모음 (상태도 의존도 없음).
// origin / cellSize / fixedY 를 인자로 받아 월드<->셀 변환, footprint, 회전을 계산한다.
// BuildManager 등이 자기 그리드 파라미터를 넘겨 호출한다.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

public static class GridMath
{
    // 월드 좌표 -> 시작 셀 (size 건물의 중심이 마우스에 오도록 round 보정)
    public static Vector2Int WorldToStartCellCentered(Vector3 worldPos, Vector2Int size, Vector3 origin, float cellSize)
    {
        Vector3 local = worldPos - origin;
        int startX = Mathf.RoundToInt(local.x / cellSize - size.x * 0.5f);
        int startZ = Mathf.RoundToInt(local.z / cellSize - size.y * 0.5f);
        return new Vector2Int(startX, startZ);
    }

    // 시작 셀 + size -> 그 건물의 월드 중심
    public static Vector3 StartCellToWorldCenter(Vector2Int startCell, Vector2Int size, Vector3 origin, float cellSize, float fixedY)
    {
        float centerX = origin.x + (startCell.x + size.x * 0.5f) * cellSize;
        float centerZ = origin.z + (startCell.y + size.y * 0.5f) * cellSize;
        return new Vector3(centerX, fixedY, centerZ);
    }

    // 시작 셀 + size -> 차지하는 모든 셀 목록
    public static List<Vector2Int> Footprint(Vector2Int startCell, Vector2Int size)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int x = 0; x < size.x; x++)
            for (int z = 0; z < size.y; z++)
                cells.Add(new Vector2Int(startCell.x + x, startCell.y + z));
        return cells;
    }

    // 회전(0/90/180/270) 적용한 size. 90/270 이면 가로세로 swap
    public static Vector2Int RotatedSize(Vector2Int originalSize, int rotationY)
    {
        rotationY %= 360;
        if (rotationY == 90 || rotationY == 270)
            return new Vector2Int(originalSize.y, originalSize.x);
        return originalSize;
    }
}
