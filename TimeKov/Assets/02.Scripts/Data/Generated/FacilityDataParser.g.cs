// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System;
using System.Collections.Generic;

public static class FacilityDataParser
{
    public static Dictionary<string, FacilityDataSheetData> Parse(CsvTable table)
    {
        var result = new Dictionary<string, FacilityDataSheetData>();

        int idx_facilityId = table.GetColumnIndex("facilityId");
        int idx_facilityName = table.GetColumnIndex("facilityName");
        int idx_gridW = table.GetColumnIndex("gridW");
        int idx_gridH = table.GetColumnIndex("gridH");
        int idx_canRotate = table.GetColumnIndex("canRotate");
        int idx_maxLevel = table.GetColumnIndex("maxLevel");
        int idx_iconKey = table.GetColumnIndex("iconKey");
        int idx_buildSlot = table.GetColumnIndex("buildSlot");

        foreach (var row in table.Rows)
        {
            var data = new FacilityDataSheetData();

            var key_facilityId = row.Get(idx_facilityId);
            data.SheetId = new FacilityDataSheetId(key_facilityId);

            data.facilityName = row.Get(idx_facilityName);
            data.gridW = int.Parse(row.Get(idx_gridW));
            data.gridH = int.Parse(row.Get(idx_gridH));
            data.canRotate = (row.Get(idx_canRotate) == "1");
            data.maxLevel = int.Parse(row.Get(idx_maxLevel));
            data.iconKey = row.Get(idx_iconKey);
            data.buildSlot = row.Get(idx_buildSlot);

            result[data.SheetId] = data;
        }

        return result;
    }
}
