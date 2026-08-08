// SettingsPanelRefs.cs
// ============================================================================
// 씬에 구운 계층에서, 실행 시 다시 찾아야 하는 참조들을 담아두는 컴포넌트.
//
// 위젯(Dropdown/Slider3/KeyRow/SegToggle)은 그 자체가 MonoBehaviour라
// GetComponentsInChildren으로 모을 수 있지만, 아래 것들은 컴포넌트가 아니라
// "특정 역할을 하는 오브젝트"라서 이름에 의존하지 않으려면 참조로 남겨야 한다.
//
// 런타임 빌드 경로에서도 빌더가 이 컴포넌트를 채워두므로, 두 경로가 같은 방식으로 동작한다.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameSettingsUI
{
    public class SettingsPanelRefs : MonoBehaviour
    {
        [Header("루트/스크롤")]
        public RectTransform root;
        public RectTransform bodyViewport;   // 드롭다운 펼침 방향 판단 기준(RectMask2D 영역)

        [Header("탭")]
        public RectTransform tabIndicator;   // 노란 알약
        public Image[] tabIcons = new Image[3];
        public Btn[]   tabButtons = new Btn[3];

        [Header("탭별 패널")]
        public RectTransform displayPanel, audioPanel, controlsPanel;
        public CanvasGroup   displayCG,    audioCG,    controlsCG;

        [Header("배경 테마 (블러 유무에 따라 실행 시 다시 칠한다)")]
        public Image scrim;                                    // 배경 막
        public Image tabBarBG;                                 // 상단 탭 컨테이너
        public List<Image> rowBackgrounds = new List<Image>();  // 설정 행 배경

        [Header("기타")]
        public GameObject controlsHint;      // 조작 탭에서만 보이는 안내 문구
        public Image      applyBG;           // '설정 적용' 버튼 배경 (적용 시 노란 플래시)
        public GameObject warningModal;      // 미적용 변경 경고창

        // 배열은 길이만 보면 안 된다. 굽고 나서 계층을 손대면 원소가 Missing으로 비어
        // 길이는 3인데 내용이 null인 상태가 되고, 그대로 채택하면 실행 중 NPE가 난다.
        public bool IsComplete =>
            root && bodyViewport && tabIndicator &&
            displayPanel && audioPanel && controlsPanel &&
            displayCG && audioCG && controlsCG &&
            controlsHint && applyBG && warningModal &&
            scrim && tabBarBG &&
            rowBackgrounds != null && rowBackgrounds.Count > 0 &&
            AllPresent(tabIcons) && AllPresent(tabButtons);

        static bool AllPresent<T>(T[] arr) where T : Object
        {
            if (arr == null || arr.Length != 3) return false;
            foreach (var e in arr) if (!e) return false;
            return true;
        }
    }
}
