using System.Collections.Generic;
using UnityEngine;

// 도감 발견/시청 상태 추적기. CodexSaveBridge(ISaveable)가 세이브 슬롯에 영구 저장한다.
// 몬스터: 처치 시 sourceId 기록(EnemyDropOnDeath). 튜토: 영상 페이지 시청 시 제목 기록(TutorialVideoUI).
// 정적 클래스라 어디서든 호출/조회 가능. 도메인 리로드 시 일단 초기화 후 CodexSaveBridge가 복원.
public static class CodexDiscovery
{
    private static readonly Dictionary<string, int> _monsterKills = new Dictionary<string, int>();
    private static readonly HashSet<string> _tutorials = new HashSet<string>();

    // 처치 수 단계 임계(킬수 소유자라 여기서 정의 - CodexUI가 이 값을 참조).
    public const int KillsForStats    = 5;     // 스탯 활성화 가능
    public const int KillsForDropRate = 10;    // 드롭 확률 활성화 가능(= 풀 카운터 기준)

    // ── 몬스터 (처치 시 +1 누적) ─────────────────────────────────
    public static void DiscoverMonster(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        string id = sourceId.Trim();
        _monsterKills.TryGetValue(id, out int c);
        int n = c + 1;
        _monsterKills[id] = n;
        // 첫 발견 / 스탯·확률 활성화 임계 도달 시 도감 알림(!) 점등 - "새로 볼 게 생김".
        if (n == 1 || n == KillsForStats || n == KillsForDropRate)
            CodexNotice.MarkUnseen(CodexNotice.Monster);
    }
    public static int MonsterKills(string sourceId)
        => (!string.IsNullOrEmpty(sourceId) && _monsterKills.TryGetValue(sourceId.Trim(), out int c)) ? c : 0;
    public static bool IsMonsterDiscovered(string sourceId) => MonsterKills(sourceId) >= 1;

    // ── 단계 활성화 (도감서 직접 클릭해 해금. 조건=킬수는 호출부서 검사) ──
    private static readonly HashSet<string> _statsOn = new HashSet<string>();
    private static readonly HashSet<string> _ratesOn = new HashSet<string>();
    public static bool IsStatsActivated(string sourceId) => !string.IsNullOrEmpty(sourceId) && _statsOn.Contains(sourceId.Trim());
    public static bool IsRatesActivated(string sourceId) => !string.IsNullOrEmpty(sourceId) && _ratesOn.Contains(sourceId.Trim());
    public static void ActivateStats(string sourceId) { if (!string.IsNullOrEmpty(sourceId)) _statsOn.Add(sourceId.Trim()); }
    public static void ActivateRates(string sourceId) { if (!string.IsNullOrEmpty(sourceId)) _ratesOn.Add(sourceId.Trim()); }

    // ── 튜토리얼 (영상 페이지 시청 시) ────────────────────────────
    public static void WatchTutorial(string title)
    {
        if (string.IsNullOrEmpty(title)) return;
        if (_tutorials.Add(title.Trim()))
            CodexNotice.MarkUnseen(CodexNotice.Tutorial);   // 처음 시청한 영상만 알림(!)
    }
    public static bool IsTutorialWatched(string title)
        => !string.IsNullOrEmpty(title) && _tutorials.Contains(title.Trim());

    // ── 세이브 캡처/복원 (CodexSaveBridge 전용) ──────────────────────────
    public static IReadOnlyDictionary<string, int> AllMonsterKills => _monsterKills;
    public static IReadOnlyCollection<string> AllWatchedTutorials => _tutorials;
    public static IReadOnlyCollection<string> AllActivatedStats => _statsOn;
    public static IReadOnlyCollection<string> AllActivatedRates => _ratesOn;

    /// <summary>세이브 복원 전용 — 알림(!) 발생 없이 조용히 채운다.</summary>
    public static void RestoreState(
        IEnumerable<KeyValuePair<string, int>> kills,
        IEnumerable<string> tutorials,
        IEnumerable<string> statsOn,
        IEnumerable<string> ratesOn)
    {
        if (kills != null) foreach (var kv in kills) _monsterKills[kv.Key] = kv.Value;
        if (tutorials != null) foreach (var t in tutorials) _tutorials.Add(t);
        if (statsOn != null) foreach (var s in statsOn) _statsOn.Add(s);
        if (ratesOn != null) foreach (var r in ratesOn) _ratesOn.Add(r);
    }

    // 세션 시작 시 1회 초기화 + CodexSaveBridge가 슬롯 전환마다(다른 월드로 이동 등) 호출해
    // 이전 슬롯의 메모리 상태가 새 슬롯으로 새는 것을 막는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() { _monsterKills.Clear(); _tutorials.Clear(); _statsOn.Clear(); _ratesOn.Clear(); }
}
