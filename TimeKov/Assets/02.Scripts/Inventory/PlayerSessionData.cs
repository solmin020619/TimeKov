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

    // =========================
    // ✅ 추가: Player Time 저장
    // =========================
    [Header("Saved Player Time")]
    public float savedCurrentTime = 0f;
    public bool hasSavedPlayerTime = false;

    // 사망 복귀 시 기본 Time
    public float deathReturnTime = 5f;

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

        for (int i = 0; i < allInv.Length; i++)
        {
            var inv = allInv[i];
            if (inv == null) continue;

            if (inv.ownerType == InventoryManager.InventoryOwnerType.Player)
                playerInv = inv;

            else if (inv.ownerType == InventoryManager.InventoryOwnerType.Warehouse)
                warehouseInv = inv;
        }

        var equip = UnityEngine.Object.FindAnyObjectByType<EquipmentManager>();
        var weaponCtrl = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponController>();
        var playerTime = UnityEngine.Object.FindAnyObjectByType<PlayerTime>();

        playerInventory = (playerInv != null) ? playerInv.ExportToSessionSnapshot() : null;
        warehouseInventory = (warehouseInv != null) ? warehouseInv.ExportToSessionSnapshot() : null;
        equipment = (equip != null) ? equip.ExportToSessionSnapshot() : null;
        weapon = (weaponCtrl != null) ? weaponCtrl.ExportToSessionSnapshot() : null;

        // =========================
        // ✅ 추가: Time 저장
        // =========================
        if (playerTime != null)
        {
            savedCurrentTime = playerTime.currentTime;
            hasSavedPlayerTime = true;
        }

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

        for (int i = 0; i < allInv.Length; i++)
        {
            var inv = allInv[i];
            if (inv == null) continue;

            if (inv.ownerType == InventoryManager.InventoryOwnerType.Player)
                playerInv = inv;

            else if (inv.ownerType == InventoryManager.InventoryOwnerType.Warehouse)
                warehouseInv = inv;
        }

        var equip = UnityEngine.Object.FindAnyObjectByType<EquipmentManager>();
        var weaponCtrl = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponController>();

        // 1) 장비 먼저
        if (equip != null && equipment != null)
            equip.ImportFromSessionSnapshot(equipment);

        // 2) 무기 복원
        if (weaponCtrl != null && weapon != null)
            weaponCtrl.ImportFromSessionSnapshot(weapon);

        // 3) 플레이어 인벤 복원
        if (playerInv != null && playerInventory != null)
            playerInv.ImportFromSessionSnapshot(playerInventory, equip);

        // 4) 창고 복원
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

        // =========================
        // ✅ 추가: 사망 시 Time 5초로 설정
        // =========================
        savedCurrentTime = deathReturnTime;
        hasSavedPlayerTime = true;
    }
}