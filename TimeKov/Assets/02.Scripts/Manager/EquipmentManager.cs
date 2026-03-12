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

    // ================================
    // [추가] 플레이어 무기 컨트롤러 참조
    // 장비창 무기 슬롯 상태와 실제 플레이어 무기/애니메이션 상태를 동기화하기 위함
    // ================================
    private PlayerWeaponController playerWeaponController;

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

        // ================================
        // [추가] PlayerWeaponController 찾기
        // ================================
        playerWeaponController = FindFirstObjectByType<PlayerWeaponController>();
    }

    // ================================
    // [추가] 시작 시 장비창 무기 슬롯 기준으로 실제 무기 상태 동기화
    // - 무기 슬롯 비어있으면 nogun
    // - 무기 슬롯에 무기 있으면 해당 itemId 장착
    // ================================
    void Start()
    {
        SyncEquippedWeaponToPlayer();

        // [추가] 시작 시 가방이 이미 껴져있는 상태면 인벤 칸수도 반영
        if (inven != null && equipBag != null)
        {
            inven.ApplyBagById(equipBag.slotIndex);
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

    // ================================
    // [추가] 장비창 무기 슬롯 -> 실제 플레이어 무기 상태 동기화 함수
    // PlayerAnimationController는 PlayerWeaponController.GetEquippedItemId()를 보고
    // 애니메이션을 바꾸므로 여기만 정확히 맞춰주면 됨
    // ================================
    private void SyncEquippedWeaponToPlayer()
    {
        if (playerWeaponController == null)
            playerWeaponController = FindFirstObjectByType<PlayerWeaponController>();

        if (playerWeaponController == null)
            return;

        int equippedWeaponId = (equipWeapon != null) ? equipWeapon.slotIndex : 0;

        if (equippedWeaponId > 0)
        {
            playerWeaponController.EquipByItemId(equippedWeaponId);
        }
        else
        {
            playerWeaponController.Unequip();
        }
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
        if (oldId == 0)
        {
            invSlot.SetSlot(0, 0);

            // ✅ 배그식(B): 빈 슬롯은 화면에서 바로 사라지게
            invSlot.gameObject.SetActive(false);

            // ⭐ 인벤 UI 갱신
            if (inven != null)
            {
                inven.ForceRefreshUI();
            }
        }
        else
        {
            invSlot.SetSlot(oldId, 1);
            if (!invSlot.gameObject.activeSelf) invSlot.gameObject.SetActive(true);
        }

        // ✅ [추가] 가방 장착/교체면 인벤 칸수 갱신
        if (type.Value == EquipSlotType.Bag && inven != null)
        {
            inven.ApplyBagById(newId);
        }

        // ================================
        // [추가] 무기 장착/교체면 실제 플레이어 무기 상태도 같이 갱신
        // ================================
        if (type.Value == EquipSlotType.Weapon)
        {
            SyncEquippedWeaponToPlayer();
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

        // ================================
        // [추가] 무기 해제면 실제 플레이어 무기 상태도 nogun으로 갱신
        // ================================
        if (type != null && type.Value == EquipSlotType.Weapon)
        {
            SyncEquippedWeaponToPlayer();
        }

        if (inven != null)
        {
            inven.ForceRefreshUI();
        }
    }

    // =========================================================
    // ✅ Session Export / Import (씬 이동 데이터 유지)
    // ❌ 기존 기능 삭제/변경 없이 "추가"만
    // =========================================================

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

        // 장비 슬롯은 "장착 여부"만 의미하므로 count는 1로 통일
        if (equipWeapon != null) equipWeapon.SetSlot(s.weaponId, s.weaponId == 0 ? 0 : 1);
        if (equipHelmet != null) equipHelmet.SetSlot(s.helmetId, s.helmetId == 0 ? 0 : 1);
        if (equipArmor != null) equipArmor.SetSlot(s.armorId, s.armorId == 0 ? 0 : 1);
        if (equipBag != null) equipBag.SetSlot(s.bagId, s.bagId == 0 ? 0 : 1);

        // ================================
        // [추가] Import 후에도 가방/무기 상태 동기화
        // ================================
        if (inven != null)
        {
            inven.ApplyBagById(s.bagId);
        }

        SyncEquippedWeaponToPlayer();
    }

    public int GetEquippedBagId()
    {
        return (equipBag != null) ? equipBag.slotIndex : 0;
    }
}