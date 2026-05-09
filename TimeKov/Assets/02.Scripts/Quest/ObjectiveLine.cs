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
    [Tooltip("좌->우 노란색 sweep 효과용 오버레이. 없으면 sweep 생략하고 fade만.")]
    [SerializeField] Image yellowSweep;

    [Header("Vanish timing")]
    [Tooltip("좌->우 sweep 길이")]
    [SerializeField] float sweepDuration = 0.25f;
    [Tooltip("sweep peak 후 fade out 길이")]
    [SerializeField] float sweepFadeDuration = 0.2f;
    [Tooltip("sweep 끝난 후 collapse 시작까지 hold")]
    [SerializeField] float postSweepHold = 0.15f;
    [Tooltip("최종 height + alpha collapse 길이")]
    [SerializeField] float collapseDuration = 0.3f;

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

        // Phase 1 (0 ~ sweepDuration): 좌->우 노란색 sweep
        if (yellowSweep != null)
        {
            seq.Join(yellowSweep.DOFillAmount(1f, sweepDuration).SetEase(Ease.OutQuad));
            // alpha는 sweep 절반 시점까지 빠르게 올라감
            seq.Join(yellowSweep.DOFade(0.6f, sweepDuration * 0.5f).SetEase(Ease.OutQuad));
        }

        // Phase 2 (sweep 절반 시점): 체크된 박스 등장 + scale pop (빈 박스는 그대로 두고 위에 덮어 그림)
        seq.InsertCallback(sweepDuration * 0.5f, PopFilled);

        // Phase 3 (sweep 끝나면): yellow fade out + label dim
        if (yellowSweep != null)
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
