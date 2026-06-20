// =====================================================================
// GameDataUtility.cs
// 각 시트 데이터 조회 유틸리티 (구버전 DataStoreUtility 대체)
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

    // 몬스터(sourceId)의 드롭 목록 반환. itemId 는 DropTable 복합키 SheetId("dropId_itemId")서 파싱.
    // 확률 = dropWeight 비율(호출부서 합산해 계산).
    public static List<(int itemId, int weight, int tier, int minCount, int maxCount)> GetMonsterDrops(string sourceId)
    {
        var result = new List<(int, int, int, int, int)>();
        if (string.IsNullOrEmpty(sourceId)) return result;
        string id = sourceId.Trim();

        foreach (var row in GameDataHolder.I.DropTable.All)
        {
            if (row.sourceType != SourceType.Monster) continue;
            string rowId = row.sourceId != null ? row.sourceId.Trim() : "";
            if (rowId != id) continue;

            string key = row.SheetId;
            int u = string.IsNullOrEmpty(key) ? -1 : key.LastIndexOf('_');
            if (u < 0 || u + 1 >= key.Length) continue;
            if (int.TryParse(key.Substring(u + 1), out int itemId) && itemId > 0)
                result.Add((itemId, row.dropWeight, row.dropTier, row.minCount, row.maxCount));
        }
        return result;
    }

    // recipeId 의 입력 재료를 (itemId, count) 목록으로 반환.
    // RecipeInputData 는 개별 필드 없이 복합키 SheetId("recipeId_inputItemId")만 가지므로
    // 복합키 파싱을 이 메서드 한 곳에 집중한다 (recipeId/itemId 자체엔 '_' 없음).
    public static List<(int itemId, int count)> GetRecipeInputs(string recipeId)
    {
        var result = new List<(int itemId, int count)>();
        if (string.IsNullOrEmpty(recipeId)) return result;

        foreach (var input in GameDataHolder.I.RecipeInputData.All)
        {
            string key = input.SheetId;
            if (string.IsNullOrEmpty(key)) continue;

            int us = key.LastIndexOf('_');
            if (us < 0) continue;

            if (key.Substring(0, us) != recipeId) continue;   // 앞부분 = recipeId

            if (int.TryParse(key.Substring(us + 1), out int itemId) && itemId > 0)
                result.Add((itemId, input.inputCount));        // 뒷부분 = inputItemId
        }
        return result;
    }
}
