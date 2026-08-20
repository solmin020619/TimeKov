using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObjectiveLine : MonoBehaviour
{
    [SerializeField] TMP_Text labelText;
    [Tooltip("평상시 표시되는 빈 체크박스. sprite는 인스펙터에서 드래그.")]
    [SerializeField] Image checkBoxEmpty;
    [Tooltip("Objective 완료 시 등장하는 체크된 박스. sprite는 인스펙터에서 드래그. 평상시 비활성.")]
    [SerializeField] Image checkBoxFilled;
    [Tooltip("좌->우 노란색 sweep 효과 fallback (Image+sprite). Sweep Effect Prefab 비어있을 때만 사용.")]
    [SerializeField] Image yellowSweep;
    [Tooltip("외부 에셋에서 만든 sweep 효과 prefab. 슬롯에 드래그하면 Image 기반 sweep 대신 이게 사용됨.\n" +
             "완료 시점에 ObjectiveLine 자식으로 Instantiate → Sweep Effect Duration 후 Destroy.")]
    [SerializeField] GameObject sweepEffectPrefab;
    [Tooltip("sweep prefab 인스턴스가 살아있는 시간. 그 후 Destroy.")]
    [SerializeField] float sweepEffectDuration = 1.0f;

    [Header("Vanish timing")]
    [Tooltip("좌->우 sweep 길이")]
    [SerializeField] float sweepDuration = 0.6f;
    [Tooltip("sweep peak 후 fade out 길이")]
    [SerializeField] float sweepFadeDuration = 0.4f;
    [Tooltip("sweep 끝난 후 collapse 시작까지 hold")]
    [SerializeField] float postSweepHold = 0.3f;
    [Tooltip("최종 height + alpha collapse 길이")]
    [SerializeField] float collapseDuration = 0.5f;
    [Tooltip("sweep 최대 알파 (강도). 0~1, 클수록 진함")]
    [SerializeField] float sweepPeakAlpha = 1.0f;

    ObjectiveSO _o;

    public void Bind(ObjectiveSO o)
    {
        _o = o;
        _o.OnProgressChanged += OnProgress;
        _o.OnCompleted += OnDone;
        Loc.OnLanguageChanged += Refresh;
        Refresh();

        if (checkBoxEmpty)
        {
            checkBoxEmpty.enabled = true;
            checkBoxEmpty.transform.localScale = Vector3.one;
        }
        if (checkBoxFilled)
        {
            checkBoxFilled.enabled = false;
            checkBoxFilled.transform.localScale = Vector3.zero;
        }
        if (yellowSweep)
        {
            yellowSweep.fillAmount = 0f;
            var c = yellowSweep.color; c.a = 0f; yellowSweep.color = c;
        }
    }

    void OnProgress(float _)
    {
        Refresh();
        GameUIController.Instance?.PulseQuestTracker();   // UI 열린 중 목표 진행 시 트래커 잠깐 팝
    }

    void OnDone(ObjectiveSO _)
    {
        GameUIController.Instance?.PulseQuestTracker();   // 목표 완료 시 트래커 잠깐 팝

        // InfoMessage(안내 전용) Objective는 collapse/sweep 없이 체크박스만 표시.
        // 다른 Objective가 같은 Quest 안에 있으면 그 줄들 사이 안내 텍스트 계속 유지.
        if (_o is InfoMessageObjective)
        {
            PopFilled();
            return;
        }
        PlayCompletionSequence();
    }

    void PlayCompletionSequence()
    {
        _collapsing = true;   // 이 시점부터 높이는 연출이 소유한다(FitHeightToText 가 끼어들면 안 됨)
        var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        // Phase 1: sweep 효과. prefab 있으면 그것 우선, 없으면 Image fallback.
        bool useSweepPrefab = sweepEffectPrefab != null;
        if (useSweepPrefab)
        {
            var effect = Instantiate(sweepEffectPrefab, transform);
            // UI prefab이면 ObjectiveLine 전체 stretch로 (사용자 prefab 어떤 anchor든 line 전체 덮음)
            var effectRT = effect.GetComponent<RectTransform>();
            if (effectRT != null)
            {
                effectRT.anchorMin = Vector2.zero;
                effectRT.anchorMax = Vector2.one;
                effectRT.offsetMin = Vector2.zero;
                effectRT.offsetMax = Vector2.zero;
                effectRT.localScale = Vector3.one;
            }
            Destroy(effect, sweepEffectDuration);
        }
        else if (yellowSweep != null)
        {
            seq.Join(yellowSweep.DOFillAmount(1f, sweepDuration).SetEase(Ease.OutQuad));
            seq.Join(yellowSweep.DOFade(sweepPeakAlpha, sweepDuration * 0.5f).SetEase(Ease.OutQuad));
        }

        // Phase 2 (sweep 절반 시점): 체크된 박스 등장 + scale pop
        seq.InsertCallback(sweepDuration * 0.5f, PopFilled);

        // Phase 3: Image sweep fade out (prefab 안 쓸 때만) + label dim
        if (!useSweepPrefab && yellowSweep != null)
            seq.Insert(sweepDuration, yellowSweep.DOFade(0f, sweepFadeDuration));
        if (labelText != null)
            seq.Insert(sweepDuration * 0.7f, labelText.DOFade(0.4f, sweepFadeDuration));

        // Phase 4 (postSweepHold 후): 전체 fade + height collapse
        seq.AppendInterval(postSweepHold);

        var cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        seq.Append(cg.DOFade(0f, collapseDuration));

        var le = GetComponent<LayoutElement>();
        if (le != null)
        {
            seq.Join(DOTween.To(
                () => le.preferredHeight,
                v => le.preferredHeight = v,
                0f, collapseDuration));
        }
    }

    void PopFilled()
    {
        if (checkBoxFilled == null) return;

        // 빈 박스 숨김 → 체크 아이콘으로 교체 (둘 다 겹쳐 보이는 거 방지)
        if (checkBoxEmpty != null) checkBoxEmpty.enabled = false;

        checkBoxFilled.enabled = true;
        var tr = checkBoxFilled.transform;
        tr.localScale = Vector3.zero;

        var pop = DOTween.Sequence().SetUpdate(true).SetLink(checkBoxFilled.gameObject);
        pop.Append(tr.DOScale(1.2f, 0.12f).SetEase(Ease.OutQuad));
        pop.Append(tr.DOScale(1.0f, 0.08f).SetEase(Ease.InQuad));
    }

    void Refresh()
    {
        if (_o == null || labelText == null) return;
        string lbl = _o.GetDisplayLabel();
        labelText.text = Loc.Get(lbl);

        // 라벨 비어있는 Objective는 줄 자체를 숨김 (인식만 하고 UI 표시 안 함).
        // → 한 퀘스트에 여러 Objective 두되 통합 라벨 하나만 표시하고 싶을 때 사용.
        bool hide = string.IsNullOrWhiteSpace(lbl);
        if (gameObject.activeSelf == hide) gameObject.SetActive(!hide);

        if (!hide) FitHeightToText();   // 글자를 줄이지 말고 줄 높이를 늘린다
    }

    // ── 줄 높이를 글자 줄 수에 맞춘다 ────────────────────────────────
    // 왜: 프리팹의 LayoutElement.preferredHeight 가 32 고정이라 두 줄짜리 문구가 들어오면
    //   높이가 안 늘고, 대신 전역 TextAutoFit 이 글자를 줄여서 우겨넣는다.
    //   그래서 문구 길이에 따라 퀘스트마다 글자 크기가 제각각으로 보였다.
    //   ★로컬라이징하면 EN/FR 이 더 길어져 이 현상이 심해진다.
    //   => 글자 크기는 프리팹 값(18) 그대로 두고 '줄'이 늘어나게 한다.
    //
    // ★★함정(한 번 당했다): Refresh() 시점에 폭을 재면 안 된다.
    //   QuestEntry 의 레이아웃 그룹이 ChildControlWidth=1 이라 줄 폭을 '런타임에' 넓혀준다.
    //   Instantiate 직후엔 아직 프리팹 폭(240)이라 그걸로 재면 한 줄짜리도 두 줄로 계산돼
    //   모든 줄이 부풀고 줄 간격이 벌어진다(종욱 스크린샷). 실제 폭이 잡힌 뒤에 재야 한다.
    //   => OnRectTransformDimensionsChange = 폭이 확정/변경될 때마다 오는 콜백. 여기서 잰다.
    const float MinLineHeight = 32f;   // 체크박스 높이. 한 줄짜리는 기존과 완전히 같게 보인다
    const float LineVPadding  = 6f;

    bool _collapsing;   // 완료 연출이 높이를 0 으로 트윈하는 중 - 그때는 건드리면 안 된다

    void OnRectTransformDimensionsChange() => FitHeightToText();

    void FitHeightToText()
    {
        if (_collapsing || labelText == null || !isActiveAndEnabled) return;

        var le = GetComponent<LayoutElement>();
        if (le == null) return;

        float w = labelText.rectTransform.rect.width;
        if (w <= 1f) return;   // 아직 레이아웃 전. 폭이 잡히면 이 콜백이 다시 온다

        float want = Mathf.Max(MinLineHeight, labelText.GetPreferredValues(labelText.text, w, 0f).y + LineVPadding);

        // 값이 실제로 달라질 때만 쓴다. 매번 대입하면 레이아웃 -> 콜백 -> 레이아웃 무한루프가 된다.
        if (Mathf.Abs(le.preferredHeight - want) < 0.5f) return;
        le.preferredHeight = want;
    }

    void OnDestroy()
    {
        if (_o != null)
        {
            _o.OnProgressChanged -= OnProgress;
            _o.OnCompleted -= OnDone;
        }
        Loc.OnLanguageChanged -= Refresh;
    }
}
