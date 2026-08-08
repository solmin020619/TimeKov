// SettingsData.cs — 새 설정 UI가 쓰는 표시용 열거형/범위 상수만 둔다.
//
// 설정값 자체와 저장·엔진 반영은 전부 GlobalSettingsManager가 담당한다.
// 이 UI는 그 위에 얹히는 "뷰"일 뿐이라 자체 상태나 PlayerPrefs를 갖지 않는다.
// (예전에는 여기에 자체 SettingsData + PlayerPrefs 계층이 있었지만, 저장 위치가
//  둘로 갈라져 실제 게임에 반영되지 않았다 — 기존 시스템으로 일원화함)
using UnityEngine;

namespace GameSettingsUI
{
    public enum SettingsTab { Display, Audio, Controls }
    public enum DisplayMode { Full, Window }

    // 설정창 열기 연출.
    // 스크림(어두운 막)이 화면 전체를 덮으므로, 화면보다 작아지거나 밀리는 연출은
    // 가장자리에 게임 화면이 비치는 틈을 만든다. 그래서 배율은 항상 1 이상에서 시작한다.
    public enum OpenAnim { None, FadeOnly, FadeSettle }

    // 슬라이더 범위/스텝
    public struct SliderMeta
    {
        public float min, max, step;
        public SliderMeta(float min, float max, float step) { this.min = min; this.max = max; this.step = step; }
        public float Snap(float v)
        {
            v = Mathf.Clamp(v, min, max);
            v = Mathf.Round(v / step) * step;
            return Mathf.Clamp(v, min, max);
        }
    }

    public static class SliderRanges
    {
        // 볼륨: 모델(GlobalSettingsManager)은 0~1로 들고 있고, 화면에는 0~100으로 보여준다.
        public static readonly SliderMeta Volume = new SliderMeta(0f, 100f, 1f);
        public static readonly SliderMeta Sens   = new SliderMeta(0.1f, 3f, 0.05f);
    }
}
