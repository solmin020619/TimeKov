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

    // ── 청사진(배치 묶음) 회전 ──────────────────────────────────────
    // 유니티 Y+90 회전 = 위에서 볼 때 시계방향 = 셀 (x,z) -> (z,-x).
    // 아래 세 함수는 전부 이 한 가지 규약이다. 청사진 리졸버만 호출한다.
    // 지난 청사진이 죽은 원인 중 하나가 회전 수학이 세 벌로 흩어져 서로 어긋난 것이라,
    // 여기 말고 다른 곳에 회전 구현을 또 만들지 마라.

    // 임의 각도를 0/90/180/270 으로 스냅 (음수/354도 같은 부동소수 잔차 방어)
    public static int Normalize90(int deg)
    {
        int r = deg % 360;
        if (r < 0) r += 360;
        return ((r + 45) / 90 * 90) % 360;
    }

    // 1x1 셀 오프셋을 원점 기준 회전 (레일 셀 위치용)
    public static Vector2Int RotateCellOffset(Vector2Int v, int rotationY)
    {
        switch (Normalize90(rotationY))
        {
            case 90:  return new Vector2Int(v.y, -v.x);
            case 180: return new Vector2Int(-v.x, -v.y);
            case 270: return new Vector2Int(-v.y, v.x);
            default:  return v;
        }
    }

    // 사각형(시작셀 + 크기)을 원점 기준 회전한 뒤의 새 시작(최소) 셀.
    // 회전 후 크기는 RotatedSize 로 따로 구한다. size 는 회전 전 크기를 넘긴다.
    public static Vector2Int RotateRectStart(Vector2Int start, Vector2Int size, int rotationY)
    {
        switch (Normalize90(rotationY))
        {
            case 90:  return new Vector2Int(start.y, -(start.x + size.x - 1));
            case 180: return new Vector2Int(-(start.x + size.x - 1), -(start.y + size.y - 1));
            case 270: return new Vector2Int(-(start.y + size.y - 1), start.x);
            default:  return start;
        }
    }

    // 레일 연결 4방향 회전. 셀 회전과 같은 규약: 90도에서 up 이 right 가 된다.
    public static void RotateRailDirs(int rotationY, ref bool up, ref bool down, ref bool left, ref bool right)
    {
        bool u = up, d = down, l = left, r = right;
        switch (Normalize90(rotationY))
        {
            case 90:  up = l; right = u; down = r; left = d; break;
            case 180: up = d; down = u; left = r; right = l; break;
            case 270: up = r; right = d; down = l; left = u; break;
        }
    }
}
