// =====================================================================
// TransmissionManager.cs
// 시간에너지 전송 시스템 전체 로직 담당 (기획서: 시간에너지 시스템)
// - TransmissionRate(전송률 0~100, 정수) 관리 — 감소하지 않음
// - 충전 키트 전송 판정 / 소모 / 전송률 상승 / 구간 보상·지역 해금 통지
// - 씬 어딘가에 하나만 배치 (싱글톤)  ─ CoreUpgradeManager 와 동일한 매니저 패턴
//
// 저장(persistence): ISaveable 로 GameSaveData.transmissionRate 에 전송률을 저장한다.
//    수령한 10% 마일스톤은 전송률 이하로 도출(재지급 방지). 설비해금/아이템/부품 등 실제 보상
//    효과는 각 시스템(FacilityUnlockManager/인벤/ShipRepair)이 따로 저장한다.
//    런타임 정적 효과(창고포트 상한 +1)만 복원 시 재적용한다.
// =====================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>전송 지역. 각 지역이 전체 전송률의 25% 구간을 담당한다.</summary>
public enum TransmissionRegion { Nature, Snow, Desert, Lava }

public class TransmissionManager : MonoBehaviour, ISaveable
{
    // ── 싱글톤 ────────────────────────────────────────────────────────
    public static TransmissionManager Instance { get; private set; }

    // ── 상수 ──────────────────────────────────────────────────────────
    public const int MaxRate = 100;

    // ── 충전 키트 정의 ────────────────────────────────────────────────
    // 표시 이름은 ItemData 시트(itemName)에서 GetKitName 으로 자동 조회한다.
    // 여기(kitDefs)엔 실제 itemId + 지역/보스/상승률만 등록한다(시트에 없는 값).
    [Serializable]
    public class ChargedKitDef
    {
        [Tooltip("가방에서 이 키트를 식별하는 실제 아이템 ID (ItemData 시트 기준).")]
        public int itemId;
        [Tooltip("표시 이름 폴백. 비우면 ItemData 시트(itemName)에서 자동으로 가져온다.")]
        public string displayName = "";
        [Tooltip("이 키트가 속한 지역. 해당 지역 전송 구간에서만 사용 가능.")]
        public TransmissionRegion region;
        [Tooltip("보스 재료가 포함된 특수 키트 여부. 지역 마지막 구간(보스 구간)을 채운다.")]
        public bool isBoss;
        [Tooltip("이 키트 1개가 올리는 전송률(%). 밸런스 값.")]
        public int ratePercent = 5;
    }

    // 충전 키트 목록 — 실제 아이템이 ItemData 시트에 추가되면 여기에 등록한다.
    //   표시 이름은 시트(itemName)에서 자동으로 가져오므로(GetKitName) 여기선 지정 불필요.
    //   지역/보스/상승률은 시트에 없는 값이라 등록 시 지정한다.
    //   예) new ChargedKitDef { itemId = <실제 itemId>, region = TransmissionRegion.Nature, isBoss = false, ratePercent = 5 }
    [Header("충전 키트 등록 (실제 itemId + 지역/보스/상승률). 이름은 시트에서 자동)")]
    [SerializeField]
    private List<ChargedKitDef> kitDefs = new();

    [Header("지역별 일반 전송 상한 (구간 시작 기준 오프셋 %)")]
    [Tooltip("각 25% 구간에서 '일반' 키트로 채울 수 있는 최대치(구간 시작 + 이 값).\n" +
             "예: 20 이면 자연맵은 0~20%까지 일반, 20~25%는 보스 키트로만 완성.")]
    [Range(0, 25)]
    [SerializeField] private int normalCapOffset = 20;

    // ── 상태 ──────────────────────────────────────────────────────────
    /// <summary>현재 시간에너지 전송률 (정수 0~100). 감소하지 않는다.</summary>
    public int TransmissionRate { get; private set; }

    // 이미 처리한 10% 정규 보상 구간 (중복 지급 방지).
    private readonly HashSet<int> _claimedMilestones = new();
    private bool _endingFired;

    // ── 이벤트 (UI / 피드백에서 구독) ─────────────────────────────────
    /// <summary>전송률이 바뀔 때. 인자 = 새 전송률(%).</summary>
    public static event Action<int> OnRateChanged;
    /// <summary>10% 단위 정규 보상 구간을 통과했을 때. 인자 = 달성 %(10,20,...).</summary>
    public static event Action<int> OnRewardMilestone;
    /// <summary>전송률 100% 달성(엔딩 트리거) 시 1회.</summary>
    public static event Action OnEndingReached;

