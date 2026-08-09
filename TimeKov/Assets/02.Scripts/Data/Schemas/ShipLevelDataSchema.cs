// =====================================================================
// Schemas/ShipLevelDataSchema.cs
// 우주선 수리 레벨별 수치.
//
// [왜 시트로 뺐나] 예전엔 이 표가 씬(World.unity)에 직렬화돼 있었다. 그래서
//   수치 하나만 고쳐도 LFS 씬 파일이 통째로 바뀌고, diff 로 뭐가 변했는지 안 보였다.
//   코드의 기본값 표와 씬 실제값이 어긋나도 아무도 모르는 상태였고, 실제로 07-22 에
//   단계 4개가 전부 38x38 로 같아져 건축 확장이 아예 안 먹던 사고가 있었다.
// =====================================================================

public class ShipLevelDataSchema : SheetSchema
{
    private const string URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vSyvDFJK2g0U1xrGTeXQlojLKW3OWoRHoqTxHiLISfqxRAVp-myZN6AVYTRPIQwv4UqljSEAvrdLTpq/pub?output=csv";

    public ShipLevelDataSchema() : base("ShipLevelData", URL)
    {
        // 수리 레벨 (PK). 1 = 시작(파손 상태).
        Add("level", ColumnType.Int, required: true, isKey: true);

        // 단계명. UI/토스트 표시용.
        Add("title", ColumnType.String, required: true);

        // 이 레벨에 '도달'하기 위해 필요한 복구 에너지 개수. Lv.1 은 0.
        Add("requiredParts", ColumnType.Int, required: true);

        // 제작시간 배수(작을수록 빠름). 1 = 기본.
        Add("factorySpeed", ColumnType.Float, required: true);

        // 연료 1개당 설비 가동 시간(초).
        Add("fuelSeconds", ColumnType.Float, required: true);

        // 건축 영역 한 변의 칸 수. 0 이면 변경 없음.
        // 단계 번호를 거치지 않고 칸 수를 직접 적는다 - 한 다리 건너면 표와 실제가 어긋난다.
        Add("zoneCells", ColumnType.Int, required: true);

        // 이 레벨에 추가로 필요한 전용 재료 이름. 비우면 없음.
        // 인벤토리 아이템이 아니라 회수하면 바로 흡수되는 방식이라 개수 개념이 없다.
        Add("extraPartName", ColumnType.String, required: false);
    }
}
