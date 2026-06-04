// =====================================================================
// ChestPanelBuilder.cs  (Editor Only)
// Tools/TIMEKOV/상자 패널 생성
// 씬의 WarehousePanel 을 찾아 복제 → 상자 패널로 변환 → 자동 연결
// =====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class ChestPanelBuilder
{
    [MenuItem("Tools/TIMEKOV/상자 패널 생성 (창고 복제)")]
    public static void Build()
    {
        // ── InventoryUIController 찾기 ────────────────────────────────
        var invCtrl = Object.FindAnyObjectByType<InventoryUIController>();
        if (invCtrl == null)
        {
            EditorUtility.DisplayDialog("오류",
                "씬에 InventoryUIController가 없습니다.", "확인");
            return;
        }

        var ctrlSO = new SerializedObject(invCtrl);

        // ── 이미 있으면 교체 여부 확인 ────────────────────────────────
        var existingChestProp = ctrlSO.FindProperty("chestPanel");
        if (existingChestProp?.objectReferenceValue != null)
        {
            bool replace = EditorUtility.DisplayDialog("경고",
                "chestPanel이 이미 연결되어 있습니다. 교체할까요?", "교체", "취소");
            if (!replace) return;
        }

        // ── WarehousePanel 찾기 (복제 원본) ──────────────────────────
        var warehousePanelProp = ctrlSO.FindProperty("warehousePanel");
        GameObject warehousePanel = warehousePanelProp?.objectReferenceValue as GameObject;

        if (warehousePanel == null)
        {
            EditorUtility.DisplayDialog("오류",
                "InventoryUIController의 warehousePanel이 연결되지 않았습니다.\n" +
                "인스펙터에서 warehousePanel을 먼저 연결해주세요.", "확인");
            return;
        }

        // ── WarehousePanel 복제 ───────────────────────────────────────
        GameObject chestPanel = Object.Instantiate(warehousePanel, warehousePanel.transform.parent);
        chestPanel.name = "ChestPanel";
        chestPanel.transform.SetSiblingIndex(warehousePanel.transform.GetSiblingIndex() + 1);
        chestPanel.SetActive(false);

        // ── 타이틀 텍스트 변경 ────────────────────────────────────────
        ReplaceTextInChildren(chestPanel, "창고", "상자");
        ReplaceTextInChildren(chestPanel, "WAREHOUSE", "CHEST");
        ReplaceTextInChildren(chestPanel, "Warehouse", "Chest");

        // ── ChestGridUI 찾기 (복제된 패널 안의 InventoryGridUI) ───────
        InventoryGridUI chestGridUI = chestPanel.GetComponentInChildren<InventoryGridUI>(true);
        if (chestGridUI != null)
        {
            // ChestInstance에 바인딩
            if (InventoryManager.ChestInstance != null)
            {
                chestGridUI.Bind(InventoryManager.ChestInstance);
            }
        }

        // ── "모두 가져오기" 버튼 찾아서 takeAllFromChestBtn에 연결 ────
        // 복제된 패널의 버튼들 중 "가져오기" or "TakeAll" 버튼 찾기
        Button takeAllBtn = FindButtonByName(chestPanel, "TakeAll");
        if (takeAllBtn == null) takeAllBtn = FindButtonByName(chestPanel, "takeAll");
        if (takeAllBtn == null) takeAllBtn = FindButtonByName(chestPanel, "가져오기");

        // ── InventoryUIController에 레퍼런스 연결 ─────────────────────
        ctrlSO.FindProperty("chestPanel")?.SetValue(chestPanel);
        ctrlSO.FindProperty("chestGridUI")?.SetValue(chestGridUI);
        if (takeAllBtn != null)
            ctrlSO.FindProperty("takeAllFromChestBtn")?.SetValue(takeAllBtn);

        ctrlSO.ApplyModifiedProperties();

        // ── ChestInventory 오브젝트 없으면 자동 생성 ─────────────────
        if (InventoryManager.ChestInstance == null)
        {
            bool createInv = EditorUtility.DisplayDialog("안내",
                "ChestInstance(InventoryManager.Chest)가 없습니다.\n" +
                "씬에 자동으로 생성할까요?", "생성", "나중에");
            if (createInv)
            {
                var invGo = new GameObject("ChestInventory");
                var inv   = invGo.AddComponent<InventoryManager>();

                // ownerType = Chest (enum index 2)
                var invSO = new SerializedObject(inv);
                invSO.FindProperty("ownerType").enumValueIndex = 2;
                invSO.FindProperty("maxSlots").intValue = 20;
                invSO.ApplyModifiedProperties();

                // 그리드 바인딩
                chestGridUI?.Bind(inv);
                Debug.Log("[ChestPanelBuilder] ChestInventory 오브젝트 생성됨");
            }
        }

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = chestPanel;

        EditorUtility.DisplayDialog("완료",
            "상자 패널 생성 완료!\n\n" +
            "✅ WarehousePanel 복제 → ChestPanel\n" +
            "✅ InventoryUIController 자동 연결\n\n" +
            "확인 사항:\n" +
            "1. ChestPanel의 타이틀 텍스트 확인\n" +
            "2. ChestInventory 오브젝트가 씬에 있는지 확인\n" +
            "3. Ctrl+S 저장", "확인");
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────

    static void ReplaceTextInChildren(GameObject root, string from, string to)
    {
        foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.text.Contains(from))
                tmp.text = tmp.text.Replace(from, to);
        }
        foreach (var txt in root.GetComponentsInChildren<Text>(true))
        {
            if (txt.text.Contains(from))
                txt.text = txt.text.Replace(from, to);
        }
    }

    static Button FindButtonByName(GameObject root, string nameHint)
    {
        foreach (var btn in root.GetComponentsInChildren<Button>(true))
        {
            if (btn.gameObject.name.Contains(nameHint) ||
                btn.gameObject.name.ToLower().Contains(nameHint.ToLower()))
                return btn;
        }
        return null;
    }
}

// SerializedProperty 확장 헬퍼
static class SerializedPropertyExtensions
{
    public static void SetValue(this SerializedProperty prop, Object value)
    {
        if (prop != null)
            prop.objectReferenceValue = value;
    }
}
