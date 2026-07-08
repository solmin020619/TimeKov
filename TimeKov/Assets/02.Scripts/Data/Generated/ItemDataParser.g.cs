// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System;
using System.Collections.Generic;

public static class ItemDataParser
{
    public static Dictionary<string, ItemDataSheetData> Parse(CsvTable table)
    {
        var result = new Dictionary<string, ItemDataSheetData>();

        int idx_itemId = table.GetColumnIndex("itemId");
        int idx_itemName = table.GetColumnIndex("itemName");
        int idx_itemGrade = table.GetColumnIndex("itemGrade");
        int idx_itemCategory = table.GetColumnIndex("itemCategory");
        int idx_iconKey = table.GetColumnIndex("iconKey");
        int idx_maxStack = table.GetColumnIndex("maxStack");

        foreach (var row in table.Rows)
        {
            var data = new ItemDataSheetData();

            var key_itemId = row.Get(idx_itemId);
            data.SheetId = new ItemDataSheetId(key_itemId);

            data.itemName = row.Get(idx_itemName);
            data.itemGrade = (ItemGrade)Enum.Parse(typeof(ItemGrade), row.Get(idx_itemGrade));
            data.itemCategory = (ItemCategory)Enum.Parse(typeof(ItemCategory), row.Get(idx_itemCategory));
            data.iconKey = row.Get(idx_iconKey);
            data.maxStack = int.Parse(row.Get(idx_maxStack));

            result[data.SheetId] = data;
        }

        return result;
    }
}
