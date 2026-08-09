// 자동 생성 파일 — 직접 수정 금지 (메뉴 '시트 > 코드 다시 만들기' 로 재생성)

using System;
using System.Collections.Generic;

public static class CoreLevelDataParser
{
    public static Dictionary<string, CoreLevelDataSheetData> Parse(CsvTable table)
    {
        var result = new Dictionary<string, CoreLevelDataSheetData>();

        int idx_level = table.GetColumnIndex("level");
        int idx_maxTime = table.GetColumnIndex("maxTime");
        int idx_requiredKitItemId = table.GetColumnIndex("requiredKitItemId");
        int idx_requiredAmount = table.GetColumnIndex("requiredAmount");
        int idx_successRate = table.GetColumnIndex("successRate");

        foreach (var row in table.Rows)
        {
            var data = new CoreLevelDataSheetData();

            var key_level = row.Get(idx_level);
            data.SheetId = new CoreLevelDataSheetId(key_level);

            data.maxTime = int.Parse(row.Get(idx_maxTime));
            data.requiredKitItemId = (row.Get(idx_requiredKitItemId) == "-" ? default : (ItemDataSheetId)row.Get(idx_requiredKitItemId));
            data.requiredAmount = int.Parse(row.Get(idx_requiredAmount));
            data.successRate = float.Parse(row.Get(idx_successRate), System.Globalization.CultureInfo.InvariantCulture);

            result[data.SheetId] = data;
        }

        return result;
    }
}
