// MachineAnimationEffect.cs
// 설비 가공 중일 때만 애니메이션·이펙트를 재생한다.
//
// [사용법]
//   1. 설비 게임오브젝트에 이 컴포넌트 추가
//   2. 인스펙터에서 animator(선택), effects(ParticleSystem 배열, 선택) 연결
//   3. ProcessingMachine이 StartProduction() / StopProduction() 자동 호출
//
// [종료 처리]
//   - 애니메이션 : 현재 루프가 끝나는 시점까지 재생 후 Animator 비활성화
//   - 이펙트     : 새 파티클 방출 중지 → 기존 파티클 수명 다할 때까지 대기 → 비활성화

using System.Collections;
using UnityEngine;

public class MachineAnimationEffect : MonoBehaviour
{
    [Header("애니메이션 (선택)")]
    [Tooltip("재생할 Animator 컴포넌트들. 비워두면 애니메이션 제어 생략.")]
    [SerializeField] private Animator[] animators;

    [Header("이펙트 (선택)")]
    [Tooltip("가공 중 재생할 ParticleSystem 목록.")]
    [SerializeField] private ParticleSystem[] effects;

    [Header("종료 대기 시간 (최대)")]
    [Tooltip("마지막 파티클이 사라질 때까지 기다리는 최대 시간 (초). 이 시간이 지나면 강제 종료.")]
    [SerializeField] private float maxEffectFadeTime = 5f;

    // ─────────────────────────────────────────────────────────────────────────

    private bool      _playing    = false;
    private Coroutine _stopRoutine = null;

    // ── 초기화 ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // 시작 시 모두 끄기 (프리팹에 상시 켜둔 상태여도 꺼줌)
        SetImmediateOff();
    }

    // ── Public API (ProcessingMachine 에서 호출) ──────────────────────────────

    /// <summary>가공 시작 — 애니메이션·이펙트 ON.</summary>
    public void StartProduction()
    {
        // 종료 코루틴이 진행 중이면 취소 (재시작 시)
        if (_stopRoutine != null)
        {
            StopCoroutine(_stopRoutine);
            _stopRoutine = null;
        }

        if (_playing) return;
        _playing = true;

        // Animator 활성화
        foreach (var anim in animators)
            if (anim != null) anim.enabled = true;

        // ParticleSystem 재생
        foreach (var ps in effects)
        {
            if (ps == null) continue;
            ps.gameObject.SetActive(true);
            ps.Play(withChildren: true);
        }
    }

    /// <summary>가공 중단/완료 — 현재 루프·파티클이 끝나면 OFF.</summary>
    public void StopProduction()
    {
        if (!_playing) return;
        _playing = false;

        if (_stopRoutine != null) StopCoroutine(_stopRoutine);
        _stopRoutine = StartCoroutine(StopGracefully());
    }

    // ── 내부 처리 ─────────────────────────────────────────────────────────────

    /// 현재 애니메이션 루프 끝까지 재생 후 Animator 비활성화.
    /// 파티클은 방출 중지 후 기존 파티클 자연 소멸 대기.
    private IEnumerator StopGracefully()
    {
        // ① 파티클 : 신규 방출 중지 (기존 파티클은 수명대로 소멸)
        foreach (var ps in effects)
        {
            if (ps == null) continue;
            ps.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
        }

        // ② 애니메이션 : 모든 Animator의 현재 루프가 끝날 때까지 대기 후 비활성화
        float maxRemaining = 0f;
        foreach (var anim in animators)
        {
            if (anim == null || !anim.enabled) continue;
            var info      = anim.GetCurrentAnimatorStateInfo(0);
            float frac    = info.normalizedTime % 1f;
            float remaining = (frac > 0.001f) ? (1f - frac) * info.length : 0f;
            if (remaining > maxRemaining) maxRemaining = remaining;
        }
        if (maxRemaining > 0.05f)
            yield return new WaitForSeconds(maxRemaining);

        foreach (var anim in animators)
            if (anim != null) anim.enabled = false;

        // ③ 파티클 : 모든 파티클이 사라질 때까지 대기 (최대 maxEffectFadeTime 초)
        float waited = 0f;
        bool anyAlive = true;
        while (anyAlive && waited < maxEffectFadeTime)
        {
            anyAlive = false;
            foreach (var ps in effects)
            {
                if (ps != null && ps.IsAlive(withChildren: true))
                {
                    anyAlive = true;
                    break;
                }
            }
            if (anyAlive)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        // ④ 완전 비활성화
        foreach (var ps in effects)
        {
            if (ps == null) continue;
            ps.Clear(withChildren: true);
            ps.gameObject.SetActive(false);
        }

        _stopRoutine = null;
    }

    /// 즉시 강제 종료 (Awake 초기화 / OnDisable 등)
    private void SetImmediateOff()
    {
        if (_stopRoutine != null) { StopCoroutine(_stopRoutine); _stopRoutine = null; }
        _playing = false;

        foreach (var anim in animators)
            if (anim != null) anim.enabled = false;

        foreach (var ps in effects)
        {
            if (ps == null) continue;
            ps.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(false);
        }
    }

    private void OnDisable() => SetImmediateOff();
}
