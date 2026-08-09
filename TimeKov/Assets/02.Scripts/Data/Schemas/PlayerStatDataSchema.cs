// =====================================================================
// Schemas/PlayerStatDataSchema.cs
// 플레이어 기본 수치. 키-값 한 줄에 하나씩.
//
// [왜 키-값인가] 플레이어는 한 명이라 '행 = 개체' 구조가 안 맞는다.
//   한 줄에 컬럼 15개를 늘어놓으면 시트를 가로로 스크롤해야 해서 읽기 나쁘다.
//   키-값이면 세로로 읽히고, 값 옆에 설명(note)을 달 수 있고, 항목 추가도 행 하나면 된다.
//
// [왜 시트로 뺐나] 값이 두 군데로 흩어져 있었다.
//   PlayerBaseStats(코드 const: ATK/DEF/스태미나) + PlayerStatComponent(씬 직렬화: 드레인/회복/무적).
//   씬 값은 고칠 때마다 LFS 씬 파일이 통째로 바뀌고 diff 도 안 보였다.
//   실제로 코드 기본값과 씬 값이 어긋나 있었다(스태미나 회복 5 vs 40, 탈진 임계 0.3 vs 0.1).
//
// [시트에 키가 없으면] 기존 값을 그대로 쓴다. 항목을 하나씩 옮겨도 안전하다.
// =====================================================================

public class PlayerStatDataSchema : SheetSchema
{
    private const string URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vQU4C1IyNPZpEZ-e_fe0Vf0pb_MkYUFBTzQgKqhIy6v6wrcjUG-z3sFkrkKysFr2tQp7dLkfqJFbWrv/pub?output=csv";

    public PlayerStatDataSchema() : base("PlayerStatData", URL)
    {
        // 수치 이름 (PK). 코드가 이 문자열로 찾는다 - 오타나면 그 항목만 조용히 기본값이 된다.
        Add("statKey", ColumnType.String, required: true, isKey: true);

        Add("value", ColumnType.Float, required: true);

        // 사람이 읽는 설명. 게임은 안 쓴다.
        Add("note", ColumnType.String, required: false);
    }
}
