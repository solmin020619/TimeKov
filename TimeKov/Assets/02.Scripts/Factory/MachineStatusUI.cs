// =====================================================================
// MachineStatusUI.cs
// MachineBase.OnStatusChanged 이벤트를 구독해 경고등·진행 바를 갱신.
//
// 설비 오브젝트에 같이 붙여 두고 Initialize(machine) 만 호출하면 된다.
// 경고등 색, 아이콘은 Inspector에서 교체 가능.
// =====================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TIMEKOV.Factory
{
    public class MachineStatusUI : MonoBehaviour
    {
        [Header("경고등")]
        [SerializeField] private Image statusLight;
        [SerializeField] private Color colorDisconnected = Color.red;
        [SerializeField] private Color colorIdle         = Color.yellow;
        [SerializeField] private Color colorProcessing   = Color.green;
        [SerializeField] private Color colorOutputFull   = new Color(1f, 0.5f, 0f);

        [Header("진행 바 (선택)")]
        [SerializeField] private Slider progressBar;

        [Header("상태 텍스트 (선택)")]
        [SerializeField] private TextMeshProUGUI statusLabel;

        private ProcessingMachine _machine;

        // ---------------------------------------------------------------
        // 초기화 — 설비 Start() 에서 호출
        // ---------------------------------------------------------------
        public void Initialize(MachineBase machine)
        {
            machine.OnStatusChanged += Refresh;
            Refresh(machine.Status);

            // 진행 바는 ProcessingMachine 만 지원
            if (machine is ProcessingMachine pm) _machine = pm;
        }

        // ---------------------------------------------------------------
        // Unity Update: 진행 바 매 프레임 갱신
        // ---------------------------------------------------------------
        private void Update()
        {
            if (_machine != null && progressBar != null)
                progressBar.value = _machine.Progress;
        }

        // ---------------------------------------------------------------
        // 상태 변경 콜백
        // ---------------------------------------------------------------
        private void Refresh(MachineStatus status)
        {
            if (statusLight != null)
                statusLight.color = status switch
                {
                    MachineStatus.Disconnected => colorDisconnected,
                    MachineStatus.Idle        => colorIdle,
                    MachineStatus.Processing  => colorProcessing,
                    MachineStatus.OutputFull  => colorOutputFull,
                    _                        => Color.white,
                };

            if (statusLabel != null)
                statusLabel.text = status switch
                {
                    MachineStatus.Disconnected => "연결 끊김",
                    MachineStatus.Idle        => "대기 중",
                    MachineStatus.Processing  => "가동 중",
                    MachineStatus.OutputFull  => "출력 대기",
                    _                        => "",
                };
        }
    }
}
