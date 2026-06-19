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

    [Header("선택 상태 크기")]
    [Tooltip("선택된 행을 살짝 키워 선택 중임을 강조하는 배율.")]
    [SerializeField] private float selectedScale = 1.12f;

    public MachineBase Machine     { get; private set; }
    public string      MachineName => nameText != null ? nameText.text : "";

    // 프리팹 원본 스케일(예: 2배). 선택 강조 배율을 이 위에 곱해야
    // 행이 작아지거나 정렬이 틀어지지 않는다.
    private Vector3 _baseScale;
    private bool    _baseScaleCaptured;

    private void Awake() => CaptureBaseScale();

    private void CaptureBaseScale()
    {
        if (_baseScaleCaptured) return;
        _baseScale = transform.localScale;
        _baseScaleCaptured = true;
    }

    public void Set(MachineBase machine, string displayName)
    {
        Machine = machine;
        if (nameText != null) nameText.text = displayName;
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        CaptureBaseScale(); // Set()이 Awake보다 먼저 불릴 경우 대비

        if (background != null)
            background.color = selected ? selectedColor : normalColor;

        // 원본 스케일을 유지한 채 선택된 행만 살짝 키운다 (정렬은 그대로, 크기만 강조).
        transform.localScale = selected ? _baseScale * selectedScale : _baseScale;
    }

    /// <summary>
    /// 선택 확정(F) 시 깜빡임용 — 크기는 선택 상태(큰 크기) 그대로 두고 색만 토글한다.
    /// SetSelected를 토글하면 크기까지 깜빡이므로 색만 바꾼다.
    /// </summary>
    public void SetFlashHighlight(bool on)
    {
        if (background != null)
            background.color = on ? selectedColor : normalColor;
    }
}
