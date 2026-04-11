// =====================================================================
// MachineInteraction.cs
// 플레이어 오브젝트에 붙이는 컴포넌트.
//
// 동작 흐름:
//   1. 플레이어가 설비 Trigger 영역 진입
//   2. 화면에 "F — [설비 이름] 열기" 힌트 표시
//   3. F키 → MachineUI.OpenFor() 호출
//   4. F키 또는 Esc → MachineUI.Close()
//
// 설비 프리팹에는 Trigger Collider + MachineZone 컴포넌트가 필요.
// =====================================================================

using UnityEngine;
using TMPro;

namespace TIMEKOV.Factory
{
    public class MachineInteraction : MonoBehaviour
    {
        [Header("설비 UI (씬에 하나만 존재)")]
        public MachineUI machineUI;

        [Header("상호작용 힌트 텍스트 (선택)")]
        public TextMeshProUGUI hintText;

        // UI 열기 전 커서 상태 저장 → 닫을 때 복원
        private CursorLockMode _prevLockState;
        private bool _prevVisible;

        private ProcessingMachine _nearMachine;
        private string _nearMachineName;
        private bool _uiOpen;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (_uiOpen)
                    CloseUI();
                else if (_nearMachine != null)
                    OpenUI();
            }

            if (Input.GetKeyDown(KeyCode.Escape) && _uiOpen)
                CloseUI();
        }

        // ── Trigger 감지 ────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            var zone = other.GetComponent<MachineZone>();
            if (zone == null || zone.machine == null) return;

            _nearMachine = zone.machine;
            _nearMachineName = zone.machineName;

            if (hintText != null)
                hintText.text = $"F  —  {_nearMachineName} 열기";
        }

        private void OnTriggerExit(Collider other)
        {
            var zone = other.GetComponent<MachineZone>();
            if (zone == null || zone.machine != _nearMachine) return;

            _nearMachine = null;
            _nearMachineName = "";

            if (hintText != null) hintText.text = "";
            if (_uiOpen) CloseUI();
        }

        // ── UI 열기/닫기 ────────────────────────────────────────────

        private void OpenUI()
        {
            if (machineUI == null || _nearMachine == null) return;

            // UIStateManager를 통해 Factory 상태로 전환
            // → RefreshCursorState가 커서를 덮어쓰지 않게 됨
            if (UIStateManager.Instance != null)
                UIStateManager.Instance.OpenFactoryUI();
            else
            {
                // UIStateManager 없을 때 직접 제어
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            machineUI.OpenFor(_nearMachine, _nearMachineName);
            _uiOpen = true;

            if (hintText != null) hintText.text = "F / ESC  —  닫기";
        }

        private void CloseUI()
        {
            machineUI?.Close();
            _uiOpen = false;

            // UIStateManager를 통해 None 상태로 복원
            if (UIStateManager.Instance != null)
                UIStateManager.Instance.CloseFactoryUI();
            else
            {
                Cursor.lockState = _prevLockState;
                Cursor.visible = _prevVisible;
            }

            if (hintText != null)
                hintText.text = _nearMachine != null
                    ? $"F  —  {_nearMachineName} 열기"
                    : "";
        }

        // ── 인벤토리 슬롯 → 설비 투입 (외부 UI 버튼 연결용) ────────

        /// <summary>
        /// 인벤토리 슬롯 클릭 이벤트에 연결.
        /// 열려있는 설비가 있으면 해당 아이템을 설비에 투입한다.
        /// </summary>
        public void TryInsertFromInventory(int itemId, int amount)
        {
            if (!_uiOpen || machineUI == null) return;
            machineUI.AddItemFromInventory(itemId, amount);
        }
    }
}