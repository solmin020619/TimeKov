using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ── 압력판 세팅 도우미(에디터 전용) ────────────────────────────────────────────
// 통/오브젝트를 바닥에 박아 '밟는 압력판'으로 쓸 때, 선택한 오브젝트에
//   ① 윗면을 덮는 트리거 BoxCollider  ② PressurePlate 컴포넌트  ③ 물리 고정(키네마틱)
// 을 한 번에 붙여준다. 공유 프리팹 소스는 건드리지 않고 '선택한 인스턴스'에만 적용.
//
//   사용법: 하이라키에서 압력판으로 쓸 오브젝트(예: P_Can_Red_01) 선택 →
//           메뉴 Tools/TIMEKOV/압력판/① 선택 오브젝트를 압력판으로 세팅
//           그 뒤 밟는 판들을 여러 개 선택하고 '② SequenceTrigger 생성'으로 조건 오브젝트를 만든다.
public static class PressurePlateSetup
{
    private const float BoxHeight = 0.6f;    // 감지 박스 높이(월드 m) — 발/다리가 겹치게
    private const float BoxSink   = 0.1f;    // 윗면보다 이만큼 아래에서 시작(살짝 걸치게)
    private const float Footprint = 0.9f;    // 윗면 대비 박스 가로/세로 비율

    // ── ① 선택 오브젝트를 압력판으로 ───────────────────────────────────────────
    [MenuItem("Tools/TIMEKOV/압력판/① 선택 오브젝트를 압력판으로 세팅")]
    private static void SetupSelected()
    {
        var go = Selection.activeGameObject;
        if (go == null) { EditorUtility.DisplayDialog("압력판 세팅", "하이라키에서 압력판으로 쓸 오브젝트를 선택하라.", "확인"); return; }

        Undo.SetCurrentGroupName("압력판 세팅");
        int group = Undo.GetCurrentGroup();

        // 렌더러 경계(월드)로 윗면 크기/높이 파악.
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) { EditorUtility.DisplayDialog("압력판 세팅", "렌더러가 없어 크기를 알 수 없다.", "확인"); return; }
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        var root = go.transform;

        // ① 감지용 트리거 BoxCollider (윗면을 덮고 위로 뻗음). 루트에 추가.
        var box = Undo.AddComponent<BoxCollider>(go);
        box.isTrigger = true;
        Vector3 worldCenter = new Vector3(b.center.x, b.max.y + BoxHeight * 0.5f - BoxSink, b.center.z);
        box.center = root.InverseTransformPoint(worldCenter);
        Vector3 ls = root.lossyScale;
        box.size = new Vector3(
            SafeDiv(b.size.x * Footprint, ls.x),
            SafeDiv(BoxHeight,            ls.y),
            SafeDiv(b.size.z * Footprint, ls.z));

        // ② 물리: press 로 콜라이더(윗면)가 움직이므로 '정적 콜라이더 이동' 안티패턴을 피해
        //    루트에 '키네마틱 Rigidbody 1개'만 둔다. 자식의 동적 Rigidbody(굴러다니는 소품용)는 제거
        //    → 자식 MeshCollider 는 루트 키네마틱 바디의 컴파운드가 됨(같이 깔끔하게 눌림).
        //    플레이어·프롭이 동적 Rigidbody 라 판이 정적/키네마틱이어도 트리거는 정상 발생.
        RemoveRigidbodies(go);
        var rb = Undo.AddComponent<Rigidbody>(go);
        rb.isKinematic = true;
        rb.useGravity  = false;

