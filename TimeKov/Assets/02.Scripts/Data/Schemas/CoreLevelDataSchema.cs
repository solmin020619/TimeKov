// =====================================================================
// Schemas/CoreLevelDataSchema.cs
// 코어 강화 단계별 테이블 스키마
// 코어 강화는 0~10단계, 단계가 오를수록 최대 시간(=HP)만 성장한다
// (공격력/방어력/스태미나는 공장 앰플이 담당 — 코어에서 분리됨)
// 0단계는 초기 상태로 requiredKitItemId 가 없어 allowDash 처리한다
// =====================================================================

public class CoreLevelDataSchema : SheetSchema
{
    private const string URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vSsddL-4EKqvbusEVGI59ZhNvxBTsuNpJKGHm0x9cz1gzef86A6Fi9bz1NnOSqlTlIxVxbKXji_rCYU/pub?output=csv";

    public CoreLevelDataSchema() : base("CoreLevelData", URL)
    {
        // 코어 강화 단계 (PK, 0~10)
        Add("level", ColumnType.Int, required: true, isKey: true);

        // 해당 단계의 최대 시간(초) — 시간 = HP. 코어는 이것만 강화한다.
        Add("maxTime", ColumnType.Int, required: true);

        // 이 단계로 승급하는 데 필요한 코어 승급 키트 아이템 ID (FK → ItemData)
        // 0단계는 초기 상태라 키트가 필요 없으므로 "-" 입력 허용
        AddRef("requiredKitItemId", "ItemData", required: false, isKey: false, allowDash: true);

        // 이 단계로 강화하는 데 필요한 키트 수량
        // 0단계는 강화 대상 아님 → 0 입력
        Add("requiredAmount", ColumnType.Int, required: true);

        // 이 단계로 강화할 때의 성공 확률 (0.0 ~ 1.0)
        // 0단계는 강화 대상 아니므로 1.0 입력
        Add("successRate", ColumnType.Float, required: true);
    }
}