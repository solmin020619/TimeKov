using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuestPanelUI : MonoBehaviour
{
    [SerializeField] Transform categoryRoot;
    [SerializeField] CategoryWidget categoryWidgetPrefab;
    [SerializeField] CanvasGroup panelGroup;
    [SerializeField] KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] float completionTimeoutSec = 3f;
    [SerializeField] float toggleDuration = 0.2f;

    readonly Dictionary<CategoryRuntime, CategoryWidget> _widgets = new();
    bool _setupDone;
    bool _collapsed;
    Coroutine _toggleCo;

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
        qm.OnCategoryCompleted += HandleCategoryCompleted;
        qm.OnAllCompleted += HandleAllCompleted;
        qm.CompletionAnimation = WaitForUIComplete;

        qm.BeginAll();   // ★ UI가 단독 책임
    }

    void HandleReset()
    {
        var qm = QuestManager.Instance;
        if (qm != null)
        {
            qm.OnQuestShown -= HandleQuestShown;
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
        qm.OnCategoryCompleted -= HandleCategoryCompleted;
        qm.OnAllCompleted -= HandleAllCompleted;

        if (qm.CompletionAnimation == WaitForUIComplete)
            qm.CompletionAnimation = null;
    }

    void HandleQuestShown(CategoryRuntime rt, QuestSO q)
    {
        if (_widgets.TryGetValue(rt, out var w) && w != null) w.ShowQuest(q);
    }

    void HandleCategoryCompleted(CategoryRuntime rt)
    {
        if (_widgets.TryGetValue(rt, out var w) && w != null) w.FadeOutCategory();
    }

    void HandleAllCompleted() => StartCoroutine(FadePanel());

    IEnumerator FadePanel()
    {
        yield return new WaitForSecondsRealtime(3f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime;
            if (panelGroup) panelGroup.alpha = 1f - t;
            yield return null;
        }
        gameObject.SetActive(false);
    }

    /// <summary>타임아웃 — 위젯 비활성/파괴로 콜백 못 받아도 매니저 안 멈춤</summary>
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
        if (_toggleCo != null) StopCoroutine(_toggleCo);
        _toggleCo = StartCoroutine(ToggleRoutine());
    }

    IEnumerator ToggleRoutine()
    {
        float startA = panelGroup.alpha;
        float endA = _collapsed ? 0f : 1f;
        float t = 0f;
        while (t < toggleDuration)
        {
            t += Time.unscaledDeltaTime;
            panelGroup.alpha = Mathf.Lerp(startA, endA, t / toggleDuration);
            yield return null;
        }
        panelGroup.alpha = endA;
        panelGroup.blocksRaycasts = !_collapsed;
        panelGroup.interactable = !_collapsed;
        _toggleCo = null;
    }
}
