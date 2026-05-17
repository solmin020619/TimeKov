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
        int idx_facilityType = table.GetColumnIndex("facilityType");
        int idx_gridW = table.GetColumnIndex("gridW");
        int idx_gridH = table.GetColumnIndex("gridH");
        int idx_inputSlotCount = table.GetColumnIndex("inputSlotCount");
        int idx_outputSlotCount = table.GetColumnIndex("outputSlotCount");
        int idx_requiresPower = table.GetColumnIndex("requiresPower");
        int idx_canRotate = table.GetColumnIndex("canRotate");
        int idx_maxLevel = table.GetColumnIndex("maxLevel");

        foreach (var row in table.Rows)
        {
            var data = new FacilityDataSheetData();

            var key_facilityId = row.Get(idx_facilityId);
            data.SheetId = new FacilityDataSheetId(key_facilityId);

            data.facilityName = row.Get(idx_facilityName);
            data.facilityType = (FacilityType)Enum.Parse(typeof(FacilityType), row.Get(idx_facilityType));
            data.gridW = int.Parse(row.Get(idx_gridW));
            data.gridH = int.Parse(row.Get(idx_gridH));
            data.inputSlotCount = int.Parse(row.Get(idx_inputSlotCount));
            data.outputSlotCount = int.Parse(row.Get(idx_outputSlotCount));
            data.requiresPower = (row.Get(idx_requiresPower) == "1");
            data.canRotate = (row.Get(idx_canRotate) == "1");
            data.maxLevel = int.Parse(row.Get(idx_maxLevel));

            result[data.SheetId] = data;
        }

        return result;
    }
}
