using System;
using UnityEngine;

// 도감 알림(!) 상태 허브 - 세션 단위(저장 없음, CodexDiscovery/RecipeProgress와 동일 정책).
// 카테고리별 "아직 안 본 새 항목" 플래그. 인덱스 = CodexUI 탭(0=튜토 / 1=몬스터 / 2=설비).
// 발견/해금/임계 도달 시 MarkUnseen, 도감서 그 탭을 열어보면 MarkSeen.
// K HUD 배지(CodexNoticeBadge)와 도감 탭 배지가 이 상태를 읽어 ! 를 띄운다.
public static class CodexNotice
{
    public const int Tutorial = 0;
    public const int Monster  = 1;
    public const int Facility = 2;
    public const int CategoryCount = 3;

    private static readonly bool[] _unseen = new bool[CategoryCount];

    // 상태 변화 통보 - HUD 배지가 구독해 즉시 on/off.
    public static event Action OnChanged;

    public static bool IsUnseen(int category)
        => category >= 0 && category < CategoryCount && _unseen[category];

    // 한 카테고리라도 안 본 새 항목이 있으면 true (K HUD 배지 표시 기준).
    public static bool AnyUnseen
    {
        get
        {
            for (int i = 0; i < CategoryCount; i++)
                if (_unseen[i]) return true;
            return false;
        }
    }

    // 새 항목 발견/해금 시 호출. 이미 표시 중이면 무시(이벤트 중복 방지).
    public static void MarkUnseen(int category)
    {
        if (category < 0 || category >= CategoryCount) return;
        if (_unseen[category]) return;
        _unseen[category] = true;
        OnChanged?.Invoke();
    }

    // 도감서 그 카테고리를 열어보면 호출. 표시 중일 때만 끄고 통보.
    public static void MarkSeen(int category)
    {
        if (category < 0 || category >= CategoryCount) return;
        if (!_unseen[category]) return;
        _unseen[category] = false;
        OnChanged?.Invoke();
    }

    // 세션 시작마다 초기화. OnChanged 도 비워 이전 플레이의 죽은 구독 제거(에디터 리로드 비활성 대비).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        for (int i = 0; i < CategoryCount; i++) _unseen[i] = false;
        OnChanged = null;
    }
}
