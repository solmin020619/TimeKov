// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System;
using System.Collections.Generic;

public static class CoreLevelDataParser
{
    public static Dictionary<string, CoreLevelDataSheetData> Parse(CsvTable table)
    {
        var result = new Dictionary<string, CoreLevelDataSheetData>();

        int idx_level = table.GetColumnIndex("level");
        int idx_maxTime = table.GetColumnIndex("maxTime");
        int idx_stamina = table.GetColumnIndex("stamina");
        int idx_atk = table.GetColumnIndex("atk");
        int idx_def = table.GetColumnIndex("def");
        int idx_requiredKitItemId = table.GetColumnIndex("requiredKitItemId");

        foreach (var row in table.Rows)
        {
            var data = new CoreLevelDataSheetData();

            var key_level = row.Get(idx_level);
            data.SheetId = new CoreLevelDataSheetId(key_level);

            data.maxTime = int.Parse(row.Get(idx_maxTime));
            data.stamina = int.Parse(row.Get(idx_stamina));
            data.atk = int.Parse(row.Get(idx_atk));
            data.def = int.Parse(row.Get(idx_def));
            data.requiredKitItemId = (row.Get(idx_requiredKitItemId) == "-" ? default : (ItemDataSheetId)row.Get(idx_requiredKitItemId));

            result[data.SheetId] = data;
        }

        return result;
    }
}
