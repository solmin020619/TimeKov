using System;
using UnityEngine;

/// <summary>
/// 여러 안내 단계를 하나의 오버레이에서 연속 표시.
/// 클릭(또는 지정 키)으로 다음 단계로 — 오버레이를 닫지 않고 배너/스포트라이트만 교체해 끊김이 없다.
/// 마지막 단계까지 넘기면 완료. (시작 가이드 투어용)
/// </summary>
[CreateAssetMenu(menuName = "Quest/Objective/GuidedTour")]
public class GuidedTourObjective : ObjectiveSO
{
    [Serializable]
    public class Step
    {
        [TextArea(2, 4)] public string stepLabel;
        [Tooltip("강조할 UI 타깃 id (비우면 전체 딤). TutorialHighlightTarget/코드 등록과 매칭.")]
        public string spotlightTargetId;
        [Tooltip("None = 아무 곳이나 클릭/키로 진행. 특정 키 지정 시 그 키로만 (예: C).")]
        public KeyCode advanceKey = KeyCode.None;
    }

    public Step[] steps;

    [Tooltip("ON이면 투어 활성 시 건설 모드가 아니면 강제로 진입(건축투어 전용).\n" +
             "퀘 사이 갭에서 B를 연타로 들어갔다 나가버려도 투어가 항상 건설 모드에서 뜨게 보장.")]
    public bool ensureBuildMode = false;

    [NonSerialized] int _index;
    [NonSerialized] bool _waitingForBuildMode;

    // 코치마크는 퀘 표시 즉시 떠야 함 - 슬라이드인(0.3s) 기다리면 그 사이 입력차단이 안 걸려 미리 조작 가능.
    // 카운트형이 아니라 고인물 방어 불필요(첫 클릭은 startGracePeriod로 무시).
    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    // 순수 설명 코치마크(여러 스텝) - 완료 시 트래커 완료배너/토스트 생략
    public override bool IsExplanation => true;

    public override void Activate()
    {
        _index = 0;
        // 건축투어: 활성 시 건설 모드가 아니면 강제 진입 (B 연타로 빠져나가도 투어가 건설 모드에서 뜨도록).
        if (ensureBuildMode)
        {
            var bm = UnityEngine.Object.FindAnyObjectByType<BuildManager>();
            if (bm != null && !bm.IsBuildMode && !bm.EnterBuildMode())
            {
                // 존 밖/다른 UI 열림 등으로 진입 실패 → 투어를 3인칭 위에 띄우지 않고, 실제 진입할 때까지 대기.
                GameEvents.OnBuildModeEntered += OnBuildModeReady;
                _waitingForBuildMode = true;
                return;
            }
        }
        ShowCurrent();
    }

    void OnBuildModeReady()
    {
        GameEvents.OnBuildModeEntered -= OnBuildModeReady;
        _waitingForBuildMode = false;
        ShowCurrent();
    }

    public override void Deactivate()
    {
        if (_waitingForBuildMode)
        {
            GameEvents.OnBuildModeEntered -= OnBuildModeReady;
            _waitingForBuildMode = false;
        }
        if (TutorialOverlay.HasInstance) TutorialOverlay.I.Hide();
    }

    public override float Progress
        => (steps == null || steps.Length == 0) ? 1f : Mathf.Clamp01((float)_index / steps.Length);

    void ShowCurrent()
    {
        if (steps == null || steps.Length == 0) { Complete(); return; }
        var ov = TutorialOverlay.I;
        if (ov == null) { Complete(); return; }   // 오버레이 생성 불가(에디터 등) 시 막히지 않게
        var s = steps[_index];
        ov.ShowContinueStep(Loc.Get(s.stepLabel), s.spotlightTargetId, OnAdvance, s.advanceKey);
    }

    void OnAdvance()
    {
        if (IsInGracePeriod) return;
        _index++;
        if (_index >= steps.Length) { Complete(); return; }
        ShowCurrent();   // 오버레이 안 닫고 내용만 교체 → 끊김 없음
    }
}
