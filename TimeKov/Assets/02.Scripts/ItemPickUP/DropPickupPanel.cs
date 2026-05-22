using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
        // 다른 UI가 열려있으면 표시 차단
        if (GameUIController.Instance != null && GameUIController.Instance.IsUIBlocking()) return;

        ClearRows();

        for (int i = 0; i < items.Count; i++)
        {
            DropPickupRow row = Instantiate(rowPrefab, rowContainer);
            row.Set(items[i].itemId, items[i].count, GetTierColor(items[i].itemId));
            _rows.Add(row);
        }

        panelRoot.SetActive(true);

        // VerticalLayoutGroup이 다음 frame에 정렬하므로 첫 frame엔 row들이 겹쳐 보임. 즉시 재계산 강제.
        if (rowContainer is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    public void Hide()
    {
        ClearRows();
        panelRoot.SetActive(false);
    }

    // 박스의 화면 좌표로 패널을 옮긴다 (엔드필드식 — 박스 위에 떠다님)
    public void SetScreenPosition(Vector3 screenPos)
    {
        if (panelRoot != null)
            panelRoot.transform.position = new Vector3(screenPos.x, screenPos.y, 0f);
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
