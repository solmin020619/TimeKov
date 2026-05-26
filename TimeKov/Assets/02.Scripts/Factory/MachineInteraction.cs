using UnityEngine;
using TMPro;

namespace TIMEKOV.Factory
{
    public class MachineInteraction : MonoBehaviour
    {
        [Header("설비 UI")]
        public MachineUI machineUI;

        [Header("상호작용 힌트 텍스트")]
        public TextMeshProUGUI hintText;

        [Header("BuildPort 감지 반경 (m)")]
        public float detectRadius = 2.5f;

        private ProcessingMachine _nearMachine;
        private string _nearMachineName;
        private bool _uiOpen;
        private int _buildPortMask;
        private Player _player;

        private void Awake()
        {
            _buildPortMask = 1 << LayerMask.NameToLayer("BuildPort");
        }

        private void Start()
        {
            _player = FindFirstObjectByType<Player>();
        }

        private void Update()
        {
            if (_uiOpen)
            {
                // GameUIController가 외부(WindowManager.HandleEscape 등)에서 Factory를 닫은 경우 감지
                var uic = GameUIController.Instance;
                if (uic != null && uic.GetCurrentState() != GameUIController.UIState.Factory)
                {
                    ForceClose();
                    return;
                }

                // F 키만 자체 처리 (ESC는 WindowManager가 디스패치)
                if (Input.GetKeyDown(KeyCode.F))
                    CloseUI();
                return;
            }

            // 다른 UI가 열려있으면 설비 열기 차단
            if (GameUIController.Instance != null && GameUIController.Instance.IsUIBlocking())
            {
                ScanNearby();
                return;
            }

            ScanNearby();

            if (Input.GetKeyDown(KeyCode.F) && _nearMachine != null)
                OpenUI();
        }

        private void ScanNearby()
        {
            var hits = Physics.OverlapSphere(transform.position, detectRadius, _buildPortMask);

            ProcessingMachine found = null;
            string foundName = "";

            foreach (var hit in hits)
            {
                var machine = hit.GetComponentInParent<ProcessingMachine>();
                if (machine == null) continue;

                found = machine;
                foundName = !string.IsNullOrEmpty(machine.machineName)
                    ? machine.machineName
                    : machine.gameObject.name;
                break;
            }

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

        private void OpenUI()
        {
            if (machineUI == null || _nearMachine == null) return;

            // GameUIController에 Factory 상태 등록 → 내부에서 WindowManager.Open("Factory") mirror
            // PlayerInputComponent.IsBlocked / Cursor.lockState·visible 은 WindowManager.ApplyGlobalState가 처리.
            GameUIController.Instance?.OpenFactoryUI();

            ThirdPersonCamera.IsUIOpen = true;
            _player?.Movement.LockMovement(true);

            machineUI.OpenFor(_nearMachine, _nearMachineName);
            _uiOpen = true;

            // 퀘스트 시스템 통지 — 실제 설비 UI 열린 시점 (빌드 모드에서 F만 눌러도 깨지던 문제 해결)
            GameEvents.RaiseFacilityInteract(_nearMachine.FacilityId);

            if (hintText != null) hintText.text = "F / ESC  —  닫기";
        }

        private void CloseUI()
        {
            // GameUIController 상태 해제 → mirror Close("Factory") → ApplyGlobalState가 Cursor·Input 복구
            GameUIController.Instance?.CloseFactoryUI();

            ThirdPersonCamera.IsUIOpen = false;
            _player?.Movement.LockMovement(false);

            machineUI?.Close();
            _uiOpen = false;

            if (hintText != null)
                hintText.text = _nearMachine != null
                    ? $"F  —  {_nearMachineName} 열기"
                    : "";
        }

        /// <summary>GameUIController가 외부(ESC 등)에서 Factory 상태를 닫았을 때 로컬 정리</summary>
        private void ForceClose()
        {
            ThirdPersonCamera.IsUIOpen = false;
            _player?.Movement.LockMovement(false);

            machineUI?.Close();
            _uiOpen = false;

            // Cursor·Input 은 이미 WindowManager.ApplyGlobalState가 복구함

            if (hintText != null)
                hintText.text = _nearMachine != null
                    ? $"F  —  {_nearMachineName} 열기"
                    : "";
        }

        public void TryInsertFromInventory(int itemId, int amount)
        {
            if (!_uiOpen || machineUI == null) return;
            machineUI.AddItemFromInventory(itemId, amount);
        }
    }
}