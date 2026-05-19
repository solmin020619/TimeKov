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

        private CursorLockMode _prevLockState;
        private bool _prevVisible;
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
                if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Escape))
                    CloseUI();
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

            ThirdPersonCamera.IsUIOpen = true;
            PlayerInputComponent.IsBlocked = true;
            _player?.Movement.LockMovement(true);

            _prevLockState = Cursor.lockState;
            _prevVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            machineUI.OpenFor(_nearMachine, _nearMachineName);
            _uiOpen = true;

            if (hintText != null) hintText.text = "F / ESC  —  닫기";
        }

        private void CloseUI()
        {
            ThirdPersonCamera.IsUIOpen = false;
            PlayerInputComponent.IsBlocked = false;
            _player?.Movement.LockMovement(false);

            machineUI?.Close();
            _uiOpen = false;

            Cursor.lockState = _prevLockState;
            Cursor.visible = _prevVisible;

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