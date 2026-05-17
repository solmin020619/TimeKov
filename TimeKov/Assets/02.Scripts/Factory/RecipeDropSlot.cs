using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using TIMEKOV.Factory;

[RequireComponent(typeof(Image))]
public class RecipeDropSlot : MonoBehaviour, IDropHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image borderHighlight;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI requiredText;

    public int RequiredItemId { get; private set; }
    public int RequiredAmount { get; private set; }
    public int CurrentAmount { get; private set; }

    private ProcessingMachine _machine;
    private InventoryManager _inventory;

    private void Awake()
    {
        GetComponent<Image>().raycastTarget = true;
    }

    public void Setup(int itemId, int amount,
                      ProcessingMachine machine, InventoryManager inventory)
    {
        RequiredItemId = itemId;
        RequiredAmount = amount;
        CurrentAmount = 0;
        _machine = machine;
        _inventory = inventory;

        // 1. 처음엔 무조건 비워둠
        ClearVisuals();

        // 2. 만약 설비 안에 이미 재료가 들어있다면 화면에 띄움
        PublicRefresh();
    }

    public void OnDrop(PointerEventData e)
    {
        var slot = e.pointerDrag?.GetComponent<DraggableSlot>();
        if (slot == null || !slot.HasItem) return;
        if (_machine == null || _inventory == null) return;

        // 레시피에 맞지 않는 아이템 드롭 방지
        if (slot.ItemId != RequiredItemId) return;

        int amount = _inventory.GetTotalItemCount(slot.ItemId);
        if (amount <= 0) return;

        int take = Mathf.Min(slot.Amount, amount);
        _inventory.TryConsumeItem(slot.ItemId, take);
        _inventory.ForceRefreshUI();
        _machine.Receive(slot.ItemId, take);

        CurrentAmount += take;
        RefreshAmount();

        int remaining = slot.Amount - take;
        if (remaining > 0) slot.SetItem(slot.ItemId, remaining);
        else slot.Clear();
    }

    public void PublicRefresh()
    {
        if (_machine == null) return;

        int current = _machine.InputBuffer.GetAmount(RequiredItemId);

        if (current <= 0)
        {
            // 버퍼 소진 시 슬롯 완전히 비우기
            CurrentAmount = 0;
            ClearVisuals();
        }
        else
        {
            CurrentAmount = current;
            RefreshAmount();
        }
    }

    private void ClearVisuals()
    {
        if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }
        if (requiredText != null) requiredText.text = "";
        if (amountText != null) amountText.text = "";
        if (borderHighlight != null) borderHighlight.color = new Color(1f, 1f, 1f, 0.4f);
    }

    private void RefreshAmount()
    {
        int current = _machine != null
            ? _machine.InputBuffer.GetAmount(RequiredItemId)
            : CurrentAmount;

        if (current <= 0)
        {
            ClearVisuals();
            return;
        }

        // 아이템이 1개라도 들어있으면 이미지와 텍스트를 로드해서 띄움
        if (iconImage != null)
        {
            var sprite = Resources.Load<Sprite>("Icon/" + RequiredItemId);
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
            iconImage.color = Color.white;
        }

        if (requiredText != null)
        {
            var row = DataStore.GetItem(RequiredItemId);
            requiredText.text = row?.itemName ?? RequiredItemId.ToString();
        }

        if (amountText != null)
            amountText.text = $"{current}/{RequiredAmount}";

        if (borderHighlight != null)
            borderHighlight.color = current >= RequiredAmount
                ? new Color(0.3f, 1f, 0.3f, 1f)
                : new Color(1f, 1f, 1f, 0.4f);
    }
}