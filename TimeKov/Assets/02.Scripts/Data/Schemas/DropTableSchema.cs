// =====================================================================
// Schemas/DropTableSchema.cs
// 드롭 테이블 스키마 (독립 확률 방식)
// 같은 dropId 를 가진 행들 = 한 몬스터/상자의 드롭 목록
// 각 아이템은 dropChance% 로 독립 판정(서로 영향 없음), 드롭 시 개수는 countChance 로 분포
// =====================================================================

public class DropTableSchema : SheetSchema
{
    private const string URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vTd0x6DH_H6JVErt2Y1M_6RwiZvR2fAeJ6DMzsivgdRx-lk41ZPxnEZ7KVva83abGlzNDSOG9RfNPic/pub?gid=0&single=true&output=csv";

    public DropTableSchema() : base("DropTable", URL)
    {
        // 드롭 그룹 ID (PK 복합)
        // 같은 dropId = 같은 드롭 풀 = 상자 VFX 1개
        // itemId 와 함께 복합키를 구성한다 — (dropId, itemId) 쌍이 고유해야 한다
        Add("dropId", ColumnType.Int, required: true, isKey: true);

        // 드롭 출처 유형 — Chest(상자) / Monster(몬스터)
        AddEnum<SourceType>("sourceType", required: true);

        // 출처 식별자
        // sourceType == Chest   → 상자 종류 이름 (예: LC_LOOT)
        // sourceType == Monster → 몬스터 데이터 ID (예: MeleeBot_Ghoul)
        // 게임 코드에서 이 문자열로 DropTable 을 조회한다
        Add("sourceId", ColumnType.String, required: true);

        // 드롭될 아이템 ID (PK 복합, FK → ItemData)
        // dropId 와 함께 복합키 — 같은 풀에 같은 아이템이 두 번 등록되는 실수를 방지한다
        AddRef("itemId", "ItemData", required: true, isKey: true);

        // 드롭 확률 (0~100). 각 아이템이 독립적으로 이 확률로 드롭된다(다른 행과 무관)
        Add("dropChance", ColumnType.Int, required: true);

        // 개수 분포. 파이프(|) 구분 = 1개|2개|3개... 의 확률(비율).
        // 예: "70|30"=1개70%/2개30%, "100"=무조건1개, "0|40|30|30"=2개40%/3개30%/4개30%
        // (구글시트가 / 를 날짜로 바꿔서 | 사용)
        Add("countDist", ColumnType.String, required: true);
    }
}