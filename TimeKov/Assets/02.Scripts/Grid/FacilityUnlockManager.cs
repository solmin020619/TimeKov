using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설비 해금 상태를 관리하는 싱글턴.
/// 해금된 설비를 획득 순서대로 저장하며, BuildManager의 퀵슬롯(1~9)과 연동된다.
/// 저장 기능 없음 — 매 세션 초기화.
/// </summary>
public class FacilityUnlockManager : MonoBehaviour
{
    public static FacilityUnlockManager Instance { get; private set; }

    public const int MaxSlots = 9;

    // 해금된 facilityId 목록 (획득 순서 = 슬롯 인덱스)
    private readonly List<int> _unlockedIds = new List<int>();

    /// <summary>설비 해금 시 발생. (facilityId, 슬롯 인덱스 0-based)</summary>
    public event System.Action<int, int> OnFacilityUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── 조회 ─────────────────────────────────────────────────────

    /// <summary>이미 해금된 설비인지 확인.</summary>
    public bool IsUnlocked(int facilityId) => _unlockedIds.Contains(facilityId);

    /// <summary>퀵슬롯이 가득 찼는지 확인.</summary>
    public bool IsFull => _unlockedIds.Count >= MaxSlots;

    /// <summary>해금된 facilityId 목록 (읽기 전용).</summary>
    public IReadOnlyList<int> UnlockedIds => _unlockedIds;

    // ── 해금 ─────────────────────────────────────────────────────

    /// <summary>
    /// 설비를 해금한다.
    /// facilityId가 그대로 슬롯 번호가 된다 (facilityId 1 → 슬롯 1번, facilityId 8 → 슬롯 8번).
    /// 이미 해금됐거나 facilityId가 범위를 벗어나면 false 반환.
    /// </summary>
    public bool TryUnlock(int facilityId)
    {
        if (IsUnlocked(facilityId))
        {
            Debug.Log($"[FacilityUnlockManager] facilityId={facilityId} 이미 해금됨.");
            return false;
        }

        // facilityId 1 → slotIndex 0, facilityId 8 → slotIndex 7 (고정 배정)
        int slotIndex = facilityId - 1;
        if (slotIndex < 0 || slotIndex >= MaxSlots)
        {
            Debug.LogWarning($"[FacilityUnlockManager] facilityId={facilityId}는 슬롯 범위(1~{MaxSlots})를 벗어남. 해금 불가.");
            return false;
        }

        _unlockedIds.Add(facilityId);

        Debug.Log($"[FacilityUnlockManager] facilityId={facilityId} 해금 → 슬롯 {slotIndex + 1}번 (고정)");
        OnFacilityUnlocked?.Invoke(facilityId, slotIndex);
        GameEvents.RaiseFacilityUnlocked(facilityId);   // 튜토리얼 등 전역 구독자 통지

        string facilityName = null;
        if (GameDataHolder.I != null && GameDataHolder.I.FacilityData.TryGet(facilityId.ToString(), out var fdata))
            facilityName = fdata.facilityName;
        ToastManager.Success(string.IsNullOrEmpty(facilityName)
            ? "설비를 해금했습니다!"
            : $"{facilityName} 설비를 해금했습니다!");
        return true;
    }
}
