// =====================================================================
// TransmissionManager.cs
// 시간에너지 전송 시스템 전체 로직 담당 (기획서: 시간에너지 시스템)
// - TransmissionRate(전송률 0~100, 정수) 관리 — 감소하지 않음
// - 충전 키트 전송 판정 / 소모 / 전송률 상승 / 구간 보상·지역 해금 통지
// - 씬 어딘가에 하나만 배치 (싱글톤)  ─ CoreUpgradeManager 와 동일한 매니저 패턴
//
// ⚠️ 저장(persistence): 전송률·해금·보상 상태는 세이브에 남아야 하지만
//    GameSaveData.cs 는 팀원 담당 파일이라 지금은 필드를 추가하지 않는다.
//    아래 LoadState()/SaveState() 는 런타임 전용 stub 이며, 팀원과 GameSaveData
//    필드를 조율한 뒤 ISaveable 로 승격한다. (CoreUpgradeManager.Capture 참고)
// =====================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>전송 지역. 각 지역이 전체 전송률의 25% 구간을 담당한다.</summary>
public enum TransmissionRegion { Nature, Snow, Desert, Lava }

public class TransmissionManager : MonoBehaviour
{
    // ── 싱글톤 ────────────────────────────────────────────────────────
    public static TransmissionManager Instance { get; private set; }

    // ── 상수 ──────────────────────────────────────────────────────────
    public const int MaxRate = 100;

    // ── 충전 키트 정의 ────────────────────────────────────────────────
    // 실제 아이템 데이터(GameDataUtility.GetItem)가 아직 없어 placeholder 로 둔다.
    // TODO: 데이터시트에 충전 키트 아이템이 추가되면 itemId 를 실제 값으로 교체하고
    //       displayName 은 GameDataUtility.GetItem(itemId).itemName 으로 대체.
    [Serializable]
    public class ChargedKitDef
    {
        [Tooltip("가방에서 이 키트를 식별하는 아이템 ID (placeholder).")]
        public int itemId;
        [Tooltip("UI 표시 이름 (아이템 데이터 붙기 전 임시).")]
        public string displayName = "충전 키트";
        [Tooltip("이 키트가 속한 지역. 해당 지역 전송 구간에서만 사용 가능.")]
        public TransmissionRegion region;
        [Tooltip("보스 재료가 포함된 특수 키트 여부. 지역 마지막 구간(보스 구간)을 채운다.")]
        public bool isBoss;
        [Tooltip("이 키트 1개가 올리는 전송률(%). 밸런스 값.")]
        public int ratePercent = 5;
    }

    [Header("충전 키트 목록 (밸런스 값 — Inspector 에서 조정)")]
    [SerializeField]
    private List<ChargedKitDef> kitDefs = new()
    {
        new ChargedKitDef { itemId = 7001, displayName = "자연 충전키트",      region = TransmissionRegion.Nature, isBoss = false, ratePercent = 5 },
        new ChargedKitDef { itemId = 7002, displayName = "자연 보스 충전키트", region = TransmissionRegion.Nature, isBoss = true,  ratePercent = 5 },
        new ChargedKitDef { itemId = 7003, displayName = "설원 충전키트",      region = TransmissionRegion.Snow,   isBoss = false, ratePercent = 5 },
        new ChargedKitDef { itemId = 7004, displayName = "설원 보스 충전키트", region = TransmissionRegion.Snow,   isBoss = true,  ratePercent = 5 },
        new ChargedKitDef { itemId = 7005, displayName = "사막 충전키트",      region = TransmissionRegion.Desert, isBoss = false, ratePercent = 5 },
        new ChargedKitDef { itemId = 7006, displayName = "사막 보스 충전키트", region = TransmissionRegion.Desert, isBoss = true,  ratePercent = 5 },
        new ChargedKitDef { itemId = 7007, displayName = "용암 충전키트",      region = TransmissionRegion.Lava,   isBoss = false, ratePercent = 5 },
        new ChargedKitDef { itemId = 7008, displayName = "용암 보스 충전키트", region = TransmissionRegion.Lava,   isBoss = true,  ratePercent = 5 },
    };

