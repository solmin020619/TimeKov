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
    [SerializeField] private TextMeshProUGUI messageText;   // 메시지 텍스트
    [SerializeField] private Button confirmBtn;    // 확인 버튼
    [SerializeField] private Button cancelBtn;     // 취소 버튼

    // 확인 버튼 눌렀을 때 실행할 콜백
    private Action _onConfirm;

    // 현재 열려있는지 여부
    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    private void Start()
    {
        if (confirmBtn != null) confirmBtn.onClick.AddListener(OnClickConfirm);
        if (cancelBtn != null) cancelBtn.onClick.AddListener(Close);
    }

    // 팝업 열기
    // message: 표시할 메시지
    // onConfirm: 확인 버튼 눌렀을 때 실행할 콜백
    public void Open(string message, Action onConfirm)
    {
        if (messageText != null)
            messageText.text = message;

        _onConfirm = onConfirm;
        gameObject.SetActive(true);
    }

    // 팝업 닫기
    public void Close()
    {
        _onConfirm = null;
        gameObject.SetActive(false);
    }

    // 확인 버튼 핸들러
    private void OnClickConfirm()
    {
        _onConfirm?.Invoke();
        Close();
    }
}