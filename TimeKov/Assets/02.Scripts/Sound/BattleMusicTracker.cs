using System.Collections.Generic;
using UnityEngine;

// 일반 몹 교전 상태를 집계해 "맵 테마 전투 BGM" 을 켜고 끈다.
//   각 적 AI(EnemyBrain / FieldMonsterAI / HellMonsterAI)가 교전 시작·종료를 알려주면
//   교전 중인 적 수를 세어(ref-count) 0→1 에 Begin, 1→0 에 End 한다.
//   ★보스는 자기 전용 BGM(WyvernBattleBgm)을 BattleBgm 으로 직접 켜므로 이 트래커를 타지 않는다.
//     (보스가 먼저 교전 중이면 BattleBgm 의 ref-count 때문에 일반 몹 Begin 은 클립을 바꾸지 않아
//      보스 BGM 이 그대로 유지된다 — 의도된 우선순위.)
public static class BattleMusicTracker
{
    // 교전 중인 적의 InstanceID 집합. 같은 적이 중복 등록돼도 1회만 세도록 Set 사용.
    private static readonly HashSet<int> _engaged = new HashSet<int>();

    // ★우리가 BattleBgm.Begin 을 실제로 호출했는지. Begin/End 를 반드시 짝으로 유지하려는 목적.
    //   해당 테마 BGM 클립이 아직 없으면 Begin 을 아예 안 하고(필드 BGM 유지), 그러면 End 도 안 한다.
    //   (BattleBgm 은 클립 유무와 무관하게 기존 BGM 을 일시정지하므로, 클립 없을 때 Begin 하면 무음이 된다)
    private static bool _musicOn;

    // 도메인 리로드를 끈 에디터에서도 재생 시작 시 상태가 깨끗하도록 statics 리셋.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { _engaged.Clear(); _musicOn = false; }

    // 적이 플레이어와 교전을 시작. 첫 교전이면 해당 테마 BGM 시작(클립이 있을 때만).
    public static void Engage(int enemyId, TransmissionRegion region)
    {
        if (!_engaged.Add(enemyId)) return;    // 이미 교전 중이던 적 → 무시
        if (_engaged.Count != 1) return;       // 0 → 1 일 때만 시작

        var id = BgmFor(region);
        if (GameSfx.TryGet(id, out var clip, out _) && clip != null)
        {
            BattleBgm.Begin(id);
            _musicOn = true;
        }
    }

    // 적이 교전을 종료(타깃 상실 / 사망 / 파괴). 마지막 적이면 BGM 종료(우리가 켰을 때만).
    public static void Disengage(int enemyId)
    {
        if (!_engaged.Remove(enemyId)) return; // 등록 안 돼 있던 적 → 무시
        if (_engaged.Count != 0) return;       // 1 → 0 일 때만 종료

        if (_musicOn)
        {
            BattleBgm.End();
            _musicOn = false;
        }
    }

    private static SfxId BgmFor(TransmissionRegion r) => r switch
    {
        TransmissionRegion.Snow   => SfxId.SnowBattleBgm,
        TransmissionRegion.Desert => SfxId.DesertBattleBgm,
        TransmissionRegion.Lava   => SfxId.LavaBattleBgm,
        _                         => SfxId.NatureBattleBgm,   // Nature 및 기본값
    };
}
