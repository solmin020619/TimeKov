using System.Collections.Generic;
using UnityEngine;

// 상황별 발견 팝업 디스패처.
//   이벤트(설비 해금 / 귀환석 / 전송기 상호작용 / 아이템 획득)를 구독해, 대응하는 DiscoveryCue 를
//   '처음 만나는 순간' 1회 팝업(TutorialVideoUI)으로 띄운다. 퀘스트 순서와 분리 = 튜토 순서 부담 감소.
//
//   안전 처리: 기지 안(safe=true) 이벤트는 즉시. 필드(safe=false)는 거점 복귀(OnBaseEntered)까지 미룸.
//   중복 방지: 기존 도감 시청 기록(CodexDiscovery, 첫 페이지 title 기준)으로 이미 본 큐는 건너뜀 = 세이브 재사용.
//   동시 처리: 한 번에 한 팝업만(TutorialVideoUI.IsShowing) - 마일스톤이 설비 2개를 동시에 풀어도 순차로.
//
//   씬 세팅 불필요: 런타임 자동 부트스트랩(DontDestroyOnLoad). 데이터는 Resources/DiscoveryCues/DiscoveryCueSet.
//   설비해금/귀환석 이벤트는 실제 획득 순간에만 발화(세이브 복원은 조용히 처리) -> 로드마다 재팝업 없음.
public class DiscoveryCueManager : MonoBehaviour
{
    private static DiscoveryCueManager _i;

