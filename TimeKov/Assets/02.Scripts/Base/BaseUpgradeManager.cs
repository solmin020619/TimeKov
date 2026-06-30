// =====================================================================
// BaseUpgradeManager.cs
// 기지(핵심 구역) 업그레이드 — 재료를 제출하면 구역을 확장하거나
// 창고 추출기 개수를 늘린다. (임시 기능: 데이터시트 대신 인스펙터 항목 정의)
//
// - 구역 확장: 기존 BuildZoneProgression.ApplyStage() 재사용 (크기조정·세이브 그대로).
// - 창고 추출기: 지정 프리팹을 구역 테두리 바깥에 고정 배치(벨트 자동연결 없음).
// - 재료 소모: CoreUpgradeManager 와 동일하게 가방 → 창고 순서로 차감.
//
// 소유/구매가능 판정은 별도 저장 없이 파생:
//   구역확장 = 현재 BuildZoneProgression 단계, 추출기 = PlayerPrefs 저장 개수.
// =====================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

public class BaseUpgradeManager : MonoBehaviour
{
    public static BaseUpgradeManager Instance { get; private set; }

    // 임시 기능 — GameSaveData(작업 중)에 의존하지 않도록 추출기 개수는 PlayerPrefs 에 저장.
    // (GameSaveData 확정되면 슬롯 세이브로 옮기면 됨. 구역 확장 단계는 기존 buildZoneStageIndex 가 담당)
    private const string PREF_EXTRACTOR_COUNT = "BaseUpgrade.ExtractorCount";

    public enum UpgradeKind { ZoneExpand, StorageExtractor }
    public enum ItemState   { Owned, Available, Locked }

    [Serializable]
    public class MaterialReq
    {
        [Tooltip("필요 재료 아이템 id")]
        public int itemId = 0;
        [Tooltip("필요 수량")]
        public int amount = 1;
    }

    [Serializable]
    public class UpgradeDef
    {
        [Tooltip("UI 표시 이름. 예: 구역 확장 · 2")]
        public string displayName = "업그레이드";
        [Tooltip("그룹 헤더. 같은 문자열끼리 한 묶음으로 표시. 예: 핵심 구역 면적 / 창고 입출력 라인 건설")]
        public string groupLabel = "";
        public UpgradeKind kind = UpgradeKind.ZoneExpand;

        [Tooltip("[구역 확장] 적용할 BuildZoneProgression 단계 인덱스(목표 tier). 직전 단계가 완료돼야 구매 가능.")]
        public int targetZoneStage = 1;
        [Tooltip("[창고 추출기] 구매 후 도달할 총 추출기 개수(목표 tier). 직전 개수일 때만 구매 가능.")]
        public int targetExtractorCount = 1;

        [Tooltip("필요 재료 목록(여러 개 가능). 가방+창고 합산으로 검사·차감. 비우면 재료 불필요.")]
        public List<MaterialReq> materials = new();
    }

    [Header("업그레이드 항목 (기획자 편집)")]
    [SerializeField] private List<UpgradeDef> upgrades = new();

    [Header("참조")]
    [Tooltip("구역 확장 대상. 비우면 씬에서 자동 탐색.")]
    [SerializeField] private BuildZoneProgression zoneProgression;
    [Tooltip("창고 추출기로 배치할 프리팹 (예: Warehouse output port). 비우면 추출기 항목 비활성.")]
    [SerializeField] private GameObject storageExtractorPrefab;
    [Tooltip("배치된 추출기들의 부모(정리용). 비우면 이 오브젝트 하위에 배치.")]
    [SerializeField] private Transform extractorParent;

