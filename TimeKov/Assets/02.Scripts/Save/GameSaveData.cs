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

    // ── 플레이어 위치 ────────────────────────────────────────────────────
    // hasPlayerPosition=false면 새 슬롯이라 씬 기본 스폰 위치를 그대로 둔다(원점으로 텔레포트 안 함).
    public bool hasPlayerPosition = false;
    public Vector3 playerPosition;
    public float playerRotationY;

    // ── Factory/Grid: 배치된 건물 / 레일 ─────────────────────────────────
    // 위치·회전·강화레벨만 저장 — 건물 내부 생산 상태(투입/산출 버퍼, 진행도, 연료)는
    // 아직 미구현(TODO 다음 단계). 레일은 연결 방향만 저장하고 흐름 화살표/포트 검증은
    // RailBuildManager.RestoreReflowAndValidate()로 복원 후 재계산한다.
    public List<PlacedBuildingData> buildings = new();
    public List<RailPieceData> rails = new();
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
