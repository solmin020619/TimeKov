using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TIMEKOV.Factory
{
    public class MachineUI : MonoBehaviour
    {
        [Header("루트 패널")]
        public GameObject uiPanel;

        [Header("설비 이름")]
        public TextMeshProUGUI machineTitleText;

        [Header("설비 입력 슬롯 부모")]
        public Transform inputSlotParent;

        [Header("레시피 부모")]
        public Transform  recipeParent;
        public GameObject recipeRowPrefab;

        [Header("출력 슬롯 부모")]
        public Transform outputSlotParent;

        [Header("진행 바 / 상태 텍스트")]
        public Slider          progressBar;
        public TextMeshProUGUI statusText;

        [Header("인벤토리 슬롯 부모 (UI 하단)")]
        public Transform inventorySlotParent;

        [Header("슬롯 프리팹 (MachineSlotWidget)")]
        public GameObject slotPrefab;

        [Header("플레이어 인벤토리")]
        public InventoryManager playerInventory;

        // ── 내부 ────────────────────────────────────────────────────
        private ProcessingMachine          _machine;
        private readonly List<GameObject>  _inputW  = new();
        private readonly List<GameObject>  _outputW = new();
        private readonly List<GameObject>  _recipeW = new();
        private readonly List<GameObject>  _invW    = new();

        // ============================================================
        // 공개 API
        // ============================================================

        public void OpenFor(ProcessingMachine machine, string title)
        {
            if (_machine != null) _machine.OnBufferChanged -= RefreshAll;
            _machine = machine;
            _machine.OnBufferChanged += RefreshAll;

            uiPanel.SetActive(true);
            if (machineTitleText != null) machineTitleText.text = title;

            BuildRecipes();
            RefreshAll();
        }

        public void Close()
        {
            if (_machine != null) _machine.OnBufferChanged -= RefreshAll;
            _machine = null;
            uiPanel.SetActive(false);
        }

        public void AddItemFromInventory(int itemId, int amount) => InsertAll(itemId, amount);

        // ============================================================
        // 레시피 (고정 — 열 때 한 번만 생성)
        // ============================================================

        private void BuildRecipes()
        {
            foreach (var g in _recipeW) Destroy(g);
            _recipeW.Clear();
            if (recipeParent == null || recipeRowPrefab == null || _machine == null) return;

            foreach (var r in _machine.Recipes)
            {
                var go = Instantiate(recipeRowPrefab, recipeParent);
                _recipeW.Add(go);
                go.GetComponent<MachineRecipeRow>()?.Setup(r);
            }
        }

        // ============================================================
        // 버퍼 갱신
        // ============================================================

        private void RefreshAll()
        {
            BuildMachineSlots(_machine.InputBuffer.Stock,  inputSlotParent,  _inputW,  isOutput: false);
            BuildMachineSlots(_machine.OutputBuffer.Stock, outputSlotParent, _outputW, isOutput: true);
            BuildInventorySlots();
        }

        // ── 설비 내부 슬롯 ──────────────────────────────────────────
        private void BuildMachineSlots(
            IReadOnlyDictionary<int, int> stock,
            Transform parent,
            List<GameObject> pool,
            bool isOutput)
        {
            if (parent == null || slotPrefab == null) return;
            foreach (var g in pool) Destroy(g);
            pool.Clear();

            foreach (var kv in stock)
            {
                if (kv.Value <= 0) continue;
                var go = Instantiate(slotPrefab, parent);
                pool.Add(go);
                if (!go.TryGetComponent<MachineSlotWidget>(out var w)) continue;

                w.Setup(kv.Key, kv.Value);
                int id = kv.Key, amt = kv.Value;
                if (isOutput)
                    w.SetDoubleClickAction(() => TakeOutput(id, amt));
                else
                    w.SetDoubleClickAction(() => ReturnInput(id, amt));
            }
        }

        // ── 인벤토리 슬롯 (DataStore.ItemById 순회) ──────────────────
        private void BuildInventorySlots()
        {
            if (inventorySlotParent == null || slotPrefab == null || playerInventory == null) return;
            foreach (var g in _invW) Destroy(g);
            _invW.Clear();

            if (!DataStore.IsLoaded) return;

            foreach (var kv in DataStore.ItemById)
            {
                int id    = kv.Key;
                int total = playerInventory.GetTotalItemCount(id);
                if (total <= 0) continue;

                var go = Instantiate(slotPrefab, inventorySlotParent);
                _invW.Add(go);
                if (!go.TryGetComponent<MachineSlotWidget>(out var w)) continue;

                w.Setup(id, total);
                int cId = id, cAmt = total;
                w.SetClickAction(      () => InsertOne(cId));
                w.SetDoubleClickAction(() => InsertAll(cId, cAmt));
            }
        }

        // ============================================================
        // 이동 로직
        // ============================================================

        // 인벤토리 → 설비 1개
        private void InsertOne(int itemId)
        {
            if (_machine == null || playerInventory == null) return;
            if (!playerInventory.HasItem(itemId, 1)) return;
            playerInventory.TryConsumeItem(itemId, 1);
            playerInventory.ForceRefreshUI();
            _machine.Receive(itemId, 1);
            BuildInventorySlots();
        }

        // 인벤토리 → 설비 전량
        private void InsertAll(int itemId, int amount)
        {
            if (_machine == null || playerInventory == null) return;
            int actual = Mathf.Min(amount, playerInventory.GetTotalItemCount(itemId));
            if (actual <= 0) return;
            playerInventory.TryConsumeItem(itemId, actual);
            playerInventory.ForceRefreshUI();
            _machine.Receive(itemId, actual);
            BuildInventorySlots();
        }

        // 설비 입력 버퍼 → 인벤토리 반환 (가공 중엔 불가)
        private void ReturnInput(int itemId, int amount)
        {
            if (_machine == null || playerInventory == null) return;
            if (_machine.Status == MachineStatus.Processing)
            {
                Debug.Log("[MachineUI] 가공 중에는 재료를 꺼낼 수 없습니다");
                return;
            }
            if (!_machine.InputBuffer.Consume(itemId, amount)) return;
            _machine.PublicNotifyBufferChanged();
            playerInventory.AddItem(itemId, amount);
            playerInventory.ForceRefreshUI();
            BuildInventorySlots();
        }

        // 출력 버퍼 → 인벤토리
        private void TakeOutput(int itemId, int amount)
        {
            if (_machine == null || playerInventory == null) return;
            if (!_machine.TryTakeOutput(itemId, amount)) return;
            playerInventory.AddItem(itemId, amount);
            playerInventory.ForceRefreshUI();
            BuildInventorySlots();
        }

        // ============================================================
        // Update: 진행 바 / 상태 텍스트
        // ============================================================

        private void Update()
        {
            if (_machine == null || !uiPanel.activeSelf) return;

            if (progressBar != null)
                progressBar.value = _machine.Progress;

            if (statusText == null) return;

            string outName = "";
            if (_machine.ActiveRecipe != null && _machine.ActiveRecipe.outputs?.Length > 0)
            {
                int id  = _machine.ActiveRecipe.outputs[0].itemId;
                var row = DataStore.GetItem(id);
                outName = row != null ? $"\n→ {row.itemName}" : "";
            }

            statusText.text = _machine.Status switch
            {
                MachineStatus.Idle         => "재료를 투입하세요",
                MachineStatus.Processing   => $"제작 중 {(_machine.Progress * 100f):F0}%{outName}",
                MachineStatus.OutputReady  => "완료 — 더블클릭으로 회수",
                MachineStatus.Disconnected => "벨트 미연결",
                _                          => ""
            };
        }
    }
}
