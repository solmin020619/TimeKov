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


    //  Player Time 저장
    
    [Header("Saved Player Time")]
    public float savedCurrentTime = 0f;
    public bool hasSavedPlayerTime = false;

    // 사망 복귀 시 기본 Time
    public float deathReturnTime = 5f;

    [Header("State")]
    public bool hasSnapshot = false;

    private readonly SceneRefs _sceneRefs = new SceneRefs();

    private sealed class SceneRefs
    {
        public InventoryManager playerInv;
        public InventoryManager warehouseInv;
        public EquipmentManager equip;
        //public PlayerWeaponController weaponCtrl; //수정
        //public PlayerTime playerTime; //수정

        public void Clear()
        {
            playerInv = null;
            warehouseInv = null;
            equip = null;
            //weaponCtrl = null; //수정
            //playerTime = null; //수정
        }
    }

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
        _sceneRefs.Clear();

        if (!hasSnapshot)
            return;

        TryRestoreInScene();
    }

    private void ResolveSceneRefs(bool includePlayerTime)
    {
        var allInv = UnityEngine.Object.FindObjectsByType<InventoryManager>(FindObjectsSortMode.None);

        _sceneRefs.playerInv = null;
        _sceneRefs.warehouseInv = null;

        for (int i = 0; i < allInv.Length; i++)
        {
            var inv = allInv[i];
            if (inv == null) continue;

            if (inv.ownerType == InventoryManager.InventoryOwnerType.Player)
                _sceneRefs.playerInv = inv;
            else if (inv.ownerType == InventoryManager.InventoryOwnerType.Warehouse)
                _sceneRefs.warehouseInv = inv;
        }

        _sceneRefs.equip = UnityEngine.Object.FindAnyObjectByType<EquipmentManager>();
        //_sceneRefs.weaponCtrl = UnityEngine.Object.FindAnyObjectByType<PlayerWeaponController>(); //수정

        //if (includePlayerTime) //수정
        //    _sceneRefs.playerTime = UnityEngine.Object.FindAnyObjectByType<PlayerTime>(); //수정
    }

 
    // 캡처(Export) - 씬 전환 직전 호출
   
    public void CaptureCurrent()
    {
        ResolveSceneRefs(includePlayerTime: true);

        playerInventory = (_sceneRefs.playerInv != null) ? _sceneRefs.playerInv.ExportToSessionSnapshot() : null;
        warehouseInventory = (_sceneRefs.warehouseInv != null) ? _sceneRefs.warehouseInv.ExportToSessionSnapshot() : null;
        equipment = (_sceneRefs.equip != null) ? _sceneRefs.equip.ExportToSessionSnapshot() : null;
        //weapon = (_sceneRefs.weaponCtrl != null) ? _sceneRefs.weaponCtrl.ExportToSessionSnapshot() : null; //수정

        //  Time 저장 //수정
        //if (_sceneRefs.playerTime != null) //수정
        //{ //수정
        //    savedCurrentTime = _sceneRefs.playerTime.currentTime; //수정
        //    hasSavedPlayerTime = true; //수정
        //} //수정

        hasSnapshot = true;
    }

 
    // 복원(Import) - 씬 로드 후 자동 호출
   
    public void TryRestoreInScene()
    {
        ResolveSceneRefs(includePlayerTime: false);

        InventoryManager playerInv = _sceneRefs.playerInv;
        InventoryManager warehouseInv = _sceneRefs.warehouseInv;
        EquipmentManager equip = _sceneRefs.equip;
        //PlayerWeaponController weaponCtrl = _sceneRefs.weaponCtrl; //수정

        // 1) 장비 먼저
        if (equip != null && equipment != null)
            equip.ImportFromSessionSnapshot(equipment);

        // 2) 무기 복원 //수정
        //if (weaponCtrl != null && weapon != null) //수정
        //    weaponCtrl.ImportFromSessionSnapshot(weapon); //수정

        // 3) 플레이어 인벤 복원
        if (playerInv != null && playerInventory != null)
            playerInv.ImportFromSessionSnapshot(playerInventory, equip);

        // 4) 창고 복원
        if (warehouseInv != null && warehouseInventory != null)
            warehouseInv.ImportFromSessionSnapshot(warehouseInventory, null);

        if (playerInv != null) playerInv.ForceRefreshUI();
        if (warehouseInv != null) warehouseInv.ForceRefreshUI();
    }


    // 사망 등 초기화
    public void ClearSnapshot()
    {
        hasSnapshot = false;

        playerInventory = null;
        warehouseInventory = null;
        equipment = null;
        //weapon = null; //수정

       
        // 사망 시 Time 5초로 설정
       
        savedCurrentTime = deathReturnTime;
        hasSavedPlayerTime = true;
    }
}