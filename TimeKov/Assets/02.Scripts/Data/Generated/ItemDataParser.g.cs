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
        int idx_stackable = table.GetColumnIndex("stackable");
        int idx_maxStack = table.GetColumnIndex("maxStack");
        int idx_weight = table.GetColumnIndex("weight");
        int idx_sellValue = table.GetColumnIndex("sellValue");
        int idx_isDroppable = table.GetColumnIndex("isDroppable");
        int idx_isCraftable = table.GetColumnIndex("isCraftable");

        foreach (var row in table.Rows)
        {
            var data = new ItemDataSheetData();

            var key_itemId = row.Get(idx_itemId);
            data.SheetId = new ItemDataSheetId(key_itemId);

            data.itemName = row.Get(idx_itemName);
            data.itemGrade = (ItemGrade)Enum.Parse(typeof(ItemGrade), row.Get(idx_itemGrade));
            data.itemCategory = (ItemCategory)Enum.Parse(typeof(ItemCategory), row.Get(idx_itemCategory));
            data.iconKey = row.Get(idx_iconKey);
            data.stackable = (row.Get(idx_stackable) == "1");
            data.maxStack = int.Parse(row.Get(idx_maxStack));
            data.weight = float.Parse(row.Get(idx_weight), System.Globalization.CultureInfo.InvariantCulture);
            data.sellValue = int.Parse(row.Get(idx_sellValue));
            data.isDroppable = (row.Get(idx_isDroppable) == "1");
            data.isCraftable = (row.Get(idx_isCraftable) == "1");

            result[data.SheetId] = data;
        }

        return result;
    }
}
