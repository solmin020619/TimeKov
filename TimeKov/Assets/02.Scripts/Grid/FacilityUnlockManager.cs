using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설비 해금 상태를 관리하는 싱글턴.
/// 해금된 설비를 획득 순서대로 저장하며, BuildManager의 퀵슬롯(1~9)과 연동된다.
/// </summary>
public class FacilityUnlockManager : MonoBehaviour, ISaveable
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

        SaveSlotManager.Instance?.Register(this);
        RestoreFromSave();
        SeedDefaultUnlocks();   // 기본 해금 설비(추출기1/배양기2) 보장 - 조용히(이벤트/토스트/도감알림 없음)
    }

    private void OnDestroy()
    {
        SaveSlotManager.Instance?.Unregister(this);
    }

    // ── 세이브/로드 (ISaveable) ───────────────────────────────────────────
    // BuildManager.Start()(InitDynamicUnlockSlots)가 IsUnlocked()를 읽기 전에 끝나야 하므로
    // Awake에서 복원한다 — 같은 씬의 모든 Awake는 어떤 Start보다 먼저 실행되므로 순서 안전.

    public void Capture(GameSaveData data)
    {
        data.unlockedFacilityIds = new List<int>(_unlockedIds);
    }

    private void RestoreFromSave()
    {
        if (SaveSlotManager.Instance == null || !SaveSlotManager.Instance.HasActiveSlot) return;

        // 이벤트/토스트/도감알림 없이 조용히 복원 — "새로 해금"이 아니라 "이미 해금된 상태"이므로.
        _unlockedIds.Clear();
        foreach (int id in SaveSlotManager.Instance.Data.unlockedFacilityIds)
            if (!_unlockedIds.Contains(id))
                _unlockedIds.Add(id);
    }

    // 시작부터 해금돼 있는 기본 설비. 튜토에서 추출기1/배양기2 '이동/해금' 단계를 제거했으므로
    // 새 슬롯이든 기존 슬롯이든 이 둘은 항상 퀵슬롯에 있게 조용히 보장한다(이벤트/토스트/도감알림 없음).
    private static readonly int[] DefaultUnlockedIds = { 1, 2 };

    private void SeedDefaultUnlocks()
    {
        foreach (int id in DefaultUnlockedIds)
            if (id >= 1 && id <= MaxSlots && !_unlockedIds.Contains(id))
                _unlockedIds.Add(id);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        // [임시 디버그] ~(백쿼트) 키 -> 데이터에 존재하는 모든 설비(1~9) 즉시 해금. 정식 빌드 차단(에디터/Dev빌드만).
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            for (int id = 1; id <= MaxSlots; id++)
            {
                if (GameDataHolder.I != null && GameDataHolder.I.FacilityData.TryGet(id.ToString(), out _))
                    TryUnlock(id);
            }
        }
    }
#endif

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

        OnFacilityUnlocked?.Invoke(facilityId, slotIndex);
        GameEvents.RaiseFacilityUnlocked(facilityId);   // 튜토리얼 등 전역 구독자 통지
        CodexNotice.MarkUnseen(CodexNotice.Facility);   // 도감 알림(!) - 설비 탭에 새 항목

        string facilityName = null;
        if (GameDataHolder.I != null && GameDataHolder.I.FacilityData.TryGet(facilityId.ToString(), out var fdata))
            facilityName = fdata.facilityName;
        ToastManager.Success(string.IsNullOrEmpty(facilityName)
            ? "설비를 해금했습니다!"
            : $"{facilityName} 설비를 해금했습니다!");
        return true;
    }
}
