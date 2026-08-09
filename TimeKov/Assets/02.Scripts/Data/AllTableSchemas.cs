// =====================================================================
// AllTableSchemas.cs
// 게임에서 사용하는 모든 테이블 스키마 목록
// 새 테이블 추가 시 GetAll() 에 항목 추가 후 메뉴 '시트 > 코드 다시 만들기' 실행
// =====================================================================

using System.Collections.Generic;

public static class AllTableSchemas
{
    // 전체 스키마 반환
    // CodeGenerator 와 TableBuildMenu 가 이 목록을 기준으로 동작한다
    public static List<SheetSchema> GetAll()
    {
        return new List<SheetSchema>
        {
            new ItemDataSchema(),           // 아이템 기본 정보
            new ConsumableEffectSchema(),   // 소모품 효과 상세
            new FacilityDataSchema(),       // 설비 기본 정보
            new RecipeDataSchema(),         // 제작 레시피
            new RecipeInputDataSchema(),    // 레시피 입력 재료
            new DropTableSchema(),          // 드롭 테이블
            new CoreLevelDataSchema(),      // 코어 강화 단계 스탯
            new ShipLevelDataSchema(),      // 우주선 수리 단계별 보상(건축범위/공장속도/연비)
            new MonsterStatDataSchema(),    // 몬스터 전투 수치
            new SkillDataSchema(),          // 플레이어 평타/스킬 피해·쿨
            new PlayerStatDataSchema(),     // 플레이어 기본 수치(키-값)
        };
    }
}