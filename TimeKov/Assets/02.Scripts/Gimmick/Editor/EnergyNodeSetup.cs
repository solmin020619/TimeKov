using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ── 에너지 노드 세팅 도우미(에디터 전용) ──────────────────────────────────────
// 통/오브젝트를 '연료 주입구'로 쓸 때, 선택한 오브젝트에
//   ① 상호작용 감지 트리거 콜라이더  ② 발광용 라이트(NodeGlow, 짧은 range)  ③ EnergyNode 컴포넌트
// 를 한 번에 붙여준다. 땅에 박아 쓰는 오브젝트를 전제로 showOutline 은 꺼둔다(통짜 외곽선 방지).
//   ★라이트 range/glowIntensity 를 작게 잡아 주변 지형 번짐을 줄였다(강하면 인스펙터서 조정).
//
//   사용법: 하이라키에서 주입구로 쓸 오브젝트 선택 →
//           Tools/TIMEKOV/에너지노드/① 선택 오브젝트를 에너지 노드로 세팅
//           그 뒤 여러 노드를 선택하고 '② EnergyConduit 생성'으로 조건 오브젝트를 만든다.
public static class EnergyNodeSetup
{
    [MenuItem("Tools/TIMEKOV/에너지노드/① 선택 오브젝트를 에너지 노드로 세팅")]
    private static void SetupSelected()
    {
        var go = Selection.activeGameObject;
        if (go == null) { EditorUtility.DisplayDialog("에너지 노드 세팅", "하이라키에서 주입구로 쓸 오브젝트를 선택하라.", "확인"); return; }

        Undo.SetCurrentGroupName("에너지 노드 세팅");
        int group = Undo.GetCurrentGroup();

        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) { EditorUtility.DisplayDialog("에너지 노드 세팅", "렌더러가 없어 크기를 알 수 없다.", "확인"); return; }
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        var root = go.transform;

        // ① 상호작용 감지용 트리거 BoxCollider(렌더러 경계를 덮음). 재실행 시 중복 안 되게 기존 것 재사용.
        var box = go.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(go);
        box.isTrigger = true;
        box.center = root.InverseTransformPoint(b.center);
        Vector3 ls = root.lossyScale;
        box.size = new Vector3(SafeDiv(b.size.x, ls.x), SafeDiv(b.size.y, ls.y), SafeDiv(b.size.z, ls.z));

        // ② 발광 라이트(NodeGlow). 주변 지형까지 번지지 않게 range/세기를 작게 잡는다.
        //    EnergyNode 가 켜고 색/밝기(glowIntensity)를 제어(시작은 소등). 재실행 시 중복 방지 위해 기존 것 제거.
        var oldGlow = go.transform.Find("NodeGlow");
        if (oldGlow != null) Undo.DestroyObjectImmediate(oldGlow.gameObject);
        var glow = new GameObject("NodeGlow");
        Undo.RegisterCreatedObjectUndo(glow, "에너지 노드 세팅");
        glow.transform.SetParent(go.transform, true);
        // ★라이트는 오브젝트 '표면 위'에 둬야 바깥 면이 밝아진다(내부에 넣으면 겉면이 안 보임). 윗면 살짝 위.
        glow.transform.position = new Vector3(b.center.x, b.max.y + 0.1f, b.center.z);
        var gl = glow.AddComponent<Light>();
        gl.type      = LightType.Point;
        gl.range     = Mathf.Max(1.2f, Mathf.Max(b.size.x, b.size.y, b.size.z) * 1.4f);   // 오브젝트를 덮되 과하지 않게
        gl.intensity = 2f;
        gl.color     = new Color(0.2f, 0.9f, 1f);
        gl.shadows   = LightShadows.None;
        gl.enabled   = false;

        // ③ EnergyNode 컴포넌트.
        var node = go.GetComponent<EnergyNode>();
        if (node == null) node = Undo.AddComponent<EnergyNode>(go);
        var so = new SerializedObject(node);
        so.FindProperty("requiredAmount").intValue  = 3;
        so.FindProperty("depositPerPress").intValue = 1;
        so.FindProperty("openDuration").floatValue  = 40f;   // 설비 연료처럼 40초 뒤 소진
        so.FindProperty("showOutline").boolValue    = false; // 땅에 박은 오브젝트 = 외곽선 대신 근접 발광
        so.FindProperty("glowIntensity").floatValue = 2f;    // 런타임 밝기(강하면 인스펙터서 낮추고, 어두우면 올림)
        var gls = so.FindProperty("glowLights");
        gls.arraySize = 1;
        gls.GetArrayElementAtIndex(0).objectReferenceValue = gl;
        so.FindProperty("glowEmissiveRenderers").arraySize = 0;
        so.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(group);
        MarkDirty(go);
        Selection.activeGameObject = go;
        Debug.Log($"[에너지노드] '{go.name}' 세팅 완료 — 트리거박스+EnergyNode+발광라이트(NodeGlow, 짧은 range). " +
                  "★인스펙터에서 Fuel Item Id(연료 아이템)를 지정하고, 문은 Targets 또는 EnergyConduit 로 연결하라.", go);
    }

    [MenuItem("Tools/TIMEKOV/에너지노드/② 선택 노드들로 EnergyConduit 생성")]
    private static void CreateConduit()
    {
        var nodes = new System.Collections.Generic.List<EnergyNode>();
        foreach (var g in Selection.gameObjects)
        {
            var n = g.GetComponent<EnergyNode>();
            if (n != null && !nodes.Contains(n)) nodes.Add(n);
        }
        if (nodes.Count == 0) { EditorUtility.DisplayDialog("EnergyConduit 생성", "EnergyNode 가 붙은 오브젝트를 선택하라.", "확인"); return; }

        var host = new GameObject("EnergyConduit");
        Undo.RegisterCreatedObjectUndo(host, "EnergyConduit 생성");
        Vector3 c = Vector3.zero;
        foreach (var n in nodes) c += n.transform.position;
        host.transform.position = c / nodes.Count;

        var trig = host.AddComponent<EnergyConduit>();
        var so = new SerializedObject(trig);
        so.FindProperty("requireAll").boolValue = true;
        // 지속 시간(40초)이 있는 노드를 쓰면 하나라도 꺼질 때 문이 닫혀야 하므로 latch 를 끈다.
        var latch = so.FindProperty("latch");
        if (latch != null) latch.boolValue = false;
        var list = so.FindProperty("nodes");
        list.arraySize = nodes.Count;
        for (int i = 0; i < nodes.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];
        so.ApplyModifiedProperties();

        Selection.activeGameObject = host;
        MarkDirty(host);
        Debug.Log($"[에너지노드] EnergyConduit 생성 — 노드 {nodes.Count}개 연결(requireAll, latch 꺼짐). " +
                  "★Targets(문 GimmickSlideDoor 등)를 연결하라.", host);
    }

    private static float SafeDiv(float a, float b) => Mathf.Approximately(b, 0f) ? a : a / b;

    private static void MarkDirty(GameObject go)
    {
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
