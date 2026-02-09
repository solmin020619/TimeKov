using UnityEngine;

public enum EquipSlotType { Weapon, Helmet, Armor, Bag }

public class EquipmentManager : MonoBehaviour
{
    [Header("장비 4칸 (각 칸에 SlotInfo 붙어있어야 함)")]
    public SlotInfo equipWeapon;
    public SlotInfo equipHelmet;
    public SlotInfo equipArmor;
    public SlotInfo equipBag;

    private InventoryManager inven;

    void Awake()
    {
        // inven = FindAnyObjectByType<InventoryManager>();  // ❌ 이거 때문에 창고 잡힘

        var all = FindObjectsByType<InventoryManager>(FindObjectsSortMode.None);
        foreach (var m in all)
        {
            if (m.ownerType == InventoryManager.InventoryOwnerType.Player)
            {
                inven = m;
                break;
            }
        }
    }

    public EquipSlotType? GetTypeById(int id)
    {
        if (id >= 1000 && id < 2000) return EquipSlotType.Weapon;
        if (id >= 2000 && id < 3000) return EquipSlotType.Helmet;
        if (id >= 3000 && id < 4000) return EquipSlotType.Armor;
        if (id >= 4000 && id < 5000) return EquipSlotType.Bag;
        return null;
    }

    public SlotInfo GetEquipSlot(EquipSlotType t)
    {
        return t switch
        {
            EquipSlotType.Weapon => equipWeapon,
            EquipSlotType.Helmet => equipHelmet,
            EquipSlotType.Armor => equipArmor,
            EquipSlotType.Bag => equipBag,
            _ => null
        };
    }

    public bool IsEquipSlot(SlotInfo s)
    {
        return s == equipWeapon || s == equipHelmet || s == equipArmor || s == equipBag;
    }

    // 인벤 슬롯 -> 장비칸 (비어있으면 장착, 차있으면 스왑)
    public void EquipOrSwapFromInventorySlot(SlotInfo invSlot)
    {
        if (invSlot == null || invSlot.slotIndex == 0) return;

        int newId = invSlot.slotIndex;
        var type = GetTypeById(newId);
        if (type == null) return;

        SlotInfo equipSlot = GetEquipSlot(type.Value);
        if (equipSlot == null) return;

        int oldId = equipSlot.slotIndex;

        // 장비칸에 새 아이템 장착
        equipSlot.SetSlot(newId, 1);

        // 인벤 슬롯은 장비템이니까 "교환"만 (스택 개념 없음)
        if (oldId == 0) invSlot.SetSlot(0, 0);
        else invSlot.SetSlot(oldId, 1);

        // ✅ [추가] 가방 장착/교체면 인벤 칸수 갱신
        if (type.Value == EquipSlotType.Bag && inven != null)
        {
            inven.ApplyBagById(newId);
        }
    }

    // 장비칸 더블클릭 -> 인벤으로 해제
    public void UnequipToInventory(SlotInfo equipSlot)
    {
        if (equipSlot == null || equipSlot.slotIndex == 0) return;
        if (inven == null) return;

        int id = equipSlot.slotIndex;
        var type = GetTypeById(id);

        inven.AddItem(id, 1);
        equipSlot.SetSlot(0, 0);

        // ✅ [추가] 가방 해제면 인벤 칸수 기본으로
        if (type != null && type.Value == EquipSlotType.Bag)
        {
            inven.ApplyBagById(0);
        }
    }
}