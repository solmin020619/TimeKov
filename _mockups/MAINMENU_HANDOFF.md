# MainMenu 메뉴 리스트 — Handoff (mainmenu_mockup.html 기본값 기준)

기준 캔버스: 1920x1080, CanvasScaler reference resolution 1920x1080, Scale With Screen Size,
앵커 좌표는 전부 "캔버스 중앙 = (0,0)" 기준 Unity anchoredPosition 값.

## 1. 변경 범위

- 제거: `MainMenu_Cinematic/PressPrompt` (HorizontalLayoutGroup + MainMenuPulse, "아무 키나 눌러 시작" 텍스트) 와 그 자식 3개.
- 제거: `MainMenu_Cinematic/Btn_Quit` 단독 배치 — 메뉴 리스트 안으로 흡수.
- 신규: `MainMenu_Cinematic/MenuList` — 세로 메뉴 컨테이너(게임 시작 / 게임 종료).
- 유지: BG_Mountain, Scrim, Particles, Logo_Timekov, Logo_Underline, Tagline, VersionChip — 위치/스타일 변경 없음.
- `TitleManager.cs` 동작 변경: `Input.anyKeyDown` 감지 제거. "게임 시작" 항목 클릭 시 기존 `StartGameRoutine()` 코루틴을 그대로 호출(사운드 재생 → WorldSelectUI.Show()). "게임 종료" 항목 클릭 시 `Application.Quit()` (+ 에디터에서 Play 종료).

## 2. MenuList 컨테이너

| 속성 | 값 |
|---|---|
| 부모 | MainMenu_Cinematic (캔버스 루트) |
| Anchor | Min(0.5, 0), Max(0.5, 0) — 하단 중앙 고정 |
| Pivot | (0.5, 0) |
| anchoredPosition | (0, 140) — 캔버스 하단 가장자리에서 위로 140px |
| Width | 600 (Layout이 가로폭 결정하지 않으므로 임의 — 자식 정렬용) |
| Height | Auto (ContentSizeFitter `Vertical: Preferred Size`) |
| 컴포넌트 | VerticalLayoutGroup |
| VerticalLayoutGroup.spacing | 22 |
| VerticalLayoutGroup.childAlignment | MiddleCenter |
| VerticalLayoutGroup.padding | 0,0,0,0 |
| VerticalLayoutGroup.childControlWidth/Height | true / true |
| VerticalLayoutGroup.childForceExpandWidth/Height | false / false |

## 3. 메뉴 아이템 2개 (MenuItem_Start, MenuItem_Quit)

각 아이템 = Button(투명 배경, 호버 시 배경만 살짝) + TMP_Text 자식.

| 속성 | 값 | 비고 |
|---|---|---|
| 텍스트 | "게임 시작" / "게임 종료" | |
| 폰트 | 남양주고딕Light (OTF) SDF | 기존 프로젝트 한글 폰트 컨벤션과 동일 |
| Font Size | 30 | 목업 기본값 |
| Font Style | Medium(500 weight 느낌) — 폰트 자체엔 weight variant 없음 → Bold 토글 OFF, 그대로 사용 | |
| Character Spacing | 8 (TMP 단위) | 목업의 CSS letter-spacing 4px를 TMP 단위로 근사 변환한 값. **근사값** — 실제 화면 보면서 6~10 사이로 미세조정 가능 |
| 기본 색상 | #F2F5F8 (RGBA 242,245,248,255) | |
| 선택/호버 색상 | #7FD0FF (RGBA 127,208,255,255) | Button.OnPointerEnter / 현재 선택된 항목에 적용 |
| Alignment | Center / Middle | |
| Button 패딩(클릭 영역) | 좌우 28px, 상하 6px | 텍스트보다 살짝 크게 — 목업과 동일 |
| Button 배경 | 기본 투명(alpha 0). 호버 시 배경 RGBA(255,255,255,15) (약 6% 흰색) | |
| Button BorderRadius | 6px (둥근 사각 스프라이트 사용, 또는 SettingsPanelRebuilder의 RoundedPillSprite 재사용 가능) | |

### 글로우 효과 (근사 처리 — 데모와 100% 동일하지 않음)
목업은 CSS `text-shadow blur 18px`로 부드러운 발광을 표현했는데, Unity TMP는 진짜 가우시안 블러 글로우가 기본 셰이더로 안 됨.
대체 방법(택1):
- A) TMP의 `Underlay` 모듈(Material에서 Face > Underlay 활성화) — Underlay Dilate/Softness로 흐릿한 발광 근사. Softness ≈ 0.5~0.7, Dilate ≈ 0.1.
- B) 그냥 생략하고 평범한 단색 텍스트로 — 가장 무난하고 프로젝트의 다른 UI(인벤/도감)와도 톤이 맞음. **추천.**

이번 빌더 스크립트는 B(글로우 생략, 단색 텍스트)로 구현한다 — 가장 단순하고 일관적이며, 나중에 마음에 안 들면 머티리얼만 교체하면 됨.

## 4. Scrim (기존 오브젝트, 값만 갱신)

| 속성 | 값 |
|---|---|
| 대상 | MainMenu_Cinematic/Scrim (기존 Image) |
| Color Alpha | 115 / 255 (0.45) | 목업 기본값과 동일 — 기존 값과 다르면 이 값으로 맞춤 |

## 5. 처리 순서 (빌더 스크립트가 할 일)

1. `MainMenu_Cinematic/PressPrompt` 와 그 자식 3개 삭제.
2. `MainMenu_Cinematic/Btn_Quit` 의 `MainMenuQuitButton` 컴포넌트와 Button/Image는 재사용 — GameObject를 삭제하지 않고 `MenuList` 의 두 번째 자식(MenuItem_Quit)으로 재사용하거나, 새로 만들고 기존 Btn_Quit는 삭제. **이번엔 기존 Btn_Quit를 삭제하고 새로 만든다** (인스펙터 참조가 끊길 위험이 적은 깨끗한 재생성 방식 — 기존 컨벤션인 WorldSelectUIBuilder/SettingsPanelRebuilder와 동일하게 "확인창 없이 항상 재생성").
3. `MenuList` 컨테이너 생성 (2번 섹션 스펙대로).
4. `MenuItem_Start`, `MenuItem_Quit` 생성 (3번 섹션 스펙대로).
5. `MenuItem_Start.onClick` → `TitleManager`의 게임 시작 처리(코루틴) 호출. `TitleManager.cs`에 `public void OnClickStart()` 같은 public 메서드를 추가해 버튼이 직접 호출.
6. `MenuItem_Quit.onClick` → `Application.Quit()` (+ `#if UNITY_EDITOR` 가드로 Play 종료). 기존 `MainMenuQuitButton` 컴포넌트를 그대로 부착해 재사용.
7. `TitleManager.cs`의 `Update()`에서 `Input.anyKeyDown` 감지 블록 제거 — "아무 키나 시작" 동작 비활성화.
8. Scrim 알파를 0.45로 맞춤(이미 그 값이면 변경 없음).
9. 씬 저장.

## 6. 데모에만 있고 실제로 안 들어가는 것
- 목업의 tweaks panel 자체(슬라이더 UI)는 디자인 툴이라 게임에 안 들어감.
- 글로우(텍스트 발광) — 위 3번 항목 설명대로 이번엔 생략.
- "1920x1080 기준 목업" 안내 텍스트, 버전 칩의 임의 버전 문자열("v0.1.0")은 실제 VersionChip 기존 값 유지(건드리지 않음).
