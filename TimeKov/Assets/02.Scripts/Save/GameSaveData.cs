// =====================================================================
// GameSaveData.cs
// 슬롯(월드) 하나의 진행 데이터 전체. JsonUtility로 save.json에 직렬화.
//
// 설정(SettingsData)은 포함하지 않음 — 설정은 슬롯과 무관한 기기 단위 환경설정이라
// 지금처럼 별도로 settings.json에 저장한다 (SaveSlotManager가 건드리지 않음).
//
// 확장 방법: 새 시스템을 추가할 때는
//   1) 여기에 [Serializable] 데이터 묶음 필드를 추가
//   2) 그 시스템이 ISaveable.Capture(data)에서 이 필드를 채움
//   3) 그 시스템의 Awake/Start에서 SaveSlotManager.Instance.Data의 이 필드를 읽어 스스로 복원
// =====================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameSaveData
{
    // 세이브 포맷 버전 — 나중에 시트/스키마가 바뀌어도 마이그레이션 분기를 둘 수 있게.
    public int saveVersion = 1;

    // ── 코어 강화 ──────────────────────────────────────────────────────
    public int coreLevel = 0;

    // ── 퀘스트 진행도 (카테고리별 현재 퀘스트 인덱스) ───────────────────
    public List<QuestCategoryProgress> questProgress = new();

    // ── 인벤토리 (가방 / 창고) ───────────────────────────────────────────
    // 빈 슬롯은 저장하지 않음 — 비어있지 않은 슬롯만 slotIndex와 함께 기록.
    public List<InventorySlotData> bagSlots = new();
    public List<InventorySlotData> storageSlots = new();

    // ── 플레이어 상태 (HP=시간) ──────────────────────────────────────────
    // 절대값이 아니라 비율로 저장 — 코어 강화로 MaxHp가 달라져도 같은 비율로 복원되게.
    // (PlayerStatComponent.Respawn(hpPercent)와 동일한 설계 원칙)
    public float playerHpPercent = 1f;

    // ── 플레이어 영구 스탯 (앰플로 누적되는 ATK/DEF/MaxStamina, ConsumableEffectApplier.ApplyPermanentStat) ──
    // 코어 강화(MaxHp)와 분리된 별도 누적치라 절대값 그대로 저장.
    // 시작값의 원본은 PlayerBaseStats 하나뿐이다. 숫자를 바꾸려면 거기서 바꿔라.
    public float playerATK = PlayerBaseStats.ATK;
    public float playerDEF = PlayerBaseStats.DEF;
    public float playerMaxStamina = PlayerBaseStats.MaxStamina;

    // ── 플레이어 위치 ────────────────────────────────────────────────────
    // hasPlayerPosition=false면 새 슬롯이라 씬 기본 스폰 위치를 그대로 둔다(원점으로 텔레포트 안 함).
    public bool hasPlayerPosition = false;
    public Vector3 playerPosition;
    public float playerRotationY;
    // 위치를 저장한 씬 이름. 복원은 같은 씬일 때만 한다. 프롤로그(Tutorial) 좌표가 World로
    // 새어들어와 바다 위 등 엉뚱한 곳에 스폰돼 즉사하던 것 방지. 다른 씬이거나 구버전 세이브(빈값)면
    // 복원을 건너뛰고 그 씬에 배치된 기본 스폰 위치를 그대로 쓴다.
    public string playerPositionScene = "";

    // ── Factory/Grid: 배치된 건물 / 레일 ─────────────────────────────────
    // 위치·회전·강화레벨만 저장. 레일은 연결 방향만 저장하고 흐름 화살표/포트 검증은
    // RailBuildManager.RestoreReflowAndValidate()로 복원 후 재계산한다.
    public List<PlacedBuildingData> buildings = new();
    public List<RailPieceData> rails = new();

    // ── 설비 내부 생산 상태 (originCell 기준으로 buildings 항목과 매칭) ───────
    // 진행 중이던 정확한 제작 시점(코루틴 elapsed)까지는 복원하지 않음 — 버퍼/연료/
    // 고정 레시피만 복원하면 재료가 충분할 때 ProcessingMachine이 스스로 생산을 재개한다.
    public List<MachineSaveData> machines = new();

    // ── 설비 해금 (퀵슬롯 1~9, FacilityUnlockManager) ───────────────────────
    public List<int> unlockedFacilityIds = new();

    // ── 건축 영역(BuildZone) 크기 (BuildZoneProgression) ────────────────────
    // 적용된 한 변의 칸 수. 0 = 저장된 값 없음(새 슬롯) → 시작 크기 그대로 둠.
    public int buildZoneCells = 0;

    // ── 우주선 수리 (ShipRepairManager) ──────────────────────────────────
    // shipRepairLevel=현재 수리 레벨(1=시작), shipRepairPartCount=보유 부품 수량(단일 종류),
    // shipRepairTakenKeys=회수한 맵 픽업 위치키 목록(재입장 중복 회수 방지, 개수 무제한).
    // shipRepairExtraMask=레벨 전용 추가 재료 보유 비트(bit L = 레벨 L 재료 보유. 수리 시 소모).
    // 새 슬롯 기본값(1/0/0)이 곧 게임 시작 상태라 그대로 복원해도 무해.
    public int shipRepairLevel = 1;
    public int shipRepairPartCount = 0;
    public List<string> shipRepairTakenKeys = new List<string>();
    public int shipRepairExtraMask = 0;

    // ── 시간에너지 전송 (TransmissionManager) ────────────────────────────────
    // transmissionRate = 전송률(0~100, 감소 안 함) = 진행의 단일 소스.
    //   수령한 10% 마일스톤은 rate 이하 전부로 도출(재지급 방지). 설비해금/아이템/부품 등 실제
    //   보상 효과는 각 시스템(FacilityUnlockManager/인벤/ShipRepair)이 따로 저장하므로 여기선 rate만.
    //   transmissionEndingReached = 100% 엔딩 트리거 1회 발생 여부.
    public int transmissionRate = 0;
    public bool transmissionEndingReached = false;

    // ── 워프 시스템 (WarpManager) ────────────────────────────────────────────
    // 활성화한 지역 워프 지점의 warpId 목록. 활성화돼야 기지→지역 워프의 랜덤 도착 후보가 된다.
    public List<string> activatedWarpIds = new();

    // ── 귀환석 (ReturnStoneManager) ──────────────────────────────────────────
    // returnStoneLevel = 보유 레벨(0=미보유, 1~3. 쿨타임 15/10/5분). 추후 시간에너지 보상으로 설정.
    // returnStoneCooldownRemaining = 남은 쿨타임(초). '결계 밖'에서 보낸 시간으로만 차감된다.
    public int returnStoneLevel = 0;
    public float returnStoneCooldownRemaining = 0f;

    // ── 도감: 아이템 최초 획득 기록 (ItemDiscovery) ──────────────────────────
    public List<int> discoveredItemIds = new();

    // ── 도감: 몬스터 처치 / 튜토 시청 기록 (CodexDiscovery) ──────────────────
    public List<MonsterKillData> monsterKills = new();
    public List<string> watchedTutorials = new();
    public List<string> activatedStats = new();
    public List<string> activatedRates = new();

    // ── 레시피 마스터 진행도 (RecipeProgress) ───────────────────────────────
    public List<RecipeCraftData> recipeCrafts = new();
    public List<int> claimedFacilityRewards = new();
    public List<string> activatedJackpots = new();

    // 이미 푼 퍼즐(시간선 복원 등)의 id. 한 번 풀면 다시 풀 필요가 없다.
    // 필드가 없던 옛 세이브는 빈 목록으로 읽혀 "아직 아무것도 안 풀었음"이 된다.
    public List<string> solvedPuzzleIds = new();

    // ── 땅에 떨어진 드롭 상자 (LootBoxSaveBridge) ───────────────────────────
    // 몹을 잡고 안 주운 상자. 저장하지 않으면 나갔다 오는 것만으로 통째로 사라진다.
    public List<DroppedBoxData> droppedBoxes = new();
}

