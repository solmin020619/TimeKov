// 자동 생성 파일 — 직접 수정 금지 (메뉴 '시트 > 코드 다시 만들기' 로 재생성)

using System;
using System.Collections.Generic;

public static class RecipeDataParser
{
    public static Dictionary<string, RecipeDataSheetData> Parse(CsvTable table)
    {
        var result = new Dictionary<string, RecipeDataSheetData>();

        int idx_recipeId = table.GetColumnIndex("recipeId");
        int idx_facilityId = table.GetColumnIndex("facilityId");
        int idx_outputItemId = table.GetColumnIndex("outputItemId");
        int idx_outputCount = table.GetColumnIndex("outputCount");
        int idx_craftTime = table.GetColumnIndex("craftTime");

        foreach (var row in table.Rows)
        {
            var data = new RecipeDataSheetData();

            var key_recipeId = row.Get(idx_recipeId);
            data.SheetId = new RecipeDataSheetId(key_recipeId);

            data.facilityId = (FacilityDataSheetId)(row.Get(idx_facilityId));
            data.outputItemId = (ItemDataSheetId)(row.Get(idx_outputItemId));
            data.outputCount = int.Parse(row.Get(idx_outputCount));
            data.craftTime = float.Parse(row.Get(idx_craftTime), System.Globalization.CultureInfo.InvariantCulture);

            result[data.SheetId] = data;
        }

        return result;
    }
}
