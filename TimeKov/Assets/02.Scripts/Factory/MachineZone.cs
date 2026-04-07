// =====================================================================
// MachineZone.cs
// 설비 프리팹에 붙이는 컴포넌트.
// Trigger Collider 영역 안에 플레이어가 들어오면
// MachineInteraction이 이 컴포넌트를 찾아 설비 참조를 얻는다.
//
// 설치 방법:
//   설비 프리팹 루트(또는 자식)에 Trigger Collider + MachineZone 추가.
//   machine 필드에 같은 오브젝트의 ProcessingMachine 드래그.
// =====================================================================

using UnityEngine;

namespace TIMEKOV.Factory
{
    public class MachineZone : MonoBehaviour
    {
        [Tooltip("이 설비의 ProcessingMachine 컴포넌트")]
        public ProcessingMachine machine;

        [Tooltip("UI에 표시할 설비 이름")]
        public string machineName = "설비";

        private void Reset()
        {
            // 같은 오브젝트의 ProcessingMachine 자동 연결
            machine = GetComponent<ProcessingMachine>();

            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }
    }
}
