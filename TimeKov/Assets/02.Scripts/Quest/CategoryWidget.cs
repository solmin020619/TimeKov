using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class CategoryWidget : MonoBehaviour
{
    [SerializeField] TMP_Text categoryTitle;
    [SerializeField] Transform questSlot;
    [SerializeField] QuestEntry questEntryPrefab;
    [SerializeField] CanvasGroup categoryGroup;

    CategoryRuntime _rt;
    QuestEntry _currentEntry;

    public void Init(CategoryRuntime rt)
    {
        _rt = rt;
        categoryTitle.text = rt.data.title;
    }

    public void ShowQuest(QuestSO q)
    {
        // 매니저가 시퀀스 후에 Present하므로 _currentEntry는 항상 null이어야 함
        if (_currentEntry != null)
        {
            Debug.LogWarning($"[CategoryWidget] {_rt.data.id}: ShowQuest while previous entry exists");
            Destroy(_currentEntry.gameObject);
        }

        _currentEntry = Instantiate(questEntryPrefab, questSlot);
        _currentEntry.PlaySlideIn(q, _rt);
    }

    public void PlayCompleteAndRemove(Action onFinished)
    {
        if (_currentEntry == null) { onFinished?.Invoke(); return; }

        var entry = _currentEntry;
        _currentEntry = null;

        entry.PlayCompleteSequence(() =>
        {
            if (entry != null) Destroy(entry.gameObject);
            onFinished?.Invoke();
        });
    }

    /// <summary>WaitForUIComplete 타임아웃 시 호출 — 진행 중 코루틴/엔트리 강제 정리</summary>
    public void ForceCleanup()
    {
        StopAllCoroutines();
        if (_currentEntry != null) { Destroy(_currentEntry.gameObject); _currentEntry = null; }
    }

    [Header("Fade timing")]
    [Tooltip("카테고리 완료 후 fade 시작까지 대기 시간 (사용자가 완료 인지)")]
    [SerializeField] float fadeOutHoldTime = 1.5f;
    [Tooltip("alpha 1→0 fade 길이")]
    [SerializeField] float fadeOutDuration = 0.5f;

    public void FadeOutCategory()
    {
        // hold → fade alpha 0 → SetActive(false). 부모 VLG가 자동 reflow.
        var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        seq.AppendInterval(fadeOutHoldTime);
        if (categoryGroup) seq.Append(categoryGroup.DOFade(0f, fadeOutDuration));
        seq.OnComplete(() => gameObject.SetActive(false));
    }

    /// <summary>
    /// 향후 재활성화용 — 특정 영역 진입 시 등 외부 트리거로 호출.
    /// SetActive 복원 + alpha 1. 단, CategoryRuntime의 activeQuestIndex 등 데이터는
    /// QuestManager 측에서 별도 리셋 필요 (이 메서드는 UI 표시만 담당).
    /// </summary>
    public void ShowCategory()
    {
        gameObject.SetActive(true);
        if (categoryGroup) categoryGroup.alpha = 1f;
    }
}