    [Header("지역별 일반 전송 상한 (구간 시작 기준 오프셋 %)")]
    [Tooltip("각 25% 구간에서 '일반' 키트로 채울 수 있는 최대치(구간 시작 + 이 값).\n" +
             "예: 20 이면 자연맵은 0~20%까지 일반, 20~25%는 보스 키트로만 완성.")]
    [Range(0, 25)]
    [SerializeField] private int normalCapOffset = 20;

    // ── 상태 ──────────────────────────────────────────────────────────
    /// <summary>현재 시간에너지 전송률 (정수 0~100). 감소하지 않는다.</summary>
    public int TransmissionRate { get; private set; }

    // 이미 처리한 10% 정규 보상 구간 / 지역 해금 (중복 처리 방지).
    private readonly HashSet<int> _claimedMilestones = new();
    private readonly HashSet<TransmissionRegion> _unlockedRegions = new();
    private bool _endingFired;

    // ── 이벤트 (UI / 피드백에서 구독) ─────────────────────────────────
    /// <summary>전송률이 바뀔 때. 인자 = 새 전송률(%).</summary>
    public static event Action<int> OnRateChanged;
    /// <summary>10% 단위 정규 보상 구간을 통과했을 때. 인자 = 달성 %(10,20,...).</summary>
    public static event Action<int> OnRewardMilestone;
    /// <summary>25/50/75% 달성으로 다음 지역이 해금됐을 때. 인자 = 해금된 지역.</summary>
    public static event Action<TransmissionRegion> OnRegionUnlocked;
    /// <summary>전송률 100% 달성(엔딩 트리거) 시 1회.</summary>
    public static event Action OnEndingReached;

    // ── 라이프사이클 ──────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadState();
    }

    private void OnDestroy()
    {
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

        // ③ 전송률 상승 + 보상/해금 처리
        ApplyRate(projected);
        ToastManager.Success("시간에너지 전송이 완료되었습니다.");
        SaveState();
        return true;
    }

    // ── 내부 처리 ─────────────────────────────────────────────────────

    // 전송률을 newRate 로 올린다(감소 불가). 통과한 10% 보상 구간과 25/50/75/100 해금을
    // 낮은 구간부터 순서대로, 이미 처리한 건 건너뛰며 통지한다.
    private void ApplyRate(int newRate)
    {
        newRate = Mathf.Clamp(newRate, TransmissionRate, MaxRate);
        if (newRate == TransmissionRate) return;

        int prev = TransmissionRate;
        TransmissionRate = newRate;
        OnRateChanged?.Invoke(TransmissionRate);

        // 10% 단위 정규 보상 — prev 초과 ~ 현재 이하 구간을 순서대로
        for (int m = 10; m <= 100; m += 10)
        {
            if (m > prev && m <= TransmissionRate && _claimedMilestones.Add(m))
            {
                // TODO 보상 지급: 설비 설계도/아이템 등 실제 지급은 보상 시스템 확정 후 연결.
                OnRewardMilestone?.Invoke(m);
            }
        }

        // 25/50/75% — 다음 지역 해금 (한 번만)
        TryUnlockAt(25, TransmissionRegion.Snow);
        TryUnlockAt(50, TransmissionRegion.Desert);
        TryUnlockAt(75, TransmissionRegion.Lava);

        // 100% — 엔딩 (1회)
        if (TransmissionRate >= MaxRate && !_endingFired)
        {
            _endingFired = true;
            OnEndingReached?.Invoke();
        }
    }

    private void TryUnlockAt(int threshold, TransmissionRegion region)
    {
        if (TransmissionRate >= threshold && _unlockedRegions.Add(region))
        {
            // TODO 지역 해금: 실제 이동 목적지 활성화는 지역/이동 시스템 확정 후 연결.
            OnRegionUnlocked?.Invoke(region);
        }
    }

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

    // ── 저장 seam (팀원과 GameSaveData 조율 후 ISaveable 로 승격) ───────
    // 현재는 런타임 전용 — 재시작 시 0%부터. 정식 저장은 아래 TODO 참고.
    private void LoadState()
    {
        // TODO: SaveSlotManager.Instance.Data 의 전송 필드에서 복원.
        //   TransmissionRate = data.transmissionRate;
        //   _claimedMilestones / _unlockedRegions 도 복원.
    }

    private void SaveState()
    {
        // TODO: GameSaveData 에 필드 추가 후 SaveSlotManager.Instance?.SaveActive();
    }
}
