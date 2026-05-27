using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TIMEKOV.Factory;

/// <summary>
/// 설비 선택 패널의 행 하나. DropPickupRow와 동일한 구조를 사용한다.
/// </summary>
public class FacilitySelectRow : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image background;

    [Header("선택 상태 색상")]
    [SerializeField] private Color normalColor   = new Color(0.08f, 0.08f, 0.10f, 0.75f);
    [SerializeField] private Color selectedColor = new Color(0.30f, 0.30f, 0.35f, 0.95f);

    public ProcessingMachine Machine   { get; private set; }
    public string             MachineName => nameText != null ? nameText.text : "";

    public void Set(ProcessingMachine machine, string displayName)
    {
        Machine = machine;
        if (nameText != null) nameText.text = displayName;
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (background != null)
            background.color = selected ? selectedColor : normalColor;
    }
}