    private readonly Dictionary<string, DiscoveryCue> _byKey = new Dictionary<string, DiscoveryCue>();
    private readonly Queue<DiscoveryCue> _ready = new Queue<DiscoveryCue>();          // 표시 대기(순차)
    private readonly List<DiscoveryCue> _fieldPending = new List<DiscoveryCue>();     // 필드 - 거점 복귀 대기
    private readonly List<DiscoveryCue> _buildPending = new List<DiscoveryCue>();     // 설비 소개 - 건축모드 진입 대기
    private readonly HashSet<string> _queuedThisSession = new HashSet<string>();      // 큐 중복 큐잉 방지(세션)
    private bool _showing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_i != null) return;
        var set = Resources.Load<DiscoveryCueSet>("DiscoveryCues/DiscoveryCueSet");
        if (set == null || set.cues == null || set.cues.Count == 0) return;   // 큐 없음 = 시스템 비활성(무해)

        var go = new GameObject("[DiscoveryCueManager]");
        DontDestroyOnLoad(go);
        _i = go.AddComponent<DiscoveryCueManager>();
        _i.Init(set);
    }

    private void Init(DiscoveryCueSet set)
    {
        bool needItem = false, needFacility = false, needReturn = false, needInteract = false, needBase = false;
        foreach (var c in set.cues)
        {
            if (c == null || string.IsNullOrEmpty(c.cueKey) || !c.HasContent) continue;
            if (!_byKey.ContainsKey(c.cueKey)) _byKey[c.cueKey] = c;

            if (c.cueKey.StartsWith("item:")) needItem = true;
            else if (c.cueKey.StartsWith("facility:")) needFacility = true;
            else if (c.cueKey.StartsWith("interact:")) needInteract = true;
            else if (c.cueKey == "returnstone") needReturn = true;
            if (!c.safe) needBase = true;   // 필드 큐가 하나라도 있으면 거점 진입 감지 필요
        }

        // 필요한 이벤트만 구독(특히 OnItemAcquired 는 매 획득마다라 큐가 있을 때만).
        if (needFacility)
        {
            GameEvents.OnFacilityUnlocked += HandleFacilityUnlocked;      // 해금 순간 = 큐 적재
            GameEvents.OnBuildModeEntered += HandleBuildModeEntered;      // 건축모드 진입 = 표시
        }
        if (needReturn)   GameEvents.OnReturnStoneChanged += HandleReturnStone;
        if (needInteract) GameEvents.OnInteracted += HandleInteracted;
        if (needItem)     GameEvents.OnItemAcquired += HandleItemAcquired;
        if (needBase)     GameEvents.OnBaseEntered += HandleBaseEntered;
    }

    private void OnDestroy()
    {
        GameEvents.OnFacilityUnlocked -= HandleFacilityUnlocked;
        GameEvents.OnBuildModeEntered -= HandleBuildModeEntered;
        GameEvents.OnReturnStoneChanged -= HandleReturnStone;
        GameEvents.OnInteracted -= HandleInteracted;
        GameEvents.OnItemAcquired -= HandleItemAcquired;
        GameEvents.OnBaseEntered -= HandleBaseEntered;
        if (_i == this) _i = null;
    }

    // ── 이벤트 -> 큐 키 ────────────────────────────────────────────────
    private void HandleFacilityUnlocked(int facilityId) => Fire("facility:" + facilityId);
    private void HandleReturnStone(int level) => Fire("returnstone");
    private void HandleInteracted(string interactId) => Fire("interact:" + interactId);
    private void HandleItemAcquired(int itemId, int count) => Fire("item:" + itemId);

    private void HandleBaseEntered()
    {
        if (_fieldPending.Count == 0) return;
        foreach (var c in _fieldPending) _ready.Enqueue(c);
        _fieldPending.Clear();
        Pump();
    }

    // 설비 소개 = 건축모드 진입 때 순차 표시. 전송 단말서 바로 뜨면 전송 UI(풀스크린)와 겹쳐 일부가 묻히던 것 방지
    //   + 곧 지을 설비라 "해금 -> B로 건설" 흐름과 타이밍이 맞다. 여러 개 밀려있으면 _ready 큐로 순차.
    private void HandleBuildModeEntered()
    {
        if (_buildPending.Count == 0) return;
        foreach (var c in _buildPending) _ready.Enqueue(c);
        _buildPending.Clear();
        Pump();
    }

    // ── 발화 ───────────────────────────────────────────────────────────
    /// <summary>이벤트가 없는 트리거(워프 등)에서 큐 키를 직접 발화. 매니저 없으면(큐셋 비어있음) 무해하게 무시.</summary>
    public static void TryFire(string key) => _i?.Fire(key);

    private void Fire(string key)
    {
        if (!_byKey.TryGetValue(key, out var cue) || cue == null) return;
        if (_queuedThisSession.Contains(key)) return;   // 이미 이번 세션에 큐잉/표시함
        if (IsAlreadyWatched(cue)) return;              // 이전 슬롯/플레이에서 본 큐(세이브 기록)
        _queuedThisSession.Add(key);

        if (key.StartsWith("facility:")) _buildPending.Add(cue);   // 설비 소개 - 건축모드 진입 때 flush(전송 UI 충돌 회피)
        else if (cue.safe) { _ready.Enqueue(cue); Pump(); }
        else _fieldPending.Add(cue);                    // 필드 - 거점 복귀 때 flush
    }

    private static bool IsAlreadyWatched(DiscoveryCue cue)
    {
        var t = cue.FirstTitle;
        return !string.IsNullOrEmpty(t) && CodexDiscovery.IsTutorialWatched(t);
    }

    // ── 순차 표시 ──────────────────────────────────────────────────────
    private void Update()
    {
        // 팝업이 다른 이유(퀘 영상 등)로 떠 있다 닫혔으면 대기 큐를 재개.
        if (!_showing && _ready.Count > 0) Pump();
    }

    private void Pump()
    {
        if (_showing || _ready.Count == 0) return;
        if (TutorialVideoUI.IsShowing) return;   // 다른 팝업 표시 중 - Update 가 나중에 재시도

        var cue = _ready.Dequeue();
        if (IsAlreadyWatched(cue)) { Pump(); return; }   // 큐잉~표시 사이에 도감 등에서 봤으면 스킵

        var ui = TutorialVideoUI.I;
        if (ui == null) { _ready.Enqueue(cue); return; }  // 아직 생성 불가 - 다음 Update 재시도
        _showing = true;
        ui.Show(cue.pages, OnClosed);
    }

    private void OnClosed()
    {
        _showing = false;
        Pump();
    }
}
