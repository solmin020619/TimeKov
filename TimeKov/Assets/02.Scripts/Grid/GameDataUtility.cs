// =====================================================================
// GameDataUtility.cs
// 새 스키마 기반 데이터 조회 유틸리티 (구버전 DataStoreUtility 대체)
// =====================================================================

using System.Collections.Generic;

public static class GameDataUtility
{
    // itemId 로 ItemDataSheetData 조회
    public static ItemDataSheetData GetItem(int itemId)
    {
        if (GameDataHolder.I.ItemData.TryGet(itemId.ToString(), out var data))
            return data;
        return null;
    }

    // facilityId 로 FacilityDataSheetData 조회
    public static FacilityDataSheetData GetFacility(int facilityId)
    {
        if (GameDataHolder.I.FacilityData.TryGet(facilityId.ToString(), out var data))
            return data;
        return null;
    }

    // facilityId 에 해당하는 레시피 목록 반환
    public static List<RecipeDataSheetData> GetRecipesByFacilityId(int facilityId)
    {
        var result = new List<RecipeDataSheetData>();
        string facilityIdStr = facilityId.ToString();

        foreach (var recipe in GameDataHolder.I.RecipeData.All)
        {
            if ((string)recipe.facilityId == facilityIdStr)
                result.Add(recipe);
        }

        return result;
    }

    // facilityId + level 복합키로 FacilityLevelData 조회
    public static FacilityLevelDataSheetData GetFacilityLevelRow(int facilityId, int level)
    {
        string key = $"{facilityId}_{level}";

        if (GameDataHolder.I.FacilityLevelData.TryGet(key, out var row))
            return row;

        return null;
    }
}