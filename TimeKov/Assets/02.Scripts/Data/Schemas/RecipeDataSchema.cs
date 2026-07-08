// =====================================================================
// Schemas/RecipeDataSchema.cs
// 제작 레시피 테이블 스키마
// 어떤 설비에서 무엇을 만드는지, 시간과 전력 비용을 정의
// 입력 재료는 RecipeInputDataTable 에 별도 저장 (1:N 관계)
// recipeOutputDataTable 을 폐기하고 outputItemId, outputCount 를 여기에 통합
// =====================================================================

public class RecipeDataSchema : SheetSchema
{
    private const string URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vRwsolM12R2G7sFVW4Jt1fQvOgNt9SVgmzMbibQXJqyQO2p6Q94uu2BuSZsLy_7gJrBes2Hi2Bpi7SH/pub?output=csv";

    public RecipeDataSchema() : base("RecipeData", URL)
    {
        // 레시피 고유 ID (PK, 예: "R1001")
        Add("recipeId", ColumnType.String, required: true, isKey: true);

        // 사용 설비 ID (FK → FacilityData)
        AddRef("facilityId", "FacilityData", required: true);

        // 생산 아이템 ID (FK → ItemData)
        AddRef("outputItemId", "ItemData", required: true);

        // 생산 수량
        Add("outputCount", ColumnType.Int, required: true);

        // 제작 소요 시간(초) — FacilityLevelData.processTimeMultiplier 와 곱해서 실제 시간 산출
        Add("craftTime", ColumnType.Float, required: true);
    }
}