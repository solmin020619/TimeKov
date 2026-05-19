using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using TIMEKOV.Factory;

[RequireComponent(typeof(Image))]
public class RecipeDropSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image borderImage;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI labelText;

    public int RequiredItemId { get; private set; }
    public int RequiredAmount { get; private set; }
    public int CurrentAmount { get; private set; }

    private ProcessingMachine _machine;
    private InventoryManager _inventory;
    private Coroutine _glowRoutine;

    private void Awake()
    {
        GetComponent<Image>().raycastTarget = true;

        if (borderImage != null)
        {
            var le = borderImage.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = borderImage.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }
    }

    public void Setup(int itemId, int amount, ProcessingMachine machine, InventoryManager inventory = null)
    {
        RequiredItemId = itemId;
        RequiredAmount = amount;
        CurrentAmount = 0;
        _machine = machine;
        _inventory = inventory != null ? inventory : InventoryManager.Instance;

        if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }
        if (amountText != null) amountText.text = "";
        if (labelText != null) labelText.text = "";
        SetBorderAlpha(0f);

        PublicRefresh();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (!DraggableSlot.IsDragging) return;
        if (labelText != null) labelText.text = "재료 넣기";
        StartGlow();
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (labelText != null) labelText.text = "";
        StopGlow();
        SetBorderAlpha(0f);
    }

    public void OnDrop(PointerEventData e)
    {
        if (labelText != null) labelText.text = "";
        StopGlow();
        SetBorderAlpha(0f);

        var slot = e.pointerDrag?.GetComponent<DraggableSlot>();
        if (slot == null || !slot.HasItem) return;
        if (_machine == null) return;
        if (slot.ItemId != RequiredItemId) return;

        int have = _inventory != null ? _inventory.GetTotalItemCount(slot.ItemId) : slot.Amount;
        if (have <= 0) return;
        int take = Mathf.Min(slot.Amount, have);
        if (_inventory != null)
        {
            _inventory.TryConsumeItem(slot.ItemId, take);
            _inventory.ForceRefreshUI();
        }
        _machine.Receive(slot.ItemId, take);

        CurrentAmount += take;
        RefreshAmount();
    }

    private void StartGlow()
    {
        StopGlow();
        _glowRoutine = StartCoroutine(GlowRoutine());
    }

    private void StopGlow()
    {
        if (_glowRoutine != null)
        {
            StopCoroutine(_glowRoutine);
            _glowRoutine = null;
        }
    }

    private IEnumerator GlowRoutine()
    {
        while (true)
        {
            float t = 0f;
            while (t < 1f) { t += Time.deltaTime * 3f; SetBorderAlpha(Mathf.Lerp(0f, 1f, t)); yield return null; }
            t = 0f;
            while (t < 1f) { t += Time.deltaTime * 3f; SetBorderAlpha(Mathf.Lerp(1f, 0f, t)); yield return null; }
        }
    }

    private void SetBorderAlpha(float alpha)
    {
        if (borderImage == null) return;
        var c = borderImage.color;
        c.a = alpha;
        borderImage.color = c;
    }

    public void PublicRefresh()
    {
        if (_machine == null) return;

        int current = _machine.InputBuffer.GetAmount(RequiredItemId);
        if (current <= 0)
        {
            CurrentAmount = 0;
            if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }
            if (amountText != null) amountText.text = "";
        }
        else
        {
            CurrentAmount = current;
            RefreshAmount();
        }
    }

    private void RefreshAmount()
    {
        int current = _machine != null
            ? _machine.InputBuffer.GetAmount(RequiredItemId)
            : CurrentAmount;

        if (current > 0 && iconImage != null && iconImage.sprite == null)
        {
            var itemData = GameDataUtility.GetItem(RequiredItemId);
            Sprite sprite = null;
            if (itemData != null && !string.IsNullOrEmpty(itemData.iconKey))
                sprite = Resources.Load<Sprite>("Icon/" + itemData.iconKey);
            iconImage.sprite = sprite;
            iconImage.color = Color.white;
            iconImage.enabled = sprite != null;
        }

        if (amountText != null)
            amountText.text = $"{current}/{RequiredAmount}";
    }
}