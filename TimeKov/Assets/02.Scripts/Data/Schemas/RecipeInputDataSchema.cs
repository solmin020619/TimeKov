// =====================================================================
// Schemas/RecipeInputDataSchema.cs
// 레시피 입력 재료 테이블 스키마
// 레시피 1개당 재료가 여러 개이므로 RecipeDataTable 과 1:N 관계
// recipeId + inputItemId 복합키
// =====================================================================

public class RecipeInputDataSchema : SheetSchema
{
    private const string URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vS_iazdrilD_nR7iru88wtZDJA4WGd_GVtuo9CJR_wSwWizFQKsXKrZhLGlTbMaiQs8lDIhbpZ99FKV/pub?output=csv";

    public RecipeInputDataSchema() : base("RecipeInputData", URL)
    {
        // 레시피 ID (PK 복합, FK → RecipeData)
        AddRef("recipeId", "RecipeData", required: true, isKey: true);

        // 재료 아이템 ID (PK 복합, FK → ItemData)
        AddRef("inputItemId", "ItemData", required: true, isKey: true);

        // 필요 재료 수량
        Add("inputCount", ColumnType.Int, required: true);
    }
}