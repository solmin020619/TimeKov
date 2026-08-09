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

    // facilityId 에 해당하는 레시피 목록 반환.
    //
    // ★정렬을 코드에서 확정한다(시트 행 순서에 기대지 않는다).
    //   All 은 Dictionary 값 순회라 순서가 보장되지 않고, 시트에서 행을 끼워 넣을 때마다
    //   설비 UI 의 레시피 칩 순서가 뒤바뀐다. 저장된 LockedRecipeIndex 가 엉뚱한 레시피를
    //   가리키게 되는 문제로도 이어진다.
    //
    // 순서 = 산출물 등급(Common -> Legend) -> 산출물 id.
    //   앰플이면 초급(Advanced) 4종 -> 중급(Rare) 4종 -> 고급(Hero) 4종 순으로 깔끔히 떨어지고,
    //   다른 설비도 '싼 것부터'가 되어 자연스럽다.
    public static List<RecipeDataSheetData> GetRecipesByFacilityId(int facilityId)
    {
        var result = new List<RecipeDataSheetData>();
        string facilityIdStr = facilityId.ToString();

        foreach (var recipe in GameDataHolder.I.RecipeData.All)
        {
            if ((string)recipe.facilityId == facilityIdStr)
                result.Add(recipe);
        }

        result.Sort(CompareByGradeThenId);
        return result;
    }

    private static int CompareByGradeThenId(RecipeDataSheetData a, RecipeDataSheetData b)
    {
        int ga = OutputGradeRank(a), gb = OutputGradeRank(b);
        if (ga != gb) return ga.CompareTo(gb);
        return string.CompareOrdinal((string)a.outputItemId, (string)b.outputItemId);
    }

    // 산출물 등급 순위. 아이템을 못 찾으면 맨 뒤로 보낸다(정렬이 흔들리지 않게).
    private static int OutputGradeRank(RecipeDataSheetData r)
    {
        return GameDataHolder.I.ItemData.TryGet((string)r.outputItemId, out var item)
            ? (int)item.itemGrade
            : int.MaxValue;
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
    // chance = dropChance(0~100, 독립 확률이라 그대로 %).
    public static List<(int itemId, int chance, int minCount, int maxCount)> GetMonsterDrops(string sourceId)
    {
        var result = new List<(int, int, int, int)>();
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
            {
                var (mn, mx) = DropCountRange(row.countDist);
                result.Add((itemId, row.dropChance, mn, mx));
            }
        }
        return result;
    }

    // countDist("70|30")를 굴려 개수 반환. 파이프 순서 = 1개,2개,3개... 의 가중치.
    public static int RollDropCount(string countDist)
    {
        if (string.IsNullOrEmpty(countDist)) return 1;
        string[] parts = countDist.Split('|');
        int total = 0;
        for (int i = 0; i < parts.Length; i++)
            if (int.TryParse(parts[i].Trim(), out int w) && w > 0) total += w;
        if (total <= 0) return 1;

        int r = UnityEngine.Random.Range(0, total);
        int acc = 0;
        for (int i = 0; i < parts.Length; i++)
            if (int.TryParse(parts[i].Trim(), out int w) && w > 0)
            {
                acc += w;
                if (r < acc) return i + 1;   // 개수 = 자리(1부터)
            }
        return 1;
    }

    // countDist 에서 표시용 개수 범위(min~max) 추출. 가중치>0 인 첫/마지막 자리.
    public static (int min, int max) DropCountRange(string countDist)
    {
        if (string.IsNullOrEmpty(countDist)) return (1, 1);
        string[] parts = countDist.Split('|');
        int min = 0, max = 0;
        for (int i = 0; i < parts.Length; i++)
            if (int.TryParse(parts[i].Trim(), out int w) && w > 0)
            {
                if (min == 0) min = i + 1;
                max = i + 1;
            }
        return min == 0 ? (1, 1) : (min, max);
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

    // 이 아이템을 "제작하는" 레시피(outputItemId == itemId) 첫 번째 반환. 없으면 null.
    // 도감 출처 추적에서 가공템이 어느 설비서 나오는지 역추적용.
    public static RecipeDataSheetData GetRecipeByOutputItem(int itemId)
    {
        string idStr = itemId.ToString();
        foreach (var r in GameDataHolder.I.RecipeData.All)
            if ((string)r.outputItemId == idStr)
                return r;
        return null;
    }

    // 이 아이템의 드롭 출처(상자/몬스터) 목록. (sourceType, sourceId) 중복 제거.
    // itemId 는 DropTable 복합키 SheetId("source_itemId")의 뒷부분.
    public static List<(SourceType type, string sourceId)> GetItemDropSources(int itemId)
    {
        var result = new List<(SourceType, string)>();
        string idStr = itemId.ToString();
        foreach (var row in GameDataHolder.I.DropTable.All)
        {
            string key = row.SheetId;
            if (string.IsNullOrEmpty(key)) continue;
            int u = key.LastIndexOf('_');
            if (u < 0 || u + 1 >= key.Length) continue;
            if (key.Substring(u + 1) != idStr) continue;

            string sid = row.sourceId != null ? row.sourceId.Trim() : "";
            bool dup = false;
            for (int i = 0; i < result.Count; i++)
                if (result[i].Item1 == row.sourceType && result[i].Item2 == sid) { dup = true; break; }
            if (!dup) result.Add((row.sourceType, sid));
        }
        return result;
    }
}
