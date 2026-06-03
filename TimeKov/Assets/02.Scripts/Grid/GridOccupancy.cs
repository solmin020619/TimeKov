// =====================================================================
// GridOccupancy.cs
// 건축 그리드에서 "어떤 칸이 점유됐는지"만 책임지는 상태 소유 클래스.
// BuildManager 가 인스턴스를 들고 설비 배치/해제 시 갱신한다.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

public class GridOccupancy
{
    private readonly HashSet<Vector2Int> cells = new HashSet<Vector2Int>();

    public bool IsOccupied(Vector2Int cell) => cells.Contains(cell);

    public bool IsAnyOccupied(List<Vector2Int> check)
    {
        for (int i = 0; i < check.Count; i++)
            if (cells.Contains(check[i]))
                return true;
        return false;
    }

    public void Occupy(List<Vector2Int> add)
    {
        for (int i = 0; i < add.Count; i++)
            cells.Add(add[i]);
    }

    public void Free(List<Vector2Int> remove)
    {
        for (int i = 0; i < remove.Count; i++)
            cells.Remove(remove[i]);
    }
}
