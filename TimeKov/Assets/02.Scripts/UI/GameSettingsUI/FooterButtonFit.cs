// FooterButtonFit.cs — 하단 버튼 폭을 라벨 길이에 맞춘다.
//
// 왜 컴포넌트로 두는가:
//   버튼 폭은 TMP_Text.GetPreferredValues()로 계산하는데, 에디터 베이크 시점에는
//   폰트 아틀라스·레이아웃이 아직 준비되지 않아 실제보다 작은 값이 나온다.
//   그 값이 씬에 박제되면 실행 시 글자가 버튼 밖으로 삐져나온다.
//   실행할 때 스스로 다시 재면 베이크 시점의 오차와 무관해지고,
//   나중에 라벨을 번역해 길이가 달라져도 따라간다.
//
// (MonoBehaviour는 클래스명과 파일명이 같아야 씬/프리팹에 저장된다)
using UnityEngine;
using TMPro;

namespace GameSettingsUI
{
    [ExecuteAlways]
    public class FooterButtonFit : MonoBehaviour
    {
        [Header("참조")]
        public TMP_Text label;
        [Tooltip("이 버튼 오른쪽에 이어 붙는 요소(안내 문구). 폭이 바뀌면 같이 밀어준다.")]
        public RectTransform follow;

        [Header("여백")]
        public float minWidth;          // 초기화/적용처럼 크기를 맞춰야 하는 버튼용
        public float leftPad  = 32f;    // 좌측 텍스트 시작 여백
        public float textGap  = 24f;    // 텍스트와 원형 아이콘 사이
        public float iconW    = 42f;    // 원형 아이콘 지름
        public float iconGap  = 12f;
        public float rightPad = 10f;
        public float followGap = 30f;

        void OnEnable() { Fit(); }

        public void Fit()
        {
            if (label == null) return;

            // 아틀라스/레이아웃을 확정시킨 뒤 재야 정확한 폭이 나온다.
            label.ForceMeshUpdate();
            float textW = label.GetPreferredValues(label.text).x;

            float w = Mathf.Max(minWidth, leftPad + textW + textGap + iconW + iconGap + rightPad);
            var rt = (RectTransform)transform;
            if (!Mathf.Approximately(rt.sizeDelta.x, w))
                rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);

            if (follow)
                follow.anchoredPosition = new Vector2(w + followGap, follow.anchoredPosition.y);
        }
    }
}