        // ③ 색 피드백용 글로우 라이트(윗면 살짝 위, 바닥에 색 웅덩이를 만든다). PressurePlate 가 켜고 색 바꾼다.
        var glow = new GameObject("PlateGlow");
        Undo.RegisterCreatedObjectUndo(glow, "압력판 세팅");
        glow.transform.SetParent(go.transform, true);
        glow.transform.position = new Vector3(b.center.x, b.max.y + 0.15f, b.center.z);
        var gl = glow.AddComponent<Light>();
        gl.type      = LightType.Point;
        gl.range     = Mathf.Max(1.5f, Mathf.Max(b.size.x, b.size.z) * 2f);
        gl.intensity = 3f;
        gl.color     = Color.white;
        gl.shadows   = LightShadows.None;
        gl.enabled   = false;   // PressurePlate 가 제어(시작은 소등)

        // ④ PressurePlate 컴포넌트.
        var plate = go.GetComponent<PressurePlate>();
        if (plate == null) plate = Undo.AddComponent<PressurePlate>(go);
        var so = new SerializedObject(plate);
        so.FindProperty("acceptPlayer").boolValue        = true;
        so.FindProperty("acceptPhysicsProps").boolValue  = true;   // 발로 찬 물리 프롭도 눌림 인정
        so.FindProperty("requiredWeight").intValue       = 1;
        so.FindProperty("pressDepth").floatValue         = 0.04f;   // 박혀 있으니 살짝만
        so.FindProperty("pressVisual").objectReferenceValue = null; // 비우면 자기 자신(통 전체)이 살짝 눌림
        var gls = so.FindProperty("glowLights");
        gls.arraySize = 1;
        gls.GetArrayElementAtIndex(0).objectReferenceValue = gl;
        so.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(group);
        MarkDirty(go);
        Selection.activeGameObject = go;
        Debug.Log($"[압력판] '{go.name}' 세팅 완료 — 트리거박스+PressurePlate+물리고정+글로우라이트. 다음: 이 판(들)을 선택해 'SequenceTrigger 생성'.", go);
    }

    // ── ② 선택한 압력판들로 SequenceTrigger(순서 밟기) 생성 ─────────────────────
    [MenuItem("Tools/TIMEKOV/압력판/② 선택 압력판들로 SequenceTrigger(순서 밟기) 생성")]
    private static void CreateTrigger()
    {
        var plates = new System.Collections.Generic.List<PressurePlate>();
        foreach (var g in Selection.gameObjects)
        {
            var p = g.GetComponent<PressurePlate>();
            if (p != null && !plates.Contains(p)) plates.Add(p);
        }
        if (plates.Count == 0) { EditorUtility.DisplayDialog("SequenceTrigger 생성", "PressurePlate 가 붙은 오브젝트를 선택하라.", "확인"); return; }

        var host = new GameObject("SequenceTrigger");
        Undo.RegisterCreatedObjectUndo(host, "SequenceTrigger 생성");
        // 판들의 평균 위치에 둔다(정리용).
        Vector3 c = Vector3.zero;
        foreach (var p in plates) c += p.transform.position;
        host.transform.position = c / plates.Count;

        var trig = host.AddComponent<SequenceTrigger>();
        var so = new SerializedObject(trig);
        // 선택 순서로 sequence 를 채우지만, Unity 선택 순서는 클릭 순서와 다를 수 있으니
        //   ★인스펙터에서 밟을 순서대로 반드시 재정렬할 것.
        var list = so.FindProperty("sequence");
        list.arraySize = plates.Count;
        for (int i = 0; i < plates.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = plates[i];
        so.ApplyModifiedProperties();

        Selection.activeGameObject = host;
        MarkDirty(host);
        Debug.Log($"[압력판] SequenceTrigger 생성 — 판 {plates.Count}개 연결됨. ★Sequence 리스트를 '밟을 순서대로' 재정렬하고, Targets(상자잠금/문)를 연결하라.", host);
    }

    // 판 계층의 모든 Rigidbody 제거(굴러다니는 소품용 동적 바디). 루트엔 이후 키네마틱 1개만 추가된다.
    private static void RemoveRigidbodies(GameObject go)
    {
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
            Undo.DestroyObjectImmediate(rb);
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
