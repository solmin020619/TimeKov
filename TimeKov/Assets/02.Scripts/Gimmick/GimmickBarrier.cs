using System.Collections;
using UnityEngine;

// ── 공용 개폐 대상: 결계 / 봉인 / 문 / 다리 전부 이거 하나로 ────────────────────
// GimmickTarget 구현체. "열림(통행 가능)/닫힘(막힘)"을 오브젝트 켜고 끄기로 표현한다.
//   • blockObjects   : 열리면 사라지는 것들 = 레이저 결계 메시/파티클, 원형 봉인, 닫힌 문.
//                      (그 안에 Collider 가 있으면 통행 차단도 같이 해제된다 — 오브젝트째 On/Off)
//   • passObjects    : 열리면 나타나는 것들 = 다리, 발판 등(막힘 상태에선 없음).
//   보통 blockObjects 만 쓴다. 다리류만 passObjects 를 쓴다. 둘 다 써도 됨(문 열리며 다리 생성 등).
//
//   flickerOut=true 면 열릴 때 blockObjects 가 "깜빡깜빡 하며 꺼짐"(전력 내려가는 연출). 재질 무관하게 동작.
//   ★이 컴포넌트는 blockObjects 와 '별개' 오브젝트(빈 부모)에 두는 걸 권장 — 깜빡임 코루틴 호스트가
//     꺼지면 안 되기 때문(자기 자신을 blockObjects 에 넣지 말 것).
public class GimmickBarrier : GimmickTarget
{
    [Header("열리면 사라짐 (결계/봉인/닫힌 문 — 통행 차단 오브젝트)")]
    [Tooltip("열림 시 비활성화. 이 안의 Collider 가 통행을 막고 있었다면 함께 해제된다.")]
    [SerializeField] private GameObject[] blockObjects;

    [Header("열리면 나타남 (다리/발판 등 — 선택)")]
    [Tooltip("열림 시 활성화. 다리 생성처럼 '열려야 생기는' 것에 쓴다. 없으면 비워둔다.")]
    [SerializeField] private GameObject[] passObjects;

    [Header("깜빡이며 사라짐 (레이저 전력 다운 연출)")]
    [Tooltip("체크: 열릴 때 blockObjects 가 깜빡깜빡 하다 꺼진다. 해제: 즉시 꺼짐.")]
    [SerializeField] private bool flickerOut = true;
    [Tooltip("깜빡임 총 지속 시간(초).")]
    [SerializeField] private float flickerDuration = 0.7f;
    [Tooltip("한 번 깜빡이는 간격(초). 작을수록 빠르게 점멸. 실제론 약간 랜덤하게 흔들린다.")]
    [SerializeField] private float flickerInterval = 0.08f;

    [Header("소멸 사운드")]
    [Tooltip("결계가 열릴(사라질) 때 재생. 레이저 결계는 기본 전자식 전환음(LaserBarrierDown).\n" +
             "문/다리 등 다른 용도로 쓸 땐 None 으로 바꾸거나 다른 사운드로 교체.")]
    [SerializeField] private SfxId powerDownSfx = SfxId.LaserBarrierDown;

    private Coroutine _flicker;

    protected override void ApplyState(bool open, bool instant)
    {
        if (!open)
        {
            // 닫힘(막힘)으로 복귀: 즉시.
            StopFlicker();
            SetActiveAll(blockObjects, true);
            SetActiveAll(passObjects, false);
            return;
        }

        // 열림: 통과용(다리)은 즉시 켜고, 막던 것은 깜빡이며(또는 즉시) 끈다.
        SetActiveAll(passObjects, true);

        // 실제 개방(연출)일 때만 소멸음. 시작 시 초기화(instant)에선 무음.
        //   powerDownSfx 가 None(기존 프리팹이 신규필드 기본값을 못 받은 경우)이면 기본 레이저음으로 폴백.
        //   ★문/다리 등 다른 용도로 쓰며 무음을 원하면 이 폴백 대신 별도 사운드를 지정할 것.
        if (!instant)
        {
            SfxId sfx = powerDownSfx == SfxId.None ? SfxId.LaserBarrierDown : powerDownSfx;
            GameSfx.Play(sfx);   // 결계 해제 이벤트 = 거리 무관 2D(빈 부모가 멀리 있어도 또렷하게)
        }

        if (instant || !flickerOut)
        {
            SetActiveAll(blockObjects, false);
        }
        else
        {
            StopFlicker();
            _flicker = StartCoroutine(FlickerThenOff());
        }
    }

    private IEnumerator FlickerThenOff()
    {
        float t = 0f;
        bool on = true;
        while (t < flickerDuration)
        {
            on = !on;
            SetActiveAll(blockObjects, on);
            // 간격을 약간 랜덤하게 → 기계적이지 않은 '전력 불안정' 느낌
            float step = flickerInterval * Random.Range(0.6f, 1.4f);
            yield return new WaitForSeconds(step);
            t += step;
        }
        SetActiveAll(blockObjects, false);   // 최종 소멸
        _flicker = null;
    }

    private void StopFlicker()
    {
        if (_flicker != null) { StopCoroutine(_flicker); _flicker = null; }
    }

    private static void SetActiveAll(GameObject[] arr, bool active)
    {
        if (arr == null) return;
        foreach (var go in arr)
            if (go != null) go.SetActive(active);
    }
}
