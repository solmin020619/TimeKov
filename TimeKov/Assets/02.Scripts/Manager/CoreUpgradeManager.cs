// =====================================================================
// CoreUpgradeManager.cs
// 코어 강화 시스템 전체 로직 담당
// - currentCoreLevel 관리 (PlayerPrefs 저장/로드)
// - 강화 조건 검사, 재료 소모, 확률 판정, 스탯 적용
// - 씬 어딘가에 하나만 배치 (싱글톤)
// =====================================================================

using System;
using UnityEngine;

public class CoreUpgradeManager : MonoBehaviour
{
    // ── 싱글톤 ────────────────────────────────────────────────────────
    public static CoreUpgradeManager Instance { get; private set; }

    // ── 저장 키 ───────────────────────────────────────────────────────
    private const string SAVE_KEY   = "CoreLevel";
    private const int    MAX_LEVEL  = 10;

    // ── 상태 ──────────────────────────────────────────────────────────
    public int CurrentCoreLevel { get; private set; } = 0;

    // ── 이벤트 (UI / 피드백에서 구독) ────────────────────────────────
    /// <summary>강화 성공 또는 실패 후 발생. true = 성공 / false = 실패</summary>
    public static event Action<bool> OnUpgradeResult;

    /// <summary>레벨이 바뀔 때 발생. UI 갱신용</summary>
    public static event Action<int>  OnLevelChanged;

    // ── 코어 부가 스탯 (레벨 0 → 최대레벨 선형 보간) ────────────────────
    // maxTime/successRate는 시트 데이터, 아래 둘은 코어에 어울리는 부가 능력으로 여기서 직접 튜닝.
    [Header("코어 부가 스탯 (레벨 0 → 최대 선형 보간)")]
    [Tooltip("몬스터 처치 시 흡수하는 체력(시간) = 최대HP × 이 비율. 레벨 0일 때 값.")]
    [Range(0f, 1f)][SerializeField] private float lifestealPercentAtLv0 = 0.03f;
    [Tooltip("몬스터 처치 흡수 비율 — 최대 레벨일 때 값.")]
    [Range(0f, 1f)][SerializeField] private float lifestealPercentAtMax = 0.10f;

    [Tooltip("몬스터 체력 흡수가 해금되는 코어 레벨. 이 레벨 미만이면 흡수 0(처음엔 막혀 있다가 코어 강화로 해금). 초반 생존 위해 1 권장.")]
    [SerializeField] private int lifestealUnlockLevel = 1;

    [Tooltip("부활 시 회복 체력 비율 — 레벨 0일 때 (0.5 = 반피).")]
    [Range(0f, 1f)][SerializeField] private float respawnHpPercentAtLv0 = 0.5f;
    [Tooltip("부활 시 회복 체력 비율 — 최대 레벨일 때 (1.0 = 풀피).")]
    [Range(0f, 1f)][SerializeField] private float respawnHpPercentAtMax = 1.0f;

