using System;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 엔드필드식 "영상 팝업" 튜토리얼 단계.
/// 여러 페이지(영상 + 제목 + 본문)를 멀티페이지 팝업으로 보여주고,
/// 다 읽고 닫으면 완료된다. 순수 설명용(IsExplanation) - 완료 배너/토스트 없음.
///
/// 실제 팝업 UI/입력/영상 재생은 TutorialVideoUI(씬 Canvas/Overlays 아래 실물)가 담당한다.
/// objective 는 "팝업 열기 -> 닫히면 완료"만 책임진다(ContinueObjective 와 동일 구조).
/// 이렇게 자기 UI 생명주기로만 구동돼 EVENT-IN-GAP 소프트락 위험이 없다.
/// </summary>
public class TutorialVideoObjective : ObjectiveSO
{
    [Tooltip("팝업에서 넘겨 볼 페이지들. 각 페이지 = 영상 클립 + 제목 + 본문. clip 이 비어도(null) 텍스트만으로 정상 동작.")]
    public VideoTutorialPage[] pages;

    // 코치마크와 동일: 슬라이드인 기다리지 않고 표시 즉시 활성(그 사이 미리 조작 방지).
    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    // 순수 설명 - 완료 시 트래커 완료배너/토스트 생략.
    public override bool IsExplanation => true;

    public override void Activate()
    {
        if (pages == null || pages.Length == 0) { Complete(); return; }

        var ui = TutorialVideoUI.I;
        if (ui != null) ui.Show(pages, OnClosed);
        else Complete();   // 씬에 팝업이 없으면(빌더 미실행) 튜토리얼이 막히지 않게 즉시 완료
    }

    public override void Deactivate()
    {
        if (TutorialVideoUI.HasInstance) TutorialVideoUI.I.Hide();
    }

    public override float Progress => IsCompleted ? 1f : 0f;

    private void OnClosed()
    {
        if (IsInGracePeriod) return;   // 직전 스텝 입력이 묻어 즉시 닫히는 것 방지
        Complete();
    }
}

/// <summary>영상 팝업 한 페이지 = 영상 클립 + 제목 + 본문.</summary>
[Serializable]
public class VideoTutorialPage
{
    [Tooltip("재생할 영상 클립. 비우면 '영상 준비 중' 플레이스홀더 + 텍스트만 표시(빌드/진행 안 깨짐).")]
    public VideoClip clip;

    [Tooltip("페이지 제목 (예: 설비 가공).")]
    public string title;

    [Tooltip("본문 설명. TMP rich text 사용 가능 (<color=#FFCC00>강조</color>).")]
    [TextArea(3, 6)]
    public string body;
}