    [Header("추출기 배치 (구역 남쪽 테두리 바깥, 왼쪽부터)")]
    [Tooltip("테두리에서 바깥쪽으로 띄울 거리(월드).")]
    [SerializeField] private float extractorOutwardOffset = 1.5f;
    [Tooltip("추출기 사이 간격(월드).")]
    [SerializeField] private float extractorSpacing = 4f;
    [Tooltip("배치 시작 모서리에서 안쪽으로 들일 여백(월드).")]
    [SerializeField] private float extractorEdgeMargin = 2f;
    [Tooltip("추출기 Y 높이(월드). 바닥에 맞춰 조정.")]
    [SerializeField] private float extractorY = 0f;

    /// <summary>구매·확장 등으로 상태가 바뀔 때. UI 갱신용.</summary>
    public static event Action OnChanged;

    private int _extractorCount;
    private readonly List<GameObject> _spawnedExtractors = new();

    public IReadOnlyList<UpgradeDef> Upgrades => upgrades;
    public int ExtractorCount => _extractorCount;
    public int CurrentZoneStage => zoneProgression != null ? zoneProgression.CurrentStageIndex : -1;
    public bool HasExtractorPrefab => storageExtractorPrefab != null;

    // ── 라이프사이클 ──────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (zoneProgression == null) zoneProgression = FindAnyObjectByType<BuildZoneProgression>();

