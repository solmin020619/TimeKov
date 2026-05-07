using System;
using System.Collections;
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
    bool _isPlayingComplete;

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

        _isPlayingComplete = true;
        var entry = _currentEntry;
        _currentEntry = null;

        entry.PlayCompleteSequence(() =>
        {
            if (entry != null) Destroy(entry.gameObject);
            _isPlayingComplete = false;
            onFinished?.Invoke();
        });
    }

    /// <summary>WaitForUIComplete 타임아웃 시 호출 — 진행 중 코루틴/엔트리 강제 정리</summary>
    public void ForceCleanup()
    {
        StopAllCoroutines();
        if (_currentEntry != null) { Destroy(_currentEntry.gameObject); _currentEntry = null; }
        _isPlayingComplete = false;
    }

    public void FadeOutCategory() => StartCoroutine(FadeRoutine());

    IEnumerator FadeRoutine()
    {
        yield return new WaitForSecondsRealtime(2f);

        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.unscaledDeltaTime;
            categoryGroup.alpha = Mathf.Lerp(1f, 0.35f, t / 0.5f);
            yield return null;
        }
    }
}
