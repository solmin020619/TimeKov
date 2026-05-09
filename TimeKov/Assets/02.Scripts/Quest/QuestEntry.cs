using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 모든 퀘스트는 헤더(제목) + ObjectiveLine 인덴트 구조로 표시.
/// 1 objective든 N objective든 동일 시각 (엔드필드 패턴, 사진 5 기준).
/// </summary>
public class QuestEntry : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] TMP_Text title;
    [SerializeField] Transform objectiveList;
    [SerializeField] ObjectiveLine objectiveLinePrefab;

    [Header("Highlight icons")]
    [Tooltip("평상시 아이콘 (큰 제목 옆, 동그라미 sprite). 인스펙터에서 드래그.")]
    [SerializeField] GameObject iconNormal;
    [Tooltip("!!! 새 퀘스트 등장 순간 아이콘 (큰 제목 옆). sprite는 인스펙터에서 드래그.")]
    [SerializeField] GameObject iconAlert;

    [Header("Highlight timing")]
    [Tooltip("새 퀘스트 등장 시 !!! 펄스 유지 시간 후 평상시 아이콘으로 전환")]
    [SerializeField] float alertHoldDuration = 1.5f;
    [Tooltip("!!! 펄스 1회 반주기. scale 1->1.15->1 한 사이클 절반")]
    [SerializeField] float alertPulseInterval = 0.25f;
    [Tooltip("!!! 에서 평상시 아이콘으로 전환 시 fade 길이")]
    [SerializeField] float alertFadeDuration = 0.2f;

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip completeSfx;

    [Header("Animation timing")]
    [Tooltip("슬라이드 인 fade 길이")]
    [SerializeField] float slideInDuration = 0.3f;
    [Tooltip("완료 시퀀스 시작 전 hold (ObjectiveLine collapse 끝나길 기다림)")]
    [SerializeField] float completeStartDelay = 0.75f;
    [Tooltip("완료 시 위로 올라가며 사라지는 거리(px)")]
    [SerializeField] float completeRiseY = 20f;
    [Tooltip("완료 시 fade + rise 길이")]
    [SerializeField] float completeFadeDuration = 0.4f;

    QuestSO _quest;
    CategoryRuntime _rt;

    public void PlaySlideIn(QuestSO q, CategoryRuntime rt)
    {
        _quest = q; _rt = rt;

        if (title != null) title.text = q.title;

        foreach (var o in rt.activeObjectives)
        {
            var line = Instantiate(objectiveLinePrefab, objectiveList);
            line.Bind(o);
            // ObjectiveLine이 자체적으로 OnDone 처리 (sweep + 체크 + collapse)
        }

        // Nested CSF chain은 동적으로 추가된 자식에 대해 초기 layout pass 누락 가능
        // 부모 체인 모두 강제 rebuild. 한 번만, init 시점.
        RebuildLayoutChainUp();

        // 시퀀스 C: 새 퀘스트 등장 시 !!! 펄스, 일정 시간 후 평상시 아이콘으로 전환
        StartHighlightSequence();

        SlideInAndActivate();
    }

    void StartHighlightSequence()
    {
        if (iconAlert == null)
        {
            if (iconNormal != null) iconNormal.SetActive(true);
            return;
        }

        if (iconNormal != null) iconNormal.SetActive(false);
        iconAlert.SetActive(true);

        var tr = iconAlert.transform;
        tr.localScale = Vector3.one;
        var cg = iconAlert.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;

        // 펄스: 1 -> 1.15 -> 1 한 사이클 = alertPulseInterval * 2
        int loops = Mathf.Max(1, Mathf.RoundToInt(alertHoldDuration / Mathf.Max(0.05f, alertPulseInterval * 2f)));
        var pulse = DOTween.Sequence().SetUpdate(true).SetLink(iconAlert);
        pulse.Append(tr.DOScale(1.15f, alertPulseInterval).SetEase(Ease.OutQuad));
        pulse.Append(tr.DOScale(1.0f, alertPulseInterval).SetEase(Ease.InQuad));
        pulse.SetLoops(loops);
        pulse.OnComplete(SwitchToNormal);
    }

    void SwitchToNormal()
    {
        if (iconAlert == null)
        {
            if (iconNormal != null) iconNormal.SetActive(true);
            return;
        }

        var cg = iconAlert.GetComponent<CanvasGroup>();
        if (cg == null) cg = iconAlert.AddComponent<CanvasGroup>();

        cg.DOFade(0f, alertFadeDuration).SetUpdate(true).SetLink(iconAlert).OnComplete(() =>
        {
            iconAlert.SetActive(false);
            cg.alpha = 1f;
            if (iconNormal != null) iconNormal.SetActive(true);
        });
    }

    void RebuildLayoutChainUp()
    {
        RectTransform cur = transform as RectTransform;
        int safety = 20;
        while (cur != null && safety-- > 0)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(cur);
            cur = cur.parent as RectTransform;
        }
    }

    // 슬라이드 인. alpha fade. 높이는 CSF chain이 자동 결정.
    void SlideInAndActivate()
    {
        var cg = GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 0f;

        var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        if (cg != null) seq.Append(cg.DOFade(1f, slideInDuration));
        seq.OnComplete(() => QuestManager.Instance.ActivateObjectives(_rt));
    }

    public void PlayCompleteSequence(Action onDone)
    {
        var cg = GetComponent<CanvasGroup>();
        var rt = transform as RectTransform;
        Vector2 startPos = rt != null ? rt.anchoredPosition : Vector2.zero;

        var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        // ObjectiveLine sweep+pop+collapse 끝나길 기다림 (약 0.7~0.9s)
        seq.AppendInterval(completeStartDelay);
        seq.AppendCallback(() =>
        {
            if (audioSource && completeSfx) audioSource.PlayOneShot(completeSfx);
        });

        // 위로 올라가며 fade. 사진 4 패턴 (내용이 위로 흡수되며 사라짐)
        if (cg != null)
            seq.Append(cg.DOFade(0f, completeFadeDuration));
        else
            seq.AppendInterval(completeFadeDuration);

        if (rt != null)
            seq.Join(rt.DOAnchorPos(startPos + new Vector2(0f, completeRiseY), completeFadeDuration)
                       .SetEase(Ease.OutQuad));

        seq.OnComplete(() => onDone?.Invoke());
    }
}
