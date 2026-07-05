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
    Heal,          // 즉시 Time 회복
    SustainHeal,   // 지속 Time 회복 (초당)
    Buff,          // 스탯 일시 강화 (duration 후 자동 원복)
    PermanentStat, // 스탯 영구 증가 (revert 없음, 누적)
    Stamina,       // 스태미나 관련 효과
    Special        // 기타 특수 효과
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
    DashStamina,  // 대시 스태미나 소모량
    DEF           // 방어력 (영구 앰플용. enum 끝에 추가해 기존 멤버 int 값 보존)
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
    BioExtractor,     // 생체 추출기 (1번)
    BioCultivator,    // 생체 배양기 (2번)
    ChemRefinery,     // 화학 정제기 (3번)
    Smelter,          // 용해로 (4번)
    BioSeparator,     // 생체 분리기 (5번)
    CoreSynthesizer,  // 코어 합성기 (6번)
    EnergyConverter,  // 에너지 변환기 (7번)
    Storage,          // 저장고 (8번)
    StoragePort       // 창고 출력 포트 (9번, 테두리 설치)
}

// 드롭 출처 유형
// DropTable.sourceType 에서 사용
public enum SourceType
{
    Chest,   // 상자
    Monster  // 몬스터
}