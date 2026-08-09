// SettingsUIBakerWindow.cs (Editor 전용)
// ============================================================================
// 설정 UI 계층을 에디터에서 씬에 굽는다. 굽고 나면 실행 시 새로 만들지 않고
// 씬에 있는 오브젝트를 그대로 쓴다(SettingsUIBuilder.AdoptBakedHierarchy).
//
// 동작:
//   1) 구워둔 PNG 스프라이트를 UISprites.Resolver에 꽂는다
//      → 빌드가 런타임 생성 대신 에셋을 참조하므로 씬에 저장된다.
//   2) 기존에 구운 자식(Root)을 지우고 빌더의 생성 로직을 그대로 한 번 돌린다.
//   3) 만들어진 역할 오브젝트 참조가 SettingsPanelRefs에 기록된다.
//
// 주의: 굽기 전에 반드시 Tools ▸ GameSettingsUI ▸ 스프라이트 굽기를 먼저 실행할 것.
//       에셋이 없으면 런타임 생성 스프라이트가 물려 씬에 저장되지 않는다.
// ============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameSettingsUI.EditorTools
{
    public static class SettingsUIBakerWindow
    {
        [MenuItem("Tools/TIMEKOV/UI/설정창 UI 굽기 (선택 오브젝트)")]
        public static void BakeSelected()
        {
            var builder = Selection.activeGameObject
                ? Selection.activeGameObject.GetComponent<SettingsUIBuilder>()
                : null;

            if (builder == null)
            {
                EditorUtility.DisplayDialog("설정 UI 굽기",
                    "SettingsUIBuilder가 붙은 오브젝트를 선택한 뒤 실행하세요.", "확인");
                return;
            }

            if (!HasBakedSprites())
            {
                EditorUtility.DisplayDialog("설정 UI 굽기",
                    "먼저 Tools ▸ GameSettingsUI ▸ 스프라이트 굽기를 실행하세요.\n\n" +
                    "스프라이트 에셋이 없으면 런타임 생성 스프라이트가 물려서 씬에 저장되지 않습니다.",
                    "확인");
                return;
            }

            // 생성한 오브젝트를 Undo에 등록하지 않으면 Ctrl+Z 시 dangling으로 남는다.
            // 굽기 전체를 하나의 Undo 묶음으로 만든다.
            const string UndoName = "설정 UI 굽기";
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);

            // 이전에 구운 계층 제거 — 그대로 두면 실행할 때마다 겹쳐 쌓인다.
            ClearBaked(builder, UndoName);
            Undo.RegisterCompleteObjectUndo(builder, UndoName);

            var saved = UISprites.Resolver;
            UISprites.Resolver = SettingsSpriteBaker.MakeResolver();
            try
            {
                // 빌더의 생성 로직을 에디터에서 한 번 돌린다.
                builder.BuildForBake();
            }
            finally { UISprites.Resolver = saved; }

            // 직속 자식만 등록해도 그 아래 계층은 부모와 함께 처리된다.
            foreach (Transform child in builder.transform)
                Undo.RegisterCreatedObjectUndo(child.gameObject, UndoName);

            var refs = builder.GetComponent<SettingsPanelRefs>();
            if (refs) Undo.RegisterCreatedObjectUndo(refs, UndoName);

            Undo.CollapseUndoOperations(undoGroup);

            EditorUtility.SetDirty(builder);
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);

            Debug.Log("[SettingsUIBaker] 씬에 UI를 구웠습니다. 이제 실행 시 새로 생성하지 않고 " +
                      "이 계층을 그대로 사용합니다.", builder);
        }

        static bool HasBakedSprites() =>
            AssetDatabase.FindAssets("t:Sprite", new[] { SettingsSpriteBaker.OutDir }).Length > 0;

        static void ClearBaked(SettingsUIBuilder builder, string undoName)
        {
            // Undo.DestroyObjectImmediate로 지워야 되돌리기로 복구된다(그냥 DestroyImmediate면 영영 사라짐).
            for (int i = builder.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(builder.transform.GetChild(i).gameObject);

            var refs = builder.GetComponent<SettingsPanelRefs>();
            if (refs) Undo.DestroyObjectImmediate(refs);
            builder.refs = null;

            // 이전 버전이 씬에 남긴 블러 캔버스 정리. 최상위 오브젝트라 자식 삭제로는 안 지워지고,
            // 남아 있으면 편집 중에도 화면 전체가 블러로 덮인다.
            foreach (var go in builder.gameObject.scene.GetRootGameObjects())
                if (go.name == "SettingsBlurCanvas") Undo.DestroyObjectImmediate(go);
        }
    }
}
#endif
