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
        if (ItemIcon != null)
            defaultSlotSprite = ItemIcon.sprite;

        EnsureDataStoreLoaded();
        CacheRefs();
    }

    private void Start()
    {
        RefreshUI();
    }

    private void EnsureDataStoreLoaded()
    {
        if (!DataStore.IsLoaded)
            DataStore.LoadAll();
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
        ItemRow item = DataStore.GetItem(insertID);
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

        EnsureDataStoreLoaded();
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

        // 인벤에 실제로 들어간 만큼만 드랍에서 감소
        int remaining = cachedInventoryManager.TryAddItemFromLoot(insertID, insertItemCount);
        int added = insertItemCount - remaining;

        if (added <= 0)
        {
            Debug.Log("[GetItem] 인벤이 가득 차서 루팅 실패");
            return;
        }

        if (remaining > 0)
        {
            insertItemCount = remaining;
            RefreshUI();
            NotifyLootChanged();
            return;
        }

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