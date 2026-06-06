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
