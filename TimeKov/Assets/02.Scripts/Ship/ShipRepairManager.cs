using System;
using System.Collections.Generic;
using UnityEngine;

// 폐우주선 단계별 수리 = 기지 업그레이드. Lv.1(시작)~Lv.N(최대), 실제 수리 (N-1)회.
// 수리 부품은 단일 종류이며 레벨마다 필요 '개수'만 다르다(기획 확정 07-11).
// 부품 획득은 AddParts 한 곳으로 통일 - 픽업/시간에너지 보상 등 어떤 시스템이든 이걸 호출하면 됨(확장 대비).
// 부품을 모아 우주선과 상호작용 -> 필요 개수 소모 -> 레벨업 -> 보상 3종 적용:
//   (1) 공장 가동속도 = 제작시간 전역 배수 (FactorySpeedMultiplier)
//   (2) 설비 연료 효율 = 연료 1개당 가동 초 (FuelConfig.SetSecondsOverride)
//   (3) 건축 범위     = BuildZoneProgression.ApplyStage
// 수치는 임시(인스펙터 편집). 저장 = 통합 세이브(ISaveable/SaveSlotManager, 슬롯 기반).
// 활성 슬롯 없으면(World 씬 직접 Play) 복원/저장 모두 건너뜀 = 매번 Lv.1 새 상태(샌드박스 테스트).
// 부품은 인벤토리로 안 들어가고 여기에 수량으로 모인다.
public class ShipRepairManager : MonoBehaviour, ISaveable
{
    public static ShipRepairManager Instance { get; private set; }

    [Serializable]
    public class LevelDef
    {
        [Tooltip("이 레벨의 단계명. 예: 시설 제어 계통 수리")]
        public string title = "";
        [Tooltip("이 레벨에 '도달'하기 위해 필요한 수리 부품 개수. Lv.1(시작)은 0.")]
        public int requiredParts = 0;
        [Tooltip("이 레벨에서의 제작시간 배수(작을수록 빠름). 1=기본.")]
        public float factorySpeed = 1f;
        [Tooltip("이 레벨에서의 연료 1개당 가동 시간(초).")]
        public float fuelSeconds = 60f;
        [Tooltip("이 레벨에서 적용할 건축영역 확장 단계(BuildZoneProgression stage). -1=변경 없음.")]
        public int zoneStage = -1;
    }

    [Header("레벨 정의 (index 0 = Lv.1 시작). 기획자 편집.")]
    [SerializeField] private List<LevelDef> levels = new();

    [Header("부품 (단일 종류)")]
    [Tooltip("수리 부품 표시 이름. 토스트/UI 공통.")]
    [SerializeField] private string partName = "수리 부품";

    [Header("참조")]
    [Tooltip("건축영역 확장 대상. 비우면 씬에서 자동 탐색.")]
    [SerializeField] private BuildZoneProgression zoneProgression;

    private int _level = 1;
    private int _partCount = 0;        // 보유 부품 수량 (수리 시 소모)
    private int _pickupTakenMask = 0;  // bit i = 맵 픽업 i번을 이미 회수함 (재입장 중복 회수 방지)

    // 공장 제작속도 전역 배수 — FacilityInstance.GetFinalProcessTime 이 곱한다. 기본 1.
    public static float FactorySpeedMultiplier { get; private set; } = 1f;

    /// <summary>레벨/부품 상태가 바뀔 때. UI/월드표시 갱신용.</summary>
    public static event Action OnChanged;

    public int CurrentLevel => _level;
    public int MaxLevel => levels != null ? Mathf.Max(1, levels.Count) : 1;
    public bool IsMaxLevel => _level >= MaxLevel;

    /// <summary>Lv.5 최종 수리 완료 여부 = 완성 우주선 노출 + 탈출 시도 개방 신호.
    /// (실제 탈출 조건에 시간에너지 100% 를 AND 로 추가하는 건 별도 시스템에서.)</summary>
    public bool IsFullyRepaired => IsMaxLevel;

    public string PartName => partName;

    /// <summary>현재 보유한 수리 부품 수량.</summary>
    public int PartCount => _partCount;

    public LevelDef GetLevel(int level)
    {
        int idx = level - 1;
        if (levels == null || idx < 0 || idx >= levels.Count) return null;
        return levels[idx];
    }

    /// <summary>레벨 L 도달에 필요한 부품 개수.</summary>
    public int RequiredPartsFor(int level)
    {
        var def = GetLevel(level);
        return def != null ? Mathf.Max(0, def.requiredParts) : 0;
    }

    /// <summary>다음 수리에 필요한 부품 개수 (최대 레벨이면 0).</summary>
    public int NextRequiredParts => IsMaxLevel ? 0 : RequiredPartsFor(_level + 1);

    // ── 라이프사이클 ──────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (zoneProgression == null) zoneProgression = FindAnyObjectByType<BuildZoneProgression>();

