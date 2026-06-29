// =====================================================================
// WorldSelectRow.cs
// WorldSelectUI 목록 안에서 슬롯(월드) 한 줄을 표시하는 UI 컴포넌트.
// 행 클릭 = "선택"(하이라이트)만 함 — 실제 입장/삭제는 WorldSelectUI 하단의
// 공용 액션 버튼(입장하기/삭제/취소)이 현재 선택된 슬롯을 대상으로 처리한다.
// =====================================================================

using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldSelectRow : MonoBehaviour
{
    [SerializeField] Image background;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI dateText;
    [SerializeField] TextMeshProUGUI levelText;
    [SerializeField] Button selectButton;

    static readonly Color NormalColor   = new Color32(0x0A, 0x10, 0x18, 0xEB);
    static readonly Color SelectedColor = new Color32(0x1E, 0x46, 0x60, 0xF5);

    public string SlotId { get; private set; }

    public void Set(SaveSlotMeta meta, Action onSelect)
    {
        SlotId = meta.slotId;
        if (nameText != null) nameText.text = meta.worldName;
        if (dateText != null) dateText.text = FormatDate(meta.lastPlayedIso);
        if (levelText != null) levelText.text = $"Lv.{meta.coreLevelSnapshot}";

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelect?.Invoke());
        }
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (background != null) background.color = selected ? SelectedColor : NormalColor;
    }

    static string FormatDate(string iso)
    {
        if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
        return "-";
    }
}
