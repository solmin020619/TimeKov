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
    // ✅ [추가] 인벤 후보 중 "실제로 아이템이 들어있는" 인벤을 우선 선택
    // - Player 인벤이 2개 이상 존재할 때(템플릿/비활성 UI 등) 잘못된 빈 인벤을 캡처하는 문제 방지
    // =========================
    private InventoryManager SelectBestInventory(InventoryManager[] allInv, InventoryManager.InventoryOwnerType owner)
    {
        InventoryManager best = null;
        int bestScore = -1;
        bool bestActive = false;

        for (int i = 0; i < allInv.Length; i++)
        {
            var inv = allInv[i];
            if (inv == null) continue;
            if (inv.ownerType != owner) continue;

            // Export 해보고 "아이템 총량"을 점수로 사용
            // (InventoryManager 수정 없이 가장 확실하게 실제 사용 인벤을 판별 가능)
            int score = 0;
            try
            {
                var snap = inv.ExportToSessionSnapshot();
                if (snap != null && snap.counts != null)
                {
                    for (int c = 0; c < snap.counts.Count; c++)
                        score += snap.counts[c];
                }
            }
            catch
            {
                // Export 중 예외나면 그냥 후보에서 밀림
                score = -1;
            }

            bool active = inv.gameObject.activeInHierarchy && inv.enabled;

            // 우선순위:
            // 1) score 높은 것(아이템 많이 들어있는 것)
            // 2) 동점이면 active인 것 우선
            if (score > bestScore || (score == bestScore && active && !bestActive))
            {
                best = inv;
                bestScore = score;
                bestActive = active;
            }
        }

        // 전부 score가 -1이거나 다 비어있으면: 그래도 하나는 리턴 (기존 동작 유지)
        if (best == null)
        {
            for (int i = 0; i < allInv.Length; i++)
            {
                var inv = allInv[i];
                if (inv != null && inv.ownerType == owner) return inv;
            }
        }

        return best;
    }

    // =========================
    // 캡처(Export) - 씬 전환 직전 호출
    // =========================
    public void CaptureCurrent()
    {
        var allInv = UnityEngine.Object.FindObjectsByType<InventoryManager>(FindObjectsSortMode.None);

        // ✅ [수정] 단순 "마지막으로 찾은 인벤"이 아니라, 실제 아이템이 있는 인벤을 선택
        InventoryManager playerInv = SelectBestInventory(allInv, InventoryManager.InventoryOwnerType.Player);
        InventoryManager warehouseInv = SelectBestInventory(allInv, InventoryManager.InventoryOwnerType.Warehouse);

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

        // ✅ [수정] 복원도 동일하게 "실제 사용 인벤"을 우선 선택
        // (씬에 템플릿/복제 UI가 떠 있어도 올바른 쪽으로 복원되게)
        InventoryManager playerInv = null;
        InventoryManager warehouseInv = null;

        // 복원은 "아이템 점수"가 의미 없을 수 있으니 active 우선 + 타입 우선으로 선택
        // 다만 SelectBestInventory는 score 기준이므로, 여기서는 active 우선으로 직접 고름.
        for (int i = 0; i < allInv.Length; i++)
        {
            var inv = allInv[i];
            if (inv == null) continue;

            if (inv.ownerType == InventoryManager.InventoryOwnerType.Player)
            {
                if (playerInv == null || (inv.gameObject.activeInHierarchy && inv.enabled))
                    playerInv = inv;
            }
            else if (inv.ownerType == InventoryManager.InventoryOwnerType.Warehouse)
            {
                if (warehouseInv == null || (inv.gameObject.activeInHierarchy && inv.enabled))
                    warehouseInv = inv;
            }
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