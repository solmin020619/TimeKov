// =====================================================================
// Schemas/FacilityLevelDataSchema.cs
// 설비 레벨별 스탯 테이블 스키마
// facilityId + level 복합키 — 같은 설비의 레벨별 성능 배율을 정의
// =====================================================================

public class FacilityLevelDataSchema : SheetSchema
{
    private const string URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vSsZpzFvy-9iDrS9bMb_g79V9X03WZyay_9D9X1X0_NZ3RwsuA_Vxw1nHSx-4nATUS1HqanJy0HRb_i/pub?output=csv";

    public FacilityLevelDataSchema() : base("FacilityLevelData", URL)
    {
        // 설비 ID (PK 복합, FK → FacilityData)
        AddRef("facilityId", "FacilityData", required: true, isKey: true);

        // 레벨 (PK 복합, 1 이상)
        Add("level", ColumnType.Int, required: true, isKey: true);

        // 제작 시간 배율
        // 1.0 = 기본, 0.8 = 20% 빠름, 숫자가 낮을수록 빠르다
        Add("processTimeMultiplier", ColumnType.Float, required: true);

        // 전력 효율 배율
        // 1.0 = 기본, 0.8 = 20% 절약, 숫자가 낮을수록 효율적이다
        Add("powerEfficiencyMultiplier", ColumnType.Float, required: true);
    }
}