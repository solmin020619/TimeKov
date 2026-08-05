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

        [Header("창고 추출 설비 UI")]
        public StorageExtractorUI storageExtractorUI;

        [Header("설비 선택 패널")]
        public FacilitySelectPanel facilitySelectPanel;

        [Header("상호작용 힌트 텍스트")]
        public TextMeshProUGUI hintText;

        [Header("BuildPort 감지 반경 (m)")]
        public float detectRadius = 2.5f;

        [Header("선택 패널 위치 조정")]
        [Tooltip("월드 기준 플레이어 위 높이 오프셋")]
        public float worldHeightOffset = 1.5f;
        [Tooltip("화면 좌표 우측 오프셋 (픽셀)")]
        public float screenRightOffset = 40f;

        // ── 내부 상태 ────────────────────────────────────────────────────

        private readonly List<(MachineBase machine, string name)> _nearMachines     = new();
        private readonly List<(MachineBase machine, string name)> _prevNearMachines = new();

        private bool      _uiOpen;
        private bool      _selectShowing;
        private bool      _wasBlocking;
        private Coroutine _flashRoutine;

        private int    _buildPortMask;
        private Player _player;
        private Camera _cam;

        private MachineBase _outlinedMachine;

        // ── 초기화 ────────────────────────────────────────────────────────

        private void Awake()
        {
            _buildPortMask = 1 << LayerMask.NameToLayer("BuildPort");
        }

        private void Start()
        {
            _player = FindFirstObjectByType<Player>();
            _cam    = Camera.main;

            // 씬에 이미 배치된 설비들의 아웃라인을 초기에 모두 OFF
            // (프리팹 기본값이 ON이어도, 플레이어가 접근할 때만 켜지도록)
            foreach (var mb in FindObjectsByType<MachineBase>(FindObjectsSortMode.None))
            {
                foreach (var outline in mb.GetComponentsInChildren<Outline>(true))
                    outline.enabled = false;
            }
        }

        // ── 매 프레임 ─────────────────────────────────────────────────────

        private void Update()
        {
            if (_uiOpen)
            {
                var uic = GameUIController.Instance;
                if (uic != null && uic.GetCurrentState() != GameUIController.UIState.Factory)
                {
                    ForceClose();
                    return;
                }
                if (Input.GetKeyDown(KeyBindings.Interact))
                    CloseUI();
                return;
            }

            if (_flashRoutine != null) return;

            bool blocking = GameUIController.Instance != null
                         && GameUIController.Instance.IsUIBlocking();
            if (blocking)
            {
                HideSelectPanel();
                if (hintText != null) hintText.text = "";
                _wasBlocking = true;
                return;
            }

            // 차단 상태(건축/인벤/설정 등)에서 막 빠져나온 프레임:
            // 변화 감지 캐시를 비워, 같은 자리에 그대로 서 있어도 선택 패널이
            // 다시 뜨도록 강제 재탐색한다. (B로 건축모드 들어갔다 나오면 F가 안 뜨던 버그)
            if (_wasBlocking)
            {
                _wasBlocking = false;
                _prevNearMachines.Clear();
            }

            ScanNearby();

            // 에임(카메라가 보는 방향) 기준으로 대상 설비를 매 프레임 갱신 - 바라보는 설비가 강조/열림.
            //   플레이어 위치 기준 근접만 보던 옛 방식(=엉뚱한 옆 설비가 잡히던 문제)을 대체한다.
            if (_selectShowing && facilitySelectPanel != null)
            {
                var aimed = PickAimedMachine();
                if (aimed != null)
                {
                    facilitySelectPanel.SelectMachine(aimed);
                    if (aimed != _outlinedMachine) SetOutline(aimed);
                    if (hintText != null) hintText.text = $"F  —  {Loc.Get(aimed.MachineName)}" + " " + Loc.Get("열기");
                }
            }

            if (Input.GetKeyDown(KeyBindings.Interact))
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
            if (_selectShowing && facilitySelectPanel != null && _cam != null)
                UpdatePanelPosition();
        }

        // ── 주변 설비 탐색 (MachineBase 기준) ───────────────────────────

        private void ScanNearby()
        {
            var hits = Physics.OverlapSphere(transform.position, detectRadius, _buildPortMask);

            _nearMachines.Clear();
            foreach (var hit in hits)
            {
                var machine = hit.GetComponentInParent<MachineBase>();
                if (machine == null) continue;
                if (!machine.ShowInteraction) continue;

                bool dup = false;
                foreach (var (m, _) in _nearMachines)
                    if (m == machine) { dup = true; break; }
                if (dup) continue;

                string name = Loc.Get(machine.MachineName);
                _nearMachines.Add((machine, name));
            }

            if (_nearMachines.Count == 0)
            {
                HideSelectPanel();
                if (hintText != null) hintText.text = "";
            }
            else
            {
                if (NearMachinesChanged())
                    ShowSelectPanel();
                // 힌트 텍스트는 Update 의 에임 추적 블록이 '바라보는 설비' 기준으로 매 프레임 설정한다.
            }

            _prevNearMachines.Clear();
            _prevNearMachines.AddRange(_nearMachines);
        }

        private bool NearMachinesChanged()
        {
            if (_nearMachines.Count != _prevNearMachines.Count) return true;
            for (int i = 0; i < _nearMachines.Count; i++)
                if (_nearMachines[i].machine != _prevNearMachines[i].machine) return true;
            return false;
        }

        // 근처 설비 중 카메라가 '바라보는' 것(화면 중앙에 가장 가까운 것)을 고른다 = 에임 기반 타겟.
        //   좌우로 나란한 두 설비도 카메라를 돌리면 그쪽이 잡힌다(마우스로 겨냥). 카메라 뒤 설비는 제외.
        private MachineBase PickAimedMachine()
        {
            if (_nearMachines.Count == 0) return null;
            if (_nearMachines.Count == 1) return _nearMachines[0].machine;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return _nearMachines[0].machine;

            Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
            MachineBase best = null;
            float bestSqr = float.MaxValue;
            foreach (var (m, _) in _nearMachines)
            {
                if (m == null) continue;
                Vector3 sp = _cam.WorldToScreenPoint(m.transform.position + Vector3.up);   // 바닥 말고 몸통 기준
                if (sp.z <= 0f) continue;                                                   // 카메라 뒤
                float sqr = ((Vector2)sp - center).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = m; }
            }
            return best != null ? best : _nearMachines[0].machine;   // 다 뒤면 첫 번째로 폴백
        }

        // ── 선택 패널 제어 ───────────────────────────────────────────────

        private void ShowSelectPanel()
        {
            if (facilitySelectPanel == null) return;
            facilitySelectPanel.Show(_nearMachines);
            _selectShowing = true;
            SetOutline(facilitySelectPanel.SelectedMachine);
        }

        private void HideSelectPanel()
        {
            if (!_selectShowing || facilitySelectPanel == null) return;
            SetOutline(null);
            facilitySelectPanel.Hide();
            _selectShowing = false;
        }

        private void UpdatePanelPosition()
        {
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

        // ── 윤곽선 제어 ──────────────────────────────────────────────────

        private void SetOutline(MachineBase machine)
        {
            if (_outlinedMachine != null)
            {
                var old = _outlinedMachine.GetComponent<Outline>();
                if (old != null) old.enabled = false;
            }

            _outlinedMachine = machine;
            if (machine == null) return;

            // 통일된 노란색 강조 아웃라인 적용
            InteractOutline.Enable(machine.GetComponent<Outline>());
        }

        // ── 깜빡임 후 설비 열기 ──────────────────────────────────────────

        private IEnumerator FlashAndOpen(MachineBase machine, string machineName)
        {
            if (facilitySelectPanel != null)
                yield return facilitySelectPanel.FlashSelected(0.4f, 3);

            HideSelectPanel();
            OpenUI(machine, machineName);

            _flashRoutine = null;
        }

        // ── 설비 UI 열기 / 닫기 ──────────────────────────────────────────

        private void OpenUI(MachineBase machine, string machineName)
        {
            if (machine == null) return;

            GameUIController.Instance?.OpenFactoryUI();
            ThirdPersonCamera.IsUIOpen = true;
            _player?.Movement.LockMovement(true);

            // 설비 타입에 따라 적합한 UI를 열기
            if (machine is ProcessingMachine pm && machineUI != null)
            {
                machineUI.OpenFor(pm, machineName);
            }
            else if (machine is StorageExtractor se && storageExtractorUI != null)
            {
                storageExtractorUI.OpenFor(se, machineName);
            }
            else
            {
                // 해당 UI가 없으면 열지 않음
                GameUIController.Instance?.CloseFactoryUI();
                ThirdPersonCamera.IsUIOpen = false;
                _player?.Movement.LockMovement(false);
                return;
            }

            _uiOpen = true;
            GameEvents.RaiseFacilityInteract(machine.FacilityId);
            if (hintText != null) hintText.text = "F / ESC  —  " + Loc.Get("닫기");
        }

        private void CloseUI()
        {
            GameUIController.Instance?.CloseFactoryUI();
            ThirdPersonCamera.IsUIOpen = false;
            _player?.Movement.LockMovement(false);

            machineUI?.Close();
            storageExtractorUI?.Close();


            _uiOpen = false;
            _prevNearMachines.Clear();
            if (hintText != null) hintText.text = "";
        }

        private void ForceClose()
        {
            ThirdPersonCamera.IsUIOpen = false;
            _player?.Movement.LockMovement(false);

            machineUI?.Close();
            storageExtractorUI?.Close();


            _uiOpen = false;
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
