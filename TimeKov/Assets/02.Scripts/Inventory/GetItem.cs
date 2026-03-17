using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GetItem : MonoBehaviour
{
    public GameObject invenMana;
    public TextMeshProUGUI getItemText;
    public Image ItemIcon;

    // 드랍 슬롯 수량 표시 텍스트
    public TextMeshProUGUI amountText;

    public int insertID;
    public int insertItemCount;

    // 슬롯 기본 배경(root) 저장
    private Sprite defaultSlotSprite;

    private InventoryManager cachedInventoryManager;
    private LootContainer cachedLootContainer;

    private static readonly Dictionary<int, Sprite> _iconCache = new Dictionary<int, Sprite>();

    private void Awake()
    {
        // 시작 전 프리팹/씬에서 세팅된 슬롯 배경을 저장해둠
        if (ItemIcon != null)
            defaultSlotSprite = ItemIcon.sprite;

        CacheRefs();
    }

    private void Start()
    {
        RefreshUI();
    }

    private void CacheRefs()
    {
        if (cachedInventoryManager == null && invenMana != null)
            cachedInventoryManager = invenMana.GetComponent<InventoryManager>();

        if (cachedLootContainer == null)
            cachedLootContainer = GetComponentInParent<LootContainer>();
    }

    public void SetData(GameObject inventoryManagerGO, int id, int count)
    {
        invenMana = inventoryManagerGO;
        cachedInventoryManager = null;

        insertID = id;
        insertItemCount = count;

        CacheRefs();
        RefreshUI();
    }

    private void RefreshAmountUI()
    {
        if (amountText == null) return;

        if (insertID != 0 && insertItemCount > 1)
        {
            if (!amountText.gameObject.activeSelf)
                amountText.gameObject.SetActive(true);

            amountText.text = insertItemCount.ToString();
        }
        else
        {
            amountText.text = "";

            if (amountText.gameObject.activeSelf)
                amountText.gameObject.SetActive(false);
        }
    }

    private void ApplyEmptyState()
    {
        getItemText.text = "";
        ItemIcon.sprite = defaultSlotSprite;
        ItemIcon.enabled = (defaultSlotSprite != null);
        RefreshAmountUI();
    }

    private void ApplyFallbackState(string textValue)
    {
        getItemText.text = textValue;
        ItemIcon.sprite = defaultSlotSprite;
        ItemIcon.enabled = (defaultSlotSprite != null);
        RefreshAmountUI();
    }

    private void ApplyFilledState()
    {
        var item = DataManager.Instance.GetItem(insertID);
        if (item == null)
        {
            Debug.LogWarning($"[GetItem] Item not found (id={insertID})");
            ApplyFallbackState(insertID.ToString());
            return;
        }

        getItemText.text = item.itemName;

        Sprite spr = GetCachedIcon(insertID);
        if (spr != null)
        {
            ItemIcon.sprite = spr;
            ItemIcon.enabled = true;
        }
        else
        {
            ItemIcon.sprite = defaultSlotSprite;
            ItemIcon.enabled = (defaultSlotSprite != null);
            Debug.LogWarning($"[GetItem] Missing icon sprite: Resources/Icon/{insertID}");
        }

        RefreshAmountUI();
    }

    private Sprite GetCachedIcon(int id)
    {
        if (id <= 0)
            return null;

        if (_iconCache.TryGetValue(id, out Sprite cached))
            return cached;

        Sprite loaded = Resources.Load<Sprite>("Icon/" + id);
        _iconCache[id] = loaded;
        return loaded;
    }

    public void RefreshUI()
    {
        if (getItemText == null || ItemIcon == null)
        {
            Debug.LogWarning($"[GetItem] UI ref missing on {gameObject.name}");
            return;
        }

        if (insertID == 0)
        {
            ApplyEmptyState();
            return;
        }

        if (DataManager.Instance == null)
        {
            Debug.LogWarning($"[GetItem] DataManager.Instance is null (insertID={insertID})");
            ApplyFallbackState(insertID.ToString());
            return;
        }

        ApplyFilledState();
    }

    public void ItemClick()
    {
        if (insertID == 0)
            return;

        CacheRefs();

        if (invenMana == null)
        {
            Debug.LogWarning($"[GetItem] invenMana is null on {gameObject.name}");
            return;
        }

        if (cachedInventoryManager == null)
        {
            Debug.LogWarning($"[GetItem] InventoryManager not found on {invenMana.name}");
            return;
        }

        // 핵심: 인벤에 "넣은 만큼만" 드랍에서 줄이기 (인벤 꽉차면 증발 방지)
        int remaining = cachedInventoryManager.TryAddItemFromLoot(insertID, insertItemCount);
        int added = insertItemCount - remaining;

        if (added <= 0)
        {
            // 하나도 못 넣었으면: 드랍 슬롯 변화 없음
            Debug.Log("[GetItem] 인벤이 가득 차서 루팅 실패");
            return;
        }

        if (remaining > 0)
        {
            // 부분만 들어갔으면: 드랍 슬롯 수량만 감소 (아이템 유지)
            insertItemCount = remaining;
            RefreshUI();
            NotifyLootChanged();
            return;
        }

        // 전부 들어갔으면: 슬롯 비우기
        insertID = 0;
        insertItemCount = 0;
        RefreshUI();
        NotifyLootChanged();
    }

    private void NotifyLootChanged()
    {
        CacheRefs();
        cachedLootContainer?.NotifyLootChanged();
    }
}