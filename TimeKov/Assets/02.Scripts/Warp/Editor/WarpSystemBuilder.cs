using UnityEditor;
using UnityEngine;

// 워프 시스템 씬 세팅 도우미. 큐브를 새로 만드는 대신, 씬에 이미 있는 원통형 프리팹 인스턴스를
// 선택해서 워프 포인트로 '지정'한다.
//
//  사용법:
//   1) Tools/TIMEKOV/워프/WarpManager 생성      → 매니저 1개 생성(없을 때만)
//   2) 하이러키에서 원통 오브젝트 선택 후
//        Tools/TIMEKOV/워프/선택 → 기지 아웃바운드_설산(사막/용암)   ← 기지의 3개
//        Tools/TIMEKOV/워프/선택 → 지역 지점_설산(사막/용암)          ← 각 지역 맵의 원통들
//      을 눌러 지정. (지역 지점 warpId는 Snow_1, Snow_2 … 자동 번호)
//   3) 각 WarpPoint의 departure/arrival VFX·사운드, WarpManager.baseReturnPoint 할당.
//
//  - 지정 시 WarpPoint 컴포넌트 추가 + '트리거 감지용' BoxCollider(렌더러 크기 자동)를 붙인다.
//    원통 본체의 기존(솔리드) 콜라이더는 건드리지 않는다 — 밟기 감지는 별도 트리거가 담당.
public static class WarpSystemBuilder
{
    private const string Root = "Tools/TIMEKOV/워프/";

    [MenuItem(Root + "WarpManager 생성", priority = 0)]
    public static void CreateManager()
    {
        var go = GameObject.Find("WarpManager");
        if (go == null)
        {
            go = new GameObject("WarpManager");
            Undo.RegisterCreatedObjectUndo(go, "Create WarpManager");
        }
        if (go.GetComponent<WarpManager>() == null)
            Undo.AddComponent<WarpManager>(go);
        Selection.activeGameObject = go;
        Debug.Log("[Warp] WarpManager 준비 완료. baseReturnPoint(기지 복귀 위치)를 할당하세요.");
    }

    // ── 기지 아웃바운드 (테마별) ─────────────────────────────────────────
    [MenuItem(Root + "선택 → 기지 아웃바운드_설산", priority = 20)]
    public static void TagBaseSnow()   => TagSelection(WarpPoint.WarpKind.BaseOutbound, TransmissionRegion.Snow);
    [MenuItem(Root + "선택 → 기지 아웃바운드_사막", priority = 21)]
    public static void TagBaseDesert() => TagSelection(WarpPoint.WarpKind.BaseOutbound, TransmissionRegion.Desert);
    [MenuItem(Root + "선택 → 기지 아웃바운드_용암", priority = 22)]
    public static void TagBaseLava()   => TagSelection(WarpPoint.WarpKind.BaseOutbound, TransmissionRegion.Lava);

    // ── 지역 지점 (테마별, warpId 자동 번호) ─────────────────────────────
    [MenuItem(Root + "선택 → 지역 지점_설산", priority = 40)]
    public static void TagRegionSnow()   => TagSelection(WarpPoint.WarpKind.RegionPoint, TransmissionRegion.Snow);
    [MenuItem(Root + "선택 → 지역 지점_사막", priority = 41)]
    public static void TagRegionDesert() => TagSelection(WarpPoint.WarpKind.RegionPoint, TransmissionRegion.Desert);
    [MenuItem(Root + "선택 → 지역 지점_용암", priority = 42)]
    public static void TagRegionLava()   => TagSelection(WarpPoint.WarpKind.RegionPoint, TransmissionRegion.Lava);

    private static void TagSelection(WarpPoint.WarpKind kind, TransmissionRegion region)
    {
        var sel = Selection.gameObjects;
        if (sel == null || sel.Length == 0)
        {
            EditorUtility.DisplayDialog("워프 지정", "하이러키에서 원통 오브젝트를 먼저 선택하세요.", "확인");
            return;
        }

        int count = 0;
        foreach (var go in sel)
        {
            if (go == null || EditorUtility.IsPersistent(go)) continue;   // 씬 인스턴스만
            TagOne(go, kind, region);
            count++;
        }
        Debug.Log($"[Warp] {count}개 오브젝트를 {KindName(kind)}({RegionName(region)})(으)로 지정했습니다.");
    }

