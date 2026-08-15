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

        [Tooltip("이 버튼 '왼쪽'에 붙는 형제 버튼(오른쪽 정렬 쌍). 폭이 바뀌면 같이 밀어준다.\n" +
                 "★없으면 형제 위치가 고정값으로 남아, 이 버튼 폭이 예상보다 커질 때 서로 겹친다.")]
        public RectTransform pushLeft;
        [Tooltip("pushLeft 형제와의 간격.")]
        public float leftGap = 22f;

        [Header("여백")]
        public float minWidth;          // 초기화/적용처럼 크기를 맞춰야 하는 버튼용
        public float leftPad  = 32f;    // 좌측 텍스트 시작 여백
        public float textGap  = 24f;    // 텍스트와 원형 아이콘 사이
        public float iconW    = 42f;    // 원형 아이콘 지름
        public float iconGap  = 12f;
        public float rightPad = 10f;
        public float followGap = 30f;

        void OnEnable()
        {
            Fit();
            // 라벨이 번역되면 길이가 달라지므로 다시 재야 한다.
            //   ★설정창 자신이 언어를 바꾸는 창이라, 이게 없으면 언어를 고른 그 순간부터
            //     닫았다 열기 전까지 글자가 버튼 밖으로 삐져나온 채로 남는다.
            if (Application.isPlaying) Loc.OnLanguageChanged += Fit;
        }

        void OnDisable()
        {
            if (Application.isPlaying) Loc.OnLanguageChanged -= Fit;
        }

        bool _fitting;   // 체인이 서로를 부르는 구성에서도 무한 재귀에 빠지지 않게 하는 가드

        public void Fit()
        {
            if (label == null || _fitting) return;
            _fitting = true;
            try { FitInternal(); }
            finally { _fitting = false; }
        }

        void FitInternal()
        {

            // 아틀라스/레이아웃을 확정시킨 뒤 재야 정확한 폭이 나온다.
            label.ForceMeshUpdate();
            float textW = label.GetPreferredValues(label.text).x;

            // 폰트 아틀라스가 아직 준비되지 않으면 0에 가까운 값이 나온다. 그대로 쓰면
            // 버튼이 글자보다 작게 잡혀 라벨이 원형 아이콘에 파묻히거나 잘린다
            // (minWidth 가 없는 '메인 메뉴로 돌아가기' 버튼에서 특히 심하다).
            // 글자 수로 넉넉히 추정해 둔다 — CategoryFilterUI 가 쓰는 것과 같은 안전장치.
            if (textW < 1f && !string.IsNullOrEmpty(label.text))
                textW = label.text.Length * Mathf.Max(label.fontSize, 16f) * 1.05f;

            float w = Mathf.Max(minWidth, leftPad + textW + textGap + iconW + iconGap + rightPad);
            var rt = (RectTransform)transform;
            if (!Mathf.Approximately(rt.sizeDelta.x, w))
                rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);

            // ★내 위치를 기준으로 민다(0 기준이 아니라). 그래야 좌측에 버튼을 여러 개 이어 붙일 수 있다.
            //   맨 왼쪽 버튼은 x=0 이라 예전 동작과 결과가 같다.
            if (follow)
            {
                follow.anchoredPosition = new Vector2(rt.anchoredPosition.x + w + followGap,
                                                      follow.anchoredPosition.y);

                // 뒤에 또 폭이 변하는 버튼이 이어지면 그쪽도 다시 맞춘다(체인).
                // 언어가 바뀔 때 각자의 OnEnable/Fit 순서에 기대지 않게 하는 장치다.
                var next = follow.GetComponent<FooterButtonFit>();
                if (next != null && next != this) next.Fit();
            }

            // 오른쪽 정렬 쌍(적용 ↔ 초기화): 내 실제 폭만큼 형제를 왼쪽으로 민다.
            //   위치를 고정값으로 두면 폭이 minWidth 를 넘는 순간 두 버튼이 겹친다.
            if (pushLeft)
                pushLeft.anchoredPosition = new Vector2(-(w + leftGap), pushLeft.anchoredPosition.y);
        }
    }
}
