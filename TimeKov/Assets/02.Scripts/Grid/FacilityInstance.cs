using System.Collections.Generic;
using UnityEngine;

public class FacilityInstance : MonoBehaviour
{
    [Header("Runtime Data")]
    [SerializeField] private int facilityId;
    [SerializeField] private int currentLevel = 1;

    private FacilityDataSheetData facilityData;
    private FacilityLevelDataSheetData currentLevelData;

    public int FacilityId => facilityId;
    public int CurrentLevel => currentLevel;
    public FacilityDataSheetData FacilityData => facilityData;
    public FacilityLevelDataSheetData CurrentLevelData => currentLevelData;

    // facilityId 로 설비 초기화 BuildManager 에서 배치 직후 호출
    public void Initialize(int newFacilityId)
    {
        facilityId = newFacilityId;
        currentLevel = 1;
        RefreshCachedData();
    }

    // GameDataHolder 에서 최신 데이터를 다시 읽어 캐시 갱신
    public void RefreshCachedData()
    {
        if (!GameDataHolder.I.FacilityData.TryGet(facilityId.ToString(), out facilityData))
        {
            Debug.LogError($"[FacilityInstance] FacilityData 없음. facilityId={facilityId}");
            facilityData = null;
            return;
        }

        currentLevelData = GameDataUtility.GetFacilityLevelRow(facilityId, currentLevel);

        if (facilityData.maxLevel > 1 && currentLevelData == null)
            Debug.LogWarning($"[FacilityInstance] FacilityLevelData 없음. facilityId={facilityId}, level={currentLevel}");
    }

    public bool CanUpgrade()
    {
        if (facilityData == null) return false;
        return currentLevel < facilityData.maxLevel;
    }

    public bool TryUpgrade()
    {
        if (!CanUpgrade()) return false;
        currentLevel++;
        RefreshCachedData();
        return true;
    }

    public int GetInputSlotCount()
    {
        if (facilityData == null) return 0;
        return facilityData.inputSlotCount;
    }

    public int GetOutputSlotCount()
    {
        if (facilityData == null) return 0;
        return facilityData.outputSlotCount;
    }

    // 구버전: requiresPower == 1  신버전: bool
    public bool RequiresPower()
    {
        if (facilityData == null) return false;
        return facilityData.requiresPower;
    }

    // 구버전: canRotate == 1  신버전: bool
    public bool CanRotate()
    {
        if (facilityData == null) return false;
        return facilityData.canRotate;
    }

    public float GetProcessTimeMultiplier()
    {
        if (currentLevelData == null) return 1f;
        return currentLevelData.processTimeMultiplier;
    }

    public float GetPowerEfficiencyMultiplier()
    {
        if (currentLevelData == null) return 1f;
        return currentLevelData.powerEfficiencyMultiplier;
    }

    // capacityBonus 컬럼 삭제됨 항상 0
    public int GetCapacityBonus() => 0;

    public List<RecipeDataSheetData> GetAvailableRecipes()
    {
        return GameDataUtility.GetRecipesByFacilityId(facilityId);
    }

    public float GetFinalProcessTime(float baseTime)
        => baseTime * GetProcessTimeMultiplier();

    public float GetFinalPowerCost(float basePowerCost)
        => basePowerCost * GetPowerEfficiencyMultiplier();
}