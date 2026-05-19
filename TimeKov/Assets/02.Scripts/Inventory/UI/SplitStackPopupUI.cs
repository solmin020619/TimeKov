// SplitStackPopupUI.cs
// SplitStackPopup 오브젝트에 붙이는 스크립트
// 스택 분할 수량 입력 팝업

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SplitStackPopupUI : MonoBehaviour
{
    [Header("텍스트 참조")]
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TMP_InputField amountInput;

    [Header("버튼 참조")]
    [SerializeField] private Button minusBtn;
    [SerializeField] private Button plusBtn;
    [SerializeField] private Button maxBtn;
    [SerializeField] private Button confirmBtn;
    [SerializeField] private Button cancelBtn;

    private InventorySlotUI _targetSlot;
    private int _splitAmount;
    private int _maxAmount;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    private void Start()
    {
        if (minusBtn != null) minusBtn.onClick.AddListener(OnClickMinus);
        if (plusBtn != null) plusBtn.onClick.AddListener(OnClickPlus);
        if (maxBtn != null) maxBtn.onClick.AddListener(OnClickMax);
        if (confirmBtn != null) confirmBtn.onClick.AddListener(OnClickConfirm);
        if (cancelBtn != null) cancelBtn.onClick.AddListener(Close);

        if (amountInput != null)
            amountInput.onEndEdit.AddListener(OnInputEndEdit);
    }

    // 팝업 열기
    public void Open(InventorySlotUI slot)
    {
        if (slot == null || slot.IsEmpty) return;

        _targetSlot = slot;
        _maxAmount = slot.SlotData.amount - 1;
        _splitAmount = 1;

        var data = ItemDatabase.GetItem(slot.SlotData.itemId);

        if (itemNameText != null)
            itemNameText.text = data != null ? data.itemName : "알 수 없는 아이템";

        if (_maxAmount <= 0)
        {
            Debug.LogWarning("[SplitStackPopupUI] 수량이 1 이하라 분할 불가");
            return;
        }

        UpdateDisplay();
        gameObject.SetActive(true);
    }

    // 팝업 닫기
    public void Close()
    {
        _targetSlot = null;
        gameObject.SetActive(false);
    }

    // 수량 표시 갱신
    private void UpdateDisplay()
    {
        if (amountInput != null)
            amountInput.text = _splitAmount.ToString();

        if (minusBtn != null) minusBtn.interactable = _splitAmount > 1;
        if (plusBtn != null) plusBtn.interactable = _splitAmount < _maxAmount;
        if (confirmBtn != null) confirmBtn.interactable = _splitAmount >= 1;
    }

    private void OnClickMinus()
    {
        _splitAmount = Mathf.Max(1, _splitAmount - 1);
        UpdateDisplay();
    }

    private void OnClickPlus()
    {
        _splitAmount = Mathf.Min(_maxAmount, _splitAmount + 1);
        UpdateDisplay();
    }

    private void OnClickMax()
    {
        _splitAmount = _maxAmount;
        UpdateDisplay();
    }

    private void OnInputEndEdit(string text)
    {
        _splitAmount = int.TryParse(text, out int v)
            ? Mathf.Clamp(v, 1, _maxAmount)
            : 1;
        UpdateDisplay();
    }

    private void OnClickConfirm()
    {
        if (_targetSlot == null) return;

        var owner = _targetSlot.Owner;
        var slot = _targetSlot.SlotData;
        if (owner == null) return;

        bool success = owner.SplitStack(slot.slotIndex, _splitAmount, owner);
        if (!success)
            Debug.LogWarning("[SplitStackPopupUI] 분할 실패: 인벤토리 가득 참");

        Close();
    }
}