using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ── 시간 감소 구역 세팅 도우미(에디터 전용) ──────────────────────────────────
// ① 위험 구역: 선택한 건물/지형을 덮는 트리거 박스 + TimeHazardZone 을 만든다.
// ② 안전지대: 위험 구역 안에 둘 작은 쉼터를 만든다(선택 오브젝트 위치, 없으면 씬뷰 중앙).
//
//   사용법: 하이라키에서 위험 지역으로 만들 건물(또는 그 범위 오브젝트)을 선택 →
//           Tools/TIMEKOV/시간구역/① 선택 범위를 시간 급속감소 구역으로 생성
//           그 안에 쉼터를 둘 위치의 오브젝트를 선택 → ② 안전지대 생성
//   생성 후 트리거 박스 크기/위치를 플레이어 동선에 맞게 다듬으면 된다.
public static class TimeHazardZoneSetup
{
    [MenuItem("Tools/TIMEKOV/시간구역/① 선택 범위를 시간 급속감소 구역으로 생성")]
    private static void CreateHazard()
    {
        var sel = Selection.gameObjects;
        if (sel == null || sel.Length == 0)
        {
            EditorUtility.DisplayDialog("시간구역 생성", "하이라키에서 위험 지역으로 쓸 건물/범위 오브젝트를 선택하라.", "확인");
            return;
        }

        Undo.SetCurrentGroupName("시간 급속감소 구역 생성");
        int group = Undo.GetCurrentGroup();

        Bounds b = SelectionBounds(sel, fallbackSize: 10f);

        var host = new GameObject("TimeHazardZone_위험구역");
        Undo.RegisterCreatedObjectUndo(host, "시간 급속감소 구역 생성");
        host.transform.position = b.center;

        var box = Undo.AddComponent<BoxCollider>(host);
        box.isTrigger = true;
        box.center = Vector3.zero;
        // 건물 경계를 그대로 덮되, 너무 얄팍하지 않게 최소 크기 보장.
        box.size = Vector3.Max(b.size, new Vector3(3f, 3f, 3f));

        // 배율·화면효과 기본값은 TimeHazardZone 의 필드 초기값을 그대로 쓴다(값이 두 군데로 갈라지지 않게).
        Undo.AddComponent<TimeHazardZone>(host);

        Undo.CollapseUndoOperations(group);
        MarkDirty(host);
        Selection.activeGameObject = host;
        Debug.Log($"[시간구역] '{host.name}' 생성 — 트리거 박스 + TimeHazardZone(배율 3배, 화면효과 On). " +
                  "★박스 크기/위치를 건물 내부 동선에 맞게 조정하고, 안쪽에 ②로 안전지대를 넣어라.", host);
    }

    [MenuItem("Tools/TIMEKOV/시간구역/② 선택 위치에 안전지대 생성")]
    private static void CreateSafe()
    {
        Undo.SetCurrentGroupName("안전지대 생성");
        int group = Undo.GetCurrentGroup();

        var sel = Selection.gameObjects;
        Vector3 pos;
        Vector3 size = new Vector3(4f, 4f, 4f);

        if (sel != null && sel.Length > 0)
        {
            Bounds b = SelectionBounds(sel, fallbackSize: 4f);
            pos  = b.center;
            size = Vector3.Max(b.size, new Vector3(3f, 3f, 3f));
        }
        else
        {
            // 선택이 없으면 씬뷰 카메라 앞에 만든다(위치는 나중에 옮기면 됨).
            var sv = SceneView.lastActiveSceneView;
            pos = sv != null ? sv.pivot : Vector3.zero;
        }

        var host = new GameObject("TimeSafeZone_안전지대");
        Undo.RegisterCreatedObjectUndo(host, "안전지대 생성");
        host.transform.position = pos;

        var box = Undo.AddComponent<BoxCollider>(host);
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = size;

        Undo.AddComponent<TimeSafeZone>(host);

        Undo.CollapseUndoOperations(group);
        MarkDirty(host);
        Selection.activeGameObject = host;
        Debug.Log($"[시간구역] '{host.name}' 생성 — 트리거 박스 + TimeSafeZone(안에 있으면 시간 감소 완전 정지). " +
                  "★위험 구역 '안쪽'에 겹쳐 두어라. 눈에 보이는 표식(빛기둥 등)은 Active Visual 에 연결.", host);
    }

    [MenuItem("Tools/TIMEKOV/시간구역/③ 선택 건물에 표면 일렁임 표식 부착")]
    private static void AddSurfaceFx()
    {
        var sel = Selection.gameObjects;
        if (sel == null || sel.Length == 0)
        {
            EditorUtility.DisplayDialog("표면 표식", "표식을 입힐 건물 오브젝트(루트)를 선택하라.", "확인");
            return;
        }

        Undo.SetCurrentGroupName("표면 표식 부착");
        int group = Undo.GetCurrentGroup();

        int added = 0;
        foreach (var go in sel)
        {
            if (go == null) continue;
            if (go.GetComponent<TimeHazardSurfaceFx>() != null) continue;   // 중복 부착 방지
            Undo.AddComponent<TimeHazardSurfaceFx>(go);
            MarkDirty(go);
            added++;
        }

        Undo.CollapseUndoOperations(group);
        Debug.Log($"[시간구역] 표면 표식 {added}개 부착 — 자식 렌더러 전체에 '껍질'을 만들어 일렁임을 입힌다.\n" +
                  "★껍질은 플레이할 때 생성된다(씬 뷰엔 안 보임). 원본 머티리얼은 건드리지 않는다.\n" +
                  "★실내는 '카메라가 건물 안이면 끄기'로 처리한다. 너무 일찍 꺼지면 Inside Margin Ratio 를 올려라.");
    }

    private static Bounds SelectionBounds(GameObject[] sel, float fallbackSize)
    {
        bool has = false;
        Bounds b = default;
        foreach (var g in sel)
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
        if (!has) b = new Bounds(sel[0].transform.position, Vector3.one * fallbackSize);
        return b;
    }

    private static void MarkDirty(GameObject go)
    {
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
