// UIColors.cs — 목업과 1:1로 일치하는 색상 상수. (Game Settings.dc.html 기준)
using UnityEngine;

namespace GameSettingsUI
{
    public static class UIColors
    {
        // HEX -> Color 헬퍼 (RGBA, 8자리 HEX는 알파 포함)
        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
        public static Color RGBA(float r, float g, float b, float a) =>
            new Color(r / 255f, g / 255f, b / 255f, a);

        /// 같은 색의 완전 투명 버전. 투명→불투명 트윈에서 RGB가 흔들리지 않게 시작색으로 쓴다.
        public static Color Transparent(Color c) => new Color(c.r, c.g, c.b, 0f);

        // --------------------------------------------------------------
        //  ⚠ 반투명 색은 전부 "합성 결과 불투명색"으로 미리 계산해 넣었다.
        //
        //  이 프로젝트는 Linear 컬러스페이스(ProjectSettings m_ActiveColorSpace: 1)라
        //  알파 블렌딩이 리니어 공간에서 일어난다. 목업(HTML/CSS)은 sRGB 공간에서
        //  합성하므로 같은 알파값이 전혀 다른 밝기로 보인다.
        //    예) 검정 배경 위 흰색 alpha 0.035
        //        CSS(sRGB)  → #0F0F10   (거의 안 보이는 은은한 패널)
        //        Unity(Linear) → #343434 (뚜렷한 회색 — 목업과 완전히 다름)
        //
        //  알파를 리니어용으로 다시 잡는 방법도 있지만, 배경색이 바뀌면 또 틀어진다.
        //  배경이 BG_Base(#060607)로 고정이므로 합성 결과를 불투명색으로 굳혔다.
        //  불투명색은 컬러스페이스와 무관하게 동일하게 렌더된다.
        //  주석의 rgba()가 목업 원본값이니 수정 시 그 기준으로 다시 계산할 것.
        // --------------------------------------------------------------

        public static readonly Color BgBase            = Hex("#060607");
        public static readonly Color AccentYellow      = Hex("#E9E24A");
        public static readonly Color AccentYellowSoft  = Hex("#ECE7B3");
        public static readonly Color SectionLabel      = Hex("#E6DB4D");
        public static readonly Color RowBG             = Hex("#0F0F10");  // rgba(255,255,255,0.035) on BG_Base
        public static readonly Color Divider           = Hex("#2E2E2F");  // rgba(255,255,255,0.16)  on BG_Base
        public static readonly Color SliderTrack       = Hex("#444445");  // rgba(255,255,255,0.22)  on Row_BG
        public static readonly Color HintText          = Hex("#C1C1C1");  // rgba(255,255,255,0.75)  on BG_Base

        public static readonly Color OffWhite          = Hex("#E6E4DD");
        public static readonly Color OffWhiteHover     = Hex("#C7C3B0");
        public static readonly Color OffWhiteActive    = Hex("#AEA892");
        public static readonly Color OptHover          = Hex("#CBC6B0");
        public static readonly Color OptActive         = Hex("#B8B39C");
        public static readonly Color OptDivider        = Hex("#CFCDC7");  // 검정 10% on OffWhite — 옵션 사이 경계선

        public static readonly Color TextDark          = Hex("#1C1C1C");
        public static readonly Color TextRow           = Hex("#EEEEEE");
        public static readonly Color TextValue         = Hex("#E8E8E8");

        public static readonly Color ToggleTrack       = Hex("#2A251C");
        public static readonly Color ToggleKnob        = Hex("#E6E4DD");
        public static readonly Color ToggleTextOff     = Hex("#B9B4A8");

        public static readonly Color TabContainer      = Hex("#1F1F23");  // rgba(52,52,58,0.55)     on BG_Base
        public static readonly Color TabBorder         = Hex("#313135");  // rgba(255,255,255,0.08)  on Tab_Container
        public static readonly Color TabIconInactive   = Hex("#DCDCDC");
        public static readonly Color TabDivider        = Hex("#3A3A3D");  // rgba(255,255,255,0.12)  on Tab_Container
        public static readonly Color TabHover          = Hex("#3E3E42");  // rgba(255,255,255,0.14)  on Tab_Container
        public static readonly Color TabActive         = Hex("#505053");  // rgba(255,255,255,0.22)  on Tab_Container

        public static readonly Color KeyBG             = Hex("#2B2B2B");
        public static readonly Color KeyBGHover        = Hex("#4A4A4A");
        public static readonly Color KeyBGActive       = Hex("#5C5C5C");
        public static readonly Color KeyBorder         = Hex("#494949");  // rgba(255,255,255,0.14)  on Key_BG
        public static readonly Color KeyListeningBG    = Hex("#4A4326");
        public static readonly Color KeyListeningAccent= Hex("#E9E24A");

        public static readonly Color CloseBG           = Hex("#1A1A1A");
        public static readonly Color CloseBGHover      = Hex("#383838");
        public static readonly Color CloseBGActive     = Hex("#4A4A4A");
        public static readonly Color CircleIconBG      = Hex("#111111");
        public static readonly Color WarnBox           = Hex("#1A1A1E");  // 미적용 변경 경고창 배경
        public static readonly Color WarnSubText       = Hex("#9A9A9A");  // 경고창 보조 문구
        public static readonly Color MuteRed           = Hex("#E05A4D");
    }

    // 애니메이션 시간/커브 상수 (DOTween Ease로 매핑)
    public static class UIAnim
    {
        public const float TabIndicator = 0.32f; // Ease.OutBack (overshoot ~1.2)
        public const float ToggleKnob   = 0.28f; // Ease.InOutCubic
        public const float DropdownIn   = 0.16f; // Ease.OutQuad
        public const float Chevron      = 0.20f;
        public const float PanelFade    = 0.28f; // Ease.OutQuad
        public const float SliderAudio  = 0.18f;
        public const float IconPop      = 0.22f; // Ease.OutBack
        public const float Pulse        = 1.0f;  // loop, Ease.InOutSine
        public const float ApplyFlash   = 0.35f;
        public const float HoverColor   = 0.15f;

        public const float TabIndicatorStep = 113f; // display=0, audio=113, controls=226
        public const float ToggleKnobTravel = 226f; // full=0, window=226
    }
}
