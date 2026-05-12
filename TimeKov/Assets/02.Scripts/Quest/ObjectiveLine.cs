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

    void OnProgress(float _) => Refresh();

    void OnDone(ObjectiveSO _)
    {
        PlayCompletionSequence();
    }

    void PlayCompletionSequence()
    {
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
        labelText.text = _o.GetDisplayLabel();
    }

    void OnDestroy()
    {
        if (_o == null) return;
        _o.OnProgressChanged -= OnProgress;
        _o.OnCompleted -= OnDone;
    }
}
