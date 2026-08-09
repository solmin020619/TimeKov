// 자동 생성 파일 — 직접 수정 금지 (메뉴 '시트 > 코드 다시 만들기' 로 재생성)

using System;
using System.Collections.Generic;

public static class PlayerStatDataParser
{
    public static Dictionary<string, PlayerStatDataSheetData> Parse(CsvTable table)
    {
        var result = new Dictionary<string, PlayerStatDataSheetData>();

        int idx_statKey = table.GetColumnIndex("statKey");
        int idx_value = table.GetColumnIndex("value");
        int idx_note = table.GetColumnIndex("note");

        foreach (var row in table.Rows)
        {
            var data = new PlayerStatDataSheetData();

            var key_statKey = row.Get(idx_statKey);
            data.SheetId = new PlayerStatDataSheetId(key_statKey);

            data.value = float.Parse(row.Get(idx_value), System.Globalization.CultureInfo.InvariantCulture);
            data.note = row.Get(idx_note);

            result[data.SheetId] = data;
        }

        return result;
    }
}
