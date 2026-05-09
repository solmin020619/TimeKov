using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class QuestPanelUI : MonoBehaviour
{
    [SerializeField] Transform categoryRoot;
    [SerializeField] CategoryWidget categoryWidgetPrefab;
    [SerializeField] CanvasGroup panelGroup;
    [SerializeField] KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] float completionTimeoutSec = 3f;
    [SerializeField] float toggleDuration = 0.2f;

    [Header("Toast (optional)")]
    [Tooltip("퀘스트 완료 시 띄울 토스트. null이면 토스트 생략.")]
    [SerializeField] ToastNotification toast;
    [Tooltip("토스트 메시지. 사진 5 기준 '업데이트 완료' 고정")]
    [SerializeField] string toastMessage = "업데이트 완료";
    [Tooltip("OnQuestCompleted 후 토스트 등장까지 대기. QuestEntry 사라지는 시간(약 1.15s) 정도 두면 사진 5 패턴. 기존 UI 사라진 후 토스트+새 UI 동시 등장")]
    [SerializeField] float toastDelay = 1.1f;

    readonly Dictionary<CategoryRuntime, CategoryWidget> _widgets = new();
    bool _setupDone;
    bool _collapsed;
    Tween _toggleTween;

    void Start()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;

        qm.OnReady += Setup;
        qm.OnReset += HandleReset;

        if (qm.IsReady) Setup();
    }

    void Setup()
    {
        if (_setupDone) return;
        _setupDone = true;

        var qm = QuestManager.Instance;
        _widgets.Clear();   // 방어적

        foreach (var rt in qm.Runtimes)
        {
            var w = Instantiate(categoryWidgetPrefab, categoryRoot);
            w.Init(rt);
            _widgets[rt] = w;
        }

        qm.OnQuestShown += HandleQuestShown;
        qm.OnQuestCompleted += HandleQuestCompleted;
        qm.OnCategoryCompleted += HandleCategoryCompleted;
        qm.OnAllCompleted += HandleAllCompleted;
        qm.CompletionAnimation = WaitForUIComplete;

        qm.BeginAll();   // UI가 단독 책임
    }

    void HandleReset()
    {
        var qm = QuestManager.Instance;
        if (qm != null)
        {
            qm.OnQuestShown -= HandleQuestShown;
            qm.OnQuestCompleted -= HandleQuestCompleted;
            qm.OnCategoryCompleted -= HandleCategoryCompleted;
            qm.OnAllCompleted -= HandleAllCompleted;
        }

        foreach (var w in _widgets.Values) if (w) Destroy(w.gameObject);
        _widgets.Clear();
        _setupDone = false;
    }

    void OnDisable()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;

        qm.OnReady -= Setup;
        qm.OnReset -= HandleReset;
        qm.OnQuestShown -= HandleQuestShown;
        qm.OnQuestCompleted -= HandleQuestCompleted;
        qm.OnCategoryCompleted -= HandleCategoryCompleted;
        qm.OnAllCompleted -= HandleAllCompleted;

        if (qm.CompletionAnimation == WaitForUIComplete)
            qm.CompletionAnimation = null;
    }

    void HandleQuestShown(CategoryRuntime rt, QuestSO q)
    {
        if (_widgets.TryGetValue(rt, out var w) && w != null) w.ShowQuest(q);
    }

    void HandleQuestCompleted(CategoryRuntime rt, QuestSO q)
    {
        if (toast != null) toast.Show(toastMessage, toastDelay);
    }

    void HandleCategoryCompleted(CategoryRuntime rt)
    {
        if (_widgets.TryGetValue(rt, out var w) && w != null) w.FadeOutCategory();
    }

    void HandleAllCompleted()
    {
        // 모든 카테고리 완료, 3초 후 패널 fade out, SetActive false
        var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        seq.AppendInterval(3f);
        if (panelGroup) seq.Append(panelGroup.DOFade(0f, 1f));
        seq.OnComplete(() => gameObject.SetActive(false));
    }

    /// <summary>타임아웃. 위젯 비활성/파괴로 콜백 못 받아도 매니저 안 멈춤</summary>
    IEnumerator WaitForUIComplete(CategoryRuntime rt, QuestSO _)
    {
        if (!_widgets.TryGetValue(rt, out var w) || w == null) yield break;

        bool done = false;
        w.PlayCompleteAndRemove(() => done = true);

        float t = 0f;
        while (!done && t < completionTimeoutSec)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!done)
        {
            w.ForceCleanup();
            Debug.LogWarning($"[QuestPanelUI] Completion animation timeout for {rt.data.id}");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) ToggleCollapse();
    }

    public void ToggleCollapse()
    {
        _collapsed = !_collapsed;
        _toggleTween?.Kill();

        if (panelGroup == null) return;

        float endA = _collapsed ? 0f : 1f;
        _toggleTween = panelGroup.DOFade(endA, toggleDuration)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                panelGroup.blocksRaycasts = !_collapsed;
                panelGroup.interactable = !_collapsed;
                _toggleTween = null;
            });
    }
}