        LoadCount();
    }

    private void Start()
    {
        // 저장된 추출기 수만큼 재배치 (구역 확장 단계는 BuildZoneProgression가 자체 복원).
        // BuildZoneProgression.Start 가 먼저 zone 크기를 확정하도록 한 프레임 뒤 배치.
        Invoke(nameof(RespawnExtractors), 0f);
    }

    private void LoadCount()
    {
        _extractorCount = PlayerPrefs.GetInt(PREF_EXTRACTOR_COUNT, 0);
    }

    // ── 상태 조회 (UI 용) ─────────────────────────────────────────────

    public ItemState GetState(UpgradeDef def)
    {
        int current = GetCurrentTier(def);
        int target  = GetTargetTier(def);
        if (current >= target)     return ItemState.Owned;
        if (current == target - 1) return ItemState.Available;
        return ItemState.Locked;
    }

    private int GetCurrentTier(UpgradeDef def)
        => def.kind == UpgradeKind.ZoneExpand ? CurrentZoneStage : _extractorCount;

    private int GetTargetTier(UpgradeDef def)
        => def.kind == UpgradeKind.ZoneExpand ? def.targetZoneStage : def.targetExtractorCount;

    /// <summary>가방+창고 합산 보유 수량.</summary>
    public int GetTotalCount(int itemId)
    {
        int bag = InventoryManager.Instance        != null ? InventoryManager.Instance.GetTotalItemCount(itemId)       : 0;
        int sto = InventoryManager.StorageInstance != null ? InventoryManager.StorageInstance.GetTotalItemCount(itemId) : 0;
        return bag + sto;
    }

    public bool HasKit(UpgradeDef def)
    {
        if (def.materials == null) return true;
        foreach (var m in def.materials)
            if (m != null && m.itemId > 0 && m.amount > 0 && GetTotalCount(m.itemId) < m.amount)
                return false;
        return true;
    }

    public bool CanPurchase(UpgradeDef def)
    {
        if (def == null || GetState(def) != ItemState.Available) return false;
        if (def.kind == UpgradeKind.StorageExtractor && storageExtractorPrefab == null) return false;
        return HasKit(def);
    }

    // ── 구매 ──────────────────────────────────────────────────────────

    public bool TryPurchase(UpgradeDef def)
    {
        if (def == null) return false;

        if (GetState(def) != ItemState.Available)
        {
            ToastManager.Warning("지금 구매할 수 없는 항목입니다");
            return false;
        }
        if (def.kind == UpgradeKind.StorageExtractor && storageExtractorPrefab == null)
        {
            Debug.LogError("[BaseUpgrade] 추출기 프리팹이 지정되지 않음");
            return false;
        }
        if (!HasKit(def))
        {
            ToastManager.Warning("재료가 부족합니다");
            return false;
        }
        if (!ConsumeKit(def))
        {
            Debug.LogError("[BaseUpgrade] 재료 소모 실패");
            return false;
        }

        if (def.kind == UpgradeKind.ZoneExpand)
        {
            if (zoneProgression == null) { Debug.LogError("[BaseUpgrade] BuildZoneProgression 없음"); return false; }
            zoneProgression.ApplyStage(def.targetZoneStage);
            RespawnExtractors();   // 구역이 커졌으니 추출기를 새 테두리에 다시 배치
        }
        else
        {
            SpawnExtractor(_extractorCount);   // index = 기존 개수
            _extractorCount++;
        }

        Save();
        ToastManager.Success($"{def.displayName} 완료");
        OnChanged?.Invoke();
        return true;
    }

    // 모든 재료가 충분한지 재확인 후, 각 재료를 가방 먼저·부족분 창고에서 차감.
    private bool ConsumeKit(UpgradeDef def)
    {
        if (def.materials == null) return true;

        // 부분 차감 방지 — 먼저 전부 충분한지 재확인
        if (!HasKit(def)) return false;

        foreach (var m in def.materials)
        {
            if (m == null || m.itemId <= 0 || m.amount <= 0) continue;
            if (!ConsumeOne(m.itemId, m.amount)) return false;
        }
        return true;
    }

    // CoreUpgradeManager 와 동일: 가방 먼저, 부족분 창고에서 차감.
    private bool ConsumeOne(int itemId, int amount)
    {
        int remaining = amount;

        if (InventoryManager.Instance != null)
        {
            int bag     = InventoryManager.Instance.GetTotalItemCount(itemId);
            int fromBag = Mathf.Min(bag, remaining);
            if (fromBag > 0)
            {
                if (!InventoryManager.Instance.TryConsumeItem(itemId, fromBag)) return false;
                remaining -= fromBag;
            }
        }

        if (remaining > 0 && InventoryManager.StorageInstance != null)
        {
            int sto     = InventoryManager.StorageInstance.GetTotalItemCount(itemId);
            int fromSto = Mathf.Min(sto, remaining);
            if (fromSto > 0)
            {
                if (!InventoryManager.StorageInstance.TryConsumeItem(itemId, fromSto)) return false;
                remaining -= fromSto;
            }
        }

        return remaining <= 0;
    }

    // ── 추출기 배치 ───────────────────────────────────────────────────

    private void RespawnExtractors()
    {
        for (int i = 0; i < _spawnedExtractors.Count; i++)
            if (_spawnedExtractors[i] != null) Destroy(_spawnedExtractors[i]);
        _spawnedExtractors.Clear();

        if (storageExtractorPrefab == null) return;
        for (int i = 0; i < _extractorCount; i++) SpawnExtractor(i);
    }

    private void SpawnExtractor(int index)
    {
        if (storageExtractorPrefab == null) return;
        Vector3 pos = GetExtractorPosition(index);
        Transform parent = extractorParent != null ? extractorParent : transform;
        var go = Instantiate(storageExtractorPrefab, pos, Quaternion.identity, parent);
        go.name = $"StorageExtractor_{index}";
        _spawnedExtractors.Add(go);
    }

    // 구역 남쪽(min Z) 테두리 바깥에 왼쪽부터 일정 간격으로 배치.
    private Vector3 GetExtractorPosition(int index)
    {
        if (zoneProgression != null && zoneProgression.TryGetZoneBounds(out var b))
        {
            float x = b.min.x + extractorEdgeMargin + index * extractorSpacing;
            float z = b.min.z - extractorOutwardOffset;
            return new Vector3(x, extractorY, z);
        }
        // bounds 조회 실패 시 폴백 (매니저 위치 기준)
        return transform.position + new Vector3(index * extractorSpacing, extractorY, 0f);
    }

    private void Save()
    {
        PlayerPrefs.SetInt(PREF_EXTRACTOR_COUNT, _extractorCount);
        PlayerPrefs.Save();
    }
}
