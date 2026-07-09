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

    // facilityId 로 초기화. BuildManager 가 설치 직후 호출.
    public void Initialize(int newFacilityId)
    {
        facilityId = newFacilityId;
        currentLevel = 1;
        RefreshCachedData();
    }

    // GameDataHolder 에서 최신 데이터를 다시 읽어 캐시 갱신.
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

    // 세이브 복원 전용 — Initialize()는 항상 레벨을 1로 리셋하므로, 그 다음에 저장된 레벨을 적용한다.
    public void SetLevel(int level)
    {
        currentLevel = Mathf.Max(1, level);
        RefreshCachedData();
    }

    // 회전 가능 여부 (canRotate)
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

    public List<RecipeDataSheetData> GetAvailableRecipes()
    {
        return GameDataUtility.GetRecipesByFacilityId(facilityId);
    }

    // 레벨 배율 x 우주선 수리 보상(공장 가동속도 전역 배수). 우주선 없으면 1.
    public float GetFinalProcessTime(float baseTime)
        => baseTime * GetProcessTimeMultiplier() * ShipRepairManager.FactorySpeedMultiplier;
}
