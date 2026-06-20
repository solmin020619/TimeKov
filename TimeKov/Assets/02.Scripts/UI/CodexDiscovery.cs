using System.Collections.Generic;
using UnityEngine;

// 도감 발견/시청 상태 추적기 (세션 단위, 저장 없음 - FacilityUnlockManager와 동일 정책).
// 몬스터: 처치 시 sourceId 기록(EnemyDropOnDeath). 튜토: 영상 페이지 시청 시 제목 기록(TutorialVideoUI).
// 정적 클래스라 어디서든 호출/조회 가능. 도메인 리로드 시 자동 초기화.
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

    // 세션 시작마다 초기화(저장 없음).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { _monsterKills.Clear(); _tutorials.Clear(); _statsOn.Clear(); _ratesOn.Clear(); }
}
