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

    [NonSerialized] int _index;

    // 입력형(클릭/키)이라 OnUIActivated (슬라이드 인 끝난 뒤 활성)
    public override ActivationTiming Timing => ActivationTiming.OnUIActivated;

    public override void Activate()
    {
        _index = 0;
        ShowCurrent();
    }

    public override void Deactivate()
    {
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
        ov.ShowContinueStep(s.stepLabel, s.spotlightTargetId, OnAdvance, s.advanceKey);
    }

    void OnAdvance()
    {
        if (IsInGracePeriod) return;
        _index++;
        if (_index >= steps.Length) { Complete(); return; }
        ShowCurrent();   // 오버레이 안 닫고 내용만 교체 → 끊김 없음
    }
}
