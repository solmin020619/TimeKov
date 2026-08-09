// 자동 생성 파일 — 직접 수정 금지 (메뉴 '시트 > 코드 다시 만들기' 로 재생성)

using System;
using System.Collections.Generic;

public static class RecipeInputDataParser
{
    public static Dictionary<string, RecipeInputDataSheetData> Parse(CsvTable table)
    {
        var result = new Dictionary<string, RecipeInputDataSheetData>();

        int idx_recipeId = table.GetColumnIndex("recipeId");
        int idx_inputItemId = table.GetColumnIndex("inputItemId");
        int idx_inputCount = table.GetColumnIndex("inputCount");

        foreach (var row in table.Rows)
        {
            var data = new RecipeInputDataSheetData();

            var key_recipeId = row.Get(idx_recipeId);
            var key_inputItemId = row.Get(idx_inputItemId);
            // 복합키: string 연결 후 explicit cast
            data.SheetId = (RecipeInputDataSheetId)(key_recipeId + "_" + key_inputItemId);

            data.inputCount = int.Parse(row.Get(idx_inputCount));

            result[data.SheetId] = data;
        }

        return result;
    }
}
