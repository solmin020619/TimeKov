// =====================================================================
// FactoryBagScrollFix.cs (Editor Only)
// Tools/TIMEKOV/공장 가방 스크롤 적용
//
// 공장(설비) UI 왼쪽 "가방"(Inven)이 maxSlots 35로 늘면서 고정 영역을
// 세로로 삐져나오던 문제 + 1.325 가로스케일 핵 때문에 아이콘이 작고
// 찌그러지던 문제 + 제목줄 긴 회색바 + 수량 숫자 작던 문제를 정리한다.
//
// 1) 거슬리는 긴 회색 바(빈 Image) 비활성화
// 2) Inven 의 가로스케일(1.325) 제거 + 시각 너비 보존
// 3) Inven 자식(중첩 Content 잔재 포함) 전부 정리 후 Content 1개를 새로 생성
//    -> GridLayoutGroup(정사각 칸) + ContentSizeFitter, Inven 은 ScrollRect+RectMask2D
//    -> 런타임 슬롯이 Content 에 쌓여 세로 스크롤
// 4) MachineUI.inventorySlotParent 을 Content 로 재지정
// 5) 슬롯 프리팹 ItemIcon = 칸 채우기, 수량칩/텍스트 = 칸 비율+자동크기
//
// ★ 복구형/재실행 안전: Inven 을 "이름"으로 찾고(=inventorySlotParent 기준 X,
//   그래야 두 번 돌려도 중첩 안 생김), 매번 자식을 비우고 새로 만든다.
//   (슬롯은 런타임 생성이라 prefab 자식 비워도 데이터 손실 없음)
// =====================================================================

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class FactoryBagScrollFix
{
    const string CanvasPath = "Assets/05.Prefabs/UI/Canvas.prefab";
    const string SlotPath   = "Assets/05.Prefabs/Inventory/InventorySlot.prefab";

    [MenuItem("Tools/TIMEKOV/공장 가방 스크롤 적용")]
    public static void Apply()
    {
        bool a = FixCanvasBag();
        bool b = FixSlotIcon();
        AssetDatabase.SaveAssets();
        if (a)
            EditorUtility.DisplayDialog("완료",
                "공장 가방 스크롤 적용 완료." + (b ? "\n슬롯 아이콘/수량도 적용." : "") +
                "\nPlay -> 설비 열어서 확인.", "확인");
    }

    static bool FixCanvasBag()
    {
        var root = PrefabUtility.LoadPrefabContents(CanvasPath);
        try
        {
            var machineUI = root.GetComponentInChildren<MachineUI>(true);
            if (machineUI == null) { Debug.LogError("[FactoryBagScrollFix] MachineUI 없음"); return false; }
            if (machineUI.uiPanel == null) { Debug.LogError("[FactoryBagScrollFix] uiPanel 없음"); return false; }
            var panel = machineUI.uiPanel.transform;

            // 거슬리는 긴 회색 바 비활성화
            DisableBagHeaderBar(machineUI);

            // ★ 원본 가방 컨테이너 "Inven"을 "이름"으로 찾는다.
            // inventorySlotParent 는 한 번 변환되면 Content 를 가리키므로 기준으로 쓰면 중첩됨.
            var inven = panel.Find("Inven") as RectTransform;
            if (inven == null)
            {
                // 폴백: 드롭존이 붙은 게 원본 Inven
                var dz = root.GetComponentInChildren<InventoryPanelDropZone>(true);
                if (dz != null) inven = dz.GetComponent<RectTransform>();
            }
            if (inven == null) { Debug.LogError("[FactoryBagScrollFix] 가방 컨테이너(Inven) 못 찾음"); return false; }

            // 1) 가로 스케일 핵 제거 + 시각 너비/높이 보존 (예: 861 * 1.325 = 1141)
            if (Mathf.Abs(inven.localScale.x - 1f) > 0.001f || Mathf.Abs(inven.localScale.y - 1f) > 0.001f)
            {
                float vw = inven.sizeDelta.x * inven.localScale.x;
                float vh = inven.sizeDelta.y * inven.localScale.y;
                inven.localScale = Vector3.one;
                inven.sizeDelta = new Vector2(vw, vh);
            }

            // 2) 기존 자식(중첩된 Content 잔재 포함) 전부 제거 - 슬롯은 런타임 생성이라 안전.
            for (int i = inven.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(inven.GetChild(i).gameObject);

            // Inven 위에 남은 GridLayoutGroup 있으면 제거 (viewport 라 레이아웃 없어야)
            var strayGrid = inven.GetComponent<GridLayoutGroup>();
            if (strayGrid != null) Object.DestroyImmediate(strayGrid);

            // 3) Content 새로 생성 (그리드 + 사이즈피터)
            int cols = 6;
            Vector2 spacing = new Vector2(5, 5);
            float cellH = 180f;
            float cellW = Mathf.Floor((inven.sizeDelta.x - spacing.x * (cols - 1)) / cols);
            Vector2 cellSize = new Vector2(cellW, cellH);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            var content = contentGo.GetComponent<RectTransform>();
            content.SetParent(inven, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.anchoredPosition = Vector2.zero;

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = cols;
            grid.childAlignment = TextAnchor.UpperCenter;

            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // 4) Inven = Viewport(RectMask2D) + ScrollRect (있으면 재사용)
            if (inven.GetComponent<RectMask2D>() == null) inven.gameObject.AddComponent<RectMask2D>();
            var scroll = inven.GetComponent<ScrollRect>();
            if (scroll == null) scroll = inven.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport = inven;
            scroll.content = content;

            // 5) 런타임 슬롯 부모 = Content
            machineUI.inventorySlotParent = content;
            EditorUtility.SetDirty(machineUI);

            PrefabUtility.SaveAsPrefabAsset(root, CanvasPath);
            Debug.Log($"[FactoryBagScrollFix] 적용/복구 완료: cols={cols} cell={cellSize} viewport={inven.sizeDelta}");
            return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static bool FixSlotIcon()
    {
        var root = PrefabUtility.LoadPrefabContents(SlotPath);
        try
        {
            var icon = root.transform.Find("ItemIcon") as RectTransform;
            if (icon == null) { Debug.LogWarning("[FactoryBagScrollFix] InventorySlot 에 ItemIcon 없음"); return false; }

            // 고정 78 -> 칸 채우기(여백 6). 90칸이면 78로 동일, 큰 칸이면 그만큼 커짐.
            icon.anchorMin = Vector2.zero;
            icon.anchorMax = Vector2.one;
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.offsetMin = new Vector2(6, 6);
            icon.offsetMax = new Vector2(-6, -6);

            var img = icon.GetComponent<Image>();
            if (img != null) img.preserveAspect = true;

            // 수량칩/텍스트: 우상단을 칸 비율(~40% x 24%)로 잡아 셀 크기 따라 같이 커지게 + 텍스트 자동크기.
            var chip = root.transform.Find("AmountChip") as RectTransform;
            if (chip != null) SetAmountRect(chip);
            var amt = root.transform.Find("AmountText") as RectTransform;
            if (amt != null)
            {
                SetAmountRect(amt);
                var tmp = amt.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 12; tmp.fontSizeMax = 40;
                    tmp.alignment = TextAlignmentOptions.Center;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, SlotPath);
            return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // 가방 제목줄에 깔린 긴 회색 바(빈 Image)를 끈다.
    // 특정: 폭 매우넓고(>800) 높이 낮고(<100) 가방제목 높이대(posY 300~550)이며 스프라이트 없는 직속 자식.
    static void DisableBagHeaderBar(MachineUI machineUI)
    {
        var panel = (machineUI != null && machineUI.uiPanel != null) ? machineUI.uiPanel.transform : null;
        if (panel == null) return;
        for (int i = 0; i < panel.childCount; i++)
        {
            var c = panel.GetChild(i) as RectTransform;
            if (c == null) continue;
            var im = c.GetComponent<Image>();
            if (im == null || im.sprite != null) continue;
            if (c.sizeDelta.x > 800f && c.sizeDelta.y < 100f &&
                c.anchoredPosition.y > 300f && c.anchoredPosition.y < 550f)
            {
                c.gameObject.SetActive(false);
                Debug.Log($"[FactoryBagScrollFix] 가방 제목줄 회색 바 비활성화: {c.name} size={c.sizeDelta} pos={c.anchoredPosition}");
                return;
            }
        }
        Debug.Log("[FactoryBagScrollFix] 가방 제목줄 회색 바 매칭 없음 (이미 꺼졌거나 구조 변경).");
    }

    // 수량칩/텍스트 공통: 우상단을 칸 비율로 잡아 셀 크기에 따라 같이 커지게.
    static void SetAmountRect(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0.56f, 0.72f);
        rt.anchorMax = new Vector2(0.96f, 0.96f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