    // ── 라이프사이클 ──────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        EnsureKitDefs();
        SaveSlotManager.Instance?.Register(this);
        RestoreFromSave();
    }

    // kitDefs 가 비어 있으면(인스펙터 미등록) 아이템 ID 규약으로 자동 구성한다.
    //   83xx = 지역 '일반' 충전 키트, 84xx = 지역 '특수(보스)' 충전 키트.
    //   끝자리 1/2/3/4 = 자연/설원/사막/용암(TransmissionRegion 순서와 동일).
    //   표시 이름은 GetKitName 이 시트(itemName)에서 가져오므로 여기선 지정하지 않는다.
    //   ★인스펙터에 하나라도 등록돼 있으면 그 값을 존중하고 자동 구성은 건너뛴다.
    private void EnsureKitDefs()
    {
        if (kitDefs != null && kitDefs.Count > 0) return;
        kitDefs = new List<ChargedKitDef>(8);
        for (int region = 0; region < 4; region++)
        {
            kitDefs.Add(new ChargedKitDef { itemId = 8301 + region, region = (TransmissionRegion)region, isBoss = false, ratePercent = 5 });
            kitDefs.Add(new ChargedKitDef { itemId = 8401 + region, region = (TransmissionRegion)region, isBoss = true,  ratePercent = 5 });
        }
    }

    private void OnDestroy()
    {
        SaveSlotManager.Instance?.Unregister(this);
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        // [Dev/테스트] F3 = 전송률 +5% (키트/조건 무시). 정식 빌드 차단.
        if (Input.GetKeyDown(KeyCode.F3))
            DevAddRate(5);
    }

    /// <summary>[테스트용] 전송률을 강제로 올린다 — 보상/해금 통지도 정상 발생.</summary>
    public void DevAddRate(int delta) => ApplyRate(TransmissionRate + delta);
