// ConfirmPopupUI.cs
// ConfirmPopup 오브젝트에 붙이는 스크립트
// 버리기 확인 팝업 (MessageText / ConfirmBtn / CancelBtn)
// Open(message, onConfirm) 으로 범용 사용 가능

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ConfirmPopupUI : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmBtn;
    [SerializeField] private Button cancelBtn;

    private Action _onConfirm;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    private void Start()
    {
        if (confirmBtn != null) confirmBtn.onClick.AddListener(OnClickConfirm);
        if (cancelBtn != null) cancelBtn.onClick.AddListener(Close);

        RefreshButtonLabels();
        Loc.OnLanguageChanged += RefreshButtonLabels;
    }

    private void OnDestroy()
    {
        Loc.OnLanguageChanged -= RefreshButtonLabels;
    }

    private void RefreshButtonLabels()
    {
        SetBtnText(confirmBtn, "확인");
        SetBtnText(cancelBtn, "취소");
    }

    private static void SetBtnText(Button btn, string key)
    {
        if (btn == null) return;
        var label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = Loc.Get(key);
    }

    public void Open(string message, Action onConfirm)
    {
        if (messageText != null)
            messageText.text = message;

        _onConfirm = onConfirm;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        _onConfirm = null;
        gameObject.SetActive(false);
    }

    private void OnClickConfirm()
    {
        _onConfirm?.Invoke();
        Close();
    }
}