/// <summary>땅에 놓인 드롭 상자 1개. 씬별로 구분해 저장한다(월드 좌표라 씬이 다르면 의미가 없다).</summary>
[Serializable]
public class DroppedBoxData
{
    public string sceneName;
    public float posX, posY, posZ;
    public List<ItemStackData> contents = new();

    // 남은 수명(초). 상자는 플레이 중에만 시간이 가므로 나갈 때 남은 값을 그대로 들고 있다가
    // 재입장에서 이어받는다. 0 이하 = 구버전 세이브 -> 복원 시 기본 수명으로 시작한다.
    public float remainingLife;
}

[Serializable]
public class QuestCategoryProgress
{
    public string categoryId;
    public int questIndex;
}

[Serializable]
public class InventorySlotData
{
    public int slotIndex;
    public int itemId;
    public int amount;
}

[Serializable]
public class PlacedBuildingData
{
    public int facilityId;
    public int originCellX;
    public int originCellY;
    public int rotationY;
}

[Serializable]
public class RailPieceData
{
    public int cellX;
    public int cellY;
    public bool up;
    public bool down;
    public bool left;
    public bool right;
}

[Serializable]
public class ItemStackData
{
    public int itemId;
    public int amount;
}

[Serializable]
public class MachineSaveData
{
    public int originCellX;
    public int originCellY;
    public List<ItemStackData> inputStock = new();
    public List<ItemStackData> outputStock = new();
    public float fuelTimeRemaining;
    public int lockedRecipeIndex = -1;
}

[Serializable]
public class MonsterKillData
{
    public string sourceId;
    public int kills;
}

[Serializable]
public class RecipeCraftData
{
    public string recipeId;
    public int crafts;
}
