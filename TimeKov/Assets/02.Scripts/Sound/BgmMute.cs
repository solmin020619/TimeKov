using System.Collections.Generic;
using UnityEngine;

// ── BGM 일시 음소거 ──────────────────────────────────────────────────────────
// "이 안에서는 음악이 안 나온다"는 구역(시간 급속감소 구역 등)에서 쓴다.
//
//   Push(주인) / Pop(주인) 으로 켜고 끈다. 여러 곳이 동시에 요구할 수 있으므로
//   주인(owner)별로 세어서, 마지막 하나가 Pop 할 때만 음소거가 풀린다.
//
// ★볼륨이 아니라 AudioSource.mute 를 쓴다.
//   BGM 볼륨은 설정 슬라이더(GlobalSettingsManager)와 BattleBgm 의 페이드 코루틴이
//   계속 덮어쓰기 때문에, 볼륨을 0으로 만들어 두면 다음 갱신에서 되살아난다.
//   mute 는 볼륨과 독립이라 그런 충돌이 없다.
//
// 대상: 필드 BGM(InGameAudioManager.gameBGMSpeaker) + 전투 BGM(BattleBgm 내부 소스).
public static class BgmMute
{
    private static readonly HashSet<Object> _owners = new();
    private static InGameAudioManager _mgr;

    /// 지금 음소거 상태인가. (나중에 만들어지는 BGM 소스가 스스로 맞춰 갈 때 참조)
    public static bool IsMuted => _owners.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _owners.Clear();
        _mgr = null;
    }

    /// 음소거 요구를 건다(중복 호출 안전).
    public static void Push(Object owner)
    {
        if (owner == null) return;
        if (_owners.Add(owner) && _owners.Count == 1) Apply(true);
    }

    /// 음소거 요구를 푼다. 모든 요구가 풀려야 실제로 소리가 돌아온다.
    public static void Pop(Object owner)
    {
        if (owner == null) return;
        if (_owners.Remove(owner) && _owners.Count == 0) Apply(false);
    }

    private static void Apply(bool muted)
    {
        // 필드 BGM
        if (_mgr == null) _mgr = Object.FindFirstObjectByType<InGameAudioManager>();
        if (_mgr != null && _mgr.gameBGMSpeaker != null) _mgr.gameBGMSpeaker.mute = muted;

        // 전투 BGM (교전 중일 수 있다)
        BattleBgm.SetMuted(muted);
    }
}
