// =====================================================================
// TransmissionComputerUIBuilder.cs (Editor Only)
//
// Tools/TIMEKOV/전송 컴퓨터 UI 생성      - 씬에 화면 실물을 만든다(기존 것은 지우고 다시).
// Tools/TIMEKOV/전송 컴퓨터 UI 폰트 세팅 - 폰트만 다시 물린다.
//
// [08-02] 런타임 생성 -> 씬 실물 전환.
//   레이아웃 코드를 여기로 옮겨 적지 않는다. 절대좌표가 수백 개라 옮기다 오타 하나 나면
//   조용히 레이아웃이 무너진다. 대신 TransmissionComputerUI.EditorBuild() 를 호출해서
//   원래 생성 코드를 그 자리서 실행시키고, 그때 대입된 참조를 SetDirty 로 씬에 저장한다.
//
// 한글 = Pretendard-SemiBold SDF.
// 스펙의 JetBrains Mono 는 프로젝트에 없어 sci-fi 대체로 Rajdhani-SemiBold SDF 사용.
// =====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class TransmissionComputerUIBuilder
{
    const string RootName = "TransmissionComputerUI";
    const string GroupName = "Panels";     // Canvas 아래 창(패널) 그룹
    const int SortingOrder = 4000;         // 전체화면 모달이라 일반 UI 위. 튜토리얼 영상(5000) 아래.

    const string KrPathSemi = "Assets/11.Font/Pretendard-SemiBold SDF.asset";
    const string KrPathExtra = "Assets/11.Font/Pretendard-ExtraBold SDF.asset";
    const string MonoPathSemi = "Assets/11.Font/Rajdhani-SemiBold SDF.asset";
    const string MonoPathReg = "Assets/11.Font/Rajdhani-Regular SDF.asset";
    // JetBrains Mono 를 임포트했다면 아래 경로에 두고 우선 사용됨.
    const string MonoPathJetBrains = "Assets/11.Font/JetBrainsMono-Regular SDF.asset";

    [MenuItem("Tools/TIMEKOV/전송 컴퓨터 UI 생성")]
    public static void BuildUI()
    {
        var canvas = UIBuilderUtil.FindMainCanvas();
        if (canvas == null)
        {
            Debug.LogError("[TIMEKOV] 씬에서 메인 Canvas 를 못 찾았다. Canvas 프리팹이 씬에 있는지 확인해라.");
            return;
        }

        // 이전 결과물의 폰트 지정을 이어받는다(수동으로 다른 폰트를 넣어뒀을 수 있음).
        TMP_FontAsset prevKr = null, prevMono = null;
        var old = Object.FindObjectsByType<TransmissionComputerUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (old.Length > 0)
        {
            var so0 = new SerializedObject(old[0]);
            prevKr = so0.FindProperty("krFont")?.objectReferenceValue as TMP_FontAsset;
            prevMono = so0.FindProperty("monoFont")?.objectReferenceValue as TMP_FontAsset;
        }
        int removed = UIBuilderUtil.RemoveExisting<TransmissionComputerUI>();

        var parent = UIBuilderUtil.EnsureGroup(canvas, GroupName);
        var go = new GameObject(RootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create Transmission Computer UI");
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // 정렬 격리 - 그룹 순서와 상관없이 항상 다른 UI 위에 뜨게 한다(전체화면 모달).
        var cv = go.AddComponent<Canvas>();
        cv.overrideSorting = true; cv.sortingOrder = SortingOrder;
        go.AddComponent<GraphicRaycaster>();

        var comp = go.AddComponent<TransmissionComputerUI>();

        // 폰트 먼저 물리고(빌드가 각 글자에 박아 넣는다) 실물 생성.
        var kr = prevKr ?? Load(KrPathSemi) ?? Load(KrPathExtra);
        var mono = prevMono ?? Load(MonoPathJetBrains) ?? Load(MonoPathSemi) ?? Load(MonoPathReg);
        AssignFonts(comp, kr, mono);

        comp.EditorBuild();

        // 절차 스프라이트는 에셋이 아니라 메모리 생성물이라 유니티를 껐다 켜면 사라진다.
        // 계층을 한 번 훑어 재생성 키를 심어두면 실행 시 스스로 되살아난다(호출부 수정 0).
        int keyed = UIBuilderUtil.AttachGeneratedSpriteKeys(go);

        EditorUtility.SetDirty(comp);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = go;

        AddToPreloaded(kr); AddToPreloaded(mono);

        Debug.Log($"[TIMEKOV] 전송 컴퓨터 UI 생성 완료 - {canvas.name}/{GroupName}/{RootName} " +
                  $"(이전 {removed}개 제거, 절차 스프라이트 {keyed}개에 재생성 키 부착)\n" +
                  $"한글: {(kr != null ? kr.name : "없음")} / 영숫자: {(mono != null ? mono.name : "없음")}\n" +
                  "레이아웃을 눈으로 보려면 하이어라키에서 Panel 을 잠깐 켜라(전체화면 백드롭이라 평소엔 꺼둔다).");
    }

    [MenuItem("Tools/TIMEKOV/전송 컴퓨터 UI 폰트 세팅")]
    public static void SetupFonts()
    {
        var existing = Object.FindObjectsByType<TransmissionComputerUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (existing.Length == 0)
        {
            Debug.LogError("[TIMEKOV] 씬에 전송 컴퓨터 UI 가 없다. 먼저 Tools/TIMEKOV/전송 컴퓨터 UI 생성 을 실행해라.");
            return;
        }
        var comp = existing[0];

        var kr = Load(KrPathSemi) ?? Load(KrPathExtra);
        var mono = Load(MonoPathJetBrains) ?? Load(MonoPathSemi) ?? Load(MonoPathReg);
        AssignFonts(comp, kr, mono);

        EditorUtility.SetDirty(comp);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = comp.gameObject;

        // Preloaded Assets 에 등록 -> 어느 씬에서든 런타임에 항상 로드돼 폰트 자동 해석이 성공한다.
        AddToPreloaded(kr);
        AddToPreloaded(mono);

        Debug.Log($"[TIMEKOV] 전송 컴퓨터 UI 폰트 세팅 완료 - 한글: {(kr != null ? kr.name : "없음")} / 영숫자: {(mono != null ? mono.name : "없음")}\n" +
                  "이미 만들어진 글자에는 반영되지 않는다. 바꿨다면 '전송 컴퓨터 UI 생성' 을 다시 실행해라.");
        if (kr == null) Debug.LogWarning("[TIMEKOV] Pretendard SDF 를 찾지 못했습니다. 경로 확인: " + KrPathSemi);
        if (mono == null) Debug.LogWarning("[TIMEKOV] Rajdhani/JetBrains SDF 를 찾지 못했습니다.");
    }

    static void AssignFonts(TransmissionComputerUI comp, TMP_FontAsset kr, TMP_FontAsset mono)
    {
        var so = new SerializedObject(comp);
        var krProp = so.FindProperty("krFont");
        var monoProp = so.FindProperty("monoFont");
        if (krProp != null && kr != null) krProp.objectReferenceValue = kr;
        if (monoProp != null && mono != null) monoProp.objectReferenceValue = mono;
        so.ApplyModifiedProperties();
    }

    static TMP_FontAsset Load(string path) => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

    static void AddToPreloaded(Object asset)
    {
        if (asset == null) return;
        var list = new System.Collections.Generic.List<Object>(PlayerSettings.GetPreloadedAssets());
        if (!list.Contains(asset)) { list.Add(asset); PlayerSettings.SetPreloadedAssets(list.ToArray()); }
    }
}
