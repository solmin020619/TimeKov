using System.Collections.Generic;
using UnityEngine;

public class DropPickupPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform rowContainer;
    [SerializeField] private DropPickupRow rowPrefab;

    [Tooltip("아이템 등급(rarityTier)별 색. 배열 인덱스 = 등급 번호.")]
    [SerializeField] private Color[] tierColors;

    private readonly List<DropPickupRow> _rows = new List<DropPickupRow>();

    void Awake()
    {
        Hide();
    }

    public void Show(IReadOnlyList<(int itemId, int count)> items)
    {
        ClearRows();

        for (int i = 0; i < items.Count; i++)
        {
            DropPickupRow row = Instantiate(rowPrefab, rowContainer);
            row.Set(items[i].itemId, items[i].count, GetTierColor(items[i].itemId));
            _rows.Add(row);
        }

        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        ClearRows();
        panelRoot.SetActive(false);
    }

    private Color GetTierColor(int itemId)
    {
        ItemDataSheetData item = GameDataUtility.GetItem(itemId);
        int tier = item != null ? (int)item.itemGrade : 0;
        if (tierColors != null && tier >= 0 && tier < tierColors.Length)
            return tierColors[tier];
        return Color.white;
    }

    private void ClearRows()
    {
        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i] != null) Destroy(_rows[i].gameObject);
        _rows.Clear();
    }
}
