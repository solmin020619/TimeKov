// =====================================================================
// AllTableSchemas.cs
// 게임에서 사용하는 모든 테이블 스키마 목록
// 새 테이블 추가 시 GetAll() 에 항목 추가 후 Tools/Sheet/Generate 실행
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
            new FacilityLevelDataSchema(),  // 설비 레벨별 스탯
            new RecipeDataSchema(),         // 제작 레시피
            new RecipeInputDataSchema(),    // 레시피 입력 재료
            new DropTableSchema(),          // 드롭 테이블
            new CoreLevelDataSchema(),      // 코어 강화 단계 스탯
        };
    }
}