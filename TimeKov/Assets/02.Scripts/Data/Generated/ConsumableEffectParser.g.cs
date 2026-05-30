// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System;
using System.Collections.Generic;

public static class ConsumableEffectParser
{
    public static Dictionary<string, ConsumableEffectSheetData> Parse(CsvTable table)
    {
        var result = new Dictionary<string, ConsumableEffectSheetData>();

        int idx_itemId = table.GetColumnIndex("itemId");
        int idx_consumableType = table.GetColumnIndex("consumableType");
        int idx_effectTarget = table.GetColumnIndex("effectTarget");
        int idx_effectValueType = table.GetColumnIndex("effectValueType");
        int idx_effectValue = table.GetColumnIndex("effectValue");
        int idx_duration = table.GetColumnIndex("duration");
        int idx_successRate = table.GetColumnIndex("successRate");

        foreach (var row in table.Rows)
        {
            var data = new ConsumableEffectSheetData();

            var key_itemId = row.Get(idx_itemId);
            data.SheetId = new ConsumableEffectSheetId(key_itemId);

            data.consumableType = (ConsumableType)Enum.Parse(typeof(ConsumableType), row.Get(idx_consumableType));
            data.effectTarget = (EffectTarget)Enum.Parse(typeof(EffectTarget), row.Get(idx_effectTarget));
            data.effectValueType = (EffectValueType)Enum.Parse(typeof(EffectValueType), row.Get(idx_effectValueType));
            data.effectValue = float.Parse(row.Get(idx_effectValue), System.Globalization.CultureInfo.InvariantCulture);
            data.duration = float.Parse(row.Get(idx_duration), System.Globalization.CultureInfo.InvariantCulture);
            data.successRate = float.Parse(row.Get(idx_successRate), System.Globalization.CultureInfo.InvariantCulture);

            result[data.SheetId] = data;
        }

        return result;
    }
}
