// =====================================================================
// Editor/PlayModeSheetSync.cs
// Play 를 누르는 순간 시트를 받아 작업용 캐시를 갱신하고, "이번은 직행 플레이"라고 표시한다.
// 실제로 데이터를 채우는 건 DirectPlayBoot(첫 씬이 열리기 전에 동기 로드).
//
// [레포 사본은 건드리지 않는다]
//   받은 내용은 Library/SheetCache 에만 쓴다. Documentation/sheet_backup 은 '시트 > 백업 저장'
//   메뉴에서만 갱신된다. 안 그러면 남이 시트를 고칠 때마다 내 작업 트리에 CSV 변경이 쌓인다.
//
// [왜 플레이 시작 전에 받나]
//   게임 안에서 받으면 받는 동안 빈 테이블로 Awake 들이 돌아 경고가 뜨고,
//   한 번만 읽는 코드는 시트값을 영영 못 받는다. 먼저 받아두면 그 창이 사라진다.
//   다운로드는 지금도 매 플레이 하고 있으므로 총 시간은 그대로다.
//
// [메인메뉴/로딩 씬에서 시작하면 아무것도 안 한다]
//   그건 플레이어가 겪는 정식 경로다. 로딩 씬이 자기 방식대로 받게 둔다.
//   (여기서 미리 채워버리면 로딩바가 즉시 100% 로 끝나 그 흐름을 테스트할 수 없다.)
// =====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PlayModeSheetSync
{
    // 정식 부팅 경로의 씬들. 여기서 시작하면 손대지 않는다.
    private static readonly string[] BootScenes = { "MainMenu", "Loading" };

    static PlayModeSheetSync()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;

        SessionState.SetBool(DirectPlayBoot.FlagKey, false);

        string scene = EditorSceneManager.GetActiveScene().name;
        foreach (var b in BootScenes)
            if (scene == b) return;

        var res = SheetCache.RefreshAll("플레이 준비 - 시트 받는 중");

        // 사본이 하나도 없으면 DirectPlayBoot 가 알아서 물러나므로 플래그는 그냥 켜도 된다.
        SessionState.SetBool(DirectPlayBoot.FlagKey, true);

        if (res.UsedPending.Count > 0)
            Debug.Log($"[시트] 게시본이 아직 옛값이라 방금 쓴 로컬 값을 쓴다: {string.Join(", ", res.UsedPending)}");

        if (res.Failed.Count > 0)
            Debug.LogWarning($"[시트] 못 받은 테이블은 기존 사본으로 진행한다: {string.Join(", ", res.Failed)}");
    }
}
