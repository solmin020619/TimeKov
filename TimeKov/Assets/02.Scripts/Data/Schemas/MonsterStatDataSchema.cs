// =====================================================================
// Schemas/MonsterStatDataSchema.cs
// 몬스터 전투 수치. SO 는 프리팹/VFX/애니 참조를 계속 들고, '숫자'만 여기서 덮어쓴다.
//
// [왜 시트로 뺐나] 31종 스탯이 SO 파일에 흩어져 있어서, 맵별 난이도 곡선을 보려면
//   파일 31개를 열어야 했다. 시트면 자연->설원->사막->용암이 한 화면에 보이고
//   "설원이 좀 낮네" 같은 판단이 즉시 된다. 밸런싱 표면적의 대부분이 여기다.
//
// [키가 왜 SO 파일명인가 - 중요]
//   enemyId 를 쓰고 싶었지만 구 EnemyData 7종(이블와쳐/미라/오크트리/해골기사/
//   언데드/늑대인간/와이번)이 전부 'tutorial_enemy' 로 같아서 키가 충돌한다.
//   enemyId 는 킬 퀘스트 매칭에 쓰이는 값이라 함부로 바꾸면 퀘스트가 깨진다.
//   SO 파일명(EnemyData_Werewolf 등)은 고유하고 퀘스트와 무관해서 안전하다.
//
// [시트에 행이 없으면] 그 몬스터는 SO 값을 그대로 쓴다. 마이그레이션 중에 빠져도 안 깨진다.
// =====================================================================

public class MonsterStatDataSchema : SheetSchema
{
    private const string URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vTU6dDCW2JUb95MV_YdfRbmJcBQcmCZqFTgsIduEk_QvaFbHFD0UKdcvOjOflS-L2pb4bjOhn2BKH-U/pub?output=csv";

    public MonsterStatDataSchema() : base("MonsterStatData", URL)
    {
        // SO 에셋 파일명 (PK). 예: EnemyData_Werewolf / HellData_hell_hound / FieldData_RockMonster_Lava
        Add("statId", ColumnType.String, required: true, isKey: true);

        // 표시 이름. 사람이 시트를 읽기 위한 것이고 게임은 SO 의 enemyName 을 쓴다.
        Add("monsterName", ColumnType.String, required: false);

        // 서식 지역: 자연 / 설원 / 사막 / 용암. 도감 액자 색 등 '이 몹이 어느 맵 소속인가'의 단일 출처.
        // [왜 시트인가] 예전엔 프리팹 이름 규칙(_Snow/_Desert/...)으로 추측했는데,
        //   본드래곤/자이언트웜/자폭거미처럼 이름에 지역이 없는 몹을 자연으로 오판했다.
        //   몹을 추가할 때마다 코드에 예외를 늘려야 하는 구조라 시트로 옮겼다.
        // 선택 컬럼: 비어 있으면 이름 규칙으로 폴백한다(RegionPalette).
        Add("region", ColumnType.String, required: false);

        Add("maxHP", ColumnType.Float, required: true);
        Add("attackDamage", ColumnType.Float, required: true);
        Add("attackRange", ColumnType.Float, required: true);
        Add("attackCooldown", ColumnType.Float, required: true);
        Add("moveSpeed", ColumnType.Float, required: true);
        Add("visionRange", ColumnType.Float, required: true);
    }
}