    // ── 라이프사이클 ──────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LoadLevel();
    }

    private void Start()
    {
        // 로드 후 첫 프레임에 스탯 적용 (Player가 Awake에서 초기화되므로 Start에서 호출)
        // 로딩씬을 거쳤으면 IsLoaded == true라 즉시 적용
        // 월드씬 직접 플레이(테스트) 시에는 OnDataLoaded 이벤트 대기
        if (DataBoot.IsLoaded)
            ApplyStatsForLevel(CurrentCoreLevel);
        else
            DataBoot.OnDataLoaded += OnDataReady;
    }

    private void OnDataReady()
    {
        DataBoot.OnDataLoaded -= OnDataReady;
        ApplyStatsForLevel(CurrentCoreLevel);
    }

    private void OnDestroy()
    {
        DataBoot.OnDataLoaded -= OnDataReady;
    }

    // ── 외부 API ──────────────────────────────────────────────────────

    /// <summary>
    /// 강화 가능 여부 전체 조건 검사.
    /// UI 버튼 활성화 여부 판단에 사용.
    /// </summary>
    public bool CanUpgrade()
    {
        if (CurrentCoreLevel >= MAX_LEVEL) return false;

        Player player = GetPlayer();
        if (player == null) return false;

        // 플레이어 상태 조건
        if (player.Stat.IsDead)             return false;
        if (player.Stat.IsHurt)             return false;
        if (player.Skill.IsExecuting)       return false;
        if (player.Dash.IsDashing)          return false;
        if (!player.Stat.IsInBase)          return false;

        // 재료 조건
        CoreLevelDataSheetData nextData = GetLevelData(CurrentCoreLevel + 1);
        if (nextData == null) return false;
        if (!HasRequiredKit(nextData)) return false;

        return true;
    }

    /// <summary>
    /// 강화 실행.
    /// 재검사 → 소모 → 확률 판정 → 스탯 적용 → 저장 순서로 처리.
    /// 성공 여부를 반환하며, OnUpgradeResult 이벤트도 발생.
    /// </summary>
    /// <param name="successRateBonus">타임 캐치 성공 시 추가 확률 (기본 0, 성공 시 0.05)</param>
    public bool TryUpgrade(float successRateBonus = 0f)
    {
        Player player = GetPlayer();
        if (player == null)
        {
            Debug.LogError("[CoreUpgrade] Player를 찾을 수 없음");
            return false;
        }

        // ① 조건 재검사 (Double-check)
        if (!CanUpgrade())
        {
            Debug.LogWarning("[CoreUpgrade] 강화 조건 미충족");
            return false;
        }

        int nextLevel = CurrentCoreLevel + 1;
        CoreLevelDataSheetData nextData = GetLevelData(nextLevel);
        if (nextData == null)
        {
            Debug.LogError($"[CoreUpgrade] nextLevel={nextLevel} 데이터 없음");
            return false;
        }

        // ② 재료 수량 재검사 (Double-check)
        if (!HasRequiredKit(nextData))
        {
            Debug.LogWarning("[CoreUpgrade] 재료 부족 (재검사 실패)");
            ToastManager.Warning("강화 키트가 부족합니다");
            return false;
        }

        // ③ 재료 소모 (성공/실패 무관하게 소모)
        if (!ConsumeKitFromBagAndStorage(nextData))
        {
            Debug.LogError("[CoreUpgrade] 재료 소모 실패");
            return false;
        }

        // ④ 확률 판정 (타임 캐치 보너스 포함)
        float finalRate = Mathf.Clamp01(nextData.successRate + successRateBonus);
        bool success = UnityEngine.Random.value < finalRate;
        Debug.Log($"[CoreUpgrade] 확률 판정: successRate={nextData.successRate} + bonus={successRateBonus} = {finalRate} → {(success ? "성공" : "실패")}");

        if (success)
        {
            // ⑤ 성공 처리
            CurrentCoreLevel = nextLevel;
            ApplyStatsForLevel(CurrentCoreLevel);
            SaveLevel();

            OnLevelChanged?.Invoke(CurrentCoreLevel);
            GameEvents.RaiseCoreUpgraded(CurrentCoreLevel);   // 튜토리얼 등 전역 구독자 통지
            ToastManager.Success("코어 강화 성공!");
            Debug.Log($"[CoreUpgrade] 강화 성공! 현재 레벨: {CurrentCoreLevel}");
        }
        else
        {
            // ⑥ 실패 처리 — 레벨 유지, 스탯 변경 없음
            // ▼ 나중에 단계 하락 추가 시 이 블록만 수정
            // CurrentCoreLevel = Mathf.Max(0, CurrentCoreLevel - 1);
            // ApplyStatsForLevel(CurrentCoreLevel);
            // SaveLevel();
            Debug.Log("[CoreUpgrade] 강화 실패. 레벨 유지.");
        }

        GameEvents.RaiseCoreUpgradeAttempt();   // 튜토 lookback: 퀘 갭에 미리 강화해도 인정되게 기록
        OnUpgradeResult?.Invoke(success);
        return success;
    }

    /// <summary>
    /// 다음 레벨 데이터를 반환. UI에서 요구 조건 표시에 사용.
    /// 최대 레벨이면 null 반환.
    /// </summary>
    public CoreLevelDataSheetData GetNextLevelData()
    {
        if (CurrentCoreLevel >= MAX_LEVEL) return null;
        return GetLevelData(CurrentCoreLevel + 1);
    }

    /// <summary>현재 레벨 데이터 반환. UI 현재 스탯 표시에 사용.</summary>
    public CoreLevelDataSheetData GetCurrentLevelData()
    {
        return GetLevelData(CurrentCoreLevel);
    }

    /// <summary>현재 코어 레벨의 0~1 진행도 (0 = Lv.0, 1 = 최대 레벨).</summary>
    public float LevelProgress => LevelProgressAt(CurrentCoreLevel);

    /// <summary>최대 코어 레벨.</summary>
    public int MaxLevel => MAX_LEVEL;

    private float LevelProgressAt(int level) => MAX_LEVEL <= 0 ? 0f : Mathf.Clamp01((float)level / MAX_LEVEL);

    /// <summary>몬스터 처치 시 흡수할 체력(시간) 비율(최대HP 대비). 코어 레벨이 오를수록 증가.</summary>
    public float GetLifestealPercent() => GetLifestealPercentAt(CurrentCoreLevel);

    /// <summary>부활 시 회복할 체력 비율. 코어 레벨이 오를수록 증가(최종 레벨 = 풀피).</summary>
    public float GetRespawnHpPercent() => GetRespawnHpPercentAt(CurrentCoreLevel);

    /// <summary>특정 레벨의 흡수율 (UI에서 현재/강화후 비교용). 해금 레벨 미만이면 0(아직 잠김).</summary>
    public float GetLifestealPercentAt(int level)
        => level < lifestealUnlockLevel ? 0f : Mathf.Lerp(lifestealPercentAtLv0, lifestealPercentAtMax, LevelProgressAt(level));

    /// <summary>몬스터 체력 흡수 해금 여부(코어 강화로 해금). 적 흡수 게이트 / UI New 표시에 사용.</summary>
    public bool IsLifestealUnlocked => CurrentCoreLevel >= lifestealUnlockLevel;
    public bool IsLifestealUnlockedAt(int level) => level >= lifestealUnlockLevel;
    public int LifestealUnlockLevel => lifestealUnlockLevel;

    /// <summary>특정 레벨의 부활 체력 비율 (UI에서 현재/강화후 비교용).</summary>
    public float GetRespawnHpPercentAt(int level) => Mathf.Lerp(respawnHpPercentAtLv0, respawnHpPercentAtMax, LevelProgressAt(level));

    /// <summary>가방 + 창고 합산 보유 수량 반환.</summary>
    public int GetTotalKitCount(int itemId)
    {
        int bagCount     = InventoryManager.Instance        != null ? InventoryManager.Instance.GetTotalItemCount(itemId)        : 0;
        int storageCount = InventoryManager.StorageInstance != null ? InventoryManager.StorageInstance.GetTotalItemCount(itemId)  : 0;
        return bagCount + storageCount;
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────

    private void ApplyStatsForLevel(int level)
    {
        CoreLevelDataSheetData data = GetLevelData(level);
        if (data == null)
        {
            Debug.LogError($"[CoreUpgrade] 레벨 {level} 데이터 없음 — 스탯 적용 불가");
            return;
        }

        Player player = GetPlayer();
        if (player == null) return;

        player.Stat.ApplyCoreStats(data.maxTime);
        Debug.Log($"[CoreUpgrade] Lv.{level} 스탯 적용: MaxTime(HP)={data.maxTime}");
    }

    private bool HasRequiredKit(CoreLevelDataSheetData data)
    {
        // requiredKitItemId가 "-" 이거나 requiredAmount가 0 이하면 재료 불필요
        string kitId = (string)data.requiredKitItemId;
        if (string.IsNullOrEmpty(kitId) || kitId == "-") return true;
        if (data.requiredAmount <= 0) return true;

        if (!int.TryParse(kitId, out int kitItemId))
        {
            Debug.LogError($"[CoreUpgrade] requiredKitItemId 파싱 실패: {kitId}");
            return false;
        }

        return GetTotalKitCount(kitItemId) >= data.requiredAmount;
    }

    private bool ConsumeKitFromBagAndStorage(CoreLevelDataSheetData data)
    {
        string kitId = (string)data.requiredKitItemId;
        if (string.IsNullOrEmpty(kitId) || kitId == "-") return true;
        if (data.requiredAmount <= 0) return true;

        if (!int.TryParse(kitId, out int kitItemId))
        {
            Debug.LogError($"[CoreUpgrade] requiredKitItemId 파싱 실패: {kitId}");
            return false;
        }

        int remaining = data.requiredAmount;

        // 가방에서 먼저 차감
        if (InventoryManager.Instance != null)
        {
            int bagCount = InventoryManager.Instance.GetTotalItemCount(kitItemId);
            int fromBag  = Mathf.Min(bagCount, remaining);
            if (fromBag > 0)
            {
                if (!InventoryManager.Instance.TryConsumeItem(kitItemId, fromBag))
                {
                    Debug.LogError("[CoreUpgrade] 가방 소모 실패");
                    return false;
                }
                remaining -= fromBag;
            }
        }

        // 부족분 창고에서 차감
        if (remaining > 0 && InventoryManager.StorageInstance != null)
        {
            int storageCount = InventoryManager.StorageInstance.GetTotalItemCount(kitItemId);
            int fromStorage  = Mathf.Min(storageCount, remaining);
            if (fromStorage > 0)
            {
                if (!InventoryManager.StorageInstance.TryConsumeItem(kitItemId, fromStorage))
                {
                    Debug.LogError("[CoreUpgrade] 창고 소모 실패");
                    return false;
                }
                remaining -= fromStorage;
            }
        }

        if (remaining > 0)
        {
            Debug.LogError($"[CoreUpgrade] 소모 후 잔여 수량 남음: {remaining} — 소모 실패 처리");
            return false;
        }

        return true;
    }

    private CoreLevelDataSheetData GetLevelData(int level)
    {
        if (!GameDataHolder.I.CoreLevelData.TryGet(level.ToString(), out var data))
            return null;
        return data;
    }

    private void SaveLevel()
    {
        // TODO: Steam + Firebase 저장 시스템 연동 시 여기에 구현
        // SaveSystem.Save("currentCoreLevel", CurrentCoreLevel);
    }

    private void LoadLevel()
    {
        // TODO: Steam + Firebase 저장 시스템 연동 시 여기에 구현
        // CurrentCoreLevel = SaveSystem.Load<int>("currentCoreLevel", defaultValue: 0);
        CurrentCoreLevel = 0;  // 저장 시스템 연동 전까지 항상 0단계로 시작
        Debug.Log("[CoreUpgrade] 코어 레벨 초기화 (저장 미연동 — 0단계 시작)");
    }

    private Player _cachedPlayer;
    private Player GetPlayer()
    {
        if (_cachedPlayer == null)
            _cachedPlayer = FindAnyObjectByType<Player>();
        return _cachedPlayer;
    }
}
