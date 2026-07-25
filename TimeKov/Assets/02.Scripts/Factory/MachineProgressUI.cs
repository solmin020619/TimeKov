// =====================================================================
// MachineProgressUI.cs
// 설비 진행 상황 월드 스페이스 UI
// 구버전 DataStore.GetItem → GameDataUtility.GetItem 으로 교체
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

        [Header("월드 스페이스 Canvas 요소")]
        public TextMeshProUGUI statusText;
        public Slider progressBar;
        public GameObject uiRoot;

        private Camera _cam;

        private void Awake()
        {
            _cam = Camera.main;
            if (uiRoot != null) uiRoot.SetActive(false);
        }

        private void LateUpdate()
        {
            if (machine == null) return;

            if (_cam != null && uiRoot != null)
                uiRoot.transform.forward = _cam.transform.forward;

            bool show = machine.Status == MachineStatus.Processing
                     || machine.Status == MachineStatus.OutputReady;

            if (uiRoot != null && uiRoot.activeSelf != show)
                uiRoot.SetActive(show);

            if (!show) return;

            if (progressBar != null)
                progressBar.value = machine.Progress;

            if (statusText == null) return;

            if (machine.Status == MachineStatus.Processing)
            {
                string outName = "";
                if (machine.ActiveRecipe != null && machine.ActiveRecipe.outputs.Length > 0)
                {
                    int id = machine.ActiveRecipe.outputs[0].itemId;
                    // 구버전: DataStore.GetItem(id) → GameDataUtility.GetItem(id)
                    var itemData = GameDataUtility.GetItem(id);
                    outName = itemData?.itemName ?? id.ToString();
                }
                statusText.text = $"[{machine.MachineName}] 제작 중\n→ {outName} {(machine.Progress * 100f):F0}%";
            }
            else if (machine.Status == MachineStatus.OutputReady)
            {
                // OutputBuffer에서 첫 번째 아이템명·수량을 표시
                string outName = "";
                int outAmt = 0;
                foreach (var kv in machine.OutputBuffer.Stock)
                {
                    if (kv.Value <= 0) continue;
                    var itemData = GameDataUtility.GetItem(kv.Key);
                    outName = itemData?.itemName ?? kv.Key.ToString();
                    outAmt  = kv.Value;
                    break;
                }
                statusText.text = string.IsNullOrEmpty(outName)
                    ? $"[{machine.MachineName}] 완료!\nF키로 회수"
                    : $"[{machine.MachineName}] 완료!\n{outName} x{outAmt}  F키로 회수";
            }
        }
    }
}