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
    // 코어 강화(MaxHp)와 분리된 별도 누적치라 절대값 그대로 저장. 인스펙터 기본값(0/0/100)과
    // 동일해 새 슬롯에서도 그대로 적용해도 무해함.
    public float playerATK = 0f;
    public float playerDEF = 0f;
    public float playerMaxStamina = 100f;

    // ── 플레이어 위치 ────────────────────────────────────────────────────
    // hasPlayerPosition=false면 새 슬롯이라 씬 기본 스폰 위치를 그대로 둔다(원점으로 텔레포트 안 함).
    public bool hasPlayerPosition = false;
    public Vector3 playerPosition;
    public float playerRotationY;

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

    // ── 건축 영역(BuildZone) 확장 단계 (BuildZoneProgression) ───────────────
    // -1 = 저장된 값 없음(새 슬롯) → 시작 단계 그대로 둠.
    public int buildZoneStageIndex = -1;

    // ── 우주선 수리 (ShipRepairManager) ──────────────────────────────────
    // shipRepairLevel=현재 수리 레벨(1=시작), shipRepairPartCount=보유 부품 수량(단일 종류),
    // shipRepairPartsMask=회수한 맵 픽업 비트(bit i = 픽업 i번 회수됨, 재입장 중복 회수 방지).
    // 새 슬롯 기본값(1/0/0)이 곧 게임 시작 상태라 그대로 복원해도 무해.
    public int shipRepairLevel = 1;
    public int shipRepairPartCount = 0;
    public int shipRepairPartsMask = 0;

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
    public int currentLevel;
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
