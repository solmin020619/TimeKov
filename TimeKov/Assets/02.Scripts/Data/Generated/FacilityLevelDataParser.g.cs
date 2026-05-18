// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System;
using System.Collections.Generic;

public static class FacilityLevelDataParser
{
    public static Dictionary<string, FacilityLevelDataSheetData> Parse(CsvTable table)
    {
        var result = new Dictionary<string, FacilityLevelDataSheetData>();

        int idx_facilityId = table.GetColumnIndex("facilityId");
        int idx_level = table.GetColumnIndex("level");
        int idx_processTimeMultiplier = table.GetColumnIndex("processTimeMultiplier");
        int idx_powerEfficiencyMultiplier = table.GetColumnIndex("powerEfficiencyMultiplier");

        foreach (var row in table.Rows)
        {
            var data = new FacilityLevelDataSheetData();

            var key_facilityId = row.Get(idx_facilityId);
            var key_level = row.Get(idx_level);
            // 복합키: string 연결 후 explicit cast
            data.SheetId = (FacilityLevelDataSheetId)(key_facilityId + "_" + key_level);

            data.processTimeMultiplier = float.Parse(row.Get(idx_processTimeMultiplier), System.Globalization.CultureInfo.InvariantCulture);
            data.powerEfficiencyMultiplier = float.Parse(row.Get(idx_powerEfficiencyMultiplier), System.Globalization.CultureInfo.InvariantCulture);

            result[data.SheetId] = data;
        }

        return result;
    }
}
