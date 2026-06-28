// =====================================================================
// WorldSelectRow.cs
// WorldSelectUI 목록 안에서 슬롯(월드) 한 줄을 표시하는 UI 컴포넌트.
// ChestItemRow와 동일한 "Set(...) + 프리팹 인스턴스" 패턴.
// =====================================================================

using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldSelectRow : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI infoText;
    [SerializeField] Button selectButton;
    [SerializeField] Button deleteButton;

    public void Set(SaveSlotMeta meta, Action onSelect, Action onDelete)
    {
        if (nameText != null) nameText.text = meta.worldName;
        if (infoText != null) infoText.text = $"강화 Lv.{meta.coreLevelSnapshot}    마지막 플레이 {FormatDate(meta.lastPlayedIso)}";

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelect?.Invoke());
        }
        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => onDelete?.Invoke());
        }
    }

    static string FormatDate(string iso)
    {
        if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        return "-";
    }
}
