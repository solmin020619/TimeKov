using System.Collections.Generic;
using UnityEngine;

public class FacilityInstance : MonoBehaviour
{
    [Header("Runtime Data")]
    [SerializeField] private int facilityId;
    [SerializeField] private int currentLevel = 1;

    private FacilityRow facilityRow;
    private FacilityLevelRow currentLevelRow;

    public int FacilityId => facilityId;
    public int CurrentLevel => currentLevel;
    public FacilityRow FacilityData => facilityRow;
    public FacilityLevelRow CurrentLevelData => currentLevelRow;

    public void Initialize(int newFacilityId)
    {
        facilityId = newFacilityId;
        currentLevel = 1;

        RefreshCachedData();
    }

    public void RefreshCachedData()
    {
        facilityRow = DataStore.GetFacility(facilityId);
        currentLevelRow = GetLevelRow(currentLevel);

        if (facilityRow == null)
        {
            Debug.LogError($"[FacilityInstance] Missing FacilityRow. facilityId={facilityId}");
            return;
        }

        if (facilityRow.maxLevel > 1 && currentLevelRow == null)
        {
            Debug.LogWarning($"[FacilityInstance] Missing FacilityLevelRow. facilityId={facilityId}, level={currentLevel}");
        }
    }

    public bool CanUpgrade()
    {
        if (facilityRow == null)
            return false;

        return currentLevel < facilityRow.maxLevel;
    }

    public bool TryUpgrade()
    {
        if (!CanUpgrade())
            return false;

        currentLevel++;
        RefreshCachedData();
        return true;
    }

    public int GetInputCount()
    {
        if (facilityRow == null)
            return 0;

        return facilityRow.inputCount;
    }

    public int GetOutputCount()
    {
        if (facilityRow == null)
            return 0;

        return facilityRow.outputCount;
    }

    public bool RequiresPower()
    {
        if (facilityRow == null)
            return false;

        return facilityRow.requiresPower == 1;
    }

    public bool CanRotate()
    {
        if (facilityRow == null)
            return false;

        return facilityRow.canRotate == 1;
    }

    public float GetProcessTimeMultiplier()
    {
        if (currentLevelRow == null)
            return 1f;

        return currentLevelRow.processTimeMultiplier;
    }

    public float GetPowerEfficiencyMultiplier()
    {
        if (currentLevelRow == null)
            return 1f;

        return currentLevelRow.powerEfficiencyMultiplier;
    }

    public int GetCapacityBonus()
    {
        if (currentLevelRow == null)
            return 0;

        return currentLevelRow.capacityBonus;
    }

    public List<RecipeRow> GetAvailableRecipes()
    {
        List<RecipeRow> result = new List<RecipeRow>();

        foreach (var kv in DataStore.RecipeById)
        {
            RecipeRow recipe = kv.Value;

            if (recipe.facilityId == facilityId)
                result.Add(recipe);
        }

        return result;
    }

    public float GetFinalProcessTime(float baseTime)
    {
        return baseTime * GetProcessTimeMultiplier();
    }

    public float GetFinalPowerCost(float basePowerCost)
    {
        return basePowerCost * GetPowerEfficiencyMultiplier();
    }

    private FacilityLevelRow GetLevelRow(int level)
    {
        List<FacilityLevelRow> levels = DataStore.GetFacilityLevels(facilityId);

        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i].level == level)
                return levels[i];
        }

        return null;
    }
}