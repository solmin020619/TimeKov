using System;

// itemDataTable.csv 한 줄 데이터
// 아이템 기본 마스터 테이블
[Serializable]
public class ItemRow
{
    public int itemId;              // 아이템 고유 ID
    public string itemName;         // 아이템 이름
    public string itemType;         // 아이템 대분류 (weapon, armor, backpack, ammo, heal, resource 등)
    public string subType;          // 아이템 소분류
    public string description;      // 아이템 설명
    public string iconKey;          // 아이콘 리소스 키
    public int stackable;           // 중첩 가능 여부 (0/1)
    public int maxStack;            // 최대 중첩 수량
    public float weight;            // 무게
    public int sellValue;           // 기본 판매 가치
    public int rarityTier;          // 희귀도 티어
    public int isDroppable;         // 드랍 가능 여부 (0/1)
    public int isCraftable;         // 제작 가능 여부 (0/1)
    public string factoryUsage;     // 공장 사용 타입
    public int isProcessed;         // 가공품 여부 (0/1)
    public int isFinalProduct;      // 최종 생산물 여부 (0/1)
}


// factoryItemDataTable.csv 한 줄 데이터
// 공장/가공 관련 추가 정보
[Serializable]
public class FactoryItemRow
{
    public int itemId;              // itemDataTable과 연결되는 itemId
    public string factoryUsage;     // smeltable, shreddable, sellOnly 등
    public int powerMinutes;        // 전력 환산용 시간 값
    public int tradeValue;          // 판매 가치
    public int isProcessed;         // 가공품 여부
    public int isFinalProduct;      // 최종 생산물 여부
}


// weaponDataTable.csv 한 줄 데이터
// 무기 성능 테이블
[Serializable]
public class WeaponRow
{
    public int itemId;                  // itemDataTable의 무기 itemId
    public int ammoItemId;              // 사용하는 탄약 itemId
    public int magazineSize;            // 탄창 크기
    public int isAutomatic;             // 연사 여부 (0/1)
    public float damage;                // 기본 데미지
    public float fireRate;              // 초당 발사 수
    public float reloadTime;            // 재장전 시간
    public float effectiveRange;        // 유효 사거리
    public int useRecoilPattern;        // 반동 패턴 사용 여부 (0/1)
    public float randomRecoilAngle;     // 랜덤 반동 각도
    public float recoilResetTime;       // 반동 복구 시간
    public int pelletsPerShot;          // 샷건 등 1발당 펠릿 수
    public float spreadAngle;           // 탄 퍼짐 각도
}


// equipmentDataTable.csv 한 줄 데이터
// 방어구 / 가방 장비 데이터

[Serializable]
public class EquipmentRow
{
    public int itemId;              // itemDataTable의 장비 itemId
    public string equipType;        // helmet, vest, bag 또는 backpack
    public int equipLevel;          // 장비 레벨
    public int defense;             // 방어력
    public int addSlotCount;        // 추가 슬롯 수 (가방 등)
    public int durability;          // 내구도
}

// facilityDataTable.csv 한 줄 데이터
// 시설 기본 정보
[Serializable]
public class FacilityRow
{
    public int facilityId;          // 시설 고유 ID
    public string facilityName;     // 시설 이름
    public string facilityType;     // 시설 분류
    public int gridW;               // 설치 가로 크기
    public int gridH;               // 설치 세로 크기
    public int inputCount;          // 입력 슬롯 수
    public int outputCount;         // 출력 슬롯 수
    public int requiresPower;       // 전력 필요 여부 (0/1)
    public int canRotate;           // 회전 가능 여부 (0/1)
    public string installRule;      // 설치 규칙
    public int maxLevel;            // 최대 레벨
}

// facilityLevelDataTable.csv 한 줄 데이터
// 시설 레벨별 보정값
[Serializable]
public class FacilityLevelRow
{
    public int facilityId;                  // 연결될 시설 ID
    public int level;                       // 레벨
    public float processTimeMultiplier;     // 처리 시간 배수
    public float powerEfficiencyMultiplier; // 전력 효율 배수
    public int capacityBonus;               // 용량 보너스
}

// recipeDataTable.csv 한 줄 데이터
// 레시피 메타 정보
[Serializable]
public class RecipeRow
{
    public int recipeId;            // 레시피 ID
    public int facilityId;          // 사용 시설 ID
    public int craftTechTier;       // 요구 제작 티어
    public float craftTime;         // 제작 시간
    public int powerCost;           // 전력 소모
    public string recipeGroup;      // 레시피 그룹
}

// recipeInputDataTable.csv 한 줄 데이터
// 레시피 재료 목록
[Serializable]
public class RecipeInputRow
{
    public int recipeId;            // 연결될 레시피 ID
    public int inputItemId;         // 재료 itemId
    public int inputCount;          // 재료 수량
}

// recipeOutputDataTable.csv 한 줄 데이터
// 레시피 결과물 목록
[Serializable]
public class RecipeOutputRow
{
    public int recipeId;            // 연결될 레시피 ID
    public int outputItemId;        // 결과물 itemId
    public int outputCount;         // 결과물 수량
}

// miningOutputTable.csv 한 줄 데이터
// 채굴 결과 테이블
[Serializable]
public class MiningOutputRow
{
    public string veinType;         // 광맥 타입
    public int facilityId;          // 사용 시설 ID
    public int outputItemId;        // 산출 itemId
    public int outputCount;         // 산출 수량
    public float baseCycleTime;     // 기본 채굴 주기
}

// dropTable.csv 한 줄 데이터
// 드랍 테이블 데이터
[Serializable]
public class DropRow
{
    public int dropId;              // 드랍 그룹 ID
    public string sourceType;       // sourceType (monster, crate 등)
    public string sourceId;         // sourceId
    public int itemId;              // 드랍되는 아이템 ID
    public int dropTier;            // 드랍 티어
    public int dropWeight;          // 가중치
    public int minCount;            // 최소 수량
    public int maxCount;            // 최대 수량
    public int pickCount;           // 이 그룹에서 뽑는 개수
}