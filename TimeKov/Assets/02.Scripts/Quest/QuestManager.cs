using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Quest System Flow
 * -------------------------------------------------------------
 *
 * [QuestManager.Start]
 *   \- Initialize() -> OnReady -> [QuestPanelUI.Setup]
 *                                  \- 위젯 생성 + 구독
 *                                  \- QuestManager.BeginAll() (UI가 단독 책임)
 *
 * [BeginAll] (멱등) -> 각 카테고리에 PresentCurrentQuest()
 *
 * [PresentCurrentQuest]
 *   |- Objective Instantiate + 구독
 *   |- OnQuestShown 발화 (매니저)
 *   |     v 구독자가 받음
 *   |   [QuestPanelUI.HandleQuestShown]
 *   |     v
 *   |   [CategoryWidget.ShowQuest] -> Instantiate(QuestEntry)
 *   |     v
 *   |   [QuestEntry.PlaySlideIn] -> SlideInAndActivate 코루틴
 *   |     |- 슬라이드 인 0.3초
 *   |     \- QuestManager.ActivateObjectives() 호출
 *   \- OnUIPresented Objective는 즉시 ActivateInternal
 *
 * [ActivateObjectives] (UI에서 호출)
 *   |- quest 캡처 + OnQuestActivated 발화 (즉시충족 대비)
 *   \- OnUIActivated Objective 활성화 -> 입력 카운트 시작
 *
 * [Objective.Complete] -> OnCompleted -> [OnObjectiveCompleted]
 *                                          \- Dictionary 조회 -> CheckQuestComplete
 *
 * [CheckQuestComplete] (모든 Objective 완료 확인)
 *   |- Objective Deactivate (구독 해제, Destroy는 미룸)
 *   |- 인덱스 +1 + Save + Flush
 *   |- OnQuestCompleted + onCompleted UnityEvent
 *   \- AfterCompletion 코루틴
 *
 * [AfterCompletion] (1.2초 시퀀스 대기)
 *   |- CompletionAnimation 콜백 -> [WaitForUIComplete]
 *   |- 옛 Objective Destroy
 *   \- 카테고리 끝 ? OnCategoryCompleted : PresentCurrentQuest (다음)
 *
 * [ResetAll] (QA 도구)
 *   |- tutorial/storage null 가드
 *   |- StopAllCoroutines
 *   |- Internal cleanup 먼저
 *   |- OnReset -> [QuestPanelUI.HandleReset] -> 위젯 정리
 *   \- Initialize -> OnReady -> Setup 재실행 -> BeginAll
 */
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [SerializeField] TutorialSO tutorial;

    [Tooltip("ON: 진행도 저장 안 함 (매 Play마다 초기화, 개발용). OFF: 저장 슬롯에 저장 (production).")]
    [SerializeField] bool useNoOpStorage = true;

    IQuestSaveStorage _storage;
    readonly List<CategoryRuntime> _runtimes = new();
    readonly Dictionary<ObjectiveSO, CategoryRuntime> _objectiveOwner = new();
    bool _hasBegun;       // BeginAll 멱등성

    public IReadOnlyList<CategoryRuntime> Runtimes => _runtimes;
    public bool IsReady { get; private set; }

    /// <summary>아직 끝나지 않은 카테고리가 하나라도 있으면 true (튜토리얼 진행 중).</summary>
    public bool IsTutorialActive
    {
        get
        {
            if (!IsReady) return false;
            foreach (var rt in _runtimes) if (!rt.IsCategoryDone) return true;
            return false;
        }
    }

    public event Action OnReady;
    public event Action OnReset;
    public event Action<CategoryRuntime, QuestSO> OnQuestShown;
    public event Action<CategoryRuntime, QuestSO> OnQuestActivated;
    public event Action<CategoryRuntime, QuestSO> OnQuestCompleted;
    public event Action<CategoryRuntime> OnCategoryCompleted;
    public event Action OnAllCompleted;

    public Func<CategoryRuntime, QuestSO, IEnumerator> CompletionAnimation;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        // tutorial null이면 Instance 세팅 안 함. 외부 코드가 명시적 null 체크
        if (tutorial == null)
        {
            Debug.LogError("[QuestManager] tutorial 미할당. 매니저 비활성화");
            enabled = false;
            return;
        }

        Instance = this;
        if (useNoOpStorage)
            _storage = new NoOpQuestStorage();
        else if (SaveSlotManager.Instance != null && SaveSlotManager.Instance.HasActiveSlot)
            _storage = new SaveSlotQuestStorage();
        else
        {
            // 활성 슬롯 없이 씬을 직접 실행한 경우(에디터 테스트 등) — 인벤토리/위치/건물과 동일하게
            // 저장 시스템 미개입 상태로 둔다. (예전엔 PlayerPrefs로 폴백했는데, 슬롯 구분이 없는
            // 전역 값이라 월드별로 따로 저장돼야 하는 지금 구조와 맞지 않아 제거함)
            _storage = new NoOpQuestStorage();
        }
    }

    void Start()
    {
        if (!enabled) return;
        Initialize();
        OnReady?.Invoke();
        // BeginAll 호출 안 함. UI Setup이 자기 흐름에서 호출
    }

    void Initialize()
    {
        foreach (var cat in tutorial.categories)
        {
            if (cat == null) continue;
            var rt = new CategoryRuntime { data = cat };
            rt.activeQuestIndex = _storage.GetCategoryIndex(cat.id);
            _runtimes.Add(rt);
        }

        if (_runtimes.Count == 0)
            Debug.LogWarning("[QuestManager] 등록된 카테고리 없음");

        IsReady = true;
    }

    /// <summary>UI가 위젯 생성 + 구독 끝낸 후 호출. 멱등성, 두 번 호출돼도 안전.</summary>
    public void BeginAll()
    {
        if (_hasBegun) return;
        _hasBegun = true;

        foreach (var rt in _runtimes)
            if (rt.activeObjectives == null && !rt.IsCategoryDone)
                PresentCurrentQuest(rt);
    }

    void PresentCurrentQuest(CategoryRuntime rt)
    {
        if (rt.IsCategoryDone) { OnCategoryCompleted?.Invoke(rt); CheckAllDone(); return; }

        var quest = rt.data.quests[rt.activeQuestIndex];
        if (quest == null || quest.objectives == null || quest.objectives.Length == 0)
        {
            Debug.LogError($"[QuestManager] {rt.data.id}[{rt.activeQuestIndex}] 비어있음. 스킵.");
            AdvanceCategory(rt);
            return;
        }

        var validObjs = new List<ObjectiveSO>(quest.objectives.Length);
        for (int i = 0; i < quest.objectives.Length; i++)
        {
            if (quest.objectives[i] == null)
            {
                Debug.LogError($"[QuestManager] {rt.data.id}[{rt.activeQuestIndex}].objectives[{i}] null", quest);
                continue;
            }
            validObjs.Add(quest.objectives[i]);
        }

        if (validObjs.Count == 0) { AdvanceCategory(rt); return; }

        rt.activeObjectives = new ObjectiveSO[validObjs.Count];
        for (int i = 0; i < validObjs.Count; i++)
        {
            var inst = Instantiate(validObjs[i]);
            _objectiveOwner[inst] = rt;
            inst.OnCompleted += OnObjectiveCompleted;
            rt.activeObjectives[i] = inst;
        }

        rt.IsActivated = false;

        // 이벤트 먼저 발화. 즉시충족 시에도 정확한 quest 참조 보장
        OnQuestShown?.Invoke(rt, quest);
        quest.onShown?.Invoke();

        // OnUIPresented Objective는 Present 시점에 즉시 활성
        foreach (var o in rt.activeObjectives)
            if (o != null && o.Timing == ActivationTiming.OnUIPresented && !o.IsCompleted)
                o.ActivateInternal();
    }

    public void ActivateObjectives(CategoryRuntime rt)
    {
        if (rt == null || rt.IsActivated || rt.activeObjectives == null) return;
        rt.IsActivated = true;

        // quest 먼저 캡처 + 이벤트 먼저 발화. 즉시충족 시 인덱스 진행돼도 정확
        var quest = rt.data.quests[rt.activeQuestIndex];
        OnQuestActivated?.Invoke(rt, quest);
        quest.onActivated?.Invoke();

        // 그 후 활성화
        foreach (var o in rt.activeObjectives)
            if (o != null && o.Timing == ActivationTiming.OnUIActivated && !o.IsCompleted)
                o.ActivateInternal();
    }

    void OnObjectiveCompleted(ObjectiveSO o)
    {
        if (_objectiveOwner.TryGetValue(o, out var rt))
            CheckQuestComplete(rt);
    }

    void CheckQuestComplete(CategoryRuntime rt)
    {
        if (rt.IsCheckingComplete || rt.activeObjectives == null) return;
        foreach (var o in rt.activeObjectives) if (o == null || !o.IsCompleted) return;

        rt.IsCheckingComplete = true;
        try
        {
            var done = rt.data.quests[rt.activeQuestIndex];
            var oldObjectives = rt.activeObjectives;

            foreach (var o in oldObjectives)
            {
                o.OnCompleted -= OnObjectiveCompleted;
                _objectiveOwner.Remove(o);
                o.Deactivate();
            }
            rt.activeObjectives = null;
            rt.IsActivated = false;

            // 보상 지급 — 인덱스 저장 직전 (지급 후 진행도 커밋 순서 유지)
            GrantRewards(done);

            rt.activeQuestIndex++;
            _storage.SetCategoryIndex(rt.data.id, rt.activeQuestIndex);
            _storage.Flush();

            OnQuestCompleted?.Invoke(rt, done);
            done.onCompleted?.Invoke();

            if (!_runtimes.Contains(rt))
            {
                if (oldObjectives != null)
                    foreach (var o in oldObjectives) if (o != null) Destroy(o);
                return;
            }

            StartCoroutine(AfterCompletion(rt, done, oldObjectives));
        }
        finally { rt.IsCheckingComplete = false; }
    }

    IEnumerator AfterCompletion(CategoryRuntime rt, QuestSO done, ObjectiveSO[] toDestroy)
    {
        // 주의: AfterCompletion 중 ResetAll 호출되면 toDestroy SO 인스턴스 누수.
        //       게임 종료 시 자동 정리되니 실용상 무시.
        if (CompletionAnimation != null)
            yield return CompletionAnimation(rt, done);

        if (toDestroy != null)
            foreach (var o in toDestroy) if (o != null) Destroy(o);

        if (!_runtimes.Contains(rt)) yield break;

        if (rt.IsCategoryDone) { OnCategoryCompleted?.Invoke(rt); CheckAllDone(); }
        else PresentCurrentQuest(rt);
    }

    void AdvanceCategory(CategoryRuntime rt)
    {
        rt.activeQuestIndex++;
        _storage.SetCategoryIndex(rt.data.id, rt.activeQuestIndex);

        if (rt.IsCategoryDone)
        {
            _storage.Flush();
            OnCategoryCompleted?.Invoke(rt);
            CheckAllDone();
        }
        else PresentCurrentQuest(rt);
    }

    void CheckAllDone()
    {
        foreach (var rt in _runtimes) if (!rt.IsCategoryDone) return;
        OnAllCompleted?.Invoke();
        _storage.Flush();
    }

    // 퀘스트 완료 보상을 인벤토리로 지급한다. rewards 비어있으면 아무것도 안 함.
    void GrantRewards(QuestSO quest)
    {
        if (quest == null || quest.rewards == null || quest.rewards.Length == 0) return;

        foreach (var reward in quest.rewards)
        {
            if (reward == null || reward.itemId <= 0 || reward.amount <= 0) continue;

            int leftover = reward.amount;

            // 1) 가방(루팅 경로 — NEW 뱃지 표시)
            if (InventoryManager.Instance != null)
                leftover = InventoryManager.Instance.TryAddItemFromLoot(reward.itemId, leftover);

            int afterBag = leftover;

            // 2) 가방 가득/없음 → 창고 폴백. (보상이 조용히 증발하면 그걸 요구하는 다음 퀘 — 예: 코어키트→코어강화 — 가
            //    영구 소프트락 되므로 반드시 어딘가엔 들어가게 한다)
            if (leftover > 0 && InventoryManager.StorageInstance != null)
                leftover = InventoryManager.StorageInstance.TryAddItemFromLoot(reward.itemId, leftover);

            // 가방이 가득 차 창고로 넘어간 분량이 있으면 사라진 게 아님을 명확히 안내
            if (afterBag > 0 && leftover < afterBag)
                ToastManager.Info("인벤토리가 가득 차 창고로 이동했습니다");

            if (leftover > 0)
            {
                ToastManager.Error("보상을 받지 못했습니다. 인벤토리를 비워주세요");
                Debug.LogError($"[QuestManager] 보상 미지급 — 가방·창고 모두 가득 " +
                               $"(quest: {quest.id}, itemId: {reward.itemId}, 미지급: {leftover}). 후속 퀘 소프트락 위험.");
            }
        }
    }

    void OnApplicationPause(bool paused) { if (paused) _storage?.Flush(); }
    void OnApplicationQuit() => _storage?.Flush();

    public void ResetAll()
    {
        if (tutorial == null || _storage == null)
        {
            Debug.LogError("[QuestManager] ResetAll: tutorial/storage 미초기화");
            return;
        }

        StopAllCoroutines();

        // Internal cleanup 먼저 (외부 알림 시 _runtimes는 빈 상태)
        foreach (var rt in _runtimes)
        {
            _storage.SetCategoryIndex(rt.data.id, 0);

            if (rt.activeObjectives != null)
                foreach (var o in rt.activeObjectives)
                {
                    o.OnCompleted -= OnObjectiveCompleted;
                    _objectiveOwner.Remove(o);
                    o.Deactivate();
                    Destroy(o);
                }
        }
        _runtimes.Clear();
        _objectiveOwner.Clear();
        IsReady = false;
        _hasBegun = false;
        _storage.Flush();

        OnReset?.Invoke();

        Initialize();
        OnReady?.Invoke();
        // BeginAll 호출 안 함. Setup이 호출
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>개발용(F7): 진행 중인 첫 카테고리의 활성 퀘스트를 강제 완료(스킵).
    /// 활성 objective 를 전부 강제 완료시켜, 보상 지급/완료 연출/다음 퀘 진행의 정상 흐름을 그대로 탄다.</summary>
    public void DevSkipCurrentQuest()
    {
        foreach (var rt in _runtimes)
        {
            if (rt.IsCategoryDone || rt.activeObjectives == null) continue;   // 완료/전환 중은 건너뜀
            // 완료 콜백이 activeObjectives 를 null 로 바꾸므로 스냅샷으로 순회.
            var snapshot = (ObjectiveSO[])rt.activeObjectives.Clone();
            foreach (var o in snapshot)
                if (o != null && !o.IsCompleted) o.DevForceComplete();
            return;   // 한 번에 한 퀘만
        }
        Debug.Log("[Dev] 스킵할 활성 퀘스트가 없다 (전부 완료 or 완료연출 전환 중)");
    }
#endif

#if UNITY_EDITOR
    [ContextMenu("Reset All Progress")]
    void ResetAllMenu() => ResetAll();
#endif
}
