using System;
using UnityEngine;

/// <summary>
/// 엔드필드식 "안내 + 강조 + 클릭하여 계속" 단계.
/// 상단 배너(label) + 선택적 스포트라이트(spotlightTargetId)를 띄우고,
/// 아무 곳이나 클릭하면 완료된다. 순수 설명/코치마크용.
/// </summary>
[CreateAssetMenu(menuName = "Quest/Objective/Continue")]
public class ContinueObjective : ObjectiveSO
{
    [Tooltip("스포트라이트로 강조할 UI 타깃 id (TutorialHighlightTarget 와 매칭). 비우면 강조 없이 배너만.")]
    public string spotlightTargetId;

    [Tooltip("None = 아무 곳이나 클릭/키로 진행. 특정 키 지정 시 그 키로만 진행 (예: C로 스탯창 열기).")]
    public KeyCode advanceKey = KeyCode.None;

    // 입력형(클릭)이라 OnUIActivated (슬라이드 인 끝난 뒤 활성, 고인물 방어)
    public override ActivationTiming Timing => ActivationTiming.OnUIActivated;

    public override void Activate()
    {
        var ov = TutorialOverlay.I;
        if (ov != null)
            ov.ShowContinueStep(label, spotlightTargetId, OnContinueClicked, advanceKey);
        else
            Complete();   // 오버레이 생성 불가(에디터 등) 시 막히지 않게 즉시 완료
    }

    public override void Deactivate()
    {
        if (TutorialOverlay.HasInstance)
            TutorialOverlay.I.Hide();
    }

    public override float Progress => IsCompleted ? 1f : 0f;

    private void OnContinueClicked()
    {
        if (IsInGracePeriod) return;
        Complete();
    }
}
