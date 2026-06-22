// DevHotkeys.cs
// 개발/테스트 전용 단축키 중앙 관리.
// 에디터와 Development Build 에서만 컴파일/동작한다(릴리스 빌드엔 아예 안 들어감 -> 플레이어가 못 누름).
// 씬에 따로 붙일 필요 없음: 게임 시작 시 자동으로 생성되어 씬 전환에도 유지된다.
// 새 개발자키는 여기 Update 에 추가하고 아래 목록 주석도 갱신할 것.
//
// ============================================================
//  현재 게임의 개발자/디버그 키 전체 목록 (2026-06-22 기준)
// ============================================================
//  [여기 DevHotkeys - 에디터/Dev빌드 전용 가드 있음]
//    F6  = 플레이어 즉사 (사망/부활 연출 테스트)
//
//  [각 컴포넌트에 흩어진 것 - 아래 스크립트에 직접 있음. 현재 가드 없음 = 릴리스에도 들어감]
//    F3  = PerfHud 표시 토글                         Debug/PerfHud.cs
//    F4  = PerfHud 누적 리셋                         Debug/PerfHud.cs
//    F5  = 도감 전부해금 토글 (도감 열려 있을 때만)    UI/CodexUI.cs        (원래 F9였으나 충돌로 이동)
//    F7  = 코어 레벨 +1 강제 (재료/확률 무시)         Manager/CoreUpgradeManager.cs
//    F8  = 인벤 블러 튜닝                            Inventory/UI/InventoryBlurTuner.cs
//    F9  = 인벤 패널 스킨 다음                        Inventory/UI/InventoryPanelSkinSwapper.cs
//    F10 = 인벤 패널 스킨 이전                        Inventory/UI/InventoryPanelSkinSwapper.cs
//    F11 = WindowManager 디버그 오버레이 (켰을 때만)   UI/Core/WindowManager.cs
//    F12 = 테스트 아이템 지급                         Factory/TestItemSpawner.cs
//    `   = 설비 1~9 전부 즉시 해금                    Grid/FacilityUnlockManager.cs
//
//  ※ F6 외 나머지는 가드가 없어 릴리스 빌드에도 포함된다. 제출 전 제거하거나 같은 방식으로 가드 권장.
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
        // F6 = 플레이어 즉사. Stat.Kill() = 무적/DEF 무시 즉사 -> 정상 사망 흐름(연출/부활) 그대로 탄다.
        if (Input.GetKeyDown(KeyCode.F6))
        {
            var player = FindAnyObjectByType<Player>();
            if (player != null && player.Stat != null) player.Stat.Kill();
        }
    }
}
#endif
