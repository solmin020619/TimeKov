// =====================================================================
// WindowOpenPolicy.cs
// "이 창이 열릴 때 같이 열거나 닫을 다른 창" 정책을 데이터로 분리
//
// 예시:
//   - 인벤(Inventory) 열 때 퀘스트 패널(QuestPanel)도 같이 표시 → alsoOpen
//   - 공장(MachineUI) 열 때 인벤(Inventory)은 닫기 → closeOthers
//
// 룰을 코드에 박지 않고 SO에서 인스펙터로 편집해 디자이너/기획자가 직접 조정 가능.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TimeKov.UI
{
    [CreateAssetMenu(fileName = "WindowOpenPolicy", menuName = "TIMEKOV/UI/Window Open Policy")]
    public class WindowOpenPolicy : ScriptableObject
    {
        [System.Serializable]
        public class Rule
        {
            [Tooltip("이 창이 열릴 때 룰 적용 (WindowId 일치)")]
            public string targetWindowId;

            [Tooltip("이 창과 같이 열어야 하는 창들의 ID")]
            public List<string> alsoOpen = new();

            [Tooltip("이 창이 열리면 닫혀야 하는 창들의 ID")]
            public List<string> closeOthers = new();
        }

        [Tooltip("창 열기 룰 목록. targetWindowId가 중복되면 첫 번째만 사용됨.")]
        public List<Rule> rules = new();

        public Rule FindRule(string windowId)
        {
            if (string.IsNullOrEmpty(windowId)) return null;
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] != null && rules[i].targetWindowId == windowId)
                    return rules[i];
            }
            return null;
        }
    }
}