#endif

    // ── 조회 API (UI 표시용) ──────────────────────────────────────────

    /// <summary>현재 전송률이 속한 지역 (0~25=자연 ... 75~100=용암). 100%면 용암.</summary>
    public TransmissionRegion CurrentRegion => RegionOf(Mathf.Min(TransmissionRate, MaxRate - 1));

    /// <summary>지역 구간의 시작 % (자연 0 / 설원 25 / 사막 50 / 용암 75).</summary>
    public static int RegionStart(TransmissionRegion r) => (int)r * 25;

    /// <summary>지역 구간의 목표 % (자연 25 / 설원 50 / 사막 75 / 용암 100).</summary>
    public static int RegionGoal(TransmissionRegion r) => RegionStart(r) + 25;

    /// <summary>현재 지역에서 '일반' 키트로 채울 수 있는 상한 %(절대값).</summary>
    public int CurrentRegionNormalCap => RegionStart(CurrentRegion) + normalCapOffset;

    /// <summary>현재 지역 목표 %(절대값).</summary>
    public int CurrentRegionGoal => RegionGoal(CurrentRegion);

    /// <summary>가방에 든 이 아이템 개수.</summary>
    public int GetOwnedCount(int itemId)
        => InventoryManager.Instance != null ? InventoryManager.Instance.GetTotalItemCount(itemId) : 0;

    /// <summary>정의된 전 키트 목록(읽기 전용). UI 목록 구성용.</summary>
    public IReadOnlyList<ChargedKitDef> KitDefs => kitDefs;

    /// <summary>가방에 1개 이상 보유한 키트만 반환. UI '보유 키트 목록' 용.</summary>
    public IEnumerable<ChargedKitDef> GetOwnedKits()
    {
        foreach (var k in kitDefs)
            if (k != null && GetOwnedCount(k.itemId) > 0)
                yield return k;
    }

    /// <summary>키트 표시 이름 — ItemData 시트(itemName)에서 우선 조회. 없으면 displayName/기본값.</summary>
    public string GetKitName(ChargedKitDef kit)
    {
        if (kit == null) return "";
        if (GameDataHolder.I != null && GameDataHolder.I.ItemData.TryGet(kit.itemId.ToString(), out var d) && !string.IsNullOrEmpty(d.itemName))
            return d.itemName;
        return string.IsNullOrEmpty(kit.displayName) ? "충전 키트" : kit.displayName;
    }

    /// <summary>이 키트를 전송하면 도달할 전송률(%). 실패 조건이면 현재 전송률 그대로.</summary>
    public int GetProjectedRate(ChargedKitDef kit)
    {
        if (kit == null) return TransmissionRate;
        int cap = SegmentCapFor(kit);
        return Mathf.Clamp(TransmissionRate + Mathf.Max(0, kit.ratePercent), TransmissionRate, cap);
    }

    /// <summary>이 키트를 지금 전송할 수 있는지. reason 에 불가 사유를 담는다(UI 안내용).</summary>
    public bool CanTransmit(ChargedKitDef kit, out string reason)
    {
        reason = null;
        if (kit == null) { reason = "전송 가능한 시간에너지 키트가 없습니다."; return false; }

        if (TransmissionRate >= MaxRate)
        {
            reason = "시간에너지 전송률 100%를 달성했습니다.";
            return false;
        }
        if (kit.region != CurrentRegion)
        {
            reason = "현재 전송 구간에서 사용할 수 없는 키트입니다.";
            return false;
        }
        if (!kit.isBoss && TransmissionRate >= CurrentRegionNormalCap)
        {
            // 일반 상한 도달 — 남은 구간은 보스 키트로만.
            reason = "지역 보스 재료가 포함된 특수 키트가 필요합니다.";
            return false;
        }
        if (GetOwnedCount(kit.itemId) <= 0)
        {
            reason = "전송 가능한 시간에너지 키트가 없습니다.";
            return false;
        }
        if (GetProjectedRate(kit) <= TransmissionRate)
        {
            // 이미 이 구간 상한이라 더 못 올림
            reason = "해당 지역의 시간에너지 전송을 완료했습니다.";
            return false;
        }
        return true;
    }

    // ── 전송 실행 ─────────────────────────────────────────────────────

    /// <summary>
    /// 충전 키트 1개를 전송한다. 성공 시 키트 소모 + 전송률 상승 + 보상/해금 통지.
    /// 전송 실패(조건 불충족)면 키트를 소모하지 않고 false 반환.
    /// </summary>
    public bool TryTransmit(int kitItemId)
    {
        var kit = FindKit(kitItemId);
        if (kit == null) return false;

        // ① 조건 검사
        if (!CanTransmit(kit, out string reason))
        {
            if (!string.IsNullOrEmpty(reason)) ToastManager.Warning(reason);
            return false;
        }

        int projected = GetProjectedRate(kit);

        // ② 키트 소모 (충전 키트는 플레이어 가방에서만 — 창고/레일 대상 아님)
        if (InventoryManager.Instance == null || !InventoryManager.Instance.TryConsumeItem(kitItemId, 1))
        {
            Debug.LogError($"[Transmission] 키트 소모 실패 itemId={kitItemId}");
            return false;
        }

        // ③ 전송률 상승 + 보상 지급 (상태 변경은 SaveSlotManager 자동 저장이 캡처)
        ApplyRate(projected);
        ToastManager.Success("시간에너지 전송이 완료되었습니다.");
        return true;
    }

    // ── 내부 처리 ─────────────────────────────────────────────────────

    /// <summary>보상이 걸린 전송률 마일스톤(불균등 5/10/25 간격). ApplyRate/복원/UI 가 공유하는 단일 소스.</summary>
    public static readonly int[] RewardMilestones = { 5, 10, 15, 20, 25, 30, 40, 50, 60, 70, 75, 80, 90, 100 };

    // 전송률을 newRate 로 올린다(감소 불가). 통과한 보상 마일스톤을 낮은 것부터 순서대로,
    // 이미 처리한 건 건너뛰며 지급/통지한다.
    private void ApplyRate(int newRate)
    {
        newRate = Mathf.Clamp(newRate, TransmissionRate, MaxRate);
        if (newRate == TransmissionRate) return;

        int prev = TransmissionRate;
        TransmissionRate = newRate;
        OnRateChanged?.Invoke(TransmissionRate);

        // 보상 마일스톤 — prev 초과 ~ 현재 이하 구간을 순서대로 실제 지급.
        foreach (int m in RewardMilestones)
        {
            if (m > prev && m <= TransmissionRate && _claimedMilestones.Add(m))
            {
                GrantMilestoneRewards(m);      // 설비 해금·우주선 부품·아이템 등 실제 지급
                OnRewardMilestone?.Invoke(m);  // UI 리빌 카드 연출
            }
        }

        // 100% — 엔딩 (1회)
        if (TransmissionRate >= MaxRate && !_endingFired)
        {
            _endingFired = true;
            OnEndingReached?.Invoke();
        }
    }

    // ── 마일스톤 보상 지급 (기획표 2026-07-24 확정) ────────────────────────
    // 각 % 도달 시 1회. 설비는 facilityId 직접 해금(이름 매칭 폐기 - 시트 개명 시 깨지던 버그 방지),
    // 우주선 부품은 수리 매니저로 바로 흡수, 창고포트 상한 증설(70/80/90 다단계),
    // 앰플 꾸러미·코어 키트는 가방 지급, 귀환석은 레벨 세팅.
    // ★시간에너지 합성기(3, 충전키트 제조기)는 5% 최초 보상 — 튜토가 스타터 키트로 5% 도달시켜 부트스트랩(순환잠금 회피).
    // ★튜토리얼(생체추출기1·생체배양기2)은 튜토에서 지급 — 여기서 건드리지 않는다.
    private void GrantMilestoneRewards(int pct)
    {
        // 설비 해금(공유 소스 — UI 카드 아이콘도 같은 목록을 쓴다)
        foreach (int fid in MilestoneFacilityIds(pct))
            FacilityUnlockManager.Instance?.TryUnlock(fid);

        // 설비 외 보상
        switch (pct)
        {
            case 20: ReturnStoneManager.Instance?.SetLevel(1); break;                            // 귀환석 Lv.1(쿨 15분) - 자연 탐험 안전망
            case 25: ShipRepairManager.Instance?.CollectExtraPart(3); break;                     // 선체 보강재(수리 Lv.3)
            case 40: ReturnStoneManager.Instance?.SetLevel(2); break;                            // 귀환석 Lv.2(쿨 10분)
            case 50: ShipRepairManager.Instance?.CollectExtraPart(4); break;                     // 동력 안정기(Lv.4)
            case 70:                                                                             // 창고포트 상한 +1(총2) + 귀환석 Lv.3(쿨 5분)
                FacilityBuildLimit.IncreaseMax(FacilityBuildLimit.WarehousePortId, 1);
                ReturnStoneManager.Instance?.SetLevel(3);
                break;
            case 75: ShipRepairManager.Instance?.CollectExtraPart(5); break;                     // 엔진(Lv.5)
            case 80: FacilityBuildLimit.IncreaseMax(FacilityBuildLimit.WarehousePortId, 3); break; // 창고포트 상한 +3(총5) - 극후반 파격
            case 90:                                                                             // 창고포트 상한 +2(총7) + 앰플 꾸러미 + 코어 키트 V x5
                FacilityBuildLimit.IncreaseMax(FacilityBuildLimit.WarehousePortId, 2);
                GrantSupplyPackage();
                GrantItemById(CoreKitVId, 5);
                break;
            // 100%: 엔딩(_endingFired 처리)
        }
    }

    private const int CoreKitVId = 6105;   // 코어 키트 V (코어 레벨 9/10 강화 재료, 1회 시도당 1개 소모)

    // 각 마일스톤이 해금하는 설비 id(없으면 빈 배열). 지급 로직과 UI 카드가 공유하는 단일 소스.
    //   이름 매칭 대신 id 직접 참조 — FacilityData 이름이 바뀌어도 안 깨진다.
    //   5=시간에너지합성기(3, 최초·부트스트랩), 10=용해로(6), 15=창고출력포트(9), 20=저장고(8),
    //   30=코어합성기(7), 60=생체분리기(4)+에너지변환기(5, 둘이 짝이라 함께).
    private static int[] MilestoneFacilityIds(int pct) => pct switch
    {
        5  => new[] { 3 },
        10 => new[] { 6 },
        15 => new[] { 9 },
        20 => new[] { 8 },
        30 => new[] { 7 },
        60 => new[] { 4, 5 },
        _  => System.Array.Empty<int>(),
    };

    /// <summary>UI 리빌 카드용: 이 마일스톤이 해금하는 설비 id 목록. 없으면 빈 리스트.</summary>
    public List<int> GetRewardFacilityIds(int pct) => new List<int>(MilestoneFacilityIds(pct));

    // 90% 앰플 꾸러미(증량) — 최종 보스 앞 대량 보급. 이름은 아이템 시트(itemName) 기준.
    private void GrantSupplyPackage()
    {
        GrantItemByName("고급 회복 앰플", 20);
        GrantItemByName("공격력 앰플", 5);
        GrantItemByName("방어력 앰플", 5);
        GrantItemByName("스태미나 앰플", 5);
    }

    // itemId 로 가방에 직접 지급(이름 조회 불필요한 확정 아이템용).
    private void GrantItemById(int itemId, int amount)
    {
        if (InventoryManager.Instance == null) { Debug.LogWarning($"[Transmission] 아이템 지급 불가(인벤 없음): {itemId}"); return; }
        InventoryManager.Instance.AddItem(itemId, amount, markAsNew: true);
    }

    // 이름으로 아이템을 찾아 가방에 지급. 공백 차이는 무시하고 매칭.
    private void GrantItemByName(string itemName, int amount)
    {
        var idata = GameDataHolder.I != null ? GameDataHolder.I.ItemData : null;
        if (idata == null || InventoryManager.Instance == null) { Debug.LogWarning($"[Transmission] 아이템 지급 불가: {itemName}"); return; }

        string target = Norm(itemName);
        foreach (var d in idata.All)
            if (Norm(d.itemName) == target && int.TryParse(d.SheetId.ToString(), out int itemId))
            {
                InventoryManager.Instance.AddItem(itemId, amount, markAsNew: true);
                return;
            }
        Debug.LogWarning($"[Transmission] ItemData 에서 '{itemName}' 아이템을 못 찾음 — 시트 이름 확인 필요.");
    }

    private static string Norm(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(" ", "");

    private int SegmentCapFor(ChargedKitDef kit)
    {
        // 일반 키트는 지역 일반 상한까지, 보스 키트는 지역 목표(구간 끝)까지 채운다.
        int baseStart = RegionStart(kit.region);
        return kit.isBoss ? baseStart + 25 : baseStart + normalCapOffset;
    }

    private ChargedKitDef FindKit(int itemId)
    {
        foreach (var k in kitDefs)
            if (k != null && k.itemId == itemId) return k;
        return null;
    }

    private static TransmissionRegion RegionOf(int rate)
    {
        int idx = Mathf.Clamp(rate / 25, 0, 3);
        return (TransmissionRegion)idx;
    }

    // ── 통합 세이브 (ISaveable) ────────────────────────────────────────
    // 슬롯 기반. 활성 슬롯 없으면(World 씬 직접 Play) 복원 스킵 = 매번 0%(샌드박스).
    // 저장 시점은 SaveSlotManager 가 관리(자동 30초 + 앱 종료). 여기선 상태만 바꾸면 캡처된다.
    public void Capture(GameSaveData data)
    {
        if (data == null) return;
        data.transmissionRate          = TransmissionRate;
        data.transmissionEndingReached = _endingFired;
    }

    private void RestoreFromSave()
    {
        var mgr = SaveSlotManager.Instance;
        if (mgr == null || !mgr.HasActiveSlot) return;   // 활성 슬롯 없으면 기본값(0%) 유지

        TransmissionRate = Mathf.Clamp(mgr.Data.transmissionRate, 0, MaxRate);
        _endingFired     = mgr.Data.transmissionEndingReached;

        // 전송률 이하 마일스톤은 이미 수령한 것으로 표시(재지급 방지).
        // 설비해금/아이템/부품 등 실제 보상 효과는 각 시스템이 따로 저장·복원한다.
        foreach (int m in RewardMilestones)
            if (m <= TransmissionRate) _claimedMilestones.Add(m);

        // 런타임 정적(FacilityBuildLimit)은 static 이라 앱 재시작엔 리셋되지만 같은 앱 재진입엔 안 된다.
        // 그래서 창고포트 상한 보상(70:+1 / 80:+3 / 90:+2)은 가산이 아니라 "기본값+누적보너스" 절대값으로 재적용.
        int portBonus = 0;
        if (TransmissionRate >= 70) portBonus += 1;
        if (TransmissionRate >= 80) portBonus += 3;
        if (TransmissionRate >= 90) portBonus += 2;
        if (portBonus > 0)
            FacilityBuildLimit.SetMax(FacilityBuildLimit.WarehousePortId,
                                      FacilityBuildLimit.DefaultMax(FacilityBuildLimit.WarehousePortId) + portBonus);
    }
}
