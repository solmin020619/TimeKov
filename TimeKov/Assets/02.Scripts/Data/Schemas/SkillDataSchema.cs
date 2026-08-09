// =====================================================================
// Schemas/SkillDataSchema.cs
// 플레이어 평타/스킬의 피해와 쿨타임. SO 는 VFX/애니 참조를 계속 들고, 숫자만 여기서 덮어쓴다.
//
// [형태가 왜 이런가] 스킬마다 타격 구성이 다르다.
//   평타       = 1히트
//   Q/E        = 2히트 (Hit1 + Hit2)
//   R 사이클론 = 회전 4연타 + 마무리 점프
//   그래서 '히트 슬롯 2개 + 각 슬롯의 반복 횟수' 로 통일했다.
//   R 은 hit1Count=4(회전), hit2Count=1(점프) 이 된다.
//
// [건드리지 않는 것] 타격 타이밍(Hit1Time 등)은 애니메이션 클립에 맞춘 값이라 SO 에 남긴다.
//   시트에 있으면 클립을 보면서 맞춰야 하는 값이 엉뚱한 곳에 흩어진다.
//
// [시트에 행이 없으면] 그 스킬은 SO 값을 그대로 쓴다.
// =====================================================================

public class SkillDataSchema : SheetSchema
{
    private const string URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vTGZuT1HIpiv0i5Asx6w5isvUrd0_B4MudIOaGjeRMd3ZfUW0CAxuwHmX-HxgGV5gSjRbUkv8FSmHl2/pub?output=csv";

    public SkillDataSchema() : base("SkillData", URL)
    {
        // SO 에셋 파일명 (PK). 예: Attack1Skill / SkillQ_ReaperSlash / SkillR_CycloneBreak
        Add("skillId", ColumnType.String, required: true, isKey: true);

        // 표시 이름. 시트를 읽기 위한 것.
        Add("skillName", ColumnType.String, required: false);

        // 쿨타임(초). 평타는 0.
        Add("coolTime", ColumnType.Float, required: true);

        // 스킬 전체 길이(초). 평타는 0(콤보 흐름이 따로 관리).
        Add("totalDuration", ColumnType.Float, required: true);

        // 1번째 타격 묶음
        Add("hit1Damage", ColumnType.Float, required: true);
        Add("hit1Radius", ColumnType.Float, required: true);
        Add("hit1Count", ColumnType.Int, required: true);

        // 2번째 타격 묶음. 없으면 전부 0.
        Add("hit2Damage", ColumnType.Float, required: true);
        Add("hit2Radius", ColumnType.Float, required: true);
        Add("hit2Count", ColumnType.Int, required: true);
    }
}
