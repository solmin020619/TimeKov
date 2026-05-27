using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace TIMEKOV.Factory
{
    public class MachineInteraction : MonoBehaviour
    {
        [Header("설비 UI")]
        public MachineUI machineUI;

        [Header("설비 선택 패널 (다중 설비)")]
        public FacilitySelectPanel facilitySelectPanel;

        [Header("상호작용 힌트 텍스트")]
        public TextMeshProUGUI hintText;

        [Header("BuildPort 감지 반경 (m)")]
        public float detectRadius = 2.5f;

        [Header("선택 패널 위치 조정")]
        [Tooltip("월드 기준 설비 위 높이 오프셋")]
        public float worldHeightOffset = 1.5f;
        [Tooltip("화면 좌표 우측 오프셋 (픽셀)")]
        public float screenRightOffset = 40f;

        // ── 내부 상태 ────────────────────────────────────────────────────

        // 단일 설비 (선택 패널 없이 직접 열 때)
        private ProcessingMachine _nearMachine;
        private string            _nearMachineName;

        // 다중 설비 목록 (현재 프레임 / 직전 프레임 비교용)
        private readonly List<(ProcessingMachine machine, string name)> _nearMachines = new();
        private readonly List<(ProcessingMachine machine, string name)> _prevNearMachines = new();

        private bool      _uiOpen;
        private bool      _selectShowing;
        private Coroutine _flashRoutine;

        private ProcessingMachine _outlinedMachine;

        private int    _buildPortMask;
        private Player _player;
        private Camera _cam;

        // ── 초기화 ────────────────────────────────────────────────────────

        private void Awake()
        {
            _buildPortMask = 1 << LayerMask.NameToLayer("BuildPort");
        }

        private void Start()
        {
            _player = FindFirstObjectByType<Player>();
            _cam    = Camera.main;
        }

        // ── 매 프레임 ─────────────────────────────────────────────────────

        private void Update()
        {
            // ── 설비 UI가 열려있는 동안 ──────────────────────────────────
            if (_uiOpen)
            {
                var uic = GameUIController.Instance;
                if (uic != null && uic.GetCurrentState() != GameUIController.UIState.Factory)
                {
                    ForceClose();
                    return;
                }
                if (Input.GetKeyDown(KeyCode.F))
                    CloseUI();
                return;
            }

            // ── 깜빡임(Flash) 코루틴 실행 중에는 입력 차단 ────────────────
            if (_flashRoutine != null) return;

            // ── 다른 UI가 열려있으면 선택 패널 숨김 ──────────────────────
            if (GameUIController.Instance != null && GameUIController.Instance.IsUIBlocking())
            {
                HideSelectPanel();
                ScanNearby();
                return;
            }

            ScanNearby();

            // ── 마우스 휠 → 선택 인덱스 이동 ─────────────────────────────
            if (_selectShowing && facilitySelectPanel != null)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll > 0f)
                {
                    facilitySelectPanel.ScrollSelection(-1);
                    SetOutline(facilitySelectPanel.SelectedMachine);
                }
                else if (scroll < 0f)
                {
                    facilitySelectPanel.ScrollSelection(1);
                    SetOutline(facilitySelectPanel.SelectedMachine);
                }
            }

            // ── F 키 처리 ─────────────────────────────────────────────────
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (_selectShowing && facilitySelectPanel != null)
                {
                    var target     = facilitySelectPanel.SelectedMachine;
                    var targetName = facilitySelectPanel.SelectedMachineName;
                    if (target != null)
                        _flashRoutine = StartCoroutine(FlashAndOpen(target, targetName));
                }
            }
        }

        private void LateUpdate()
        {
            // 선택 패널이 보이는 동안 가장 가까운 설비 위에 패널 위치 갱신
            if (_selectShowing && facilitySelectPanel != null && _cam != null)
                UpdatePanelPosition();
        }

        // ── 주변 설비 탐색 ───────────────────────────────────────────────

        private void ScanNearby()
        {
            var hits = Physics.OverlapSphere(transform.position, detectRadius, _buildPortMask);

            _nearMachines.Clear();
            foreach (var hit in hits)
            {
                var machine = hit.GetComponentInParent<ProcessingMachine>();
                if (machine == null) continue;

                // 중복 제거
                bool dup = false;
                foreach (var (m, _) in _nearMachines)
                    if (m == machine) { dup = true; break; }
                if (dup) continue;

                string name = !string.IsNullOrEmpty(machine.machineName)
                    ? machine.machineName
                    : machine.gameObject.name;
                _nearMachines.Add((machine, name));
            }

            // ── 분기: 0 / 1 이상 ─────────────────────────────────────────

            if (_nearMachines.Count == 0)
            {
                _nearMachine     = null;
                _nearMachineName = "";
                HideSelectPanel();
                if (hintText != null) hintText.text = "";
            }
            else
            {
                // 1개든 다수든 항상 선택 패널 표시
                _nearMachine     = null;
                _nearMachineName = "";

                if (NearMachinesChanged())
                    ShowSelectPanel();

                if (hintText != null)
                    hintText.text = _nearMachines.Count == 1
                        ? $"F  —  {_nearMachines[0].name} 열기"
                        : "스크롤  —  선택  /  F  —  열기";
            }

            // 이전 목록 저장
            _prevNearMachines.Clear();
            _prevNearMachines.AddRange(_nearMachines);
        }

        // 현재와 직전 목록이 다른지 (수량 또는 구성 변화)
        private bool NearMachinesChanged()
        {
            if (_nearMachines.Count != _prevNearMachines.Count) return true;
            for (int i = 0; i < _nearMachines.Count; i++)
                if (_nearMachines[i].machine != _prevNearMachines[i].machine) return true;
            return false;
        }

        // ── 선택 패널 제어 ───────────────────────────────────────────────

        private void ShowSelectPanel()
        {
            if (facilitySelectPanel == null) return;
            facilitySelectPanel.Show(_nearMachines);
            _selectShowing = true;
            ThirdPersonCamera.BlockZoom = true;
            SetOutline(facilitySelectPanel.SelectedMachine);
        }

        private void HideSelectPanel()
        {
            if (!_selectShowing || facilitySelectPanel == null) return;
            SetOutline(null);
            facilitySelectPanel.Hide();
            _selectShowing = false;
            ThirdPersonCamera.BlockZoom = false;
        }

        // ── 윤곽선 제어 ──────────────────────────────────────────────────

        private void SetOutline(ProcessingMachine machine)
        {
            // 이전 윤곽선 끄기
            if (_outlinedMachine != null)
            {
                var old = _outlinedMachine.GetComponent<Outline>();
                if (old != null) old.enabled = false;
            }

            _outlinedMachine = machine;

            if (machine == null) return;

            // 프리팹에 미리 추가된 Outline 컴포넌트를 켜기
            var outline = machine.GetComponent<Outline>();
            if (outline != null) outline.enabled = true;
        }

        // 가장 가까운 설비의 월드 위치를 화면 좌표로 변환해 패널 이동
        private void UpdatePanelPosition()
        {
            if (_nearMachines.Count == 0) return;

            // 플레이어 위치 기준으로 패널 표시
            Vector3 world  = transform.position + Vector3.up * worldHeightOffset;
            Vector3 screen = _cam.WorldToScreenPoint(world);

            if (screen.z < 0f)
            {
                facilitySelectPanel.SetScreenPosition(new Vector3(-10000f, -10000f, 0f));
                return;
            }

            screen.x += screenRightOffset;
            facilitySelectPanel.SetScreenPosition(screen);
        }

        // ── 깜빡임 후 설비 열기 ──────────────────────────────────────────

        private IEnumerator FlashAndOpen(ProcessingMachine machine, string machineName)
        {
            // 선택된 행 깜빡임
            if (facilitySelectPanel != null)
                yield return facilitySelectPanel.FlashSelected(0.4f, 3);

            HideSelectPanel();
            OpenUI(machine, machineName);

            _flashRoutine = null;
        }

        // ── 설비 UI 열기 / 닫기 ──────────────────────────────────────────

        private void OpenUI(ProcessingMachine machine, string machineName)
        {
            if (machineUI == null || machine == null) return;

            GameUIController.Instance?.OpenFactoryUI();
            ThirdPersonCamera.IsUIOpen = true;
            _player?.Movement.LockMovement(true);

            machineUI.OpenFor(machine, machineName);
            _uiOpen = true;

            GameEvents.RaiseFacilityInteract(machine.FacilityId);

            if (hintText != null) hintText.text = "F / ESC  —  닫기";
        }

        private void CloseUI()
        {
            GameUIController.Instance?.CloseFactoryUI();
            ThirdPersonCamera.IsUIOpen = false;
            _player?.Movement.LockMovement(false);

            machineUI?.Close();
            _uiOpen = false;

            // 이전 목록을 비워 다음 ScanNearby()에서 패널이 즉시 재표시되도록
            _prevNearMachines.Clear();
            if (hintText != null) hintText.text = "";
        }

        /// <summary>GameUIController가 외부(ESC 등)에서 Factory 상태를 닫았을 때 로컬 정리.</summary>
        private void ForceClose()
        {
            ThirdPersonCamera.IsUIOpen = false;
            _player?.Movement.LockMovement(false);

            machineUI?.Close();
            _uiOpen = false;

            // 이전 목록을 비워 다음 ScanNearby()에서 패널이 즉시 재표시되도록
            _prevNearMachines.Clear();
            if (hintText != null) hintText.text = "";
        }

        public void TryInsertFromInventory(int itemId, int amount)
        {
            if (!_uiOpen || machineUI == null) return;
            machineUI.AddItemFromInventory(itemId, amount);
        }
    }
}
