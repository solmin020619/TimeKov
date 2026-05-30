// =====================================================================
// MissingScriptCleaner.cs
// 씬/프리팹/선택 오브젝트의 Missing Script(깨진 컴포넌트)를 일괄 제거.
//
// 팀원이 스크립트를 지우거나 옮기면 씬/프리팹에 "Missing (Mono Script)"
// 컴포넌트가 남는데, 이걸 하나씩 손으로 지우는 대신 메뉴 한 번으로 정리.
//
// 메뉴:
//   Tools/Cleanup/Remove Missing Scripts (열린 씬 전체)
//   Tools/Cleanup/Remove Missing Scripts (Selection)  — 선택한 오브젝트 + 자식만
//
// 동작:
//   - 열린 씬의 모든 GameObject(비활성 포함)를 훑어 Missing 컴포넌트 제거
//   - 제거 개수를 콘솔에 출력, 변경된 씬은 dirty 표시 (저장은 수동)
// =====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissingScriptCleaner
{
    // ── 열린 씬 전체 ──────────────────────────────────────────────

    [MenuItem("Tools/Cleanup/Remove Missing Scripts (Scene)")]
    public static void CleanOpenScenes()
    {
        int totalGo = 0;
        int totalRemoved = 0;

        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
                CleanRecursive(root.transform, ref totalGo, ref totalRemoved);

            if (totalRemoved > 0)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log($"[MissingScriptCleaner] 씬 정리 완료 — GameObject {totalGo}개 검사, " +
                  $"Missing 컴포넌트 {totalRemoved}개 제거. (변경됐으면 씬 저장 필요)");
    }

    // ── 선택한 오브젝트 + 자식만 ──────────────────────────────────

    [MenuItem("Tools/Cleanup/Remove Missing Scripts (Selection)")]
    public static void CleanSelection()
    {
        var sel = Selection.gameObjects;
        if (sel == null || sel.Length == 0)
        {
            Debug.LogWarning("[MissingScriptCleaner] 선택된 오브젝트가 없습니다.");
            return;
        }

        int totalGo = 0;
        int totalRemoved = 0;

        foreach (var go in sel)
            CleanRecursive(go.transform, ref totalGo, ref totalRemoved);

        Debug.Log($"[MissingScriptCleaner] 선택 정리 완료 — GameObject {totalGo}개 검사, " +
                  $"Missing 컴포넌트 {totalRemoved}개 제거.");
    }

    // 선택이 있을 때만 메뉴 활성화
    [MenuItem("Tools/Cleanup/Remove Missing Scripts (Selection)", true)]
    public static bool ValidateCleanSelection() => Selection.gameObjects.Length > 0;

    // ── 재귀 정리 ─────────────────────────────────────────────────

    private static void CleanRecursive(Transform t, ref int goCount, ref int removedCount)
    {
        goCount++;

        // 이 GameObject의 Missing 컴포넌트 개수만큼 제거 후 카운트 누적
        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        if (removed > 0)
        {
            removedCount += removed;
            // Undo 등록은 불가(이미 제거됨)하지만 dirty는 호출부에서 처리
        }

        for (int i = 0; i < t.childCount; i++)
            CleanRecursive(t.GetChild(i), ref goCount, ref removedCount);
    }
}
