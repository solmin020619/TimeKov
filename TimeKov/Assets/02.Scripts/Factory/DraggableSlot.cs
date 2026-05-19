using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class DraggableSlot : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI amountText;

    public int ItemId { get; private set; }
    public int Amount { get; private set; }
    public bool HasItem => ItemId > 0 && Amount > 0;

    private static GameObject _dragVisual;
    private Canvas _canvas;

    public static bool IsDragging = false;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
    }

    public Sprite GetIcon() => iconImage != null ? iconImage.sprite : null;

    public void SetItem(int itemId, int amount)
    {
        ItemId = itemId;
        Amount = amount;

        bool has = itemId > 0 && amount > 0;

        if (iconImage != null)
        {
            Sprite sprite = null;
            if (has)
            {
                var itemData = GameDataUtility.GetItem(itemId);
                if (itemData != null && !string.IsNullOrEmpty(itemData.iconKey))
                    sprite = Resources.Load<Sprite>("Icon/" + itemData.iconKey);
            }
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }

        if (amountText != null)
            amountText.text = has && amount > 1 ? $"x{amount}" : "";
    }

    public void Clear() => SetItem(0, 0);

    // ���� �巡�� ��������������������������������������������������������������������������������������������

    public void OnBeginDrag(PointerEventData e)
    {
        if (!HasItem) { e.pointerDrag = null; return; }
        IsDragging = true;

        // �巡�� �� ������ �ӽ� �̹��� ����
        _dragVisual = new GameObject("DragVisual");
        _dragVisual.transform.SetParent(_canvas.transform, false);
        _dragVisual.transform.SetAsLastSibling();

        var rt = _dragVisual.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(60, 60);

        var img = _dragVisual.AddComponent<Image>();
        img.sprite = iconImage.sprite;
        img.raycastTarget = false;

        var cg = _dragVisual.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData e)
    {
        if (_dragVisual == null) return;

        // Overlay ��İ� Camera ��� ��� ����
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            e.position,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
            out Vector2 localPos))
        {
            _dragVisual.transform.localPosition = localPos;
        }
    }

    public void OnEndDrag(PointerEventData e)
    {
        IsDragging = false;
        if (_dragVisual != null)
        {
            Destroy(_dragVisual);
            _dragVisual = null;
        }
    }
}