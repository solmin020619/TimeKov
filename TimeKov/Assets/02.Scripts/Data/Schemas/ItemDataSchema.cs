// =====================================================================
// Schemas/ItemDataSchema.cs
// 아이템 기본 정보 테이블 스키마
// 게임 내 모든 아이템의 마스터 데이터
// =====================================================================

public class ItemDataSchema : SheetSchema
{
    private const string URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vT1hzCMhJ9lUwmhGX7pStCPnoSXF6nTq3c2oqBfUgIDFJ4Z-Cmj18pIjLi0NNlTkGt9yELSQM6gRuww/pub?output=csv";

    public ItemDataSchema() : base("ItemData", URL)
    {
        // 아이템 고유 ID (PK, 예: "1101")
        Add("itemId", ColumnType.String, required: true, isKey: true);

        // 아이템 이름
        Add("itemName", ColumnType.String, required: true);

        // 등급 — Common / Advanced / Rare / Hero / Legend
        AddEnum<ItemGrade>("itemGrade", required: true);

        // 분류 — RawMaterial / ProcessedTier1 / ProcessedTier2 / TacticalConsumable / CoreUpgrade / Special
        AddEnum<ItemCategory>("itemCategory", required: true);

        // UI 아이콘 리소스 키 (Addressables 또는 Resources 경로)
        Add("iconKey", ColumnType.String, required: true);

        // 최대 중첩 수량 (1 = 중첩 불가)
        Add("maxStack", ColumnType.Int, required: true);
    }
}