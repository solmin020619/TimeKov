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

    [Header("Display")]
    [Tooltip("OFF면 카테고리 제목 GameObject 비활성화. 단일 카테고리 통합 UI(엔드필드 패턴)에 사용.")]
    [SerializeField] bool showCategoryTitle = true;

    CategoryRuntime _rt;
    QuestEntry _currentEntry;

    public void Init(CategoryRuntime rt)
    {
        _rt = rt;
        if (showCategoryTitle)
        {
            if (categoryTitle != null) categoryTitle.text = rt.data.title;
        }
        else if (categoryTitle != null)
        {
            // 글자만 숨기고 GameObject는 활성 유지 — LayoutElement 공간이 보존돼 아래 퀘스트가
            // 위로 밀리지 않음(토스트와 겹침 방지). SetActive(false)는 VLG에서 빠져 레이아웃이 올라감.
            categoryTitle.enabled = false;
        }
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

    /// <summary>설명 코치마크 퀘 완료용: 완료 연출(sweep/rise/fade) 없이 즉시 제거.
    /// 트래커는 코치 중 이미 숨겨져 있어 깜빡임 없음. 갭이 0이 돼 다음 행동퀘가 바로 활성됨.</summary>
    public void RemoveImmediate(Action onFinished)
    {
        if (_currentEntry == null) { onFinished?.Invoke(); return; }
        var entry = _currentEntry;
        _currentEntry = null;
        Destroy(entry.gameObject);
        onFinished?.Invoke();
    }

    /// <summary>WaitForUIComplete 타임아웃 시 호출. 진행 중 코루틴/엔트리 강제 정리</summary>
    public void ForceCleanup()
    {
        StopAllCoroutines();
        if (_currentEntry != null) { Destroy(_currentEntry.gameObject); _currentEntry = null; }
    }

    /// <summary>토스트 사라짐 시점에 호출. 현재 표시 중인 QuestEntry의 iconAlert→iconNormal 전환</summary>
    public void TriggerIconSwitchToNormal()
    {
        if (_currentEntry != null) _currentEntry.TriggerIconSwitchToNormal();
    }

    [Header("Fade timing")]
    [Tooltip("카테고리 완료 후 fade 시작까지 대기 시간 (사용자가 완료 인지)")]
    [SerializeField] float fadeOutHoldTime = 1.5f;
    [Tooltip("alpha 1->0 fade 길이")]
    [SerializeField] float fadeOutDuration = 0.5f;

    public void FadeOutCategory()
    {
        // 카테고리 제목을 안 쓰는(단일 트래커) 스타일이면 완료 시 위젯이 비어 있다(퀘 엔트리는 이미
        //   자체 완료연출로 제거됨). 그런데도 1.5초 hold + fade 로 빈 자리를 붙잡으면, 다음 카테고리
        //   (엔드게임) 첫 퀘가 그 아래에 떴다가 이 위젯이 사라질 때 위로 점프한다(토스트와 높낮이 어긋남).
        //   -> 보여줄 게 없으니 즉시 레이아웃에서 빼서, 다음 카테고리가 곧장 윗자리에 뜨게 한다.
        if (!showCategoryTitle)
        {
            gameObject.SetActive(false);
            return;
        }

        // (제목 표시형) hold, fade alpha 0, SetActive(false). 부모 VLG가 자동 reflow.
        var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        seq.AppendInterval(fadeOutHoldTime);
        if (categoryGroup) seq.Append(categoryGroup.DOFade(0f, fadeOutDuration));
        seq.OnComplete(() => gameObject.SetActive(false));
    }
}
