using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ── 우주선 단계별 모델 세팅 도우미(에디터 전용) ────────────────────────────────
// 선택한 오브젝트를 우주선 표시 루트로 삼아
//   ① ShipStageVisual 부착
//   ② Spaceship_1 … _5 프리팹을 자식으로 깔고(같은 자리, 첫 단계만 활성)
//   ③ stageModels 배열에 순서대로 연결
// 을 한 번에 해준다.
//
//   레벨 구간은 ShipStageVisual 이 모델 개수로 자동 분배한다(10단계·5모델 → 2단계마다 교체).
//   펜스는 더 쓰지 않는다 — 우주선이 가려져 수리 진행이 안 보였기 때문. 씬에 남아 있으면
//   ShipStageVisual 의 Fence Root 에 물려두면 실행 시 자동으로 꺼진다.
public static class ShipStageVisualSetup
{
    private const string MenuPath  = "Tools/TIMEKOV/우주선 단계별 모델 세팅";
    private const string PrefabDir = "Assets/03.Model/Spaceship";
    private const int    ModelCount = 5;

    [MenuItem(MenuPath)]
    private static void Setup()
    {
        var host = Selection.activeGameObject;
        if (host == null)
        {
            EditorUtility.DisplayDialog("우주선 단계 모델", "우주선을 표시할 오브젝트(빈 루트)를 선택하라.", "확인");
            return;
        }

        // 프리팹 먼저 확보 — 하나라도 없으면 아무것도 만들지 않는다(반쯤 세팅된 상태 방지).
        var prefabs = new GameObject[ModelCount];
        for (int i = 0; i < ModelCount; i++)
        {
            string path = $"{PrefabDir}/Spaceship_{i + 1}.prefab";
            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabs[i] == null)
            {
                EditorUtility.DisplayDialog("우주선 단계 모델", $"프리팹을 찾지 못했다:\n{path}", "확인");
                return;
            }
        }

        Undo.SetCurrentGroupName("우주선 단계별 모델 세팅");
        int group = Undo.GetCurrentGroup();

        var visual = host.GetComponent<ShipStageVisual>();
        if (visual == null) visual = Undo.AddComponent<ShipStageVisual>(host);

        // 이전에 깔아둔 단계 모델이 있으면 치운다(다시 실행해도 중복되지 않게).
        for (int i = host.transform.childCount - 1; i >= 0; i--)
        {
            var c = host.transform.GetChild(i);
            if (c.name.StartsWith("Stage_")) Undo.DestroyObjectImmediate(c.gameObject);
        }

        var models = new GameObject[ModelCount];
        for (int i = 0; i < ModelCount; i++)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], host.transform);
            inst.name = $"Stage_{i + 1}";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale    = Vector3.one;
            inst.SetActive(i == 0);   // 시작은 1단계만
            Undo.RegisterCreatedObjectUndo(inst, "우주선 단계별 모델 세팅");
            models[i] = inst;
        }

        // 파편 덩어리라 외곽선이 오히려 지저분해진다(땅에 박힌 조각까지 다 그려짐)
        //   → 우주선 본체에는 외곽선을 만들지 않는다. 상호작용 표시는 별도 오브젝트/F 프롬프트로.
        if (host.GetComponent<InteractOutlineStyle>() == null)
        {
            var style = Undo.AddComponent<InteractOutlineStyle>(host);
            style.showOutline = false;
        }

        // 외형이 바뀔 때의 연출(페이드 → 고정 카메라 → 복귀).
        if (host.GetComponent<ShipStageCinematic>() == null)
            Undo.AddComponent<ShipStageCinematic>(host);

        var so = new SerializedObject(visual);
        var arr = so.FindProperty("stageModels");
        arr.arraySize = ModelCount;
        for (int i = 0; i < ModelCount; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = models[i];
        so.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(group);
        MarkDirty(host);
        Selection.activeGameObject = host;

        Debug.Log($"[우주선] '{host.name}' 에 단계별 모델 {ModelCount}개 세팅 완료.\n" +
                  $"{StageTableText()}\n" +
                  "★모델마다 위치/회전/크기가 다르면 각 Stage_N 을 씬에서 맞춰라(로컬 0으로 깔아 뒀다).\n" +
                  "★펜스가 남아 있으면 ShipStageVisual 의 Fence Root 에 물려두면 실행 시 꺼진다.\n" +
                  "★ShipStageCinematic 의 Showcase Camera 에 '바뀐 우주선을 비출 고정 카메라'를 연결하라 " +
                  "(씬에 배치 후 비활성 상태로 두면 된다). 비우면 시점 전환 없이 페이드만 돈다.", host);
    }

    // 실제 단계 수(ShipRepairManager)가 있으면 그 값으로, 없으면 10단계 가정으로 표를 만든다.
    private static string StageTableText()
    {
        var mgr = Object.FindFirstObjectByType<ShipRepairManager>();
        int max = mgr != null ? mgr.MaxLevel : 10;

        var sb = new System.Text.StringBuilder("레벨 → 모델: ");
        int prev = -1;
        for (int lv = 1; lv <= max; lv++)
        {
            int idx = ShipStageVisual.StageIndexFor(lv, max, ModelCount);
            if (idx != prev) { sb.Append($"Lv{lv}~→{idx + 1}번  "); prev = idx; }
        }
        return sb.ToString();
    }

    private static void MarkDirty(GameObject go)
    {
        if (Application.isPlaying) return;
        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(go.scene);
    }
}
