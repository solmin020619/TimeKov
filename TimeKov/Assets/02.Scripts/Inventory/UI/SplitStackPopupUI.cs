// SplitStackPopupUI.cs
// SplitStackPopup ������Ʈ�� ���̴� ��ũ��Ʈ
// ���� ���� ���� �Է� �˾�

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SplitStackPopupUI : MonoBehaviour
{
    [Header("�ؽ�Ʈ ����")]
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TMP_InputField amountInput;

    [Header("��ư ����")]
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

        RefreshButtonLabels();
        Loc.OnLanguageChanged += RefreshButtonLabels;
    }

    private void OnDestroy()
    {
        Loc.OnLanguageChanged -= RefreshButtonLabels;
    }

    private void RefreshButtonLabels()
    {
        SetBtnText(maxBtn, "최대");
        SetBtnText(confirmBtn, "확인");
        SetBtnText(cancelBtn, "취소");
    }

    private static void SetBtnText(Button btn, string key)
    {
        if (btn == null) return;
        var label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = Loc.Get(key);
    }

    // �˾� ����
    public void Open(InventorySlotUI slot)
    {
        if (slot == null || slot.IsEmpty) return;

        _targetSlot = slot;
        _maxAmount = slot.SlotData.amount - 1;
        _splitAmount = 1;

        var data = ItemDatabase.GetItem(slot.SlotData.itemId);

        if (itemNameText != null)
            itemNameText.text = data != null ? data.GetLocalizedName() :"�� �� ���� ������";

        if (_maxAmount <= 0)
        {
            Debug.LogWarning("[SplitStackPopupUI] ������ 1 ���϶� ���� �Ұ�");
            return;
        }

        UpdateDisplay();
        gameObject.SetActive(true);
    }

    // �˾� �ݱ�
    public void Close()
    {
        _targetSlot = null;
        gameObject.SetActive(false);
    }

    // ���� ǥ�� ����
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
            Debug.LogWarning("[SplitStackPopupUI] ���� ����: �κ��丮 ���� ��");

        Close();
    }
}