using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSessionData : MonoBehaviour
{
    public static PlayerSessionData Instance { get; private set; }

    [Serializable]
    public class InventorySnapshot
    {
        public int ownerType;
        public int slotCount;
        public List<int> ids = new List<int>();
        public List<int> counts = new List<int>();
    }

    [Serializable]
    public class EquipmentSnapshot
    {
        public int weaponId;
        public int helmetId;
        public int armorId;
        public int bagId;
    }

    [Serializable]
    public class WeaponSnapshot
    {
        public int equippedItemId;
        public int currentAmmoInMag;
    }

    [Header("Saved Data")]
    public InventorySnapshot playerInventory;
    public InventorySnapshot warehouseInventory;
    public EquipmentSnapshot equipment;
    public WeaponSnapshot weapon;

    [Header("State")]
    public bool hasSnapshot = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!hasSnapshot) return;
        TryRestoreInScene();
    }

    // =========================
    // 캡처(Export) - 씬 전환 직전 호출
    // =========================
    public void CaptureCurrent()
    {
        var allInv = UnityEngine.Object.FindObjectsByType<InventoryManager>(FindObjectsSortMode.None);

        InventoryManager playerInv = null;
        InventoryManager warehouseInv = null;

        foreach (var inv in allInv)
        {
            if (inv == null) continue;
            if (inv.ownerType == InventoryManager.InventoryOwnerType.Player) playerInv = inv;
            else if (inv.ownerType == InventoryManager.InventoryOwnerType.Warehouse) warehouseInv = inv;
        }

        var equip = UnityEngine.Object.FindAnyObjectByType<EquipmentManager>();
        var weaponCtrl = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponController>();

        playerInventory = (playerInv != null) ? playerInv.ExportToSessionSnapshot() : null;
        warehouseInventory = (warehouseInv != null) ? warehouseInv.ExportToSessionSnapshot() : null;
        equipment = (equip != null) ? equip.ExportToSessionSnapshot() : null;
        weapon = (weaponCtrl != null) ? weaponCtrl.ExportToSessionSnapshot() : null;

        hasSnapshot = true;
    }

    // =========================
    // 복원(Import) - 씬 로드 후 자동 호출
    // =========================
    public void TryRestoreInScene()
    {
        var allInv = UnityEngine.Object.FindObjectsByType<InventoryManager>(FindObjectsSortMode.None);

        InventoryManager playerInv = null;
        InventoryManager warehouseInv = null;

        foreach (var inv in allInv)
        {
            if (inv == null) continue;
            if (inv.ownerType == InventoryManager.InventoryOwnerType.Player) playerInv = inv;
            else if (inv.ownerType == InventoryManager.InventoryOwnerType.Warehouse) warehouseInv = inv;
        }

        var equip = UnityEngine.Object.FindAnyObjectByType<EquipmentManager>();
        var weaponCtrl = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponController>();

        // 1) 장비 먼저 (가방 슬롯 영향)
        if (equip != null && equipment != null)
            equip.ImportFromSessionSnapshot(equipment);

        // 2) 무기/탄창 복원 (무기 장착 상태 포함)
        if (weaponCtrl != null && weapon != null)
            weaponCtrl.ImportFromSessionSnapshot(weapon);

        // 3) 플레이어 인벤 복원 (가방 반영 위해 equip 전달)
        if (playerInv != null && playerInventory != null)
            playerInv.ImportFromSessionSnapshot(playerInventory, equip);

        // 4) 창고는 베이스씬에 있을 때만 존재하므로 "있으면" 복원
        if (warehouseInv != null && warehouseInventory != null)
            warehouseInv.ImportFromSessionSnapshot(warehouseInventory, null);

        if (playerInv != null) playerInv.ForceRefreshUI();
        if (warehouseInv != null) warehouseInv.ForceRefreshUI();
    }

    // =========================
    // 사망 등 초기화
    // =========================
    public void ClearSnapshot()
    {
        hasSnapshot = false;
        playerInventory = null;
        warehouseInventory = null;
        equipment = null;
        weapon = null;
    }
}
