// 자동 생성 파일 — 직접 수정 금지 (Tools/Sheet/Generate 로 재생성)

using System;
using System.Collections.Generic;

public static class SkillDataParser
{
    public static Dictionary<string, SkillDataSheetData> Parse(CsvTable table)
    {
        var result = new Dictionary<string, SkillDataSheetData>();

        int idx_skillId = table.GetColumnIndex("skillId");
        int idx_skillName = table.GetColumnIndex("skillName");
        int idx_coolTime = table.GetColumnIndex("coolTime");
        int idx_totalDuration = table.GetColumnIndex("totalDuration");
        int idx_hit1Damage = table.GetColumnIndex("hit1Damage");
        int idx_hit1Radius = table.GetColumnIndex("hit1Radius");
        int idx_hit1Count = table.GetColumnIndex("hit1Count");
        int idx_hit2Damage = table.GetColumnIndex("hit2Damage");
        int idx_hit2Radius = table.GetColumnIndex("hit2Radius");
        int idx_hit2Count = table.GetColumnIndex("hit2Count");

        foreach (var row in table.Rows)
        {
            var data = new SkillDataSheetData();

            var key_skillId = row.Get(idx_skillId);
            data.SheetId = new SkillDataSheetId(key_skillId);

            data.skillName = row.Get(idx_skillName);
            data.coolTime = float.Parse(row.Get(idx_coolTime), System.Globalization.CultureInfo.InvariantCulture);
            data.totalDuration = float.Parse(row.Get(idx_totalDuration), System.Globalization.CultureInfo.InvariantCulture);
            data.hit1Damage = float.Parse(row.Get(idx_hit1Damage), System.Globalization.CultureInfo.InvariantCulture);
            data.hit1Radius = float.Parse(row.Get(idx_hit1Radius), System.Globalization.CultureInfo.InvariantCulture);
            data.hit1Count = int.Parse(row.Get(idx_hit1Count));
            data.hit2Damage = float.Parse(row.Get(idx_hit2Damage), System.Globalization.CultureInfo.InvariantCulture);
            data.hit2Radius = float.Parse(row.Get(idx_hit2Radius), System.Globalization.CultureInfo.InvariantCulture);
            data.hit2Count = int.Parse(row.Get(idx_hit2Count));

            result[data.SheetId] = data;
        }

        return result;
    }
}
