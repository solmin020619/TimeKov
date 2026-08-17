using System.Collections;
using UnityEngine;

// ── 개폐 대상: 미닫이(슬라이드) 문 ──────────────────────────────────────────────
// GimmickTarget 구현체. 외부 에셋 DoorHori(좌우로 미끄러지는 자동문)를 '기믹으로' 여닫는다.
//   DoorHori 자체는 근접 트리거로 열렸다가 waitTime 뒤 자동으로 닫히지만, 이 컴포넌트는
//   퍼즐이 풀리면 '열린 채로 계속 유지'한다(자동 닫힘 없음). 각 문짝의 이동은 DoorHori 의
//   translateValue 를 그대로 읽어 동일한 궤적(부모 로컬 Y축)으로 재현하므로 어긋나지 않는다.
//
//   ★ 이 스크립트는 문짝마다 붙이지 않는다. 문 루트(또는 아무 오브젝트) 하나에 붙이고
//     doors 에 Door_Left / Door_Right 두 짝(DoorHori)을 넣는다.
public class GimmickSlideDoor : GimmickTarget
{
    [Header("문짝 (DoorHori)")]
    [Tooltip("열 문짝들의 DoorHori. 예) Door_Left, Door_Right 두 개를 넣는다.\n" +
             "각 문짝은 자기 translateValue 만큼 밀려 열린다(방향/거리 자동).")]
    [SerializeField] private DoorHori[] doors;

    [Tooltip("여닫는 데 걸리는 시간(초). DoorHori 의 easeTime 과 비슷하게.")]
    [SerializeField] private float slideTime = 0.5f;

    private Vector3[] _closedLocal;   // 각 문짝의 닫힘(원래) 로컬 위치
    private Coroutine _move;

    private bool _closedCaptured;

    protected override void Start()
    {
        // 아직 아무도 안 움직인 '닫힘' 위치를 먼저 캡처한 뒤 base.Start(초기상태 반영)를 부른다.
        EnsureClosedCaptured();
        base.Start();
    }

    /// <summary>닫힘(원래) 위치 캡처. Start 뿐 아니라 ApplyState 첫 호출에서도 부른다.
    /// ★세이브 복원 트리거는 자기 Awake 에서 SetOpen 을 부르는데 Awake 끼리의 실행 순서는
    ///   정해져 있지 않다 → Start 보다 ApplyState 가 먼저 올 수 있고, 그때 _closedLocal 이
    ///   null 이면 TargetLocal 에서 터진다. 순서에 의존하지 않게 여기서 한 번만 잡는다.</summary>
    private void EnsureClosedCaptured()
    {
        if (_closedCaptured) return;
        _closedCaptured = true;

        int n = doors != null ? doors.Length : 0;
        _closedLocal = new Vector3[n];
        for (int i = 0; i < n; i++)
            if (doors[i] != null) _closedLocal[i] = doors[i].transform.localPosition;
    }

    protected override void ApplyState(bool open, bool instant)
    {
        if (doors == null) return;
        EnsureClosedCaptured();

        if (_move != null) { StopCoroutine(_move); _move = null; }

        // 실제 개폐(연출)일 때 문짝의 기존 AudioSource(원래 슬라이드 소리)를 그대로 재생. 시작 초기화(instant)엔 무음.
        if (!instant) PlayDoorAudio();

        if (instant || slideTime <= 0f)
        {
            for (int i = 0; i < doors.Length; i++)
                if (doors[i] != null) doors[i].transform.localPosition = TargetLocal(i, open);
            return;
        }

        _move = StartCoroutine(SlideRoutine(open));
    }

    private IEnumerator SlideRoutine(bool open)
    {
        var from = new Vector3[doors.Length];
        var to   = new Vector3[doors.Length];
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null) continue;
            from[i] = doors[i].transform.localPosition;
            to[i]   = TargetLocal(i, open);
        }

        float t = 0f;
        while (t < slideTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / slideTime));
            for (int i = 0; i < doors.Length; i++)
                if (doors[i] != null) doors[i].transform.localPosition = Vector3.Lerp(from[i], to[i], k);
            yield return null;
        }

        for (int i = 0; i < doors.Length; i++)
            if (doors[i] != null) doors[i].transform.localPosition = to[i];
        _move = null;
    }

    // 문짝에 붙은 기존 AudioSource(원래 문 슬라이드 소리)를 재생 — DoorHori.OpenDoor 와 같은 소리.
    private void PlayDoorAudio()
    {
        foreach (var d in doors)
        {
            if (d == null) continue;
            var au = d.GetComponent<AudioSource>();
            if (au != null) au.Play();
        }
    }

    // 열림 목표 = 닫힘 + (0, -translateValue, 0). DoorHori 의 OpenDoor 궤적과 동일.
    private Vector3 TargetLocal(int i, bool open)
    {
        Vector3 closed = _closedLocal[i];
        if (!open) return closed;
        return closed + new Vector3(0f, -doors[i].translateValue, 0f);
    }
}