    private static void TagOne(GameObject go, WarpPoint.WarpKind kind, TransmissionRegion region)
    {
        // WarpPoint 의 [RequireComponent(Collider)] 는 Collider 가 추상 클래스라 자동 추가에 실패한다.
        // 트리거 콜라이더를 먼저 보장한 뒤 WarpPoint 를 붙여야 AddComponent 가 null 을 반환하지 않는다.
        EnsureTrigger(go);

        var wp = go.GetComponent<WarpPoint>();
        if (wp == null) wp = Undo.AddComponent<WarpPoint>(go);
        if (wp == null)
        {
            Debug.LogError($"[Warp] '{go.name}' 에 WarpPoint 추가 실패.", go);
            return;
        }
        Undo.RecordObject(wp, "Configure WarpPoint");

        wp.kind = kind;
        wp.region = region;
        wp.warpId = kind == WarpPoint.WarpKind.RegionPoint ? NextRegionId(region, wp) : "";

        EditorUtility.SetDirty(wp);
        EditorUtility.SetDirty(go);
    }

    // 해당 지역의 기존 RegionPoint 수를 세어 다음 번호 부여(자기 자신이 이미 유효 id면 유지).
    private static string NextRegionId(TransmissionRegion region, WarpPoint self)
    {
        string prefix = region.ToString();   // Snow / Desert / Lava
        if (self != null && !string.IsNullOrEmpty(self.warpId) && self.warpId.StartsWith(prefix))
            return self.warpId;

        int max = 0;
        foreach (var p in Object.FindObjectsByType<WarpPoint>(FindObjectsSortMode.None))
        {
            if (p == null || p == self || !p.IsRegionPoint || p.region != region) continue;
            if (!string.IsNullOrEmpty(p.warpId) && p.warpId.StartsWith(prefix + "_")
                && int.TryParse(p.warpId.Substring(prefix.Length + 1), out int n))
                max = Mathf.Max(max, n);
        }
        return $"{prefix}_{max + 1}";
    }

    // 감지용 BoxCollider(트리거)를 원통 '내부'에 맞춘다.
    //   - 중심은 피벗이 아니라 렌더러 지오메트리 중심(치우침 방지)
    //   - 수평은 좁게(원통 안에 실제로 들어와야 발동), 수직은 서 있는 높이 정도
    //   우리가 만든 박스 트리거면 재실행 시 다시 맞추고, 다른 트리거가 이미 있으면 존중해 그대로 둔다.
    private static void EnsureTrigger(GameObject go)
    {
        BoxCollider box = null;
        bool hasNonBoxTrigger = false;
        foreach (var c in go.GetComponents<Collider>())
        {
            if (!c.isTrigger) continue;
            if (c is BoxCollider bc) box = bc;
            else hasNonBoxTrigger = true;
        }
        if (box == null && hasNonBoxTrigger) return;   // 수동 트리거 있음 → 건드리지 않음
        if (box == null) box = Undo.AddComponent<BoxCollider>(go);

        Undo.RecordObject(box, "Fit warp trigger");
        box.isTrigger = true;

        var bounds = WorldRendererBounds(go);
        if (bounds.HasValue)
        {
            var b = bounds.Value;
            var s = go.transform.lossyScale;
            float w = s.x != 0 ? Mathf.Abs(b.size.x / s.x) : b.size.x;
            float h = s.y != 0 ? Mathf.Abs(b.size.y / s.y) : b.size.y;
            float d = s.z != 0 ? Mathf.Abs(b.size.z / s.z) : b.size.z;

            box.center = go.transform.InverseTransformPoint(b.center);   // 지오메트리 중심
            box.size = new Vector3(w * 0.5f, h, d * 0.5f);               // 수평은 절반(내부만)
        }
    }

    private static Bounds? WorldRendererBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs == null || rs.Length == 0) return null;
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static string KindName(WarpPoint.WarpKind k) =>
        k == WarpPoint.WarpKind.BaseOutbound ? "기지 아웃바운드" : "지역 지점";

    private static string RegionName(TransmissionRegion r) => r switch
    {
        TransmissionRegion.Snow => "설산",
        TransmissionRegion.Desert => "사막",
        TransmissionRegion.Lava => "용암",
        _ => "자연",
    };
}
