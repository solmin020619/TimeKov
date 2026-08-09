// 자동 생성 파일 — 직접 수정 금지 (메뉴 '시트 > 코드 다시 만들기' 로 재생성)

using System;
using System.Collections.Generic;

public static class MonsterStatDataParser
{
    public static Dictionary<string, MonsterStatDataSheetData> Parse(CsvTable table)
    {
        var result = new Dictionary<string, MonsterStatDataSheetData>();

        int idx_statId = table.GetColumnIndex("statId");
        int idx_monsterName = table.GetColumnIndex("monsterName");
        int idx_maxHP = table.GetColumnIndex("maxHP");
        int idx_attackDamage = table.GetColumnIndex("attackDamage");
        int idx_attackRange = table.GetColumnIndex("attackRange");
        int idx_attackCooldown = table.GetColumnIndex("attackCooldown");
        int idx_moveSpeed = table.GetColumnIndex("moveSpeed");
        int idx_visionRange = table.GetColumnIndex("visionRange");

        foreach (var row in table.Rows)
        {
            var data = new MonsterStatDataSheetData();

            var key_statId = row.Get(idx_statId);
            data.SheetId = new MonsterStatDataSheetId(key_statId);

            data.monsterName = row.Get(idx_monsterName);
            data.maxHP = float.Parse(row.Get(idx_maxHP), System.Globalization.CultureInfo.InvariantCulture);
            data.attackDamage = float.Parse(row.Get(idx_attackDamage), System.Globalization.CultureInfo.InvariantCulture);
            data.attackRange = float.Parse(row.Get(idx_attackRange), System.Globalization.CultureInfo.InvariantCulture);
            data.attackCooldown = float.Parse(row.Get(idx_attackCooldown), System.Globalization.CultureInfo.InvariantCulture);
            data.moveSpeed = float.Parse(row.Get(idx_moveSpeed), System.Globalization.CultureInfo.InvariantCulture);
            data.visionRange = float.Parse(row.Get(idx_visionRange), System.Globalization.CultureInfo.InvariantCulture);

            result[data.SheetId] = data;
        }

        return result;
    }
}
