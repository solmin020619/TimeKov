// DevHotkeys.cs
// 개발/테스트 전용 단축키 중앙 관리.
// 에디터와 Development Build 에서만 컴파일/동작한다(릴리스 빌드엔 아예 안 들어감 -> 플레이어가 못 누름).
// 씬에 따로 붙일 필요 없음: 게임 시작 시 자동으로 생성되어 씬 전환에도 유지된다.
// 새 개발자키는 여기 Update 에 추가하고 아래 목록 주석도 갱신할 것.
//
// ============================================================
//  현재 게임의 개발자/디버그 키 전체 목록 (2026-06-22 정리)
// ============================================================
//  F1 = 플레이어 즉사 (사망/부활 연출 테스트)      Debug/DevHotkeys.cs   [에디터/Dev빌드 전용 가드 있음]
//  F2 = 코어 레벨 +1 강제 (재료/확률 무시)         Manager/CoreUpgradeManager.cs
//  F3 = 도감 전부해금 토글 (도감 열려 있을 때만)    UI/CodexUI.cs
//  F4 = 테스트 아이템 지급                         Factory/TestItemSpawner.cs  (debugKey = 인스펙터 직렬화값)
//  `  = 설비 1~9 전부 즉시 해금                    Grid/FacilityUnlockManager.cs
//
//  ※ F1 외 나머지는 가드가 없어 릴리스 빌드에도 들어간다. 제출 전 제거 권장.
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
    }
}
#endif
