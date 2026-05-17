// =====================================================================
// GameEnums.cs
// 게임 전체에서 사용하는 Enum 타입 모음
// 스키마에서 AddEnum<T> 로 참조하고,
// 생성된 SheetData 클래스의 필드 타입으로 사용된다
// =====================================================================

// 아이템 등급
// ItemDataTable.itemGrade 에서 사용
public enum ItemGrade
{
    Common,   // 일반
    Advanced, // 고급
    Rare,     // 희귀
    Hero,     // 영웅
    Legend    // 전설
}

// 아이템 분류
// ItemDataTable.itemCategory 에서 사용
public enum ItemCategory
{
    RawMaterial,        // 원초 재료 (필드/몬스터 수집)
    ProcessedTier1,     // 1차 가공품 (설비 1단계 처리)
    ProcessedTier2,     // 2차 심화 가공품 (설비 2단계 처리)
    TacticalConsumable, // 전술 소모품 (힐/버프/스태미나)
    CoreUpgrade,        // 코어 강화 재료
    Special             // 보스 코어 조각 등 특수 아이템
}

// 소모품 효과 유형
// ConsumableEffectTable.consumableType 에서 사용
public enum ConsumableType
{
    Heal,        // 즉시 Time 회복
    SustainHeal, // 지속 Time 회복 (초당)
    Buff,        // 스탯 일시 강화
    Stamina,     // 스태미나 관련 효과
    Special      // 기타 특수 효과
}

// 소모품 효과 대상 스탯
// ConsumableEffectTable.effectTarget 에서 사용
public enum EffectTarget
{
    Time,         // 시간(HP)
    ATK,          // 공격력
    MoveSpeed,    // 이동 속도
    Stamina,      // 스태미나 최대치
    SkillGauge,   // 스킬 게이지 충전량
    TimeDecay,    // 필드 시간 감소율
    AllStats,     // 전체 스탯
    DashStamina   // 대시 스태미나 소모량
}

// 소모품 수치 적용 방식
// ConsumableEffectTable.effectValueType 에서 사용
public enum EffectValueType
{
    Flat,       // 고정 수치 증가 (예: ATK +30)
    Percent,    // 현재 값 기준 퍼센트 (예: ATK +30%)
    MaxPercent  // 최대치 기준 퍼센트 (예: 최대 Time 의 30%)
}

// 설비 유형
// FacilityDataTable.facilityType 에서 사용
public enum FacilityType
{
    BioExtractor,   // 생체 추출기 (1번)
    MineralCrusher, // 광물 분쇄기 (2번)
    ChemRefinery,   // 화학 정제기 (3번)
    MatterFuser,    // 물질 융합기 (4번)
    TacticalMfg,    // 전술 장비 제조기 (5번)
    BioInjector,    // 생체 주입기 (6번)
    CoreFusion,     // 코어 융합실 (7번)
    VoidAssembler   // 보이드 조립기 (8번)
}

// 드롭 출처 유형
// DropTable.sourceType 에서 사용
public enum SourceType
{
    Chest,   // 상자
    Monster  // 몬스터
}