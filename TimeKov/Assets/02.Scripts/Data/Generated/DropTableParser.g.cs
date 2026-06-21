// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System;
using System.Collections.Generic;

public static class DropTableParser
{
    public static Dictionary<string, DropTableSheetData> Parse(CsvTable table)
    {
        var result = new Dictionary<string, DropTableSheetData>();

        int idx_dropId = table.GetColumnIndex("dropId");
        int idx_sourceType = table.GetColumnIndex("sourceType");
        int idx_sourceId = table.GetColumnIndex("sourceId");
        int idx_itemId = table.GetColumnIndex("itemId");
        int idx_dropTier = table.GetColumnIndex("dropTier");
        int idx_dropChance = table.GetColumnIndex("dropChance");
        int idx_countDist = table.GetColumnIndex("countDist");

        foreach (var row in table.Rows)
        {
            var data = new DropTableSheetData();

            var key_dropId = row.Get(idx_dropId);
            var key_itemId = row.Get(idx_itemId);
            // 복합키: string 연결 후 explicit cast
            data.SheetId = (DropTableSheetId)(key_dropId + "_" + key_itemId);

            data.sourceType = (SourceType)Enum.Parse(typeof(SourceType), row.Get(idx_sourceType));
            data.sourceId = row.Get(idx_sourceId);
            data.dropTier = int.Parse(row.Get(idx_dropTier));
            data.dropChance = int.Parse(row.Get(idx_dropChance));
            data.countDist = row.Get(idx_countDist);

            result[data.SheetId] = data;
        }

        return result;
    }
}
