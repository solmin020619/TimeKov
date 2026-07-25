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
    CategoryWidget _lastShownWidget;   // 토스트 사라짐 → 다음 QuestEntry iconAlert 전환 동기화용

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
            // 첫 퀘가 표시될 때까지 위젯 비활성 = 휴면(엔드게임) 카테고리가 빈 제목 높이만큼 공간 차지하는 것 방지.
            // 메인은 BeginAll 이 같은 프레임에 첫 퀘를 present -> HandleQuestShown 에서 즉시 활성(깜빡임 없음).
            w.gameObject.SetActive(false);
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
        if (_widgets.TryGetValue(rt, out var w) && w != null)
        {
            if (!w.gameObject.activeSelf) w.gameObject.SetActive(true);   // 휴면 위젯이 깨어나 첫 퀘 표시
            w.ShowQuest(q);
            _lastShownWidget = w;
        }
        GameUIController.Instance?.PulseQuestTracker();   // 새 퀘 등장 시 (UI 열린 중이어도) 트래커 잠깐 팝
    }

    void HandleQuestCompleted(CategoryRuntime rt, QuestSO q)
    {
        // 설명 코치마크 퀘 = '완료' 토스트 없음 (읽기만 한 거라 완료 팡파레가 어리둥절하게 함)
        if (IsExplanationQuest(q)) return;
        if (toast == null) return;

        // 토스트가 hold 끝나고 좌측으로 사라지기 시작하는 시점 = 다음 QuestEntry의 iconAlert -> iconNormal 전환 시점
        toast.Show(toastMessage, toastDelay, onHideStart: () =>
        {
            if (_lastShownWidget != null) _lastShownWidget.TriggerIconSwitchToNormal();
        });
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
    IEnumerator WaitForUIComplete(CategoryRuntime rt, QuestSO q)
    {
        if (!_widgets.TryGetValue(rt, out var w) || w == null) yield break;

        bool done = false;
        // 완료연출(1.15s) 스킵: (1) 완료된 게 설명퀘(팡파레 억제) 또는
        // (2) 다음 퀘가 설명/코치마크 - 액션 직후 코치마크가 늦게 떠 그 사이 미리 조작 가능한 갭을 없앤다(공장/코어/건설진입).
        if (IsExplanationQuest(q) || IsExplanationQuest(NextQuest(rt)))
            w.RemoveImmediate(() => done = true);
        else
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

    // 순수 설명(코치마크) 퀘 = 모든 objective가 설명형(Continue/GuidedTour). 완료 시 토스트/완료연출 생략.
    static bool IsExplanationQuest(QuestSO q)
    {
        if (q == null || q.objectives == null || q.objectives.Length == 0) return false;
        foreach (var o in q.objectives)
            if (o == null || !o.IsExplanation) return false;
        return true;
    }

    // 현재 카테고리의 다음 퀘 (완료 시점엔 activeQuestIndex가 이미 ++돼 있어 이게 곧 표시될 다음 퀘). 없으면 null.
    static QuestSO NextQuest(CategoryRuntime rt)
    {
        if (rt == null || rt.data == null || rt.IsCategoryDone) return null;
        var quests = rt.data.quests;
        int i = rt.activeQuestIndex;
        if (quests == null || i < 0 || i >= quests.Length) return null;
        return quests[i];
    }

    // TAB 키 토글 제거 — TAB은 인벤토리(InventoryUIController)가 단독 사용
    // 퀘스트 HUD 가시성은 GameUIController.ApplyState()가 CanvasGroup으로 관리

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
