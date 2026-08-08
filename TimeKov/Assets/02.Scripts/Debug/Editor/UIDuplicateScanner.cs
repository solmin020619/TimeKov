// =====================================================================
// UIDuplicateScanner.cs  (Editor 전용)
// "하나만 있어야 하는" UI 가 두 벌 있는지 찾아, 지워야 할 쪽에만 빨간 배지를 붙인다.
// 메뉴: Tools/TIMEKOV/UI 중복 검사  /  Tools/TIMEKOV/UI 중복 표시 지우기
// 플레이는 필요 없다. 에디트 모드에서 누르면 그 자리에서 판정한다.
//
// [왜 필요한가]
//   씬의 프리팹 인스턴스에 오브젝트를 추가한 상태로 'Apply All Overrides' 를 누르면
//   그 오브젝트가 프리팹으로 복사된다. 씬 쪽 추가분은 남으므로 프리팹 1벌 + 씬 1벌이 된다.
//   런타임에는 싱글톤이 하나를 파괴해 겉보기엔 멀쩡하고, 인스펙터 참조가 파괴된 쪽을
//   가리키는 날에야 "UI 가 안 뜬다"로 터진다. 그래서 커밋 전에 잡아야 한다.
//
// [검사 대상 - 추측 없음]
//   [SingleInstance] 속성이 붙은 타입만 검사 A 대상이다. static Instance 유무를 리플렉션으로
//   추측했다가 양쪽으로 다 틀린 이력이 있다(실패 사례는 SingleInstanceAttribute.cs 주석).
//   싱글톤이 아닌 장식/패널(Title, FuelSlot 등)의 겹침은 검사 B 가 잡는다. 거기서도
//   짝의 출신이 씬 원본/프리팹 복사본으로 갈리면 검사 A 와 같은 확정 배지를 붙인다.
//   (08-08 실측: Apply 는 싱글톤 UI 만이 아니라 추가돼 있던 패널/장식까지 통째로 구웠다)
//
// [지울 쪽 판정 - 근거는 프리팹 구조 하나다]
//   같은 타입이 2개 이상이면 각 인스턴스의 출신을 프리팹 API 로 가린다.
//     - 씬에서 추가된 것(하이어라키 + 표시)  -> 원본. 남긴다. 배지 없음.
//     - 프리팹 내용물로 온 것               -> Apply 로 구워진 복사본. '삭제 필요' 배지.
//     - 프리팹과 무관한 씬 직속             -> 원본. 남긴다.
//   원본이 확인될 때만 복사본에 배지를 붙이므로, 묶음 전체가 삭제 표시되는 일은 구조상 없다.
//   전부 같은 출신이라 못 가리는 묶음은 배지 없이 '직접 확인'으로만 보고한다. 오탐보다 낫다.
//
// [플레이 기록을 버린 이유 - 이 툴이 실제로 틀렸던 지점]
//   1차 설계는 플레이 중 '실제로 파괴된 쪽'의 경로를 기록해 에디터에서 되찾는 방식이었다.
//   그런데 중복 두 오브젝트는 이름/부모가 같아 경로가 완전히 동일하다. 기록 하나가
//   양쪽 모두에 매칭돼 '둘 다 삭제 필요'가 붙었다. 게다가 살아남는 쪽은 Awake 순서
//   소관이라 실행마다 바뀐다. 실행 결과는 판정 근거가 못 된다.
//   프리팹 구조는 실행과 무관하게 확정이고 에디트 모드에서 바로 읽힌다. 그래서 그것만 쓴다.
//
// [이름을 안 바꾸는 이유]
//   transform.Find("CatLabel") 처럼 이름으로 자식을 찾는 코드가 23곳이라 이름을 바꾸면
//   UI 가 깨진다. 프리팹 인스턴스의 이름 변경은 오버라이드로 남아 씬 파일도 더러워진다.
//   그래서 배지만 덧그린다.
// =====================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class UIDuplicateScanner
{
    // 지워야 할 오브젝트만 담는다(instanceID). 나머지는 넣지 않는다.
    private static readonly HashSet<int> _toDelete = new();
    private static GUIStyle _badge;

    // 인스턴스의 출신. 판정의 전부다.
    private enum Origin
    {
        AddedInScene,    // 프리팹 인스턴스에 씬에서 추가된 것(+) = 원본
        PrefabContent,   // 프리팹 내용물로 딸려 온 것 = Apply 로 구워진 복사본
        PlainScene,      // 프리팹과 무관한 씬 직속 = 원본
    }

    static UIDuplicateScanner()
    {
        EditorApplication.hierarchyWindowItemOnGUI -= DrawBadge;
        EditorApplication.hierarchyWindowItemOnGUI += DrawBadge;
    }

    [MenuItem("Tools/TIMEKOV/UI 중복 검사")]
    public static void Scan()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[UI 중복 검사] 플레이를 정지하고 다시 눌러라. 플레이는 필요 없다 - 씬의 프리팹 구조만 보고 판정한다.");
            return;
        }

        _toDelete.Clear();

        // 1) [SingleInstance] 타입별로 씬의 인스턴스를 모은다(꺼진 것 포함, 프리팹 편집 화면은 제외).
        var byType = new Dictionary<Type, List<MonoBehaviour>>();
        foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            var scene = mb.gameObject.scene;
            if (!scene.IsValid() || EditorSceneManager.IsPreviewScene(scene)) continue;
            var t = mb.GetType();
            // 판정은 오직 [SingleInstance] 선언으로만 한다. 추측 금지(근거는 속성 파일 주석).
            if (!t.IsDefined(typeof(SingleInstanceAttribute), false)) continue;
            if (!byType.TryGetValue(t, out var list)) { list = new List<MonoBehaviour>(); byType[t] = list; }
            list.Add(mb);
        }

        var dups = byType.Where(kv => kv.Value.Count > 1).OrderBy(kv => kv.Key.Name).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"[UI 중복 검사] 단일 인스턴스 타입 중복 {dups.Count}종.");
        if (dups.Count == 0) sb.AppendLine("  (없음)");

        // 참조 정보는 판정에 쓰지 않는다. 못 가린 묶음의 수동 확인을 돕는 참고로만 쓴다.
        HashSet<int> refCache = null;

        // 프리팹 에셋 경로 -> 그 프리팹 안에서 지워야 할 위치들. 근본 조치 안내용.
        var prefabsToFix = new Dictionary<string, List<string>>();

        foreach (var kv in dups)
        {
            var members = kv.Value.Select(m => (mb: m, origin: OriginOf(m.gameObject))).ToList();
            var copies = members.Where(x => x.origin == Origin.PrefabContent).ToList();
            bool hasOriginal = members.Count > copies.Count;
            // 원본이 최소 하나 확인될 때만 복사본을 삭제 대상으로 확정한다.
            // (원본 없이 전부 복사본이면 하나는 남겨야 하므로 여기선 판정하지 않는다)
            bool decided = hasOriginal && copies.Count > 0;

            sb.AppendLine($"\n[{kv.Key.Name}] x{members.Count}");

            foreach (var (mb, origin) in members)
            {
                bool del = decided && origin == Origin.PrefabContent;
                if (del) _toDelete.Add(mb.gameObject.GetInstanceID());

                string tag = del ? "[삭제 필요]" : "[남김     ]";
                string from = origin == Origin.AddedInScene ? "씬에서 추가(+)"
                            : origin == Origin.PrefabContent ? "프리팹 내용물"
                            : "씬에 직접";
                // 중복끼리는 경로가 같아서, 부모 아래 몇 번째인지로 구분해준다.
                sb.AppendLine($"    {tag} ({from})  {UIDuplicateGuard.FullPath(mb.transform)}  (부모 아래 {mb.transform.GetSiblingIndex() + 1}번째)");

                if (del && TryGetPrefabSource(mb.gameObject, out string assetPath, out string insidePath))
                {
                    if (!prefabsToFix.TryGetValue(assetPath, out var l)) { l = new List<string>(); prefabsToFix[assetPath] = l; }
                    if (!l.Contains(insidePath)) l.Add(insidePath);
                }
            }

            if (!decided)
            {
                sb.AppendLine(copies.Count == members.Count
                    ? "      -> 전부 프리팹 내용물이라 어느 것이 원본인지 구조로는 못 가린다. 배지 없음 - 직접 확인해라."
                    : "      -> 프리팹에서 온 복사본이 없다(전부 씬 소속). Apply 사고가 아니라 씬 안에서 복제된 것이다. 배지 없음 - 직접 확인해라.");
                refCache ??= BuildReferenceCache();
                foreach (var (mb, _) in members)
                {
                    bool referenced = refCache.Contains(mb.GetInstanceID()) || refCache.Contains(mb.gameObject.GetInstanceID());
                    sb.AppendLine($"         참고: 부모 아래 {mb.transform.GetSiblingIndex() + 1}번째 = {(referenced ? "다른 오브젝트가 인스펙터로 가리키고 있다" : "아무도 안 가리킨다")}");
                }
            }
        }

        int pending = ScanCloneOverlaps(sb, prefabsToFix);

        if (_toDelete.Count > 0)
        {
            sb.AppendLine($"\n=> 빨간 '삭제 필요' 배지 {_toDelete.Count}개. 각 묶음에서 남길 쪽에는 배지가 없다.");
            if (prefabsToFix.Count > 0)
            {
                sb.AppendLine("   근본 조치는 프리팹 파일 안에서 지우는 것이다. 프리팹에서 지우면 이 프리팹을 쓰는");
                sb.AppendLine("   모든 씬에서 한 번에 사라진다. 씬에서 지우면 이 씬만 정리되고 프리팹 안에는 남는다.");
                foreach (var kv in prefabsToFix)
                {
                    sb.AppendLine($"   - {kv.Key} 를 더블클릭으로 열고 아래를 지운 뒤 저장:");
                    foreach (var inside in kv.Value) sb.AppendLine($"       {inside}");
                }
                sb.AppendLine("   지운 뒤 다시 검사해서 0종인지 확인해라.");
            }
        }

        if (dups.Count > 0 || _toDelete.Count > 0) Debug.LogError(sb.ToString());
        else if (pending > 0) Debug.LogWarning(sb.ToString());
        else Debug.Log(sb.ToString());

        EditorApplication.RepaintHierarchyWindow();

        // 펼쳐 보여줄 대상: 확정된 '삭제 필요'가 있으면 그것, 없으면 첫 중복 묶음(위치라도 보게).
        var reveal = _toDelete.ToList();
        if (reveal.Count == 0 && dups.Count > 0)
            reveal = dups[0].Value.Select(m => m.gameObject.GetInstanceID()).ToList();
        RevealTargets(reveal);
    }

    // 출신 판별. '씬에서 추가(+)'가 먼저다 - 추가된 오브젝트가 그 자체로 프리팹 인스턴스일 수도
    // 있어서(프리팹 안에 프리팹을 추가한 경우), 순서를 바꾸면 원본을 복사본으로 오판한다.
    private static Origin OriginOf(GameObject go)
    {
        if (PrefabUtility.IsAddedGameObjectOverride(go)) return Origin.AddedInScene;
        if (PrefabUtility.IsPartOfPrefabInstance(go)) return Origin.PrefabContent;
        return Origin.PlainScene;
    }

    // 이 복사본이 어느 프리팹 에셋에서 왔는지와, 그 프리팹 안에서의 위치를 알아낸다.
    private static bool TryGetPrefabSource(GameObject go, out string assetPath, out string insidePath)
    {
        assetPath = null;
        insidePath = null;
        var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
        if (src == null) return false;
        assetPath = AssetDatabase.GetAssetPath(src);
        if (string.IsNullOrEmpty(assetPath)) return false;

        var sb = new StringBuilder(src.name);
        int guard = 0;
        for (var p = src.transform.parent; p != null && guard++ < 32; p = p.parent)
            sb.Insert(0, p.name + " / ");
        insidePath = sb.ToString();
        return true;
    }

    /// <summary>대상들을 하이어라키에서 '펼쳐서' 보여준다. 선택만 하면 부모가 접혀 있어 안 보인다.</summary>
    private static void RevealTargets(List<int> ids)
    {
        if (ids == null || ids.Count == 0) return;
        var want = new HashSet<int>(ids);

        var targets = UnityEngine.Object.FindObjectsByType<Transform>(
                          FindObjectsInactive.Include, FindObjectsSortMode.None)
                      .Where(t => t != null && want.Contains(t.gameObject.GetInstanceID()))
                      .ToList();
        if (targets.Count == 0) return;

        // 대상의 '부모 체인'을 전부 펼친다. 유니티 내부 API 라 버전이 바뀌면 없을 수 있어 예외를 삼킨다
        // (실패해도 아래 PingObject 가 최소한 첫 항목까지는 스크롤해준다).
        var toExpand = new HashSet<int>();
        foreach (var t in targets)
            for (var p = t.parent; p != null; p = p.parent)
                toExpand.Add(p.gameObject.GetInstanceID());

        try
        {
            var hierType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
            if (hierType != null)
            {
                const BindingFlags AnyInst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                foreach (var w in Resources.FindObjectsOfTypeAll(hierType))
                {
                    object host = hierType.GetProperty("sceneHierarchy", AnyInst)?.GetValue(w) ?? w;
                    var m = host.GetType().GetMethod("SetExpanded", AnyInst, null,
                                                     new[] { typeof(int), typeof(bool) }, null);
                    if (m == null) continue;
                    foreach (int id in toExpand) m.Invoke(host, new object[] { id, true });
                }
            }
        }
        catch { /* 내부 API 부재/변경 - 아래 폴백으로 충분 */ }

        Selection.objects = targets.Select(t => (UnityEngine.Object)t.gameObject).ToArray();
        EditorGUIUtility.PingObject(targets[0].gameObject);   // 첫 항목으로 스크롤 + 강조
        EditorApplication.RepaintHierarchyWindow();
    }

    // -- 검사 B: 싱글톤이 아닌 '겹쳐 그려지는 복제본' ---------------------
    // 싱글톤 검사만으로는 Title/FuelSlot/패널 같은 장식성 오브젝트를 못 잡는다.
    // 얘들은 파괴되지도 않고 둘 다 살아서 같은 자리에 겹쳐 그려진다(글자가 두 번 찍혀 진해진다).
    //
    // '같은 부모 아래'에서만, '로컬 좌표'로 비교한다. 한때 부모를 무시하고 월드 좌표로
    // 비교했더니 오탐이 폭발했다 - 꺼져 있는 패널은 레이아웃이 계산되지 않아 월드 좌표가
    // 뭉개져서, 서로 다른 버튼의 자식이나 다른 패널의 CloseButton 까지 한 자리로 묶였다.
    // 로컬 좌표는 꺼져 있어도 정확하다.
    //
    // 배지 기준은 검사 A 와 같은 구조 판정이다: 모양이 같은 짝에서 한쪽이 씬 소속(원본),
    // 한쪽이 프리팹 내용물(복사본)이면 그건 추정이 아니라 Apply 사고의 확정 신호다.
    // (08-08 실측: 이 서명에 걸린 20건을 프리팹/씬 파일 대조로 전수 확인 = 전부 진짜 중복)
    // 출신이 안 갈리는 짝만 '확인 필요'로 남긴다 - 그건 런타임에 코드가 채워 넣는
    // 리스트 항목(RecipeSlot 등)일 수 있어서 추정만으로 배지를 붙이면 안 된다.
    private static int ScanCloneOverlaps(StringBuilder sb, Dictionary<string, List<string>> prefabsToFix)
    {
        // 검사 A 가 이미 잡은 것과 그 자식은 보고하지 않는다.
        // 부모를 지우면 자식은 같이 사라지므로, 자식까지 나열하면 목록만 부풀고 헷갈린다.
        var covered = new HashSet<Transform>();
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (_toDelete.Contains(t.gameObject.GetInstanceID())) covered.Add(t);

        int found = 0;       // 출신이 안 갈려 '확인 필요'로만 남긴 것
        int confirmed = 0;   // 구조로 확정해 배지를 붙인 것
        var reported = new List<Transform>();   // 이미 보고한 지점(그 하위는 생략)
        foreach (var parent in UnityEngine.Object.FindObjectsByType<RectTransform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None)
                 .OrderBy(t => Depth(t)))       // 얕은 곳부터 = 부모를 먼저 보고
        {
            if (parent == null || parent.childCount < 2) continue;
            // 런타임에 자리를 잡아주는 컨테이너는 저장 좌표가 전부 같아 리스트 항목이 겹쳐 보인다.
            if (parent.GetComponent<UnityEngine.UI.LayoutGroup>() != null) continue;
            if (IsUnder(parent, covered) || IsUnder(parent, reported)) continue;

            var groups = new Dictionary<string, List<RectTransform>>();
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i) as RectTransform;
                if (c == null) continue;
                string sig = KindKey(c) + "|" + c.anchoredPosition + c.sizeDelta + c.anchorMin + c.anchorMax;
                if (!groups.TryGetValue(sig, out var list)) { list = new List<RectTransform>(); groups[sig] = list; }
                list.Add(c);
            }

            foreach (var kv in groups)
            {
                if (kv.Value.Count < 2) continue;
                if (kv.Value.Any(v => covered.Contains(v))) continue;   // 검사 A 가 이미 다룬 대상
                reported.Add(kv.Value[0]);

                // 짝의 출신을 가른다. 원본(씬 소속)이 확인될 때만 프리팹 복사본에 배지를 붙인다.
                var members = kv.Value.Select(v => (rt: v, origin: OriginOf(v.gameObject))).ToList();
                var copies = members.Where(x => x.origin == Origin.PrefabContent).ToList();
                bool decided = copies.Count > 0 && copies.Count < members.Count;

                if (!decided)
                {
                    found++;
                    sb.AppendLine($"\n[확인 필요] 같은 자리에 겹친 복제본 '{kv.Value[0].name}' x{kv.Value.Count}");
                    sb.AppendLine($"      부모: {UIDuplicateGuard.FullPath(parent)}");
                    continue;
                }

                confirmed++;
                sb.AppendLine($"\n[{kv.Value[0].name}] x{kv.Value.Count} (장식/패널 중복 - 출신으로 확정)");
                sb.AppendLine($"      부모: {UIDuplicateGuard.FullPath(parent)}");
                foreach (var (rt, origin) in members)
                {
                    bool del = origin == Origin.PrefabContent;
                    if (del)
                    {
                        _toDelete.Add(rt.gameObject.GetInstanceID());
                        covered.Add(rt);   // 이 복사본의 자식들은 따로 보고하지 않는다
                        if (TryGetPrefabSource(rt.gameObject, out string assetPath, out string insidePath))
                        {
                            if (!prefabsToFix.TryGetValue(assetPath, out var l)) { l = new List<string>(); prefabsToFix[assetPath] = l; }
                            if (!l.Contains(insidePath)) l.Add(insidePath);
                        }
                    }
                    string tag = del ? "[삭제 필요]" : "[남김     ]";
                    string from = origin == Origin.AddedInScene ? "씬에서 추가(+)"
                                : origin == Origin.PrefabContent ? "프리팹 내용물"
                                : "씬에 직접";
                    sb.AppendLine($"    {tag} ({from})  부모 아래 {rt.GetSiblingIndex() + 1}번째");
                }
            }
        }

        if (found == 0 && confirmed == 0) sb.AppendLine("\n(같은 자리에 겹친 복제본: 없음)");
        else if (found > 0) sb.AppendLine($"\n* [확인 필요] {found}건은 출신이 안 갈려 추정으로 남았다. 배지 없음 - 눈으로 확인하고 지워라.");
        return found;
    }

    /// <summary>
    /// 씬 안의 모든 컴포넌트가 인스펙터 필드로 '가리키고 있는' 대상들의 instanceID 집합.
    /// 판정에는 안 쓰고, 구조로 못 가린 묶음에서 수동 확인을 돕는 참고 자료로만 쓴다.
    /// (자기 자신/자기 자식이 가리키는 건 제외한다 - 복제본은 자기 내부 배선을 그대로 갖고 있어
    ///  그것까지 세면 양쪽 다 '참조됨'이 되어 정보가 무의미해진다)
    /// </summary>
    private static HashSet<int> BuildReferenceCache()
    {
        var refs = new HashSet<int>();
        foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            SerializedObject so;
            try { so = new SerializedObject(mb); } catch { continue; }

            var p = so.GetIterator();
            while (p.NextVisible(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                var target = p.objectReferenceValue;
                if (target == null) continue;

                Transform tt = target is Component c ? c.transform
                             : target is GameObject go ? go.transform : null;
                if (tt == null) continue;
                // 같은 서브트리 안에서의 자체 배선은 제외
                bool selfWiring = false;
                for (var x = tt; x != null; x = x.parent) if (x == mb.transform) { selfWiring = true; break; }
                if (selfWiring) continue;

                refs.Add(target.GetInstanceID());
                if (target is Component cc) refs.Add(cc.gameObject.GetInstanceID());
            }
        }
        return refs;
    }

    private static int Depth(Transform t)
    {
        int d = 0;
        for (var p = t.parent; p != null && d < 64; p = p.parent) d++;
        return d;
    }

    /// <summary>t 가 목록 중 하나의 자손(또는 자기 자신)인가. 부모가 이미 다뤄졌으면 자식은 건너뛴다.</summary>
    private static bool IsUnder(Transform t, IEnumerable<Transform> roots)
    {
        foreach (var r in roots)
        {
            if (r == null) continue;
            for (var p = t; p != null; p = p.parent) if (p == r) return true;
        }
        return false;
    }

    // '같은 종류'인지: 복제 접미사(Title (1), Title (Clone))를 뗀 이름 + 컴포넌트 구성.
    private static string KindKey(RectTransform t)
    {
        string name = System.Text.RegularExpressions.Regex.Replace(t.name, @"\s*(\(\d+\)|\(Clone\))\s*$", "");
        var comps = t.GetComponents<Component>().Where(c => c != null)
                     .Select(c => c.GetType().Name).OrderBy(n => n, StringComparer.Ordinal);
        return name + "|" + string.Join(",", comps);
    }

    [MenuItem("Tools/TIMEKOV/UI 중복 표시 지우기")]
    public static void ClearMarks()
    {
        _toDelete.Clear();
        EditorApplication.RepaintHierarchyWindow();
        Debug.Log("[UI 중복 검사] 하이어라키 표시를 지웠다.");
    }

    private static void DrawBadge(int instanceID, Rect rect)
    {
        if (_toDelete.Count == 0 || !_toDelete.Contains(instanceID)) return;

        if (_badge == null)
        {
            _badge = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        const string Text = "삭제 필요";
        float w = _badge.CalcSize(new GUIContent(Text)).x + 12f;
        var r = new Rect(rect.xMax - w - 2f, rect.y + 1f, w, rect.height - 2f);
        EditorGUI.DrawRect(r, new Color(0.78f, 0.18f, 0.18f, 0.95f));
        GUI.Label(r, Text, _badge);
    }
}
