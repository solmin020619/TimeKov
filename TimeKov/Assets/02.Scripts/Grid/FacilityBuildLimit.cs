// =====================================================================
// FacilityBuildLimit.cs
// 설비별 설치 개수 상한. 기본 = 무제한, 제한이 필요한 설비만 등록.
// 현재: 창고 출력 포트(9) = 기본 1개.
// 우주선 수리 등 진행 보상이 IncreaseMax/SetMax 로 상한을 올린다(확장 포인트).
// 상한의 영속화(세이브)는 상한을 올리는 시스템(우주선 수리)이 생길 때 함께 붙인다.
// =====================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

public static class FacilityBuildLimit
{
    public const int WarehousePortId = 9;

    // 기본 상한(새 게임 기준). 여기 없는 설비 = 무제한.
    private static readonly Dictionary<int, int> _max = new Dictionary<int, int>
    {
        { WarehousePortId, 1 },
    };

    /// <summary>상한이 바뀔 때(우주선 수리 보상 등). 건축 바 UI 갱신용.</summary>
    public static event Action OnChanged;

    public static bool HasLimit(int facilityId) => _max.ContainsKey(facilityId);

    public static int GetMax(int facilityId)
        => _max.TryGetValue(facilityId, out var m) ? m : int.MaxValue;

    public static void SetMax(int facilityId, int max)
    {
        _max[facilityId] = Mathf.Max(0, max);
        OnChanged?.Invoke();
    }

    /// <summary>진행 보상용: 상한 +delta. (예: 우주선 수리 단계 보상으로 창고포트 +1)</summary>
    public static void IncreaseMax(int facilityId, int delta = 1)
        => SetMax(facilityId, HasLimit(facilityId) ? GetMax(facilityId) + delta : delta);
}
