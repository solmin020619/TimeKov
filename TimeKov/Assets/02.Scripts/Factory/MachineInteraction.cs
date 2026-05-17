// =====================================================================
// MachineInteraction.cs
// 플레이어 오브젝트에 붙이는 컴포넌트.
//
// 동작 흐름:
//   1. 매 프레임 OverlapSphere로 "BuildPort" 레이어 오브젝트 감지
//   2. 감지되면 화면에 "F — [설비 이름] 열기" 힌트 표시
//   3. F키 → MachineUI.OpenFor() 호출
//   4. F키 또는 Esc → MachineUI.Close()
//
// 설비 오브젝트(또는 자식)의 레이어를 "BuildPort"로 설정해야 합니다.
// MachineZone / Trigger Collider 불필요.
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

        [Header("BuildPort 감지 반경 (m)")]
        public float detectRadius = 2.5f;

        // UI 열기 전 커서 상태 저장 → 닫을 때 복원
        private CursorLockMode _prevLockState;
        private bool _prevVisible;

        private ProcessingMachine _nearMachine;
        private string _nearMachineName;
        private bool _uiOpen;

        // "BuildPort" 레이어 마스크 (런타임에 한 번만 계산)
        private int _buildPortMask;

        private void Awake()
        {
            _buildPortMask = 1 << LayerMask.NameToLayer("BuildPort");
        }

        // ============================================================
        // 매 프레임 감지 + 입력 처리
        // ============================================================

        private void Update()
        {
            // UI가 열려 있는 동안은 감지 갱신 없이 닫기 입력만 처리
            if (_uiOpen)
            {
                if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Escape))
                    CloseUI();
                return;
            }

            // BuildPort 레이어 오브젝트 탐색
            ScanNearby();

            if (Input.GetKeyDown(KeyCode.F) && _nearMachine != null)
                OpenUI();
        }

        // ── BuildPort 레이어 OverlapSphere 감지 ─────────────────────
        private void ScanNearby()
        {
            var hits = Physics.OverlapSphere(transform.position, detectRadius, _buildPortMask);

            ProcessingMachine found = null;
            string foundName = "";

            foreach (var hit in hits)
            {
                // BuildPort 오브젝트 자신 또는 부모에서 ProcessingMachine 탐색
                var machine = hit.GetComponentInParent<ProcessingMachine>();
                if (machine == null) continue;

                found = machine;

                // 이름은 MachineZone이 있으면 그걸 우선, 없으면 GameObject 이름
                var zone = hit.GetComponentInParent<MachineZone>();
                foundName = (zone != null && !string.IsNullOrEmpty(zone.machineName))
                    ? zone.machineName
                    : machine.gameObject.name;

                break; // 가장 먼저 걸리는 설비 하나만 사용
            }

            // 상태 변화가 있을 때만 힌트 갱신
            if (found != _nearMachine)
            {
                _nearMachine = found;
                _nearMachineName = foundName;

                if (hintText != null)
                    hintText.text = _nearMachine != null
                        ? $"F  —  {_nearMachineName} 열기"
                        : "";
            }
        }

        // ── UI 열기/닫기 ────────────────────────────────────────────

        private void OpenUI()
        {
            if (machineUI == null || _nearMachine == null) return;

            ThirdPersonCamera.IsUIOpen = true;
            PlayerInputComponent.IsBlocked = true;

            // 플레이어 이동 잠금
            var player = FindFirstObjectByType<Player>();
            if (player != null) player.Movement.LockMovement(true);

            if (UIStateManager.Instance != null)
                UIStateManager.Instance.OpenFactoryUI();
            else
            {
                _prevLockState = Cursor.lockState;
                _prevVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            machineUI.OpenFor(_nearMachine, _nearMachineName);
            _uiOpen = true;

            if (hintText != null) hintText.text = "F / ESC  —  닫기";
        }

        private void CloseUI()
        {
            ThirdPersonCamera.IsUIOpen = false;
            PlayerInputComponent.IsBlocked = false;

            // 플레이어 이동 잠금 해제
            var player = FindFirstObjectByType<Player>();
            if (player != null) player.Movement.LockMovement(false);

            machineUI?.Close();
            _uiOpen = false;

            if (UIStateManager.Instance != null)
                UIStateManager.Instance.CloseFactoryUI();
            else
            {
                Cursor.lockState = _prevLockState;
                Cursor.visible = _prevVisible;
            }

            if (hintText != null)
                hintText.text = _nearMachine != null ? $"F  —  {_nearMachineName} 열기" : "";
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