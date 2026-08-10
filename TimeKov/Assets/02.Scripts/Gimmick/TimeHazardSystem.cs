using System.Collections.Generic;
using UnityEngine;

// ── 시간 급속 감소 구역 중재기 ───────────────────────────────────────────────
// 위험 구역(TimeHazardZone)과 그 안의 안전지대(TimeSafeZone)는 서로 겹쳐 있다.
// 플레이어가 "위험 구역 안 + 안전지대 안"에 동시에 있을 수 있으므로, 누가 이기는지
// 한 곳에서 판정해야 값이 엉키지 않는다. 그 판정을 여기서 한다.
//
//   우선순위:  안전지대 > 위험 구역 > 평소
//     • 안전지대 안         → 시간 감소 완전 정지(배율 0), 화면효과 끔
//     • 위험 구역 안(만)    → 가장 강한 구역의 배율 적용, 화면효과 켬
//     • 둘 다 밖            → 평소대로(배율 1), 화면효과 끔
//
//   시간 감소 자체는 PlayerStatComponent.HandleHpDrain 이 담당한다:
//       CurrentHp -= HpDrainRate * HpDrainMultiplier * Time.deltaTime;   // 기지 안이면 건너뜀
//   즉 여기서는 HpDrainMultiplier(배율)만 바꾼다. 감소 로직·HUD 표시는 기존 것을 그대로 쓴다.
//
//   ⚠ HpDrainMultiplier 는 보스 포효(BossRoarDebuff)도 쓰는 공용 값이다. 위험 구역 안에서
//     보스 포효가 끝나면 포효가 배율을 1로 되돌려 구역 효과가 풀릴 수 있다. 그때는
//     Refresh() 가 다시 불릴 때(구역 출입 시) 복구된다. 완전 분리가 필요해지면
//     PlayerStatComponent 에 별도 채널을 추가해야 한다.
public static class TimeHazardSystem
{
    private static readonly List<TimeHazardZone> _hazards = new();
    private static readonly List<TimeSafeZone>   _safes   = new();

    private static PlayerStatComponent _stat;
    private static Object _fxOwner;      // 지금 화면효과를 켠 구역(끌 때 주인 확인용)

    // 에디터에서 플레이 반복 시 이전 판의 등록이 남지 않도록 초기화.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _hazards.Clear();
        _safes.Clear();
        _stat = null;
        _fxOwner = null;
    }

    // ── 구역이 호출 ──────────────────────────────────────────────────────────
    public static void EnterHazard(TimeHazardZone zone, PlayerStatComponent stat)
    {
        if (zone == null) return;
        _stat = stat;
        if (!_hazards.Contains(zone)) _hazards.Add(zone);
        Refresh();
    }

    public static void ExitHazard(TimeHazardZone zone)
    {
        if (_hazards.Remove(zone)) Refresh();
    }

    public static void EnterSafe(TimeSafeZone zone, PlayerStatComponent stat)
    {
        if (zone == null) return;
        _stat = stat;
        if (!_safes.Contains(zone)) _safes.Add(zone);
        Refresh();
    }

    public static void ExitSafe(TimeSafeZone zone)
    {
        if (_safes.Remove(zone)) Refresh();
    }

    // 현재 상태(디버그/HUD 확장용)
    public static bool InSafeZone   => _safes.Count   > 0;
    public static bool InHazardZone => _hazards.Count > 0;

    // ── 판정 ─────────────────────────────────────────────────────────────────
    private static void Refresh()
    {
        // 파괴된 구역 정리(오브젝트가 사라져도 목록에 남아 있으면 영영 안 풀린다).
        _hazards.RemoveAll(z => z == null);
        _safes.RemoveAll(z => z == null);

        if (_stat == null) return;

        if (_safes.Count > 0)
        {
            // 안전지대: 시간 감소 완전 정지. 화면도 평온하게.
            _stat.HpDrainMultiplier = 0f;
            HideFx();
            return;
        }

        if (_hazards.Count > 0)
        {
            // 위험 구역이 여러 개 겹치면 가장 강한(빨리 닳는) 쪽을 따른다.
            TimeHazardZone worst = _hazards[0];
            for (int i = 1; i < _hazards.Count; i++)
                if (_hazards[i].DrainMultiplier > worst.DrainMultiplier) worst = _hazards[i];

            _stat.HpDrainMultiplier = Mathf.Max(0f, worst.DrainMultiplier);
            ShowFx(worst);
            return;
        }

        // 전부 밖: 평소대로.
        _stat.HpDrainMultiplier = 1f;
        HideFx();
    }

    private static void ShowFx(TimeHazardZone zone)
    {
        if (!zone.UseScreenEffect) { HideFx(); return; }
        _fxOwner = zone;
        TimeHazardScreenFx.Show(zone, zone.BuildFxConfig());
    }

    private static void HideFx()
    {
        if (_fxOwner == null) return;
        TimeHazardScreenFx.Hide(_fxOwner);
        _fxOwner = null;
    }
}
