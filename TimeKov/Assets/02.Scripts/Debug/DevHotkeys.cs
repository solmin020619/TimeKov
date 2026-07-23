// DevHotkeys.cs
// 개발/테스트 전용 단축키 중앙 관리.
// 에디터와 Development Build 에서만 컴파일/동작한다(릴리스 빌드엔 아예 안 들어감 -> 플레이어가 못 누름).
// 씬에 따로 붙일 필요 없음: 게임 시작 시 자동으로 생성되어 씬 전환에도 유지된다.
// 새 개발자키는 여기 Update 에 추가하고 아래 목록 주석도 갱신할 것.
//
// ============================================================
//  현재 게임의 개발자/디버그 키 전체 목록 (2026-06-24 정리)
// ============================================================
//  F1 = 플레이어 즉사 (사망/부활 연출 테스트)      Debug/DevHotkeys.cs           [에디터/Dev빌드 전용 가드 있음]
//  F2 = 코어 레벨 +1 강제 (재료/확률 무시)         Manager/CoreUpgradeManager.cs [에디터/Dev빌드 전용 가드 있음]
//  F3 = 도감 전부해금 토글 (도감 열려 있을 때만)    UI/CodexUI.cs                 [에디터/Dev빌드 전용 가드 있음]
//  F4 = 테스트 아이템 지급                         Factory/TestItemSpawner.cs    [에디터/Dev빌드 전용 가드 있음]  (debugKey = 인스펙터 직렬화값)
//  F5 = 우주선 다음 수리 재료 전부 지급             Debug/DevHotkeys.cs           [에디터/Dev빌드 전용 가드 있음]
//  F6 = 플레이어 비행 토글 (Space 상승+애니정지 <-> 다시 누르면 낙하)  Debug/DevHotkeys.cs [에디터/Dev빌드 전용 가드 있음]
//  F7 = 현재 튜토리얼 퀘스트 스킵 (보상 정상 지급)               Debug/DevHotkeys.cs [에디터/Dev빌드 전용 가드 있음]
//  `  = 설비 1~9 전부 즉시 해금                    Grid/FacilityUnlockManager.cs [에디터/Dev빌드 전용 가드 있음]
//
//  F1~F7 + ` : 정식 빌드에선 컴파일에서 빠진다(에디터/Development Build 에서만 동작).
// ============================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

public class DevHotkeys : MonoBehaviour
{
    // 게임 시작 시 자동 생성(씬에 수동 배치 불필요). 씬 전환에도 유지.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        var go = new GameObject("[DevHotkeys]");
        go.AddComponent<DevHotkeys>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        // F1 = 플레이어 즉사. Stat.Kill() = 무적/DEF 무시 즉사 -> 정상 사망 흐름(연출/부활) 그대로 탄다.
        if (Input.GetKeyDown(KeyCode.F1))
        {
            var player = FindAnyObjectByType<Player>();
            if (player != null && player.Stat != null) player.Stat.Kill();
        }

        // F5 = 우주선 다음 수리에 필요한 재료를 전부 지급(모자란 만큼만).
        //   복구 에너지와 레벨 전용 추가 재료는 둘 다 실제로는 다른 시스템이 주는 보상이라
        //   (추가 재료 = 시간에너지 보상) 맵에 테스트용 픽업을 깔 이유가 없다. 이 키로 대체한다.
        if (Input.GetKeyDown(KeyCode.F5))
            GrantShipRepairMaterials();

        // F6 = 플레이어 비행 토글. 애니 정지 + 자유 비행 -> 다시 누르면 중력 복귀(낙하).
        //   높은 곳 확인/맵 이동 테스트용. WASD 수평 조종 + Space 상승(누르는 동안).
        if (Input.GetKeyDown(KeyCode.F6))
        {
            var p = FindAnyObjectByType<Player>();
            if (p != null && p.Movement != null) p.Movement.SetFlyMode(!p.Movement.FlyMode);
        }

        // F7 = 현재 진행 중인 튜토리얼 퀘스트 강제 완료(스킵). 보상도 정상 지급되고 다음 퀘로 넘어간다.
        if (Input.GetKeyDown(KeyCode.F7))
            QuestManager.Instance?.DevSkipCurrentQuest();
    }

    private static void GrantShipRepairMaterials()
    {
        var ship = ShipRepairManager.Instance;
        if (ship == null) { Debug.LogWarning("[Dev] ShipRepairManager 가 씬에 없다"); return; }
        if (ship.IsMaxLevel) { Debug.Log("[Dev] 우주선이 이미 최대 레벨이다"); return; }

        int lack = ship.NextRequiredParts - ship.PartCount;
        if (lack > 0) ship.AddParts(lack);

        // 레벨 전용 추가 재료(선체 보강재 / 동력 안정기 / 엔진)
        if (!ship.NextExtraReady) ship.CollectExtraPart(ship.CurrentLevel + 1);

        Debug.Log($"[Dev] 우주선 Lv.{ship.CurrentLevel} -> Lv.{ship.CurrentLevel + 1} 수리 재료 지급 완료");
    }
}
#endif
