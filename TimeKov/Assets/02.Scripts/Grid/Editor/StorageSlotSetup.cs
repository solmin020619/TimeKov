// =====================================================================
// Editor/StorageSlotSetup.cs
// Tools/TIMEKOV/설비/연마기 등록 + 저장고 G 슬롯 생성
//
// 손으로 하면 번거로운 두 가지를 한 번에 처리한다.
//   1) 씬의 FacilityPrefabDatabase 에 연마기(facilityId 10) 등록
//   2) 창고 출력 포트(T) 슬롯을 복제해 그 옆에 저장고(G) 슬롯을 만들고,
//      BuildManager.slotIconUIs 배열에 10번째로 연결
//
// 왜 T 를 복제하나:
//   저장고는 창고 출력 포트와 같은 '저장' 계열이고 개수 상한 배지(0/1)도 똑같이 쓴다.
//   T 슬롯을 그대로 복제하면 배지/스킨/크기가 자동으로 일치한다.
//
// 간격은 하드코딩하지 않고 '기존 슬롯 사이 간격'을 실측해서 그대로 이어 붙인다.
//
// [실행 전] World 씬이 열려 있어야 한다(씬 참조를 건드리므로).
// [재실행 안전] 이미 만든 G 슬롯이 있으면 지우고 다시 만든다.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public static class StorageSlotSetup
{
    private const string GrinderPrefabPath = "Assets/05.Prefabs/Grid/5X5/연마기.prefab";
    private const int GrinderFacilityId = 10;

    private const string CloneName = "Quick_G_Storage";
    private const string StorageKeyCap = "G";
    private const string PortKeyCap = "T";

    // ── 배치 조정값 (여기 세 개만 바꾸면 된다) ─────────────────────────
    // 0 이하 = 자동(현재 레이아웃에서 실측한 값 사용).

    /// 키 그룹(E/T/G) 칸 사이 간격(px). 0 이하면 숫자칸 간격과 똑같이 맞춘다.
    private const float KeyGroupGap = 172f;

    /// E 칸을 경계선 기준으로 대칭 배치할지.
    /// true = '8번 -> 경계선' 거리와 '경계선 -> E' 거리를 똑같이 맞춘다(경계선은 제자리).
    /// false = KeyGroupOffsetX 로 E 를 현 위치에서 상대 이동.
    private const bool MirrorEAcrossDivider = true;

    /// E 칸을 현 위치에서 가로로 이동(px). 양수 오른쪽. T/G 는 이 위치를 기준으로 줄 세운다.
    private const float KeyGroupOffsetX = 60f;

    /// 숫자칸(8번)과 키 그룹 첫 칸(E) 사이 간격(px). 0 이하면 E 를 현 위치 기준으로 둔다.
    private const float GroupSplitGap = 0f;

    /// 숫자칸 그룹(1~8)을 통째로 가로 이동(px). 양수 오른쪽, 음수 왼쪽.
    /// ★패널이 아니라 '칸들만' 움직인다 - 패널은 건설모드 UI 전체라 건드리면 화면이 밀린다.
    private const float NumberGroupOffsetX = 0f;

    [MenuItem("Tools/TIMEKOV/설비/연마기 등록 + 저장고 G 슬롯 생성")]
    public static void Run()
    {
        var build = Object.FindAnyObjectByType<BuildManager>(FindObjectsInactive.Include);
        if (build == null)
        {
            Debug.LogError("[설비셋업] 씬에서 BuildManager 를 못 찾았다. World 씬을 열고 다시 실행해라.");
            return;
        }

        RegisterGrinderPrefab();
        CreateStorageSlot(build);

        EditorUtility.SetDirty(build);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(build.gameObject.scene);
        Debug.Log("[설비셋업] 완료. 씬을 저장해라(Ctrl+S).");
    }

    // ── 1) 프리팹 DB 등록 ────────────────────────────────────────────

    private static void RegisterGrinderPrefab()
    {
        var db = Object.FindAnyObjectByType<FacilityPrefabDatabase>(FindObjectsInactive.Include);
        if (db == null) { Debug.LogError("[설비셋업] FacilityPrefabDatabase 를 못 찾았다."); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrinderPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[설비셋업] 연마기 프리팹이 없다: {GrinderPrefabPath}\n" +
                           "  먼저 'Tools > TIMEKOV > 설비 > 연마기 프리팹 생성' 을 실행해라.");
            return;
        }

        var so = new SerializedObject(db);
        var entries = so.FindProperty("entries");

        // 이미 등록돼 있으면 프리팹만 갱신(중복 추가 방지)
        for (int i = 0; i < entries.arraySize; i++)
        {
            var e = entries.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("facilityId").intValue != GrinderFacilityId) continue;
            e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(db);
            Debug.Log($"[설비셋업] 프리팹 DB {GrinderFacilityId}번 갱신");
            return;
        }

        entries.arraySize++;
        var added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
        added.FindPropertyRelative("facilityId").intValue = GrinderFacilityId;
        added.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(db);
        Debug.Log($"[설비셋업] 프리팹 DB 에 연마기(facilityId {GrinderFacilityId}) 등록");
    }

    // ── 2) 저장고 G 슬롯 ─────────────────────────────────────────────

    private static void CreateStorageSlot(BuildManager build)
    {
        var slots = build.slotIconUIs;
        if (slots == null || slots.Length == 0)
        {
            Debug.LogError("[설비셋업] BuildManager.slotIconUIs 가 비어 있다.");
            return;
        }

        // 배열에 죽은 참조(직전 실행분)가 섞여 있을 수 있으니 먼저 걸러낸다.
        var live = new List<QuickSlotIconUI>();
        foreach (var s in slots) if (s != null) live.Add(s);
        if (live.Count == 0) { Debug.LogError("[설비셋업] 살아있는 슬롯이 없다."); return; }

        // ★복제 대상은 아이콘이 아니라 '슬롯 한 칸 전체'다.
        //   slotIconUIs 는 아이콘 오브젝트만 가리키고, 키캡/개수배지는 그 형제라서
        //   아이콘만 복제하면 키캡이 안 따라온다.
        //
        //   컨테이너를 이름이나 키캡으로 추측하지 않는다(구조가 바뀌면 바로 깨진다).
        //   대신 '두 슬롯 아이콘의 공통 조상'을 찾아 슬롯들이 나란히 놓이는 부모를 구하고,
        //   그 부모의 바로 아래 자식 = 슬롯 한 칸으로 확정한다. 계층 깊이와 무관하게 맞다.
        // ★슬롯 '한 칸'을 정확히 특정한다.
        //   칸 = 키캡과 아이콘을 '둘 다' 품는 가장 작은 조상.
        //   공통 조상으로 잡으면 안 된다 - 숫자칸은 QuickSlotPanel, T칸은 Canvas 쪽이라
        //   공통 조상이 패널 전체가 되고, 그걸 복제하면 UI 한 덩어리가 통째로 복사돼
        //   원본을 덮어버린다(T 슬롯이 사라져 보이던 원인).
        RectTransform src = null;
        foreach (var s in live)
        {
            var cell = FindCell(s);
            if (cell == null) continue;
            if (FindKeyCap(cell, PortKeyCap) != null) { src = cell; break; }
        }
        if (src == null)
        {
            // 'T' 를 못 찾으면 화면상 가장 오른쪽 칸
            foreach (var s in live)
            {
                var cell = FindCell(s);
                if (cell == null) continue;
                if (src == null || cell.position.x > src.position.x) src = cell;
            }
            if (src != null) Debug.LogWarning($"[설비셋업] 'T' 키캡을 못 찾아 가장 오른쪽 칸('{src.name}')을 복제한다.");
        }
        if (src == null)
        {
            Debug.LogError("[설비셋업] 복제할 슬롯 칸을 못 찾았다.\n" + DumpChain(live[0].transform));
            return;
        }

        // 안전장치 - 칸 하나치고 너무 크면 패널을 잘못 잡은 것이다. 복제하면 UI 가 두 벌 된다.
        float cellW = src.rect.width * Mathf.Abs(src.lossyScale.x);
        if (cellW > Screen.width * 0.5f)
        {
            Debug.LogError($"[설비셋업] 잡은 칸('{src.name}')이 너무 크다(폭 {cellW:0}px). " +
                           "슬롯 한 칸이 아니라 패널을 잡은 것이라 중단한다.\n" + DumpChain(src));
            return;
        }

        var parent = src.parent;
        Debug.Log($"[설비셋업] 슬롯 칸 = '{src.name}' (부모 '{parent.name}', 칸 폭 {cellW:0}px)");

        // ★이전 실행이 남긴 복제본을 씬 전체에서 지운다.
        //   예전 버전이 엉뚱한 부모에 만들어 둔 것도 있어서, 부모 밑만 뒤지면 놓친다.
        //   놓치면 배열에 유령 슬롯이 남아 인덱스가 밀리고, 결국 저장고 자리에 엉뚱한 칸이 걸려
        //   건축바에서 잠긴 것처럼 보인다(11개가 되던 원인).
        int stale = 0;
        foreach (var rt in build.GetComponentsInChildren<RectTransform>(true))
            if (rt != null && rt.name == CloneName) { Object.DestroyImmediate(rt.gameObject); stale++; }
        foreach (var root in build.gameObject.scene.GetRootGameObjects())
            foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                if (rt != null && rt.name == CloneName) { Object.DestroyImmediate(rt.gameObject); stale++; }
        if (stale > 0) Debug.Log($"[설비셋업] 이전 복제본 {stale}개 제거");

        // 파괴로 죽은 참조를 배열에서도 걷어낸다
        live.RemoveAll(s => s == null);

        // 간격 = 'T 칸과 그 왼쪽 이웃(E) 사이' 간격.
        // ★slotIconUIs 만 보고 재면 안 된다 - 레일(E)은 배열이 아니라 railSlotIcon 이라는
        //   별도 필드라서 후보에서 빠지고, 그러면 훨씬 먼 8번 칸과의 거리가 잡혀 배치가 어긋난다.
        //   부모 아래 '칸다운 형제'를 직접 보고 잰다.
        float step = MeasureNeighborGap(parent, src);

        var clone = (RectTransform)Object.Instantiate(src.gameObject, parent).transform;
        clone.name = CloneName;
        clone.SetSiblingIndex(src.GetSiblingIndex() + 1);
        clone.anchorMin = src.anchorMin;
        clone.anchorMax = src.anchorMax;
        clone.pivot = src.pivot;
        clone.anchoredPosition = src.anchoredPosition + new Vector2(step, 0f);

        // 키캡 T -> G. 정확히 'T' 인 라벨이 없으면 한 글자짜리 라벨을 키캡으로 본다.
        var cap = FindKeyCap(clone, PortKeyCap) ?? FindSingleCharLabel(clone);
        if (cap != null)
        {
            Debug.Log($"[설비셋업] 키캡 '{cap.text}' -> '{StorageKeyCap}' ({cap.name})");
            cap.text = StorageKeyCap;
        }
        else
        {
            Debug.LogWarning("[설비셋업] 복제본에서 키캡을 못 찾았다. 인스펙터에서 글자를 직접 G 로 바꿔라.\n"
                             + DumpLabels(clone));
        }

        // slotIconUIs 배열에 10번째로 추가 (아이콘 컴포넌트는 컨테이너 안에 있다)
        var cloneIcon = clone.GetComponentInChildren<QuickSlotIconUI>(true);
        if (cloneIcon == null) { Debug.LogError("[설비셋업] 복제본에서 QuickSlotIconUI 를 못 찾았다."); return; }

        // 원본이 꺼진 상태로 저장돼 있으면 복제본도 꺼진 채로 태어난다.
        // 그러면 런타임에 해금 연출 코루틴을 못 돌려 슬롯이 잠긴 채로 남는다 - 켜 둔다.
        if (!cloneIcon.gameObject.activeSelf) cloneIcon.gameObject.SetActive(true);
        if (!clone.gameObject.activeSelf) clone.gameObject.SetActive(true);

        live.Remove(cloneIcon);
        live.Add(cloneIcon);
        build.slotIconUIs = live.ToArray();

        // ★칸 수 검증. 시트 buildSlot 최대치(저장고=10)와 배열 길이가 어긋나면
        //   해금된 설비가 엉뚱한 칸에 걸려 건축바에서 잠긴 것처럼 보인다.
        //   (유령 복제본 탓에 11개가 되면서 저장고 자리가 밀렸던 게 정확히 그 증상이었다)
        if (live.Count != FacilityUnlockManager.MaxSlots)
            Debug.LogError($"[설비셋업] slotIconUIs 가 {live.Count}개다. " +
                           $"{FacilityUnlockManager.MaxSlots}개여야 맞다(MaxSlots). " +
                           "유령 슬롯이 남아 있거나 배열에 빠진 칸이 있다 - 이 로그를 알려달라.");

        // 배치 결과 검증 - 실제로 얼마나 떨어졌는지 화면 좌표로 재서 남긴다.
        Canvas.ForceUpdateCanvases();
        float gap = Mathf.Abs(clone.position.x - src.position.x) / Mathf.Max(0.0001f, Mathf.Abs(src.lossyScale.x));
        if (gap < 5f)
        {
            Debug.LogError($"[설비셋업] 새 칸이 원본과 겹쳤다(간격 {gap:0.#}px). 간격 실측 실패 - 이 로그를 알려달라.");
        }
        else
        {
            Debug.Log($"[설비셋업] 저장고 슬롯 생성: '{src.name}' 복제 -> '{CloneName}'. " +
                      $"간격 {gap:0.#}px. slotIconUIs = {live.Count}개. 키 = G");

            // 오른쪽 키 그룹(E/T/G)을 왼쪽부터 E, T, G 순서로 균등 재배치.
            // ★현재 위치에서 상대로 밀지 않고 절대 위치로 다시 깐다 - 툴을 몇 번 돌려도 결과가 같다
            //   (예전엔 실행할 때마다 왼쪽으로 누적 이동해서 슬롯 바가 화면 밖으로 밀렸다).
            RectTransform eCell = build.railSlotIcon != null ? FindCellFrom(build.railSlotIcon) : null;
            // 키 그룹 간격은 숫자칸 리듬을 그대로 따른다(측정 실패 시에만 이웃 간격 폴백).
            float numberGap = MeasureNumberGap(live, eCell, src, clone);
            float keyGap = KeyGroupGap > 0f ? KeyGroupGap : (numberGap > 0f ? numberGap : step);
            ArrangeKeyGroup(eCell, src, clone, keyGap, live);

            // 숫자칸 그룹만 따로 밀고 싶을 때(패널은 안 건드린다)
            if (Mathf.Abs(NumberGroupOffsetX) > 0.01f)
                ShiftNumberGroup(live, eCell, src, clone, NumberGroupOffsetX);

            // ★패널 자체는 절대 옮기지 않는다.
            //   'QuickSlotPanel' 은 슬롯 바가 아니라 건설모드 UI 전체다(탑뷰 나가기 / 청사진 /
            //   해제 / 회전 / 구분선 / 배경까지 전부 이 밑에 있다). 이걸 옮기면 화면이 통째로
            //   밀려서 배경이 한쪽만 잘린다. 슬롯 위치는 칸 단위로만 조정한다.
            var panel = parent as RectTransform;
            if (panel != null) RestorePanelPosition(panel);
        }
    }

    /// 슬롯 '한 칸' = 아이콘과 키캡(한 글자 라벨)을 둘 다 품는 가장 작은 조상.
    /// 아이콘에서 위로 한 단계씩 올라가며 처음으로 키캡이 포함되는 지점에서 멈춘다.
    private static RectTransform FindCell(QuickSlotIconUI icon)
    {
        for (Transform t = icon.transform; t != null; t = t.parent)
        {
            var rt = t as RectTransform;
            if (rt == null) continue;
            if (FindSingleCharLabel(rt) != null) return rt;   // 키캡까지 들어온 순간이 칸 경계
            if (t.GetComponent<Canvas>() != null) break;       // 캔버스까지 가면 실패
        }
        return null;
    }

    /// 두 오브젝트의 가장 가까운 공통 조상. 슬롯들이 나란히 놓이는 부모를 구조 추측 없이 찾는다.
    private static Transform LowestCommonAncestor(Transform a, Transform b)
    {
        var chain = new HashSet<Transform>();
        for (Transform t = a; t != null; t = t.parent) chain.Add(t);
        for (Transform t = b; t != null; t = t.parent)
            if (chain.Contains(t)) return t;
        return null;
    }

    /// parent 의 직계 자식 중 descendant 를 품고 있는 것(= 슬롯 한 칸).
    private static Transform ChildOf(Transform parent, Transform descendant)
    {
        for (Transform t = descendant; t != null; t = t.parent)
            if (t.parent == parent) return t;
        return null;
    }

    /// 한 글자짜리 라벨 = 키캡으로 간주(키캡 글자가 T 가 아니어도 잡히게).
    private static TMP_Text FindSingleCharLabel(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == null || string.IsNullOrWhiteSpace(t.text)) continue;
            if (t.text.Trim().Length == 1) return t;
        }
        return null;
    }

    /// 실패 진단용 - 부모 체인 덤프
    private static string DumpChain(Transform t)
    {
        var sb = new System.Text.StringBuilder("  부모 체인: ");
        for (; t != null; t = t.parent) sb.Append(t.name).Append(" < ");
        return sb.ToString();
    }

    /// 실패 진단용 - 복제본 안 라벨 덤프
    private static string DumpLabels(Transform root)
    {
        var sb = new System.Text.StringBuilder("  안에 있는 라벨: ");
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            sb.Append($"[{t.name}='{t.text}'] ");
        return sb.ToString();
    }

    /// 슬롯 컨테이너 찾기 - 아이콘에서 위로 올라가며 '키캡 텍스트를 자손으로 갖는' 첫 조상.
    /// keyCap 이 null 이면 키캡 종류를 가리지 않고 아무 키캡이나 품은 조상을 찾는다.
    /// (slotIconUIs 는 아이콘만 가리키는데, 키캡/배지는 그 형제라 컨테이너째 복제해야 한다)
    private static RectTransform FindSlotContainer(Transform icon, string keyCap)
    {
        for (Transform t = icon; t != null; t = t.parent)
        {
            var rt = t as RectTransform;
            if (rt == null) continue;
            if (keyCap != null)
            {
                if (FindKeyCap(rt, keyCap) != null) return rt;
            }
            else
            {
                // 키캡 후보 = 한 글자짜리 라벨. 슬롯 하나를 통째로 감싸는 최소 단위를 잡는다.
                foreach (var txt in rt.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (txt == null || string.IsNullOrEmpty(txt.text)) continue;
                    if (txt.text.Trim().Length == 1) return rt;
                }
            }
            // 캔버스까지 올라가면 그만(슬롯 하나가 아니라 패널 전체를 잡는 사고 방지)
            if (t.GetComponent<Canvas>() != null) break;
        }
        return null;
    }

    /// 오른쪽 키 그룹을 왼쪽부터 E, T, G 순서로 같은 간격에 다시 깐다.
    /// 세 칸의 현재 위치 중 가장 왼쪽을 기준점으로 삼아 절대 배치하므로 재실행해도 결과가 같다.
    private static void ArrangeKeyGroup(RectTransform e, RectTransform t, RectTransform g, float step,
                                        List<QuickSlotIconUI> allSlots)
    {
        var order = new List<RectTransform>();
        // 부모가 다르면 로컬 좌표를 나란히 비교할 수 없다 - 그 칸은 건드리지 않는다.
        if (e != null && t != null && e.parent != t.parent)
        {
            Debug.LogWarning($"[설비셋업] 레일(E) 칸의 부모가 달라('{e.parent?.name}') 정렬에서 제외한다.");
            e = null;
        }
        if (e != null) order.Add(e);
        if (t != null) order.Add(t);
        if (g != null) order.Add(g);
        if (order.Count == 0) return;

        // 기준 = E 칸의 현재 위치(+오프셋). E 가 없으면 그룹에서 가장 왼쪽 칸.
        float baseX;
        if (e != null)
        {
            baseX = e.anchoredPosition.x + KeyGroupOffsetX;
        }
        else
        {
            baseX = float.MaxValue;
            foreach (var rt in order) baseX = Mathf.Min(baseX, rt.anchoredPosition.x);
        }

        // 숫자칸(키 그룹이 아닌 칸들) 중 가장 오른쪽 = 경계선의 왼쪽 기준점.
        RectTransform lastNumber = null;
        if (allSlots != null)
        {
            foreach (var s in allSlots)
            {
                if (s == null) continue;
                var cell = FindCell(s);
                if (cell == null || order.Contains(cell) || cell.parent != order[0].parent) continue;
                if (lastNumber == null || cell.anchoredPosition.x > lastNumber.anchoredPosition.x) lastNumber = cell;
            }
            // 두 그룹 사이 간격을 지정했으면 그 값으로 E 위치를 다시 잡는다.
            if (lastNumber != null && GroupSplitGap > 0f) baseX = lastNumber.anchoredPosition.x + GroupSplitGap;
        }

        // 경계선 기준 대칭 배치 - '8번 -> 경계선' 만큼 '경계선 -> E' 를 띄운다.
        // 경계선은 건드리지 않고 E 만 맞추므로, 눈에 보이는 좌우 여백이 같아진다.
        RectTransform dividerRt = FindDivider(order[0].parent, order[0]);
        if (MirrorEAcrossDivider && dividerRt != null && lastNumber != null)
        {
            float dividerX = dividerRt.anchoredPosition.x;
            float leftGap = dividerX - lastNumber.anchoredPosition.x;
            if (leftGap > 0.5f)
            {
                baseX = dividerX + leftGap;
                Debug.Log($"[설비셋업] E 를 경계선 기준 대칭 배치: 8번 {lastNumber.anchoredPosition.x:0} " +
                          $"-> 경계선 {dividerX:0} -> E {baseX:0} (간격 {leftGap:0}px)");
            }
        }

        for (int i = 0; i < order.Count; i++)
        {
            var rt = order[i];
            var p = rt.anchoredPosition;
            p.x = baseX + step * i;
            rt.anchoredPosition = p;
            rt.SetAsLastSibling();   // 그리기 순서도 E -> T -> G 로 맞춘다
        }

        // ★경계선은 옮기지 않는다. 위에서 E 위치의 '기준'으로 쓰고 있어서,
        //   여기서 또 움직이면 실행할 때마다 기준이 흔들려 배치가 매번 달라진다.
        if (dividerRt == null)
            Debug.LogWarning("[설비셋업] 경계선을 못 찾아 E 를 대칭 배치하지 못했다. 인스펙터에서 직접 맞춰라.");

        Canvas.ForceUpdateCanvases();
        Debug.Log($"[설비셋업] 키 그룹 정렬: E -> T -> G, 간격 {step:0}px" +
                  (lastNumber != null ? $", 숫자칸과의 간격 {GroupSplitGap:0}px" : ""));
    }

    /// 숫자칸(1~8)들 사이의 실제 간격. 키 그룹도 이 리듬을 그대로 따르게 하려고 잰다.
    private static float MeasureNumberGap(List<QuickSlotIconUI> slots, RectTransform e,
                                          RectTransform t, RectTransform g)
    {
        var xs = new List<float>();
        foreach (var s in slots)
        {
            if (s == null) continue;
            var cell = FindCell(s);
            if (cell == null || cell == e || cell == t || cell == g) continue;
            xs.Add(cell.anchoredPosition.x);
        }
        if (xs.Count < 2) return 0f;
        xs.Sort();

        float best = 0f;
        for (int i = 1; i < xs.Count; i++)
        {
            float d = xs[i] - xs[i - 1];
            if (d > 0.5f && (best <= 0f || d < best)) best = d;
        }
        return best;
    }

    /// 숫자칸(키 그룹이 아닌 칸)들만 가로로 민다. 패널은 건드리지 않는다.
    private static void ShiftNumberGroup(List<QuickSlotIconUI> slots, RectTransform e,
                                         RectTransform t, RectTransform g, float dx)
    {
        var moved = new HashSet<RectTransform>();
        foreach (var s in slots)
        {
            if (s == null) continue;
            var cell = FindCell(s);
            if (cell == null || cell == e || cell == t || cell == g) continue;
            if (!moved.Add(cell)) continue;
            cell.anchoredPosition += new Vector2(dx, 0f);
        }
        Canvas.ForceUpdateCanvases();
        Debug.Log($"[설비셋업] 숫자칸 {moved.Count}개를 {dx:0}px 이동");
    }

    /// 건설모드 UI 패널의 위치를 프리팹 원본값으로 되돌린다.
    /// 예전 버전이 이 패널을 통째로 왼쪽으로 밀어 화면이 잘렸었다(실행할 때마다 누적됐다).
    /// 프리팹 값으로 revert 하면 몇 번을 밀었든 한 번에 제자리로 돌아온다.
    private static void RestorePanelPosition(RectTransform panel)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(panel))
        {
            Debug.LogWarning($"[설비셋업] '{panel.name}' 이 프리팹 인스턴스가 아니라 위치 복구를 건너뛴다. " +
                             "위치가 틀어져 있으면 인스펙터에서 직접 되돌려라.");
            return;
        }

        Vector2 before = panel.anchoredPosition;
        var so = new SerializedObject(panel);
        var prop = so.FindProperty("m_AnchoredPosition");
        if (prop == null) return;

        PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
        Canvas.ForceUpdateCanvases();

        Vector2 after = panel.anchoredPosition;
        if ((after - before).sqrMagnitude > 0.01f)
            Debug.Log($"[설비셋업] '{panel.name}' 위치 복구: {before} -> {after} (프리팹 원본값)");
    }

    /// 숫자칸과 키 그룹 사이의 경계선(칸보다 확연히 좁은 세로 장식) 찾기.
    private static RectTransform FindDivider(Transform parent, RectTransform sample)
    {
        float cellW = sample.rect.width;
        RectTransform best = null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var rt = parent.GetChild(i) as RectTransform;
            if (rt == null || rt == sample) continue;
            if (rt.rect.width >= cellW * 0.5f) continue;          // 칸 급이면 장식이 아니다
            if (rt.rect.height < sample.rect.height * 0.3f) continue;  // 너무 납작한 것도 제외
            if (best == null || rt.rect.width < best.rect.width) best = rt;
        }
        return best;
    }

    /// Transform 에서 슬롯 칸 찾기(레일 아이콘처럼 QuickSlotIconUI 참조가 아닌 경우용).
    private static RectTransform FindCellFrom(Transform icon)
    {
        for (Transform t = icon; t != null; t = t.parent)
        {
            var rt = t as RectTransform;
            if (rt == null) continue;
            if (FindSingleCharLabel(rt) != null) return rt;
            if (t.GetComponent<Canvas>() != null) break;
        }
        return null;
    }

    /// src 칸과 '바로 왼쪽 이웃 칸' 사이의 로컬 간격.
    /// 부모의 형제들 중 크기가 칸에 준하는 것만 이웃으로 본다(구분선 같은 얇은 장식은 제외).
    /// 레일(E)처럼 배열에 없는 칸도 형제로는 잡히므로 실제 눈에 보이는 간격이 나온다.
    private static float MeasureNeighborGap(Transform parent, RectTransform src)
    {
        float srcX = src.anchoredPosition.x;
        float minW = src.rect.width * 0.5f;
        float leftX = float.NegativeInfinity;

        for (int i = 0; i < parent.childCount; i++)
        {
            var rt = parent.GetChild(i) as RectTransform;
            if (rt == null || rt == src) continue;
            if (rt.rect.width < minW) continue;              // 구분선/장식 제외
            float x = rt.anchoredPosition.x;
            if (x < srcX - 0.5f && x > leftX) leftX = x;      // src 왼쪽 중 가장 가까운 칸
        }

        if (leftX > float.NegativeInfinity) return srcX - leftX;
        return Mathf.Max(src.rect.width, 60f) + 12f;          // 못 재면 칸 폭 + 여백
    }

    // 슬롯 안의 키캡 텍스트(정확히 일치) 찾기
    private static TMP_Text FindKeyCap(Transform root, string cap)
    {
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == null || t.text == null) continue;
            if (t.text.Trim() == cap) return t;
        }
        return null;
    }
}
