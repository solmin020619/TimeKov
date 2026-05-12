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
    [Tooltip("Title 라인 검정 배경박스. 슬라이드 인 시 좌→우 이동. 인스펙터에서 RectTransform 드래그.")]
    [SerializeField] RectTransform backgroundBox;
    [Tooltip("Title + ObjectiveList wrapper의 CanvasGroup. 슬라이드와 분리되어 자리에서 fade in.")]
    [SerializeField] CanvasGroup textGroup;

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
    [Tooltip("배경박스 좌→우 슬라이드 길이")]
    [SerializeField] float slideInDuration = 0.3f;
    [Tooltip("배경박스 슬라이드 시작 시 X 오프셋 (음수 = 좌측에서 들어옴)")]
    [SerializeField] float backgroundSlideOffsetX = -200f;
    [Tooltip("배경박스 슬라이드 시작 후 텍스트 fade in 시작까지 대기")]
    [SerializeField] float textFadeDelay = 0.15f;
    [Tooltip("텍스트 fade in 길이")]
    [SerializeField] float textFadeDuration = 0.25f;
    [Tooltip("완료 시퀀스 시작 전 hold (ObjectiveLine collapse 끝나길 기다림)")]
    [SerializeField] float completeStartDelay = 0.75f;
    [Tooltip("완료 시 위로 올라가며 사라지는 거리(px)")]
    [SerializeField] float completeRiseY = 20f;
    [Tooltip("완료 시 fade + rise 길이")]
    [SerializeField] float completeFadeDuration = 0.4f;

    QuestSO _quest;
    CategoryRuntime _rt;
    Sequence _alertPulseSeq;

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
            if (iconNormal != null)
            {
                iconNormal.SetActive(true);
                var ncg = iconNormal.GetComponent<CanvasGroup>();
                if (ncg != null) ncg.alpha = 1f;
            }
            return;
        }

        if (iconNormal != null) iconNormal.SetActive(false);
        iconAlert.SetActive(true);

        var tr = iconAlert.transform;
        tr.localScale = Vector3.one;
        var cg = iconAlert.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;

        // 펄스: 1 -> 1.15 -> 1 한 사이클 = alertPulseInterval * 2
        // 펄스 자체 완료 후 SwitchToNormal은 안전망 (외부 트리거 없을 때 폴백, 첫 퀘스트용)
        int loops = Mathf.Max(1, Mathf.RoundToInt(alertHoldDuration / Mathf.Max(0.05f, alertPulseInterval * 2f)));
        _alertPulseSeq = DOTween.Sequence().SetUpdate(true).SetLink(iconAlert);
        _alertPulseSeq.Append(tr.DOScale(1.15f, alertPulseInterval).SetEase(Ease.OutQuad));
        _alertPulseSeq.Append(tr.DOScale(1.0f, alertPulseInterval).SetEase(Ease.InQuad));
        _alertPulseSeq.SetLoops(loops);
        _alertPulseSeq.OnComplete(SwitchToNormal);
    }

    /// <summary>외부 트리거 (QuestPanelUI가 토스트 사라짐 시점에 호출). 펄스 안전망 Kill 후 즉시 전환.</summary>
    public void TriggerIconSwitchToNormal()
    {
        if (_alertPulseSeq != null && _alertPulseSeq.IsActive())
        {
            _alertPulseSeq.Kill();
            _alertPulseSeq = null;
        }
        SwitchToNormal();
    }

    void SwitchToNormal()
    {
        // iconAlert 없으면 normal만 즉시 활성
        if (iconAlert == null)
        {
            if (iconNormal != null)
            {
                iconNormal.SetActive(true);
                var ncg = iconNormal.GetComponent<CanvasGroup>();
                if (ncg != null) ncg.alpha = 1f;
            }
            return;
        }

        var alertCg = iconAlert.GetComponent<CanvasGroup>();
        if (alertCg == null) alertCg = iconAlert.AddComponent<CanvasGroup>();

        // iconNormal 준비 (alpha 0으로 미리 활성, fade in 대상)
        CanvasGroup normalCg = null;
        if (iconNormal != null)
        {
            normalCg = iconNormal.GetComponent<CanvasGroup>();
            if (normalCg == null) normalCg = iconNormal.AddComponent<CanvasGroup>();
            normalCg.alpha = 0f;
            iconNormal.SetActive(true);
        }

        // alert fade out → normal fade in 순차 (사용자 요구: "그 이후에" 원래 아이콘 Fade In)
        var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        seq.Append(alertCg.DOFade(0f, alertFadeDuration));
        seq.AppendCallback(() =>
        {
            iconAlert.SetActive(false);
            alertCg.alpha = 1f;   // 다음 등장 위해 리셋
        });
        if (normalCg != null)
            seq.Append(normalCg.DOFade(1f, alertFadeDuration));
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

    // 슬라이드 인. 배경박스만 좌→우 슬라이드, 텍스트는 자리에서 fade in (사진 4 패턴).
    // 높이는 CSF chain이 자동 결정.
    void SlideInAndActivate()
    {
        // root는 항상 보임 (페이드 X)
        var rootCg = GetComponent<CanvasGroup>();
        if (rootCg != null) rootCg.alpha = 1f;

        // 텍스트는 alpha 0에서 시작
        if (textGroup != null) textGroup.alpha = 0f;

        // 배경박스를 좌측 오프셋 위치로
        Vector2 bgRestPos = Vector2.zero;
        if (backgroundBox != null)
        {
            bgRestPos = backgroundBox.anchoredPosition;
            backgroundBox.anchoredPosition = bgRestPos + new Vector2(backgroundSlideOffsetX, 0f);
        }

        var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        // 배경박스: 좌→우 슬라이드
        if (backgroundBox != null)
            seq.Append(backgroundBox.DOAnchorPos(bgRestPos, slideInDuration).SetEase(Ease.OutCubic));
        else
            seq.AppendInterval(slideInDuration);

        // 텍스트: 슬라이드 시작 후 textFadeDelay 시점부터 fade in (자리에서)
        if (textGroup != null)
            seq.Insert(textFadeDelay, textGroup.DOFade(1f, textFadeDuration));

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
