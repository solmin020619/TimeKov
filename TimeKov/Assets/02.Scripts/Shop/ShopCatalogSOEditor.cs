#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShopCatalogSO))]
public class ShopCatalogSOEditor : Editor
{
    // 입력값(에디터에서만 사용)
    private ItemDataBase itemDatabase;

    private int fromId = 7101;
    private int toId = 7125;

    private int defaultStock = -1;           // -1 = 무한
    private float buyPriceMultiplier = 2f;   // buyPrice = saleTime * multiplier

    private bool clearBeforeAdd = false;
    private bool preventDuplicates = true;

    public override void OnInspectorGUI()
    {
        // 기본 인스펙터(Entries 리스트 포함)
        DrawDefaultInspector();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Range Add Tools", EditorStyles.boldLabel);

        // ItemDataBase 연결(없으면 자동 찾기 버튼 제공)
        itemDatabase = (ItemDataBase)EditorGUILayout.ObjectField(
            "ItemDataBase", itemDatabase, typeof(ItemDataBase), false);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto Find ItemDataBase"))
            {
                itemDatabase = FindFirstItemDataBaseAsset();
                if (itemDatabase == null)
                    Debug.LogWarning("ItemDataBase 에셋을 못 찾았음. Project에 ItemDataBase가 있는지 확인해.");
            }

            if (GUILayout.Button("Ping ItemDataBase") && itemDatabase != null)
            {
                EditorGUIUtility.PingObject(itemDatabase);
                Selection.activeObject = itemDatabase;
            }
        }

        EditorGUILayout.Space(6);

        fromId = EditorGUILayout.IntField("From ID", fromId);
        toId = EditorGUILayout.IntField("To ID", toId);

        defaultStock = EditorGUILayout.IntField("Default Stock (-1=Infinite)", defaultStock);
        buyPriceMultiplier = EditorGUILayout.FloatField("BuyPrice Multiplier", buyPriceMultiplier);

        clearBeforeAdd = EditorGUILayout.ToggleLeft("Clear Entries Before Add", clearBeforeAdd);
        preventDuplicates = EditorGUILayout.ToggleLeft("Prevent Duplicates", preventDuplicates);

        EditorGUILayout.Space(6);

        using (new EditorGUI.DisabledScope(itemDatabase == null))
        {
            if (GUILayout.Button("Add Range From ItemDataBase (saleTime 기반)"))
            {
                AddRange();
            }
        }

        if (itemDatabase == null)
        {
            EditorGUILayout.HelpBox(
                "ItemDataBase가 연결되어야 범위 추가가 가능함.\n" +
                "위 ObjectField에 드래그하거나 'Auto Find ItemDataBase'를 눌러.",
                MessageType.Info);
        }
    }

    private void AddRange()
    {
        ShopCatalogSO catalog = (ShopCatalogSO)target;

        if (itemDatabase == null)
        {
            Debug.LogWarning("ItemDataBase가 null이라 범위 추가 불가");
            return;
        }

        int a = Mathf.Min(fromId, toId);
        int b = Mathf.Max(fromId, toId);

        if (clearBeforeAdd)
            catalog.entries.Clear();

        // 중복 방지용 set
        HashSet<int> existing = new HashSet<int>();
        if (preventDuplicates)
        {
            for (int i = 0; i < catalog.entries.Count; i++)
                existing.Add(catalog.entries[i].itemId);
        }

        int added = 0;
        int skippedMissing = 0;
        int skippedDuplicate = 0;

        for (int id = a; id <= b; id++)
        {
            if (preventDuplicates && existing.Contains(id))
            {
                skippedDuplicate++;
                continue;
            }

            ItemInfo item = itemDatabase.GetItemById(id);
            if (item == null)
            {
                skippedMissing++;
                continue;
            }

            int buyPrice = Mathf.RoundToInt(item.saleTime * buyPriceMultiplier);
            if (buyPrice < 0) buyPrice = 0;

            ShopEntry entry = new ShopEntry();
            entry.itemId = id;
            entry.buyPrice = buyPrice;
            entry.stock = defaultStock;

            catalog.entries.Add(entry);

            if (preventDuplicates)
                existing.Add(id);

            added++;
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        Debug.Log($"[ShopCatalogSO] Range Add 완료: Added={added}, MissingID={skippedMissing}, DuplicateSkipped={skippedDuplicate}");
    }

    private ItemDataBase FindFirstItemDataBaseAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemDataBase");
        if (guids == null || guids.Length == 0) return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<ItemDataBase>(path);
    }
}
#endif