        // 통합 세이브 등록 + 활성 슬롯 있을 때만 복원.
        // World 씬 직접 Play = 슬롯 없음 = 복원 안 함 = Lv.1 새 상태(샌드박스).
        SaveSlotManager.Instance?.Register(this);
        RestoreFromSave();
    }

    private void Start()
    {
        // BuildZoneProgression 등 다른 Start 초기화가 끝나도록 한 프레임 뒤 보상 재적용(복원 반영).
        Invoke(nameof(ReapplyAllEffects), 0f);
    }

    private void OnDestroy()
    {
        SaveSlotManager.Instance?.Unregister(this);
        if (Instance == this) Instance = null;
    }

    // ── 부품 획득 ──────────────────────────────────────────────────────

    /// <summary>수리 부품 지급 - 유일한 획득 통로. 맵 픽업/시간에너지 보상 등 어떤 시스템이든 이걸 호출.</summary>
    public void AddParts(int amount)
    {
        if (amount <= 0) return;
        _partCount += amount;

        ToastManager.Success(amount == 1 ? $"{partName} 회수" : $"{partName} +{amount}");
        OnChanged?.Invoke();
    }

    /// <summary>맵 픽업 i번을 이미 회수했는지 (세이브 복원 후 중복 회수 방지용).</summary>
    public bool IsPickupTaken(int index)
        => index >= 0 && index < 32 && (_pickupTakenMask & (1 << index)) != 0;

    /// <summary>맵 픽업 회수: 회수 기록 + 부품 지급. index = 씬에서 픽업마다 유니크하게(0~31).</summary>
    public void CollectPickup(int index, int amount)
    {
        if (IsPickupTaken(index)) return;
        if (index >= 0 && index < 32) _pickupTakenMask |= (1 << index);
        AddParts(amount);
    }

    // ── 수리 (다음 레벨) ──────────────────────────────────────────────

    public bool CanRepairNext()
    {
        if (IsMaxLevel) return false;
        return _partCount >= NextRequiredParts;
    }

    public bool TryRepairNext()
    {
        if (IsMaxLevel)
        {
            ToastManager.Info("우주선 수리가 완료되었습니다");
            return false;
        }

        int need = NextRequiredParts;
        if (_partCount < need)
        {
            ToastManager.Warning($"{partName}이 부족합니다 ({_partCount} / {need})");
            return false;
        }

        _partCount -= need;
        _level += 1;
        ApplyLevelEffects(_level);

        ToastManager.Success($"우주선 수리 Lv.{_level} 완료");
        OnChanged?.Invoke();
        return true;
    }

    // ── 보상 적용 ──────────────────────────────────────────────────────

    private void ReapplyAllEffects() => ApplyLevelEffects(_level);

    private void ApplyLevelEffects(int level)
    {
        var def = GetLevel(level);
        if (def == null) return;

        FactorySpeedMultiplier = def.factorySpeed > 0f ? def.factorySpeed : 1f;
        FuelConfig.SetSecondsOverride(def.fuelSeconds);

        if (def.zoneStage >= 0 && zoneProgression != null)
            zoneProgression.ApplyStage(def.zoneStage);
    }

    // ── 통합 세이브 (ISaveable) ────────────────────────────────────────
    // 슬롯 기반. 활성 슬롯 없으면(World 씬 직접 Play) 복원/저장 둘 다 스킵 = 매번 Lv.1 샌드박스.
    // 저장 시점은 SaveSlotManager 가 관리(자동 30초 + 앱 종료 시). 여기선 상태만 바꾸면 캡처된다.

    public void Capture(GameSaveData data)
    {
        if (data == null) return;
        data.shipRepairLevel     = _level;
        data.shipRepairPartCount = _partCount;
        data.shipRepairPartsMask = _pickupTakenMask;
    }

    private void RestoreFromSave()
    {
        var mgr = SaveSlotManager.Instance;
        if (mgr == null || !mgr.HasActiveSlot) return;   // 활성 슬롯 없으면 기본값(Lv.1/0) 유지

        _level           = Mathf.Clamp(mgr.Data.shipRepairLevel, 1, MaxLevel);
        _partCount       = Mathf.Max(0, mgr.Data.shipRepairPartCount);
        _pickupTakenMask = mgr.Data.shipRepairPartsMask;
    }

#if UNITY_EDITOR
    // 컴포넌트 처음 부착 시 5레벨 기본값을 채운다(빈 리스트부터 안 만들게). 값은 인스펙터서 자유 수정.
    private void Reset()
    {
        if (levels != null && levels.Count > 0) return;

        levels = new List<LevelDef>
        {
            new LevelDef { title = "파손 상태",           requiredParts = 0, factorySpeed = 1.0f, fuelSeconds = 60f,  zoneStage = 0 },
            new LevelDef { title = "시설 제어 계통 수리",    requiredParts = 1, factorySpeed = 0.9f, fuelSeconds = 60f,  zoneStage = 1 },
            new LevelDef { title = "동력 변환 계통 수리",    requiredParts = 1, factorySpeed = 0.8f, fuelSeconds = 75f,  zoneStage = 2 },
            new LevelDef { title = "공간 안정화 계통 수리",  requiredParts = 2, factorySpeed = 0.8f, fuelSeconds = 90f,  zoneStage = 3 },
            new LevelDef { title = "주 동력 계통 최종 수리",  requiredParts = 2, factorySpeed = 0.7f, fuelSeconds = 105f, zoneStage = 4 },
        };
    }
#endif
}
