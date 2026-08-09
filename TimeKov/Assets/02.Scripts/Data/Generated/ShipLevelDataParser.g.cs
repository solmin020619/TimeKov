// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System;
using System.Collections.Generic;

public static class ShipLevelDataParser
{
    public static Dictionary<string, ShipLevelDataSheetData> Parse(CsvTable table)
    {
        var result = new Dictionary<string, ShipLevelDataSheetData>();

        int idx_level = table.GetColumnIndex("level");
        int idx_title = table.GetColumnIndex("title");
        int idx_requiredParts = table.GetColumnIndex("requiredParts");
        int idx_factorySpeed = table.GetColumnIndex("factorySpeed");
        int idx_fuelSeconds = table.GetColumnIndex("fuelSeconds");
        int idx_zoneCells = table.GetColumnIndex("zoneCells");
        int idx_extraPartName = table.GetColumnIndex("extraPartName");

        foreach (var row in table.Rows)
        {
            var data = new ShipLevelDataSheetData();

            var key_level = row.Get(idx_level);
            data.SheetId = new ShipLevelDataSheetId(key_level);

            data.title = row.Get(idx_title);
            data.requiredParts = int.Parse(row.Get(idx_requiredParts));
            data.factorySpeed = float.Parse(row.Get(idx_factorySpeed), System.Globalization.CultureInfo.InvariantCulture);
            data.fuelSeconds = float.Parse(row.Get(idx_fuelSeconds), System.Globalization.CultureInfo.InvariantCulture);
            data.zoneCells = int.Parse(row.Get(idx_zoneCells));
            data.extraPartName = row.Get(idx_extraPartName);

            result[data.SheetId] = data;
        }

        return result;
    }
}
