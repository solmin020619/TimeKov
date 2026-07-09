// =====================================================================
// TransmissionComputerUIBuilder.cs (Editor Only)
// Tools/TIMEKOV/전송 컴퓨터 UI 폰트 세팅
// 씬에 TransmissionComputerUI를 배치(없으면 생성)하고 krFont/monoFont를
// 11.Font 의 SDF 에셋으로 직접 할당한다. 런타임 폰트 자동로드가 실패해도
// Inspector 지정 폰트(직렬화)를 사용하므로 목업 폰트가 정확히 나온다.
//
// 한글 = Pretendard-SemiBold SDF.
// 스펙의 JetBrains Mono 는 프로젝트에 없어 sci-fi 대체로 Rajdhani-SemiBold SDF 사용.
//   (JetBrains Mono SDF 를 임포트하면 monoFont 필드만 그걸로 교체하면 됨.)
// =====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

public static class TransmissionComputerUIBuilder
{
    const string KrPathSemi = "Assets/11.Font/Pretendard-SemiBold SDF.asset";
    const string KrPathExtra = "Assets/11.Font/Pretendard-ExtraBold SDF.asset";
    const string MonoPathSemi = "Assets/11.Font/Rajdhani-SemiBold SDF.asset";
    const string MonoPathReg = "Assets/11.Font/Rajdhani-Regular SDF.asset";
    // JetBrains Mono 를 임포트했다면 아래 경로에 두고 우선 사용됨.
    const string MonoPathJetBrains = "Assets/11.Font/JetBrainsMono-Regular SDF.asset";

    [MenuItem("Tools/TIMEKOV/전송 컴퓨터 UI 폰트 세팅")]
    public static void SetupFonts()
    {
        var existing = Object.FindObjectsByType<TransmissionComputerUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        GameObject go;
        if (existing.Length > 0)
        {
            go = existing[0].gameObject;
        }
        else
        {
            go = new GameObject("TransmissionComputerUI");
            go.AddComponent<TransmissionComputerUI>();
        }

        var comp = go.GetComponent<TransmissionComputerUI>();
        var so = new SerializedObject(comp);

        var kr = Load(KrPathSemi) ?? Load(KrPathExtra);
        var mono = Load(MonoPathJetBrains) ?? Load(MonoPathSemi) ?? Load(MonoPathReg);

        var krProp = so.FindProperty("krFont");
        var monoProp = so.FindProperty("monoFont");
        if (krProp != null && kr != null) krProp.objectReferenceValue = kr;
        if (monoProp != null && mono != null) monoProp.objectReferenceValue = mono;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(comp);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = go;

        // Preloaded Assets 에 등록 → 어느 씬에서든 런타임에 항상 로드돼 폰트 자동 해석이 성공한다
        // (씬 오브젝트를 못 쓰는 경로/다른 씬에서 열려도 폰트가 맞도록 이중 보장).
        AddToPreloaded(kr);
        AddToPreloaded(mono);

        Debug.Log($"[TIMEKOV] 전송 컴퓨터 UI 폰트 세팅 완료 — 한글: {(kr != null ? kr.name : "없음")} / 영숫자: {(mono != null ? mono.name : "없음")}  (Preloaded Assets 등록 포함)");
        if (kr == null) Debug.LogWarning("[TIMEKOV] Pretendard SDF 를 찾지 못했습니다. 경로 확인: " + KrPathSemi);
        if (mono == null) Debug.LogWarning("[TIMEKOV] Rajdhani/JetBrains SDF 를 찾지 못했습니다.");
    }

    static TMP_FontAsset Load(string path) => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

    static void AddToPreloaded(Object asset)
    {
        if (asset == null) return;
        var list = new System.Collections.Generic.List<Object>(PlayerSettings.GetPreloadedAssets());
        if (!list.Contains(asset)) { list.Add(asset); PlayerSettings.SetPreloadedAssets(list.ToArray()); }
    }
}
