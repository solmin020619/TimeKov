// =====================================================================
// BaseUpgradeMaterialSlot.cs
// 기지 업그레이드 패널 하단의 재료 칸 1개.
// 선택한 업그레이드의 필요 재료 아이콘(부족하면 흐리게 미리보기) + "현재/필요" 표시.
// 호버 시 인벤/창고와 동일한 ItemTooltipUI 로 아이템 정보를 띄운다.
// =====================================================================

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BaseUpgradeMaterialSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI countText;

    private int _itemId = -1;
    private Canvas _canvas;

    private static readonly Color EnoughCol = new Color(0.86f, 0.92f, 0.98f, 1f);
    private static readonly Color ShortCol  = new Color(1f, 0.45f, 0.45f, 1f);

    /// <summary>재료 표시. have=보유(가방+창고), need=필요. 부족하면 아이콘을 흐리게(미리보기).</summary>
    public void Set(int itemId, int have, int need)
    {
        _itemId = itemId;
        gameObject.SetActive(true);

        var item = ItemDatabase.GetItem(itemId);
        var spr  = item != null ? ItemDatabase.GetIcon(item.iconKey) : null;
        bool enough = have >= need;

        if (icon != null)
        {
            icon.sprite  = spr;
            icon.enabled = spr != null;
            icon.color   = new Color(1f, 1f, 1f, enough ? 1f : 0.4f);   // 부족=흐리게 미리보기
        }
        if (countText != null)
        {
            countText.text  = $"{have}/{need}";
            countText.color = enough ? EnoughCol : ShortCol;
        }
    }

    /// <summary>빈 칸 (선택한 업그레이드가 이 칸만큼 재료를 안 쓸 때).</summary>
    public void Clear()
    {
        _itemId = -1;
        if (icon != null) icon.enabled = false;
        if (countText != null) countText.text = "";
        gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (_itemId <= 0) return;
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        ItemTooltipUI.Instance?.Show(_itemId, _canvas);
    }

    public void OnPointerExit(PointerEventData e) => ItemTooltipUI.Instance?.Hide();
}
