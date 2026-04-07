// =====================================================================
// MachineUI.cs
// 플레이어가 설비에 가까이 가서 F키를 누르면 열리는 설비 전용 UI.
//
// UI 구성:
//   [입력 슬롯]  현재 설비 InputBuffer 내용 (아이콘 + 이름 + 수량)
//   [레시피]     이 설비의 조합식 목록
//   [출력 슬롯]  완성된 아이템 (아이콘 + 이름 + 수량) + 회수 버튼
//   [진행 바]    가공 진행도
//
// MachineInteraction 이 SetMachine() 을 호출해 대상 설비를 넣어준다.
// =====================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TIMEKOV.Factory
{
    public class MachineUI : MonoBehaviour
    {
        [Header("패널 루트")]
        public GameObject uiPanel;

        [Header("설비 이름")]
        public TextMeshProUGUI machineTitleText;

        [Header("입력 슬롯 영역")]
        public Transform inputSlotParent;
        public GameObject slotPrefab; // MachineSlotWidget 프리팹

        [Header("레시피 영역")]
        public Transform recipeParent;
        public GameObject recipeRowPrefab; // MachineRecipeRow 프리팹

        [Header("출력 슬롯 영역")]
        public Transform outputSlotParent;

        [Header("진행 바 + 상태 텍스트")]
        public Slider     progressBar;
        public TextMeshProUGUI statusText;

        [Header("인벤토리 참조 (재료 투입/회수용)")]
        public InventoryManager playerInventory;

        // ── 내부 상태 ───────────────────────────────────────────────
        private ProcessingMachine _machine;
        private string            _machineName;

        private readonly List<GameObject> _inputWidgets  = new();
        private readonly List<GameObject> _outputWidgets = new();
        private readonly List<GameObject> _recipeWidgets = new();

        // ============================================================
        // 외부 호출 API
        // ============================================================

        /// <summary>MachineInteraction이 호출 — 대상 설비 세팅 후 UI 열기</summary>
        public void OpenFor(ProcessingMachine machine, string machineName)
        {
            if (_machine != null)
                _machine.OnBufferChanged -= RefreshBufferUI;

            _machine     = machine;
            _machineName = machineName;
            _machine.OnBufferChanged += RefreshBufferUI;

            uiPanel.SetActive(true);
            if (machineTitleText != null)
                machineTitleText.text = machineName;

            BuildRecipeUI();
            RefreshBufferUI();
        }

        public void Close()
        {
            if (_machine != null)
                _machine.OnBufferChanged -= RefreshBufferUI;

            _machine = null;
            uiPanel.SetActive(false);
        }

        // ============================================================
        // UI 빌드
        // ============================================================

        /// <summary>레시피 목록은 고정이므로 한 번만 생성</summary>
        private void BuildRecipeUI()
        {
            foreach (var go in _recipeWidgets) Destroy(go);
            _recipeWidgets.Clear();

            if (recipeParent == null || recipeRowPrefab == null) return;

            foreach (var recipe in _machine.Recipes)
            {
                var row = Instantiate(recipeRowPrefab, recipeParent);
                _recipeWidgets.Add(row);

                if (row.TryGetComponent<MachineRecipeRow>(out var r))
                    r.Setup(recipe);
            }
        }

        /// <summary>버퍼 변경 시마다 호출 — 입력/출력 슬롯 갱신</summary>
        private void RefreshBufferUI()
        {
            RefreshSlots(_machine.InputBuffer.Stock,  inputSlotParent,  _inputWidgets,  false);
            RefreshSlots(_machine.OutputBuffer.Stock, outputSlotParent, _outputWidgets, true);
        }

        private void RefreshSlots(
            System.Collections.Generic.IReadOnlyDictionary<int, int> stock,
            Transform parent,
            List<GameObject> pool,
            bool isOutput)
        {
            if (parent == null || slotPrefab == null) return;

            // 기존 위젯 제거
            foreach (var go in pool) Destroy(go);
            pool.Clear();

            foreach (var kv in stock)
            {
                if (kv.Value <= 0) continue;

                var go = Instantiate(slotPrefab, parent);
                pool.Add(go);

                if (go.TryGetComponent<MachineSlotWidget>(out var w))
                {
                    w.Setup(kv.Key, kv.Value);

                    // 출력 슬롯이면 클릭 시 인벤토리로 회수
                    if (isOutput)
                    {
                        int capturedId     = kv.Key;
                        int capturedAmount = kv.Value;
                        w.SetClickAction(() => TakeOutput(capturedId, capturedAmount));
                    }
                    else
                    {
                        w.SetClickAction(null);
                    }
                }
            }
        }

        // ============================================================
        // 매 프레임: 진행 바 + 상태 텍스트 갱신
        // ============================================================

        private void Update()
        {
            if (_machine == null || !uiPanel.activeSelf) return;

            // 진행 바
            if (progressBar != null)
                progressBar.value = _machine.Progress;

            // 상태 텍스트
            if (statusText != null)
            {
                statusText.text = _machine.Status switch
                {
                    MachineStatus.Idle         => "대기 중",
                    MachineStatus.Processing   =>
                        $"제작 중... {(_machine.Progress * 100f):F0}%"
                        + (_machine.ActiveRecipe != null
                            ? $"\n→ {GetOutputName(_machine.ActiveRecipe)}"
                            : ""),
                    MachineStatus.OutputReady  => "완료 — 클릭해서 회수",
                    MachineStatus.Disconnected => "벨트 미연결",
                    _                          => ""
                };
            }
        }

        // ============================================================
        // 버튼: 인벤토리에서 재료 꺼내 설비에 투입
        // ============================================================

        /// <summary>
        /// UI 버튼에 연결.
        /// itemId와 amount를 Inspector에서 받거나 별도 InputField로 받아서 호출.
        /// </summary>
        public void AddItemFromInventory(int itemId, int amount)
        {
            if (_machine == null || playerInventory == null) return;

            if (!playerInventory.HasItem(itemId, amount))
            {
                Debug.Log($"[MachineUI] 인벤토리에 아이템 부족 (ID:{itemId})");
                return;
            }

            playerInventory.TryConsumeItem(itemId, amount);
            playerInventory.ForceRefreshUI();
            _machine.Receive(itemId, amount);
        }

        // ============================================================
        // 출력 슬롯 클릭 → 인벤토리 회수
        // ============================================================

        private void TakeOutput(int itemId, int amount)
        {
            if (_machine == null || playerInventory == null) return;

            if (_machine.TryTakeOutput(itemId, amount))
            {
                playerInventory.AddItem(itemId, amount);
                playerInventory.ForceRefreshUI();
                Debug.Log($"[MachineUI] 회수: ID {itemId} x{amount} → 인벤토리");
            }
        }

        // ── 유틸 ────────────────────────────────────────────────────

        private string GetOutputName(FactoryRecipe recipe)
        {
            if (recipe.outputs == null || recipe.outputs.Length == 0) return "";
            var item = DataManager.Instance?.GetItem(recipe.outputs[0].itemId);
            return item != null ? item.itemName : recipe.outputs[0].itemId.ToString();
        }
    }
}
