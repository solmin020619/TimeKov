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
    //private PlayerWeaponController playerWeaponController; //수정

    void Awake()
    {
        EnsureDataStoreLoaded();

        var all = FindObjectsByType<InventoryManager>(FindObjectsSortMode.None);
        foreach (var m in all)
        {
            if (m.ownerType == InventoryManager.InventoryOwnerType.Player)
            {
                inven = m;
                break;
            }
        }

        //playerWeaponController = FindFirstObjectByType<PlayerWeaponController>(); //수정
    }

    void Start()
    {
        //SyncEquippedWeaponToPlayer(); //수정

        if (inven != null && equipBag != null)
        {
            inven.ApplyBagById(equipBag.slotIndex);
        }
    }

    private void EnsureDataStoreLoaded()
    {
        if (!DataStore.IsLoaded)
            DataStore.LoadAll();
    }

    public EquipSlotType? GetTypeById(int id)
    {
        if (id <= 0)
            return null;

        EnsureDataStoreLoaded();

        // 1) 무기 테이블에 있으면 무기
        if (DataStore.GetWeapon(id) != null)
            return EquipSlotType.Weapon;

        // 2) 장비 테이블 확인
        EquipmentRow equip = DataStore.GetEquipment(id);
        if (equip == null)
            return null;

        string equipType = (equip.equipType ?? string.Empty).Trim().ToLowerInvariant();

        switch (equipType)
        {
            case "helmet":
                return EquipSlotType.Helmet;

            case "armor":
            case "armour":
            case "vest":
                return EquipSlotType.Armor;

            case "bag":
            case "backpack":
                return EquipSlotType.Bag;
        }

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

    private void SyncEquippedWeaponToPlayer() //수정 무기 시스템 제거로 빈 메서드 (호출처 보존용)
    {
        //if (playerWeaponController == null) //수정
        //    playerWeaponController = FindFirstObjectByType<PlayerWeaponController>(); //수정
        //if (playerWeaponController == null) return; //수정
        //int equippedWeaponId = (equipWeapon != null) ? equipWeapon.slotIndex : 0; //수정
        //if (equippedWeaponId > 0) //수정
        //    playerWeaponController.EquipByItemId(equippedWeaponId); //수정
        //else //수정
        //    playerWeaponController.Unequip(); //수정
    }

    public void EquipOrSwapFromInventorySlot(SlotInfo invSlot)
    {
        if (invSlot == null || invSlot.slotIndex == 0) return;

        int newId = invSlot.slotIndex;
        var type = GetTypeById(newId);
        if (type == null) return;

        SlotInfo equipSlot = GetEquipSlot(type.Value);
        if (equipSlot == null) return;

        int oldId = equipSlot.slotIndex;

        equipSlot.SetSlot(newId, 1);

        if (oldId == 0)
        {
            invSlot.SetSlot(0, 0);
            invSlot.gameObject.SetActive(false);

            if (inven != null)
                inven.ForceRefreshUI();
        }
        else
        {
            invSlot.SetSlot(oldId, 1);
            if (!invSlot.gameObject.activeSelf)
                invSlot.gameObject.SetActive(true);
        }

        if (type.Value == EquipSlotType.Bag && inven != null)
        {
            inven.ApplyBagById(newId);
        }

        //if (type.Value == EquipSlotType.Weapon) //수정
        //{ //수정
        //    SyncEquippedWeaponToPlayer(); //수정
        //} //수정
    }

    public void UnequipToInventory(SlotInfo equipSlot)
    {
        if (equipSlot == null || equipSlot.slotIndex == 0) return;
        if (inven == null) return;

        int id = equipSlot.slotIndex;
        var type = GetTypeById(id);

        inven.AddItem(id, 1);
        equipSlot.SetSlot(0, 0);

        if (type != null && type.Value == EquipSlotType.Bag)
        {
            inven.ApplyBagById(0);
        }

        //if (type != null && type.Value == EquipSlotType.Weapon) //수정
        //{ //수정
        //    SyncEquippedWeaponToPlayer(); //수정
        //} //수정

        if (inven != null)
            inven.ForceRefreshUI();
    }

    public PlayerSessionData.EquipmentSnapshot ExportToSessionSnapshot()
    {
        var s = new PlayerSessionData.EquipmentSnapshot();
        s.weaponId = (equipWeapon != null) ? equipWeapon.slotIndex : 0;
        s.helmetId = (equipHelmet != null) ? equipHelmet.slotIndex : 0;
        s.armorId = (equipArmor != null) ? equipArmor.slotIndex : 0;
        s.bagId = (equipBag != null) ? equipBag.slotIndex : 0;
        return s;
    }

    public void ImportFromSessionSnapshot(PlayerSessionData.EquipmentSnapshot s)
    {
        if (s == null) return;

        if (equipWeapon != null) equipWeapon.SetSlot(s.weaponId, s.weaponId == 0 ? 0 : 1);
        if (equipHelmet != null) equipHelmet.SetSlot(s.helmetId, s.helmetId == 0 ? 0 : 1);
        if (equipArmor != null) equipArmor.SetSlot(s.armorId, s.armorId == 0 ? 0 : 1);
        if (equipBag != null) equipBag.SetSlot(s.bagId, s.bagId == 0 ? 0 : 1);

        if (inven != null)
            inven.ApplyBagById(s.bagId);

        //SyncEquippedWeaponToPlayer(); //수정
    }

    public int GetEquippedBagId()
    {
        return (equipBag != null) ? equipBag.slotIndex : 0;
    }
}