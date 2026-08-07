// TopViewBannerLabel.cs
// 건축 모드 상단 배너("탑뷰 모드 나가기 [B]")의 문구와 폭을 관리한다.
//
// 하는 일 두 가지
//   1) 모드에 맞는 문구/키 표시  - 해제 모드(X)면 "해제 모드 [X]", 아니면 "탑뷰 모드 나가기 [B]"
//   2) 알약 폭을 내용물에 맞춰 자동 조절 - 문구 길이가 바뀌어도 여백이 항상 일정하다
//
// 2번이 중요한 이유: 문구를 고정 폭 알약에 넣으면 모드가 바뀌거나(글자 수 다름)
// 언어가 바뀔 때(영어는 훨씬 길다) 가운데가 휑해지거나 글자가 삐져나온다.
// 여기서는 반대로 내용물을 재서 알약을 늘렸다 줄인다.
//
// 레이아웃 규칙: [여백][문구][여백][키박스][여백]  - 빈 공간 셋이 전부 gap 으로 같다.

using UnityEngine;
using TMPro;

public class TopViewBannerLabel : MonoBehaviour
{
    [Header("참조 (탑뷰 배너 재생성 툴이 자동으로 물린다)")]
    [SerializeField] private RectTransform pill;        // 알약 배경
    [SerializeField] private TextMeshProUGUI label;     // 문구
    [SerializeField] private RectTransform keyBox;      // 키 박스
    [SerializeField] private TextMeshProUGUI keyText;   // 키 박스 안 글자

    [Header("여백")]
    [Tooltip("문구 좌우와 키박스 좌우에 똑같이 들어가는 간격. 이 값 하나로 알약 폭이 정해진다.")]
    [SerializeField] private float gap = 40f;
    [Tooltip("키박스의 '보이는' 가로 크기. 스프라이트에 투명 여백이 있어 RectTransform 폭과 다르다.")]
    [SerializeField] private float keyBoxVisibleWidth = 65.24f;

    [Header("문구")]
    [SerializeField] private string normalLabel   = "탑뷰 모드 나가기";
    [SerializeField] private string normalKey     = "B";
    [SerializeField] private string demolishLabel = "해제 모드";
    [SerializeField] private string demolishKey   = "X";

    private bool _demolish;

    private void OnEnable()
    {
        // 건축 모드에 들어갈 때 BuildManager 가 해제 모드를 항상 끄므로 false 로 시작하는 게 맞다.
        _demolish = false;
        BuildManager.OnDemolishModeChanged += OnDemolishChanged;
        Loc.OnLanguageChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        BuildManager.OnDemolishModeChanged -= OnDemolishChanged;
        Loc.OnLanguageChanged -= Apply;
    }

    private void OnDemolishChanged(bool on)
    {
        _demolish = on;
        Apply();
    }

    private void Apply()
    {
        if (label == null || keyText == null) return;

        label.text   = Loc.Get(_demolish ? demolishLabel : normalLabel);
        keyText.text = _demolish ? demolishKey : normalKey;

        Relayout();
    }

    private void Relayout()
    {
        if (pill == null || keyBox == null) return;

        // ★[08-07] TextAutoFit 이 이 라벨을 줄이지 못하게 한다.
        //   이 배너는 '글자에 맞춰 알약이 늘어나는' 구조라 글자를 줄이면 안 된다. 그런데
        //   예전엔 라벨 rect 를 authored 폭(240) 그대로 뒀더니, 프랑스어처럼 긴 문구에서
        //   TextAutoFit 이 "상자를 넘쳤다"고 보고 축소 + 말줄임까지 걸어버렸다.
        //   줄어든 preferredWidth 때문에 알약도 덜 늘어나 원래 의도가 깨진다.
        //   -> 아래에서 라벨 rect 를 글자 폭에 딱 맞춰준다. 넘치지 않으니 TextAutoFit 은
        //      건드릴 게 없고, 혹시 이미 줄여놨다면 여기서 원래 크기로 되돌린 뒤 잰다.
        if (label.enableAutoSizing)
        {
            label.enableAutoSizing = false;
            if (label.fontSizeMax > 0f) label.fontSize = label.fontSizeMax;
        }
        if (label.overflowMode == TMPro.TextOverflowModes.Ellipsis)
            label.overflowMode = TMPro.TextOverflowModes.Overflow;

        // preferredWidth 는 메시가 갱신돼 있어야 정확하다.
        label.ForceMeshUpdate();
        float labelW = label.preferredWidth;

        float pillW = gap + labelW + gap + keyBoxVisibleWidth + gap;

        pill.sizeDelta = new Vector2(pillW, pill.sizeDelta.y);
        if (transform is RectTransform selfRt)
            selfRt.sizeDelta = new Vector2(pillW, pill.sizeDelta.y);

        float left = -pillW * 0.5f;
        label.rectTransform.sizeDelta         = new Vector2(labelW, label.rectTransform.sizeDelta.y);
        label.rectTransform.anchoredPosition  = new Vector2(left + gap + labelW * 0.5f, 0f);
        keyBox.anchoredPosition               = new Vector2(left + gap + labelW + gap + keyBoxVisibleWidth * 0.5f, 0f);
    }

#if UNITY_EDITOR
    // 인스펙터에서 gap 을 만지면 바로 보이게(에디터 전용).
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (label != null && keyText != null) Relayout();
    }
#endif
}
