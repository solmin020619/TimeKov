// =====================================================================
// Schemas/DropTableSchema.cs
// 드롭 테이블 스키마
// 같은 dropId 를 가진 행들이 하나의 드롭 풀을 구성한다
// dropId 는 그룹 식별자 — 몬스터/상자마다 dropId 1개가 할당된다
// pickCount 개수만큼 가중치(dropWeight) 기반으로 아이템을 뽑는다
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

        // 드롭 상자/VFX 등급 (1 = 일반, 2 = 중급, 3 = 보스)
        // 같은 dropId 의 모든 행은 반드시 동일한 dropTier 를 가져야 한다
        Add("dropTier", ColumnType.Int, required: true);

        // 드롭 가중치 — 높을수록 자주 뽑힌다
        // 동일 풀 내에서 상대적 비율로 계산한다
        Add("dropWeight", ColumnType.Int, required: true);

        // 드롭 최소 수량
        Add("minCount", ColumnType.Int, required: true);

        // 드롭 최대 수량
        Add("maxCount", ColumnType.Int, required: true);

        // 한 번 드롭 시 풀에서 뽑는 아이템 종류 수
        // 예: pickCount == 2 → 가중치 기반으로 2종 아이템을 각각 1회씩 뽑는다
        Add("pickCount", ColumnType.Int, required: true);
    }
}