using System.Collections.Generic;
using UnityEngine;

// ── 순서 밟기 조건 트리거 (압력판 순차 퍼즐) ─────────────────────────────────────
// PressurePlate 들을 '정해진 순서대로' 밟아야 열린다.
//   • sequence 순서대로 밟으면 진행(맞은 판은 눌린 채 고정) → 전부 맞게 밟으면 타깃 개방.
//   • 이미 맞게 밟은 판을 다시 밟으면 → 아무 일 없음(실패 아님).
//   • 아직 안 밟은 '엉뚱한 판'을 밟으면 → 진행 0으로 초기화(눌린 판 전부 올라오고 전체 빨강 점등).
//
//   압력판은 '밟았다 떼는' 순간형이라 '눌리는 순간(rising edge)'만 본다(떼는 건 무시).
//   sequence 의 null 슬롯은 자동 제거, 같은 판 중복 등록도 안전(구독은 1회).
//   래치(latch, 부모)=true 로 두면 한 번 완성되면 계속 열림(상자 해금/문 등, 기본).
public class SequenceTrigger : GimmickTrigger
{
    [Header("밟을 순서 (위→아래 순)")]
    [Tooltip("이 순서대로 밟아야 열린다. 리스트 순서가 곧 정답 순서.")]
    [SerializeField] private List<PressurePlate> sequence = new();

    [Header("오답 처리")]
    [Tooltip("체크: 순서를 틀리면 진행도를 0으로 되돌린다(대부분 체크).")]
    [SerializeField] private bool resetOnWrong = true;
    [Tooltip("틀렸을 때 재생할 사운드(삑- 등). None 이면 무음.")]
    [SerializeField] private SfxId wrongSfx = SfxId.None;
    [Tooltip("한 단계 맞게 밟을 때 재생(선택). None 이면 무음.")]
    [SerializeField] private SfxId stepSfx = SfxId.None;

    [Header("색 피드백")]
    [Tooltip("체크: 맞게 밟은 판은 파랑으로 켜지고, 틀리면 전체가 빨강으로 한 번 점등된다.")]
    [SerializeField] private bool colorFeedback = true;
    [Tooltip("맞게 밟은 판이 켜지는 색.")]
    [SerializeField] private Color correctColor = new Color(0.2f, 0.6f, 1f);   // 파랑
    [Tooltip("틀렸을 때 전체가 점등되는 색.")]
    [SerializeField] private Color wrongColor = new Color(1f, 0.15f, 0.1f);    // 빨강
    [Tooltip("틀림 점등 지속 시간(초).")]
    [SerializeField] private float wrongFlashTime = 0.35f;

    private int _index;                                    // 다음에 밟아야 할 순서 인덱스
    private readonly List<PressurePlate> _seq = new();      // null 제거한 실제 순서
    private readonly List<PressurePlate> _subscribed = new(); // 중복 없이 실제 구독한 판(이중진행 방지)

    private void Start()
    {
        // null 슬롯 제거(있으면 퍼즐이 영영 안 풀림) + 중복 판은 한 번만 구독(이중 진행 방지).
        _seq.Clear(); _subscribed.Clear();
        foreach (var p in sequence)
        {
            if (p == null) continue;
            _seq.Add(p);
            if (!_subscribed.Contains(p))
            {
                _subscribed.Add(p);
                p.OnChanged += OnPlateChanged;
            }
        }

        RestoreProgress();
    }

    // ── 세이브 ────────────────────────────────────────────────────────────
    // '몇 번째까지 맞게 밟았는지'를 저장한다. 그래야 3칸 중 1칸만 밟아 둔 상태로 나갔다 와도
    //   그 1칸만 눌린 채로 돌아온다(전부 처음부터 다시 밟게 하지 않는다).
    //
    //   ★완성(IsSatisfied)은 부모(GimmickTrigger)가 따로 저장한다. 여기서는 '진행 중'만 다룬다.
    //   ★판 복원은 Start 에서 한다 — PressurePlate 의 발광 준비가 끝난 뒤여야 색이 먹는다.
    private string ProgressKey => GimmickSave.Key("seq", this, saveId);

    private void RestoreProgress()
    {
        if (!persistSatisfied) return;

        // 이미 완성된 판이면 전부 눌린 채로. 문은 부모가 이미 열어 뒀고, 판만 올라와 있으면
        //   다 푼 퍼즐이 안 푼 것처럼 보인다.
        int held = IsSatisfied
            ? _seq.Count
            : Mathf.Clamp(GimmickSave.GetInt(ProgressKey), 0, _seq.Count);
        if (held <= 0) return;

        _index = held;
        for (int i = 0; i < held; i++)
        {
            var p = _seq[i];
            if (p == null) continue;
            p.SetHeldDown(true);
            if (colorFeedback) p.SetGlow(correctColor);
        }
    }

    private void SaveProgress()
    {
        if (persistSatisfied) GimmickSave.Set(ProgressKey, _index);
    }

    private void OnDestroy()
    {
        foreach (var p in _subscribed)
            if (p != null) p.OnChanged -= OnPlateChanged;
    }

    private void OnPlateChanged(PressurePlate plate)
    {
        if (plate == null || !plate.IsPressed) return;   // '눌리는 순간'만, 떼는 건 무시
        if (_seq.Count == 0 || IsSatisfied) return;       // 이미 완성됐으면 무시

        // 1) 다음에 밟아야 할 판을 맞게 밟음 → 진행.
        if (plate == _seq[_index])
        {
            _index++;
            plate.SetHeldDown(true);                          // 발 떼도 눌린 채 유지
            if (colorFeedback) plate.SetGlow(correctColor);   // 맞은 판 파랑 고정
            SaveProgress();
            if (_index >= _seq.Count)
                SetSatisfied(true);   // 순서 완성 → 타깃 개방(래치면 영구)
            else
                GameSfx.Play(stepSfx, plate.transform.position);   // None 은 자동 무음
            return;
        }

        // 2) 이미 맞게 밟아 고정된 판을 또 밟음 → 아무 일 없음(실패 아님).
        if (plate.IsHeldDown) return;

        // 3) 그 외(아직 안 밟은 엉뚱한 판) → 진행 초기화 + 전체 빨강 한 번 점등.
        if (!resetOnWrong) return;
        _index = 0;
        SaveProgress();   // 틀려서 처음으로 돌아간 것도 저장한다
        GameSfx.Play(wrongSfx, plate.transform.position);
        foreach (var p in _seq)
        {
            p.SetHeldDown(false);                    // 고정 해제 → 다시 올라옴
            if (colorFeedback)
            {
                p.ClearGlow();                       // 진행(파랑) 끔 — 같은 프레임이라 소등은 안 보임
                p.Flash(wrongColor, wrongFlashTime); // 빨강 한 번 점등 후 소등 복귀
            }
        }
    }
}
