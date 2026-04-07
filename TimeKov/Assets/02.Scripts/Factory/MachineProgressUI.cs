// =====================================================================
// MachineProgressUI.cs
// 설비 오브젝트 위에 떠있는 월드 스페이스 진행 표시.
// 항상 카메라를 향하며, 가공 중일 때만 표시한다.
// =====================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TIMEKOV.Factory
{
    public class MachineProgressUI : MonoBehaviour
    {
        [Header("이 컴포넌트가 붙은 설비")]
        public ProcessingMachine machine;

        [Header("설비 이름 (UI 표시용)")]
        public string machineName = "설비";

        [Header("월드 스페이스 Canvas 안의 요소들")]
        public TextMeshProUGUI statusText;
        public Slider          progressBar;
        public GameObject      uiRoot; // 평소에 숨길 루트 오브젝트

        private Camera _cam;

        private void Awake()
        {
            _cam = Camera.main;
            if (uiRoot != null) uiRoot.SetActive(false);
        }

        private void LateUpdate()
        {
            if (machine == null) return;

            // 카메라 방향
            if (_cam != null && uiRoot != null)
                uiRoot.transform.forward = _cam.transform.forward;

            bool show = machine.Status == MachineStatus.Processing
                     || machine.Status == MachineStatus.OutputReady;

            if (uiRoot != null && uiRoot.activeSelf != show)
                uiRoot.SetActive(show);

            if (!show) return;

            // 진행 바
            if (progressBar != null)
                progressBar.value = machine.Progress;

            // 상태 텍스트
            if (statusText != null)
            {
                if (machine.Status == MachineStatus.Processing)
                {
                    string outName = "";
                    if (machine.ActiveRecipe != null && machine.ActiveRecipe.outputs.Length > 0)
                    {
                        int id   = machine.ActiveRecipe.outputs[0].itemId;
                        var item = DataManager.Instance?.GetItem(id);
                        outName  = item != null ? item.itemName : id.ToString();
                    }
                    statusText.text = $"[{machineName}] 제작 중\n→ {outName} {(machine.Progress * 100f):F0}%";
                }
                else if (machine.Status == MachineStatus.OutputReady)
                {
                    statusText.text = $"[{machineName}] 완료!\nF키로 회수";
                }
            }
        }
    }
}
