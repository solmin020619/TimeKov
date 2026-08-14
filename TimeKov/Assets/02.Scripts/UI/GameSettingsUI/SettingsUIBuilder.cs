// SettingsUIBuilder.cs
// ============================================================================
// 씬 세팅 0 — 이 스크립트 하나로 설정 패널 UI 전체를 코드로 생성한다.
// 라운드 코너(9-slice 런타임 생성), 아이콘(런타임 SDF 래스터라이즈),
// 정확한 좌표/폰트/색상, 호버/클릭/애니메이션까지 목업과 동일하게 재현.
//
// 사용법:
//   1) UIColors.cs, SettingsData.cs 와 함께 Assets/Scripts/GameSettingsUI/ 에 둔다.
//   2) 빈 GameObject에 이 스크립트를 붙인다. (Canvas/EventSystem 자동 생성)
//   3) 인스펙터에서 koreanFont(Pretendard TMP SDF)만 지정하면 끝. (미지정 시 기본폰트)
//   4) 실행.  DOTween 불필요.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using JeffGrawAssets.FlexibleUI;   // UIBlur — 인벤토리 UI와 동일한 블러 경로

namespace GameSettingsUI
{
    public class SettingsUIBuilder : MonoBehaviour
    {
        [Tooltip("Pretendard 등 한글 TMP SDF 폰트. 비우면 TMP 기본 폰트 사용(한글 깨질 수 있음).")]
        public TMP_FontAsset koreanFont;

        [Tooltip("마우스 휠 한 칸당 본문 스크롤 이동량(px). Unity 기본값 1은 너무 느림.")]
        public float scrollSensitivity = 60f;

        [Header("배경 블러 (프로스티드 글래스)")]
        [Tooltip("켜면 뒤 게임 화면을 블러로 비춘다. 캔버스가 ScreenSpaceCamera로 바뀐다.\n끄면 기존처럼 불투명 단색 배경 + Overlay 캔버스.")]
        public bool backgroundBlur = true;
        [Tooltip("블러 강도. 높을수록 뭉개짐. 6 = 배경을 알아볼 만큼만 부드럽게(인벤토리와 동일 기준).")]
        [Range(0f, 16f)] public float blurStrength = 6f;
        [Tooltip("반복 횟수. 클수록 더 부드럽게 퍼짐.")]
        [Range(1, 8)] public int blurIterations = 5;
        [Tooltip("샘플 간격.")]
        [Range(0.5f, 4f)] public float blurSampleDistance = 1.5f;
        [Tooltip("블러 위에 덮는 어두운 막. 알파를 낮추면 게임 화면이 더 비치고, 높이면 글자가 잘 읽힌다.")]
        public Color blurScrim = new Color(0.024f, 0.024f, 0.027f, 0.72f);
        [Tooltip("설정 행 유리막 색. 블러 위에 덮는 틴트다.\n어둡게/알파를 높일수록 패널이 진해지고 글자가 잘 읽힌다.")]
        public Color frostedRowColor = new Color(0.08f, 0.08f, 0.09f, 0.55f);
        [Tooltip("상단 탭 컨테이너의 유리막 농도.")]
        [Range(0f, 0.8f)] public float frostedTabAlpha = 0.45f;


        const float W = 1920f, H = 1080f, SIDE = 150f;

        // 하단 고정 풋터. 본문 스크롤 하단과 풋터 위치를 여기서 한 번에 파생시켜
        // 버튼 위/아래 여백이 항상 같도록 한다. (여백을 키우려면 FOOTER_PAD만 조정)
        const float FOOTER_BTN_H = 62f;   // 하단 버튼 높이
        const float FOOTER_PAD   = 34f;   // 버튼 위/아래 동일 여백
        const float FOOTER_STRIP = FOOTER_PAD * 2 + FOOTER_BTN_H;   // 본문이 비워둬야 할 하단 높이

        // 헤더. 탭바(84 높이)와 닫기(56)의 세로 중심이 대략 맞도록 잡았다.
        //   탭바 중심 = TAB_TOP + 42 = 82,  닫기 중심 = CLOSE_MARGIN + 28 = 80
        const float TAB_TOP      = 40f;   // 상단 탭 컨테이너 위 여백
        const float CLOSE_MARGIN = 52f;   // 닫기(X) 우측·상단 공통 여백

        // 설정값 접근은 전부 SettingsBinding 경유. 이 UI는 값을 들고 있지 않는다.

        SettingsTab tab = SettingsTab.Display;
        int listening = -1;
        int listenFrame = -1;   // 리스닝을 시작한 프레임(그 프레임의 클릭 입력은 무시)

        Canvas canvas;
        RectTransform root;
        RectTransform tabIndicator;
        readonly List<Dropdown> dropdowns = new List<Dropdown>();
        RectTransform displayPanel, audioPanel, controlsPanel;
        CanvasGroup displayCG, audioCG, controlsCG;
        SegToggle displayToggle;
        readonly List<Slider3> sliders = new List<Slider3>();
        readonly List<KeyRow> keyRows = new List<KeyRow>();
        readonly List<Image> rowBGs = new List<Image>();   // 블러 유무에 따라 다시 칠할 행 배경들
        Image scrimImg, tabBarImg;
        GameObject controlsHint;
        GameObject warningModal;   // 미적용 변경 경고창
        RectTransform bodyViewport;   // 드롭다운 펼침 방향 판단 기준(RectMask2D 클리핑 영역)
        bool blurActive;              // 블러가 실제로 성립했는지(카메라를 못 찾으면 false로 떨어진다)
        Image applyBG;
        TMP_Text _titleTmp;

        [Header("열기 애니메이션")]
        [Tooltip("설정창이 열릴 때의 연출.\n" +
                 "FadeSettle — 살짝 크게 시작해 제자리로 가라앉으며 페이드인 (기본)\n" +
                 "FadeOnly   — 페이드인만\n" +
                 "None       — 즉시 표시")]
        public OpenAnim openAnim = OpenAnim.FadeSettle;
        [Range(0.05f, 0.6f)] public float openDuration = 0.22f;
        [Tooltip("FadeSettle에서 시작 배율. 1보다 작게 두면 화면 가장자리에 틈이 생기니 1 이상으로.")]
        [Range(1f, 1.2f)] public float openStartScale = 1.04f;

        [Tooltip("전용 Overlay 캔버스를 쓸 때의 정렬 순서. 다른 UI보다 위에 오도록 크게 둔다.")]
        public int overlaySortingOrder = 500;

        [Header("씬 베이크")]
        [Tooltip("에디터로 구운 계층의 참조 묶음. 채워져 있으면 실행 시 새로 만들지 않고 이걸 그대로 쓴다.")]
        public SettingsPanelRefs refs;

        bool built;

        void Start()
        {
            // 값·저장·엔진 반영을 전부 매니저에 위임하므로 없으면 UI가 성립하지 않는다.
            if (!SettingsBinding.Ready)
            {
                Debug.LogError("[GameSettingsUI] 씬에 GlobalSettingsManager가 없습니다. " +
                               "이 설정 UI는 해당 매니저를 모델로 사용합니다.");
                enabled = false;
                return;
            }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;   // UI 조작용 커서 해제(데모)

            if (refs && refs.IsComplete)
            {
                AdoptBakedHierarchy();   // 씬에 이미 구워져 있음
            }
            else
            {
                // 구운 계층이 있는데 참조가 불완전하면(굽고 나서 UI 구성이 바뀐 경우) 실행 중에 다시 만든다.
                // 이때 남아 있는 옛 계층을 먼저 치우지 않으면 두 벌이 겹쳐 그려진다.
                if (refs || transform.childCount > 0)
                {
                    Debug.LogWarning("[GameSettingsUI] 구워둔 계층이 현재 구성과 맞지 않아 실행 중에 다시 만듭니다. " +
                                     "Tools ▸ GameSettingsUI ▸ 선택한 오브젝트에 UI 굽기를 다시 실행하세요.");
                    for (int i = transform.childCount - 1; i >= 0; i--)
                        Destroy(transform.GetChild(i).gameObject);
                }
                BuildCanvas(); BuildUI();
            }

            built = true;   // 여기까지 와야 계층이 완성된 것. 중간에 예외가 나면 false로 남는다.
            WireFooterPair();      // 적용 버튼 폭이 바뀌면 초기화 버튼이 따라 밀리도록(두 경로 공통)
            FixSectionDividers();  // 섹션 구분선을 라벨 실제 폭 뒤로 (베이크 시 측정 오차 보정)
            SettingsBinding.NormalizeResolution();   // 저장값이 선택지에 없으면 목록 안 값으로 보정
            ApplyBackdropTheme();   // 실행 시의 블러 상태 기준으로 배경색 확정 (베이크 값 덮어씀)
            SwitchTab(SettingsTab.Display, true);
            PlayOpenAnim();   // OnEnable이 Start보다 먼저라 첫 표시는 여기서 재생한다
        }

        // 에디터로 구운 계층을 그대로 쓴다. 위젯은 스스로 MonoBehaviour라 자식에서 모으고,
        // 그 외 역할 오브젝트는 SettingsPanelRefs가 들고 있다.
        // 두 경로가 공통으로 필요로 하는 것. 베이크 채택 경로에서 이걸 빠뜨리면
        // 트윈 러너가 없어 모든 애니메이션이 첫 프레임에서 멈추고(페이드가 알파 0 근처에 고정),
        // EventSystem이 없는 씬에서는 클릭이 아예 안 먹는다.
        void EnsureRuntimeDeps()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(null);
            }
            UITween.Ensure();
        }

        void AdoptBakedHierarchy()
        {
            canvas = GetComponentInParent<Canvas>();
            EnsureRuntimeDeps();

            root = refs.root;
            bodyViewport = refs.bodyViewport;
            tabIndicator = refs.tabIndicator;
            _tabIcons = refs.tabIcons;
            _tabBtns  = refs.tabButtons;
            displayPanel = refs.displayPanel; audioPanel = refs.audioPanel; controlsPanel = refs.controlsPanel;
            displayCG    = refs.displayCG;    audioCG    = refs.audioCG;    controlsCG    = refs.controlsCG;
            controlsHint = refs.controlsHint;
            applyBG      = refs.applyBG;
            warningModal = refs.warningModal;
            displayToggle = GetComponentInChildren<SegToggle>(true);

            dropdowns.Clear(); dropdowns.AddRange(GetComponentsInChildren<Dropdown>(true));
            sliders.Clear();   sliders.AddRange(GetComponentsInChildren<Slider3>(true));

            // 키 행은 화면 순서가 아니라 액션 인덱스 순으로 맞춰야 listening 인덱스와 어긋나지 않는다.
            keyRows.Clear();
            var rows = GetComponentsInChildren<KeyRow>(true);
            for (int i = 0; i < SettingsBinding.ActionCount; i++)
                foreach (var r in rows) if (r.actionIndex == i) { keyRows.Add(r); break; }

            // 위젯을 다 모은 뒤에 옮긴다 — 먼저 옮기면 GetComponentsInChildren이 못 찾는다.
            EnsureOverlayHost();

            var blurCam = (backgroundBlur && CanUseBlur()) ? PickScreenCamera() : null;
            blurActive = blurCam;
            if (blurCam) BuildBlurCanvas(blurCam);

            // 제목 TMP는 BuildHeader에서만 _titleTmp에 할당되므로, 베이크 경로에서는 이름으로 다시 찾는다.
            var titleChild = root.Find("Text_설정Settings");
            if (titleChild) _titleTmp = titleChild.GetComponent<TMP_Text>();

            RefreshAll();   // 표시값을 현재 설정값으로 갱신 (배경색은 Start가 확정한다)
            LocalizedLabel.AttachToStaticLabels(root.gameObject);   // 베이크된 한글 라벨에 자동 구독 부착
        }

        // 섹션 제목('언어 설정' 등) 옆 구분선이 글자 뒤로 파고들던 문제.
        //   선 시작 위치는 라벨의 실제 폭으로 잡는데(labelW + 26), 구울 때는 폰트 아틀라스가
        //   덜 준비돼 폭이 실제보다 작게 나오고 그 값이 그대로 씬에 박제된다.
        //   언어를 바꿔 라벨이 길어져도 선은 그대로라 같은 증상이 난다.
        //   → 실행할 때(그리고 언어가 바뀔 때) 다시 재서 라벨 뒤로 물러나게 한다.
        const float SECTION_LINE_GAP = 26f;

        void FixSectionDividers()
        {
            if (root == null) return;

            foreach (var line in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (line.name != "SectionDivider") continue;
                if (line.parent is not RectTransform row) continue;

                var label = row.GetComponentInChildren<TMP_Text>(true);
                if (label == null) continue;

                label.ForceMeshUpdate();
                float w = label.GetPreferredValues(label.text).x;
                // 폰트가 아직 준비되지 않았을 때의 폴백(FooterButtonFit 과 같은 안전장치).
                if (w < 1f && !string.IsNullOrEmpty(label.text))
                    w = label.text.Length * Mathf.Max(label.fontSize, 16f) * 1.05f;

                float x = w + SECTION_LINE_GAP;
                float rowW = row.rect.width;

                var lrt = label.rectTransform;
                lrt.sizeDelta = new Vector2(w, lrt.sizeDelta.y);   // 라벨 칸도 실제 폭으로
                line.anchoredPosition = new Vector2(x, line.anchoredPosition.y);
                line.sizeDelta = new Vector2(Mathf.Max(0f, rowW - x), line.sizeDelta.y);
            }
        }

        // 오른쪽 푸터 두 버튼('설정 적용' ↔ '설정 초기화')을 폭 변화에 연동한다.
        //   폭은 FooterButtonFit 이 라벨에 맞춰 런타임에 다시 재는데 위치는 만들 때 값(또는 베이크 값)에
        //   고정돼 있어, 폭이 그 가정보다 커지면 두 버튼이 겹쳤다. 베이크/런타임 두 경로 모두에서 걸어준다.
        void WireFooterPair()
        {
            if (applyBG == null) return;
            var apply = applyBG.rectTransform;
            var parent = apply.parent;
            if (parent == null) return;

            var resetTr = parent.Find($"Footer_{SettingsAction.ResetAll}") as RectTransform;
            if (resetTr == null) return;

            var fit = apply.GetComponent<FooterButtonFit>();
            if (fit == null) return;

            fit.pushLeft = resetTr;
            fit.Fit();   // 지금 폭 기준으로 즉시 한 번 맞춘다
        }

        // 블러 패스는 Overlay 캔버스보다 먼저 그려지므로, UI가 Overlay가 아니면 덮인다.
        // 호스트 캔버스가 Overlay가 아니면 전용 Overlay 캔버스를 만들어 UI를 그 아래로 옮긴다.
        // → 씬의 캔버스 설정과 무관하게 블러·레이아웃이 어디서나 동일하게 동작한다.
        //   덤으로 CanvasScaler를 직접 쥐게 되어 기준 해상도(1920x1080)도 보장된다.
        void EnsureOverlayHost()
        {
            if (!Application.isPlaying || root == null) return;   // 베이크 중에는 계층을 옮기지 않는다
            var rc = canvas ? canvas.rootCanvas : null;
            if (rc != null && rc.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                EnsureRectTransform();   // 호스트 캔버스를 그대로 쓸 때만 조상 rect를 펴면 된다
                return;
            }

            overlayCanvasGO = new GameObject("SettingsOverlayCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = overlayCanvasGO.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = overlaySortingOrder;
            var sc = overlayCanvasGO.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(W, H);
            sc.matchWidthOrHeight = 0.5f;

            root.SetParent((RectTransform)overlayCanvasGO.transform, false);
            Stretch(root);
            canvas = c;
        }

        void Update()
        {
            // 리스닝을 시작시킨 그 클릭이 아직 GetMouseButtonDown으로 잡히는 프레임은 건너뛴다.
            // EventSystem이 이 컴포넌트보다 먼저 도는 실행 순서에서는, 버튼을 누른 즉시
            // 그 클릭이 Mouse0으로 그대로 바인딩되어 버린다.
            // GameUIController가 없는 씬(MainMenu 등)에서는 ESC를 받아줄 곳이 없다.
            // 그 경우에만 직접 처리한다 — 있으면 그쪽이 처리하므로 건드리지 않는다(이중 처리 방지).
            if (listening < 0 && GameUIController.Instance == null && Input.GetKeyDown(KeyCode.Escape))
            {
                if (TryCloseFromEscape()) ClosePanel();
                return;
            }

            if (listening < 0 || Time.frameCount == listenFrame) return;

            var code = DetectKeyCode();
            if (code == KeyCode.None) return;

            if (code != KeyCode.Escape)   // Esc는 캡처만 취소 (기존 시스템과 동일한 규칙)
            {
                if (!SettingsBinding.TryRebind(listening, code, out string conflict))
                    Debug.LogWarning($"[Settings] '{code}' 키는 이미 '{conflict}'에 사용 중입니다.");
            }
            keyRows[listening].SetListening(false);   // 라벨은 여기서 현재 바인딩으로 복원된다
            listening = -1;
        }

        // 드롭다운 바깥 클릭 닫기.
        // LateUpdate에서 처리하는 이유: EventSystem은 Update에서 클릭을 소화하므로,
        // Update에서 닫으면 옵션을 누른 클릭이 팝업이 꺼진 뒤에 처리되어 그냥 씹힌다.
        // LateUpdate면 옵션 클릭이 이미 끝난 뒤라 안전하고, 스크립트 실행 순서와도 무관하다.
        void LateUpdate()
        {
            if (!built || !Input.GetMouseButtonDown(0)) return;

            Dropdown open = null;
            foreach (var d in dropdowns) if (d.isOpen) { open = d; break; }
            if (open == null) return;

            // 이 시점의 "열린 드롭다운"은 이번 클릭까지 반영된 결과다. 다른 드롭다운 버튼을
            // 눌러 방금 열린 경우엔 클릭 지점이 그 버튼 위이므로 닫히지 않는다.
            // 중첩 캔버스는 자기 renderMode가 무시되므로 실제로 그리는 루트 캔버스를 봐야 한다.
            var rc = canvas ? canvas.rootCanvas : null;
            var cam = rc && rc.renderMode != RenderMode.ScreenSpaceOverlay ? rc.worldCamera : null;
            Vector2 p = Input.mousePosition;
            if (RectTransformUtility.RectangleContainsScreenPoint(open.popup, p, cam)) return;
            if (RectTransformUtility.RectangleContainsScreenPoint(open.button, p, cam)) return;
            CloseAll();
        }

        /// 현재 열려 있는 설정 패널. ESC 경로(GameUIController)가 이 패널의 경고창을 쓰도록 알려준다.
        public static SettingsUIBuilder Active { get; private set; }

        /// 키 리바인딩 입력을 기다리는 중인가. ESC를 설정창 닫기로 흘리지 않기 위해 외부에서 확인한다.
        public bool IsListeningKey => listening >= 0;

        /// ESC로 닫으려 할 때 호출. 닫아도 되면 true, 경고창을 띄워 막았으면 false.
        public bool TryCloseFromEscape()
        {
            if (warningModal && warningModal.activeSelf) { ShowWarning(false); return false; }  // 경고창이 떠 있으면 그것부터 닫는다
            if (SettingsBinding.HasUnappliedChanges) { CloseAll(); ShowWarning(true); return false; }
            return true;
        }

        void OnEnable()
        {
            Active = this;
            if (blurCanvasGO) blurCanvasGO.SetActive(true);
            if (overlayCanvasGO) overlayCanvasGO.SetActive(true);
            // 다시 열 때는 매니저가 편집 폼을 _data 기준으로 되돌린 뒤일 수 있으므로 다시 읽어온다.
            if (built) { EnsureBlur(); ResetMuteStates(); RefreshAll(); PlayOpenAnim(); }
            Loc.OnLanguageChanged += RefreshTitleLabel;
            Loc.OnLanguageChanged += FixSectionDividers;   // 라벨 길이가 바뀌면 구분선도 다시 물러나야 한다
            RefreshTitleLabel();
        }

        // 열기 연출. 스크림이 화면 전체를 덮으므로 배율은 1 이상에서만 시작한다
        // (1 미만이면 축소 중 가장자리에 게임 화면이 그대로 비친다).
        void PlayOpenAnim()
        {
            if (openAnim == OpenAnim.None || root == null) return;

            var cg = root.GetComponent<CanvasGroup>();
            if (cg == null) cg = root.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            UITween.Fade(cg, 1f, openDuration, Ease.OutQuad);

            if (openAnim != OpenAnim.FadeSettle) { root.localScale = Vector3.one; return; }
            root.localScale = Vector3.one * Mathf.Max(1f, openStartScale);
            UITween.Scale(root, 1f, openDuration, Ease.OutQuad);
        }

#if UNITY_EDITOR
        /// 에디터 베이크 전용 진입점. 실행 중이 아닐 때 계층을 한 번 만들어 씬에 남긴다.
        /// (SettingsUIBakerWindow가 UISprites.Resolver를 꽂은 상태로 호출한다)
        public void BuildForBake()
        {
            if (Application.isPlaying) { Debug.LogWarning("[GameSettingsUI] 재생 중에는 베이크할 수 없습니다."); return; }
            // 표시값을 매니저에서 읽어와야 계층을 만들 수 있다. 없으면 NPE 대신 이유를 알린다.
            if (!SettingsBinding.Ready)
            {
                Debug.LogError("[GameSettingsUI] 같은 씬에 GlobalSettingsManager가 없어 베이크할 수 없습니다.");
                return;
            }
            BuildCanvas();
            BuildUI();
        }
#endif

        // ==================================================================
        //  캔버스
        // ==================================================================
        void BuildCanvas()
        {
            // 부모 체인에 이미 캔버스가 있으면 여기에 또 만들면 안 된다.
            // 유니티는 중첩 캔버스의 renderMode/CanvasScaler를 무시하고 부모를 따르므로,
            // 새로 만들어도 설정이 먹지 않고 레이아웃만 어긋난다.
            var parent = GetComponentInParent<Canvas>();
            bool nested = parent != null && parent.gameObject != gameObject;

            if (nested)
            {
                canvas = parent;
                EnsureRectTransform();

                var rootScaler = parent.rootCanvas ? parent.rootCanvas.GetComponent<CanvasScaler>() : null;
                if (rootScaler && rootScaler.referenceResolution != new Vector2(W, H))
                    Debug.LogWarning($"[GameSettingsUI] 부모 캔버스의 기준 해상도가 {rootScaler.referenceResolution}입니다. " +
                                     $"이 UI는 {W}x{H} 기준 좌표로 배치되므로 크기가 어긋날 수 있습니다.");
            }
            else
            {
                canvas = gameObject.GetComponent<Canvas>();
                if (canvas == null) canvas = gameObject.AddComponent<Canvas>();   // Unity Object는 ?? 안 통함(fake null)

                // ⚠ 직접 만드는 경우 UI 캔버스는 반드시 Overlay로 둔다.
                // FlexibleBlurFeature의 renderPassEvent가 600(AfterRenderingPostProcessing)이라
                // 블러는 카메라 렌더가 끝난 뒤에 그려진다. UI를 Screen Space-Camera에 두면
                // UI가 먼저 그려지고 그 위를 블러가 덮어 화면이 블러만 남는다.
                // Overlay 캔버스는 그 뒤에 그려지므로 블러 위에 얹힌다.
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = gameObject.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(W, H);
                scaler.matchWidthOrHeight = 0.5f;

                if (!gameObject.GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();
            }

            EnsureRuntimeDeps();

            // 일단 이 오브젝트의 자식으로 만든다(베이크는 이 상태로 씬에 저장된다).
            // 실행 중이고 호스트가 Overlay가 아니면 아래 EnsureOverlayHost가 전용 캔버스로 옮긴다.
            root = NewRect("Root", (RectTransform)transform);
            Stretch(root);
            EnsureOverlayHost();

            // 에디터 베이크에서는 아직 전용 Overlay 캔버스로 옮기기 전이라 CanUseBlur()가
            // 무조건 false가 된다. 그 판정으로 색을 구우면 실행 시와 달라지므로,
            // 베이크 때는 설정값(backgroundBlur)만 보고 굽고 실제 판정은 실행 시에 한다.
            bool wantBlur = backgroundBlur && (!Application.isPlaying || CanUseBlur());
            blurActive = wantBlur;
            BuildBackdrop(wantBlur ? PickScreenCamera() : null);
        }

        // 부모 캔버스를 쓰는 경우 이 오브젝트가 UI 계층의 일부가 되므로 RectTransform이 필요하다.
        // (빈 GameObject로 만들어졌다면 Transform이라 UI 배치가 성립하지 않는다)
        void EnsureRectTransform()
        {
            var rt = transform as RectTransform;
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();

            // 이 오브젝트부터 루트 캔버스 직전까지 전부 화면을 채우도록 편다.
            // 조상 중 하나라도 rect가 0이거나 작으면 그 아래는 전부 그 크기에 갇히고,
            // stretch 앵커가 한 점으로 수렴해 UI가 화면 중앙에 뭉친다.
            // (설정 패널은 화면 전체를 덮는 UI라 조상도 전체 크기인 게 맞다)
            var rootCanvas = canvas ? canvas.rootCanvas : null;
            for (var t = rt; t != null; t = t.parent as RectTransform)
            {
                if (rootCanvas && t.gameObject == rootCanvas.gameObject) break;
                t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one;
                t.offsetMin = Vector2.zero; t.offsetMax = Vector2.zero;
                t.pivot = new Vector2(0.5f, 0.5f);
                t.localScale = Vector3.one;
                t.localRotation = Quaternion.identity;
            }
        }

        // 배경: 블러 위 어두운 막, 또는 불투명 단색.
        // 어느 쪽이든 화면 전체를 덮는 raycast 대상이 하나는 있어야 뒤 게임으로 클릭이 새지 않는다.
        void BuildBackdrop(Camera blurCam)
        {
            // 배경막은 블러 유무와 상관없이 항상 만든다.
            // 없으면 실행 시 ApplyBackdropTheme이 다시 칠할 대상이 없어져,
            // 베이크할 때 정해진 색에 영영 갇힌다.
            var scrim = NewRect("Scrim", root);
            Stretch(scrim);
            scrimImg = scrim.gameObject.AddComponent<Image>();
            scrimImg.color = blurActive ? blurScrim : UIColors.BgBase;

            // 블러 캔버스는 최상위 오브젝트라 씬에 구우면 편집 중에도 화면을 계속 덮는다.
            // 실행 시에만 만든다(베이크된 계층은 AdoptBakedHierarchy에서 만들어 준다).
            if (blurCam && Application.isPlaying) BuildBlurCanvas(blurCam);
        }

        // 블러 영역 전용 캔버스. Overlay 캔버스 안에 중첩하면 렌더 모드가 무시되므로
        // 반드시 씬 최상위(부모 없음)로 만든다. 수명은 이 컴포넌트가 직접 맞춘다.
        // 블러는 창을 만들 때 딱 한 번만 판정했었다. 그 순간 카메라가 없으면
        // (씬 로드 직후·컷신·카메라 교체 타이밍) 그 뒤로 영영 블러 없이 남고,
        // 배경/행 색까지 불투명 테마로 굳어 "창 색감이 이상하다"로 보였다.
        // 닫았다 열어도 Start 는 다시 안 돌아 씬을 다시 로드해야 복구됐다.
        //   → 인벤토리 블러(InventoryBlurTuner)처럼 '열 때마다' 다시 잡는다.
        void EnsureBlur()
        {
            if (!backgroundBlur || blurCanvasGO) return;   // 이미 성립했으면 그대로 둔다
            if (!CanUseBlur()) return;

            var cam = PickScreenCamera();
            if (cam == null) return;

            blurActive = true;
            BuildBlurCanvas(cam);
            ApplyBackdropTheme();   // 불투명 테마로 굳어 있던 배경/행 색을 유리막으로 되돌린다
        }

        void BuildBlurCanvas(Camera cam)
        {
            blurCanvasGO = new GameObject("SettingsBlurCanvas", typeof(Canvas), typeof(CanvasScaler));
            var c = blurCanvasGO.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = cam.nearClipPlane + 0.01f;   // 월드 오브젝트가 가릴 수 없는 최소 거리
            var sc = blurCanvasGO.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(W, H);
            sc.matchWidthOrHeight = 0.5f;

            // 블러는 전체 화면 1회만. 행마다 UIBlur를 달면 패스가 행 수만큼 늘어 비용이 급증하고
            // UIBlur.CanBatch가 false라 배칭도 안 된다. 행/탭바는 이 블러 위에 얇은 유리막을 덮는다.
            var region = NewRect("BlurRegion", (RectTransform)blurCanvasGO.transform);
            Stretch(region);
            ConfigureBlur(region.gameObject.AddComponent<UIBlur>(), cam);
        }

        GameObject blurCanvasGO;
        GameObject overlayCanvasGO;   // 호스트가 Overlay가 아닐 때 UI를 담는 전용 캔버스
        // 블러 캔버스는 별도 최상위 오브젝트라 이 패널을 켜고 끌 때 같이 따라가야 한다. (켜기는 OnEnable에서)
        void OnDisable()
        {
            if (Active == this) Active = null;
            ShowWarning(false);   // 다음에 열 때 경고창이 떠 있는 채로 시작하지 않도록
            if (blurCanvasGO) blurCanvasGO.SetActive(false);
            if (overlayCanvasGO) overlayCanvasGO.SetActive(false);
            Loc.OnLanguageChanged -= RefreshTitleLabel;
            Loc.OnLanguageChanged -= FixSectionDividers;
        }
        void OnDestroy() { if (blurCanvasGO) Destroy(blurCanvasGO); if (overlayCanvasGO) Destroy(overlayCanvasGO); }

        void ConfigureBlur(UIBlur blur, Camera cam)
        {
            var common = blur.Common;
            common.cameraReference = cam;
            var s = common.blurInstanceSettings;   // BlurSettings 기본 생성자가 정상 섹션을 이미 채워 둔다
            s.blurAdditionalDistancePerIteration = blurStrength;
            if (s.blurSections != null)
                foreach (var sec in s.blurSections)
                {
                    sec.iterations = Mathf.Max(1, blurIterations);
                    sec.sampleDistance = blurSampleDistance;
                }
            common.ValidateBlur();
        }

        // 배경 색은 블러 유무에 따라 완전히 달라진다.
        //   블러 O — 반투명 유리막 (뒤 게임 화면이 블러로 비침)
        //   블러 X — 불투명 단색 (비칠 게 없으니 반투명이면 대비만 무너진다)
        // 구울 때의 상태로 색이 박제되므로, 실행 시 지금 상태에 맞게 다시 칠한다.
        // (World에서 블러 켜고 구운 계층을 메인메뉴에서 쓰면 여기서 불투명으로 바뀐다)
        void ApplyBackdropTheme()
        {
            if (refs == null) return;

            if (refs.scrim)    refs.scrim.color    = blurActive ? blurScrim     : UIColors.BgBase;
            if (refs.tabBarBG) refs.tabBarBG.color = blurActive ? TabBarColor() : UIColors.TabContainer;

            var rowColor = blurActive ? frostedRowColor : UIColors.RowBG;
            if (refs.rowBackgrounds != null)
                foreach (var im in refs.rowBackgrounds) if (im) im.color = rowColor;
        }

        // 블러 패스는 카메라 렌더가 전부 끝난 뒤(FlexibleBlurFeature renderPassEvent=600)에 그려진다.
        // 그래서 UI가 Overlay 캔버스가 아니면 블러가 UI 위를 덮어 화면에 블러만 남는다.
        // 이 조합이면 UI가 아예 안 보이므로, 블러를 포기하고 UI를 살린다.
        bool CanUseBlur()
        {
            var rc = canvas ? canvas.rootCanvas : null;
            if (rc == null || rc.renderMode == RenderMode.ScreenSpaceOverlay) return true;

            Debug.LogWarning($"[GameSettingsUI] 루트 캔버스가 {rc.renderMode}라 배경 블러를 끕니다. " +
                             "블러는 카메라 렌더 뒤에 그려져 Overlay가 아닌 UI를 덮어버립니다. " +
                             "이 씬에서도 블러를 쓰려면 설정 UI를 ScreenSpaceOverlay 캔버스 아래로 옮기세요.", rc);
            return false;
        }

        // 화면에 직접 렌더하는 카메라 중 depth가 가장 높은 것 (RenderTexture 대상 제외).
        // InventoryBlurTuner와 동일한 선택 규칙.
        static Camera PickScreenCamera()
        {
            var main = Camera.main;
            if (main && main.isActiveAndEnabled && main.targetTexture == null) return main;
            Camera best = null;
            foreach (var c in Camera.allCameras)
            {
                if (c.targetTexture != null) continue;
                if (best == null || c.depth > best.depth) best = c;
            }
            return best ? best : main;
        }

        // 배경이 블러면 반투명 유리막, 아니면 배경색에 맞춰 미리 계산해 둔 불투명색.
        Color RowColor() => blurActive
            ? frostedRowColor
            : UIColors.RowBG;
        Color TabBarColor() => blurActive
            ? new Color(52f / 255f, 52f / 255f, 58f / 255f, frostedTabAlpha)   // 목업 rgba(52,52,58,·)
            : UIColors.TabContainer;

        // ==================================================================
        //  전체 UI
        // ==================================================================
        void BuildUI()
        {
            BuildHeader();
            BuildBody();
            BuildFooter();
            BuildWarningModal();
            CaptureRefs();
        }

        // ---------------- 미적용 변경 경고창 ----------------
        // 닫으려는데 아직 "설정 적용"을 안 눌렀을 때 뜬다. 기존 시스템의 applyWarningModal을
        // 새 UI에서 다시 만든 것 — 그게 없으면 RequestClose()가 계속 false를 반환해
        // 아무 안내도 없이 창이 안 닫히는 상태가 된다.
        void BuildWarningModal()
        {
            var overlay = NewRect("ApplyWarning", root);
            Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);  // 뒤 클릭 차단 겸 딤

            var box = Panel(overlay, "WarningBox", 720, 260, 24, UIColors.WarnBox);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = Vector2.zero;
            AddOutline(box.GetComponent<Image>(), UIColors.TabBorder, 1);

            var msg = LocText(box, "적용하지 않은 변경사항이 있습니다.", 26, FontWeight.Bold,
                           UIColors.TextRow, TextAlignmentOptions.Center);
            AnchorTL(msg.rectTransform, 40, 52, 640, 40);
            var sub = LocText(box, "적용하고 닫을까요?", 22, FontWeight.Regular,
                           UIColors.WarnSubText, TextAlignmentOptions.Center);
            AnchorTL(sub.rectTransform, 40, 96, 640, 36);

            WarnButton(box, 40,  "계속 편집",     SettingsAction.WarnCancel);
            WarnButton(box, 262, "적용하고 닫기", SettingsAction.WarnApplyClose);
            WarnButton(box, 484, "그냥 닫기",     SettingsAction.WarnDiscardClose);

            warningModal = overlay.gameObject;
            warningModal.SetActive(false);
        }
        void WarnButton(RectTransform box, float x, string label, SettingsAction action)
        {
            var b = Panel(box, $"WarnBtn_{action}", 196, 62, 31, UIColors.KeyBG);
            AnchorTL(b, x, 164, 196, 62);
            AddOutline(b.GetComponent<Image>(), UIColors.KeyBorder, 1);
            var t = LocText(b, label, 21, FontWeight.Bold, UIColors.TextValue, TextAlignmentOptions.Center);
            AnchorLeftMiddle(t.rectTransform, 0, 196, 40); t.alignment = TextAlignmentOptions.Center;
            // 키 바인딩 버튼과 같은 어두운 계열 — 패널 안의 다른 어두운 컨트롤과 톤을 맞춘다.
            AddBtn(b.gameObject, b.GetComponent<Image>(), UIColors.KeyBG, UIColors.KeyBGHover,
                   UIColors.KeyBGActive, null, null, action);
        }

        void ShowWarning(bool on) { if (warningModal) warningModal.SetActive(on); }

        // 닫기 요청 — 미적용 변경이 있으면 닫지 않고 경고창을 띄운다.
        void RequestClose()
        {
            SettingsBinding.PlayClick();
            if (SettingsBinding.HasUnappliedChanges) { CloseAll(); ShowWarning(true); return; }
            ClosePanel();
        }
        // 오브젝트만 끄면 일시정지·입력잠금을 관리하는 쪽은 여전히 "설정창 열림"으로 남는다.
        // (그래서 X로 닫아도 게임이 멈춘 채였고 ESC를 한 번 더 눌러야 풀렸다)
        // 기존 시스템의 닫기 경로를 타야 SetState(None)이 일시정지 해제와
        // WindowManager 동기화까지 해준다.
        void ClosePanel()
        {
            ShowWarning(false);

            var gui = GameUIController.Instance;
            if (gui != null && gui.GetCurrentState() == GameUIController.UIState.Settings)
                gui.CloseSettings();

            // GameUIController가 없는 씬(MainMenu 등)이거나, settingsPanel 참조가
            // 이 오브젝트를 가리키지 않아 위에서 안 꺼졌을 때의 보루.
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        // 만든 계층의 역할 오브젝트들을 참조 컴포넌트에 기록한다.
        // 런타임 빌드에서는 없어도 그만이지만, 에디터 베이크는 이 값이 씬에 저장되어야
        // 다음 실행 때 계층을 그대로 주워 쓸 수 있다.
        void CaptureRefs()
        {
            if (refs == null) refs = GetComponent<SettingsPanelRefs>();
            if (refs == null) refs = gameObject.AddComponent<SettingsPanelRefs>();

            refs.root = root;
            refs.bodyViewport = bodyViewport;
            refs.tabIndicator = tabIndicator;
            refs.tabIcons = _tabIcons;
            refs.tabButtons = _tabBtns;
            refs.displayPanel = displayPanel; refs.audioPanel = audioPanel; refs.controlsPanel = controlsPanel;
            refs.displayCG    = displayCG;    refs.audioCG    = audioCG;    refs.controlsCG    = controlsCG;
            refs.controlsHint = controlsHint;
            refs.applyBG      = applyBG;
            refs.warningModal = warningModal;
            refs.scrim        = scrimImg;
            refs.tabBarBG     = tabBarImg;
            refs.rowBackgrounds = new List<Image>(rowBGs);
        }

        // ---------------- 헤더 ----------------
        void BuildHeader()
        {
            // 제목 "설정" + 부제 "Settings"
            // 오브젝트 2개로 나누면 62/30 폰트의 baseline을 맞출 수 없다(각자 rect 중앙 정렬이라 어긋남).
            // 한 TMP 안에서 <size>로 크기만 바꾸면 TMP가 같은 baseline 위에 이어서 그린다.
            //   <space=14> 간격 / <color=#8A8A8A> 부제 회색 / <cspace> 자간 +0.5px
            var title = Text(root,
                "설정<space=14><size=26><cspace=0.02em><color=#8A8A8A>Settings</color></cspace></size>",
                62, FontWeight.Heavy, Color.white, TextAlignmentOptions.Left);
            AnchorTL(title.rectTransform, SIDE, 46, 900, 80);   // 부제까지 들어가도록 폭 확보
            title.characterSpacing = -2f;                       // -2 units ≈ -1.24px @62 (부제 구간은 cspace가 덮어씀)
            _titleTmp = title;
            RefreshTitleLabel();

            // 중앙 탭 컨테이너
            var container = Panel(root, "TabBar", 300 + 24 + 26, 84, 20, TabBarColor()); // 3*88 + gaps... 계산 후 재조정
            tabBarImg = container.GetComponent<Image>();
            // 정확 폭: padding(12*2) + 88*3 + 구분선/여백. 아래에서 자식 배치로 폭 확정.
            float pad = 12f, btnW = 88f, btnH = 72f, divGap = 12f, divW = 1f;
            float innerW = btnW * 3 + (divGap * 2 + divW) * 2;
            float contW = innerW + pad * 2;
            SetSize(container, contW, 72 + 12);
            AnchorTop(container, TAB_TOP);
            var cimg = container.GetComponent<Image>();
            AddOutline(cimg, UIColors.TabBorder, 1);

            // 인디케이터(노란 알약) — 먼저(뒤쪽)
            tabIndicator = Panel(container, "TabIndicator", btnW, btnH, 16, UIColors.AccentYellow);
            AnchorTL(tabIndicator, pad, 6, btnW, btnH);

            // 구분선 2개 — 버튼보다 먼저(뒤쪽). 호버로 커진 버튼이 구분선을 덮도록.
            float step = UIAnim.TabIndicatorStep;   // 113 = btnW(88) + divGap*2(24) + divW(1). 인디케이터와 동일 간격.
            Divider(container, pad + btnW + divGap, 40);
            Divider(container, pad + step + btnW + divGap, 40);

            // 탭 버튼 3개 — 인디케이터와 같은 step으로 배치해야 노란 알약이 아이콘에 정확히 겹친다.
            var gear = TabButton(container, Icons.Gear(), pad + step * 0, () => SwitchTab(SettingsTab.Display), SettingsAction.TabDisplay, out var fx0);
            var note = TabButton(container, Icons.Note(), pad + step * 1, () => SwitchTab(SettingsTab.Audio), SettingsAction.TabAudio, out var fx1);
            var mouse = TabButton(container, Icons.Mouse(), pad + step * 2, () => SwitchTab(SettingsTab.Controls), SettingsAction.TabControls, out var fx2);
            _tabIcons = new[] { gear, note, mouse };
            _tabBtns = new[] { fx0, fx1, fx2 };

            // 닫기(X)
            var close = Panel(root, "Close", 56, 56, 28, UIColors.CloseBG);
            AnchorTR(close, CLOSE_MARGIN, CLOSE_MARGIN, 56, 56);   // 우측·상단 여백 동일
            var closeIcon = IconChild(close, Icons.Close(), 20, 20, Color.white, "Icon_Close");
            AddBtn(close.gameObject, close.GetComponent<Image>(), UIColors.CloseBG, UIColors.CloseBGHover, UIColors.CloseBGActive, null,
                   RequestClose, SettingsAction.Close);
        }
        Image[] _tabIcons;
        Btn[] _tabBtns;

        // ---------------- 본문 ----------------
        void BuildBody()
        {
            // 스크롤 뷰
            var scroll = NewRect("Body", root);
            AnchorStretchTB(scroll, 138, FOOTER_STRIP); // 헤더 아래 ~ 푸터 위(풋터 여백까지 비움)
            var sr = scroll.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = scrollSensitivity;   // 기본 1은 휠 한 칸에 거의 안 움직임
            sr.inertia = false;                          // 휠 조작이 미끄러지지 않게
            var viewport = NewRect("Viewport", scroll);
            Stretch(viewport);
            var vpImg = viewport.gameObject.AddComponent<Image>(); vpImg.color = new Color(0,0,0,0);
            viewport.gameObject.AddComponent<RectMask2D>();   // Mask(스텐실)+alpha0 이미지는 Game에서 내부를 전부 클리핑함 → RectMask2D로
            sr.viewport = viewport;
            bodyViewport = viewport;

            displayPanel = MakePanel(viewport, "DisplayPanel", out displayCG);
            audioPanel = MakePanel(viewport, "AudioPanel", out audioCG);
            controlsPanel = MakePanel(viewport, "ControlsPanel", out controlsCG);
            sr.content = displayPanel; // 활성 패널로 갱신됨

            BuildDisplay(displayPanel);
            BuildAudio(audioPanel);
            BuildControls(controlsPanel);
        }

        RectTransform MakePanel(RectTransform parent, string name, out CanvasGroup cg)
        {
            var p = NewRect(name, parent);
            p.anchorMin = new Vector2(0, 1); p.anchorMax = new Vector2(1, 1); p.pivot = new Vector2(0.5f, 1);
            p.offsetMin = new Vector2(SIDE, 0); p.offsetMax = new Vector2(-SIDE, 0);
            p.anchoredPosition = new Vector2(0, -20);
            cg = p.gameObject.AddComponent<CanvasGroup>();
            return p;
        }

        float _y; // 현재 패널 y 커서 (위에서부터의 거리, 아래로 갈수록 증가/양수)
        void ResetCursor() { _y = 0; }

        void SectionHeader(RectTransform panel, string label)
        {
            _y += 6;
            float h = 28;
            var row = NewRect($"Section_{label}", panel);
            AnchorTL(row, 0, _y, PanelW(), h);
            var t = LocText(row, label, 23, FontWeight.Heavy, UIColors.SectionLabel, TextAlignmentOptions.Left);
            // 라벨 폭을 200px로 고정하면 짧은 라벨일수록 구분선이 멀리 밀린다(목업은 라벨 바로 뒤 26px).
            // 실제 렌더 폭을 재서 붙인다.
            float labelW = t.GetPreferredValues(label).x;
            AnchorTL(t.rectTransform, 0, 0, labelW, h);
            var line = NewRect("SectionDivider", row);
            var li = line.gameObject.AddComponent<Image>(); li.color = UIColors.Divider;
            float lineX = labelW + 26;
            AnchorTL(line, lineX, (h - 1) / 2, PanelW() - lineX, 1);
            _y += h + 22;
        }

        RectTransform SettingRow(RectTransform panel, string label, out RectTransform controlSlot, float bottomGap = 24f)
        {
            var row = Panel(panel, $"Row_{label}", PanelW(), 96, 18, RowColor());
            rowBGs.Add(row.GetComponent<Image>());
            AnchorTL(row, 0, _y, PanelW(), 96);
            var t = LocText(row, label, 25, FontWeight.SemiBold, UIColors.TextRow, TextAlignmentOptions.Left);
            AnchorLeftMiddle(t.rectTransform, 44, 700, 40);
            controlSlot = NewRect("ControlSlot", row);
            controlSlot.anchorMin = new Vector2(1, 0.5f); controlSlot.anchorMax = new Vector2(1, 0.5f); controlSlot.pivot = new Vector2(1, 0.5f);
            controlSlot.anchoredPosition = new Vector2(-44, 0);
            _y += 96 + bottomGap;
            return row;
        }

        float PanelW() => W - SIDE * 2; // 1620

        void BuildDisplay(RectTransform panel)
        {
            ResetCursor();
            RectTransform slot;

            SectionHeader(panel, "언어 설정");
            SettingRow(panel, "언어", out slot, 34);        AddDropdown(slot, SettingId.Language);

            SectionHeader(panel, "성능 및 화면");
            SettingRow(panel, "화면 품질", out slot);        AddDropdown(slot, SettingId.ScreenQuality);
            SettingRow(panel, "표시 모드", out slot);        AddToggle(slot);
            SettingRow(panel, "해상도", out slot);           AddDropdown(slot, SettingId.Resolution);
            SettingRow(panel, "그림자 품질", out slot);      AddDropdown(slot, SettingId.ShadowQuality);
            SettingRow(panel, "텍스처 품질", out slot);      AddDropdown(slot, SettingId.TextureQuality);

            FitPanel(panel);
        }

        void BuildAudio(RectTransform panel)
        {
            ResetCursor();
            SectionHeader(panel, "오디오 설정");
            RectTransform slot;
            SettingRow(panel, "마스터 볼륨", out slot);  AddVolume(slot, SettingId.MasterVolume);
            SettingRow(panel, "배경음(BGM)", out slot);  AddVolume(slot, SettingId.BgmVolume);
            SettingRow(panel, "효과음(SFX)", out slot);  AddVolume(slot, SettingId.SfxVolume);
            FitPanel(panel);
        }

        void AddVolume(RectTransform slot, SettingId id) =>
            AddSlider(slot, id, true, false, Icons.Speaker(), Icons.Mute());

        // 음소거 상태는 각 슬라이더가 직접 들고 있다. 패널을 다시 열거나 초기화할 때만 표시를 푼다.
        void ResetMuteStates() { foreach (var s in sliders) s.ClearMute(); }

        void BuildControls(RectTransform panel)
        {
            ResetCursor();
            SectionHeader(panel, "조작 설정");
            RectTransform slot;
            SettingRow(panel, "마우스 감도", out slot);
            AddSlider(slot, SettingId.Sensitivity, false, true, Icons.Mouse(), null);

            for (int i = 0; i < SettingsBinding.ActionCount; i++)
            {
                SettingRow(panel, SettingsBinding.ActionLabel(i), out slot);
                AddKeyBind(slot, i);
            }
            FitPanel(panel);
        }

        void FitPanel(RectTransform panel)
        {
            float total = _y + 30;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, total);
        }

        // ---------------- 푸터 ----------------
        void BuildFooter()
        {
            var footer = NewRect("Footer", root);
            footer.anchorMin = new Vector2(0, 0); footer.anchorMax = new Vector2(1, 0); footer.pivot = new Vector2(0.5f, 0);
            // 아래 여백 = FOOTER_PAD, 위 여백 = FOOTER_STRIP - FOOTER_PAD - FOOTER_BTN_H = FOOTER_PAD (동일)
            footer.offsetMin = new Vector2(SIDE, FOOTER_PAD);
            footer.offsetMax = new Vector2(-SIDE, FOOTER_PAD + FOOTER_BTN_H);

            // 좌측: 메인 메뉴 + 안내
            var main = FooterButton(footer, "메인 메뉴로 돌아가기", Icons.Back(), OnMainMenu, SettingsAction.MainMenu);
            main.anchorMin = new Vector2(0, 0.5f); main.anchorMax = new Vector2(0, 0.5f); main.pivot = new Vector2(0, 0.5f);
            main.anchoredPosition = new Vector2(0, 0);

            controlsHint = LocText(footer, "변경하고자 하는 키를 눌러서 선택해 주세요.", 22, FontWeight.Regular, UIColors.HintText, TextAlignmentOptions.Left).gameObject;
            var hr = (RectTransform)controlsHint.transform;
            hr.anchorMin = new Vector2(0, 0.5f); hr.anchorMax = new Vector2(0, 0.5f); hr.pivot = new Vector2(0, 0.5f);
            hr.anchoredPosition = new Vector2(main.sizeDelta.x + 30, 0);
            hr.sizeDelta = new Vector2(560, FOOTER_BTN_H);   // 버튼과 같은 높이 → 버튼 라벨과 세로 중심이 일치
            // 메인 메뉴 버튼 폭이 실행 시 다시 계산되므로, 안내 문구도 그때 같이 밀리게 연결한다.
            var mainFit = main.GetComponent<FooterButtonFit>();
            if (mainFit) { mainFit.follow = hr; mainFit.Fit(); }

            // 우측: 초기화 + 적용
            var apply = FooterButton(footer, "설정 적용", Icons.Check(), OnApply, SettingsAction.Apply, 230);
            apply.anchorMin = new Vector2(1, 0.5f); apply.anchorMax = new Vector2(1, 0.5f); apply.pivot = new Vector2(1, 0.5f);
            apply.anchoredPosition = new Vector2(0, 0);
            applyBG = apply.GetComponent<Image>();

            var reset = FooterButton(footer, "설정 초기화", Icons.Reset(), OnReset, SettingsAction.ResetAll, 230);
            reset.anchorMin = new Vector2(1, 0.5f); reset.anchorMax = new Vector2(1, 0.5f); reset.pivot = new Vector2(1, 0.5f);
            reset.anchoredPosition = new Vector2(-(230 + 22), 0);
        }

        RectTransform FooterButton(RectTransform parent, string label, Sprite icon, Action onClick, SettingsAction action, float minW = 0)
        {
            var b = Panel(parent, $"Footer_{action}", 0, FOOTER_BTN_H, FOOTER_BTN_H / 2f, UIColors.OffWhite);
            var txt = LocText(b, label, 21, FontWeight.Bold, UIColors.TextDark, TextAlignmentOptions.Center);
            // 원형 아이콘
            var circle = Panel(b, "IconCircle", 42, 42, 21, UIColors.CircleIconBG);
            AnchorRightMiddle(circle, 10, 42, 42);
            IconChild(circle, icon, 20, 20, Color.white, "Icon");
            // 텍스트: 왼쪽끝 ~ 아이콘 사이 중앙
            var tr = txt.rectTransform;
            tr.anchorMin = new Vector2(0, 0); tr.anchorMax = new Vector2(1, 1);
            tr.offsetMin = new Vector2(32, 0); tr.offsetMax = new Vector2(-(42 + 12 + 10), 0);
            // 폭은 라벨 길이에 맞춘다. 베이크 시점의 측정값은 폰트 아틀라스가 덜 준비돼
            // 실제보다 작게 나올 수 있으므로, 실행할 때 다시 재도록 컴포넌트로 붙인다.
            SetSize(b, Mathf.Max(minW, 200f), FOOTER_BTN_H);
            var fit = b.gameObject.AddComponent<FooterButtonFit>();
            fit.label = txt; fit.minWidth = minW;
            fit.Fit();

            AddBtn(b.gameObject, b.GetComponent<Image>(), UIColors.OffWhite, UIColors.OffWhiteHover, UIColors.OffWhiteActive, null, onClick, action);
            return b;
        }

        // ==================================================================
        //  위젯: 탭 버튼
        // ==================================================================
        Image TabButton(RectTransform parent, Sprite icon, float x, Action onClick, SettingsAction action, out Btn fxOut)
        {
            // 호버 배경은 인디케이터(노란 알약)와 같은 88x72 / radius 16 라운드 사각형이어야 한다.
            // NewRect + 민무늬 Image는 각진 사각형이 나오므로 Panel(9-slice 라운드 스프라이트)로 만든다.
            var btn = Panel(parent, $"Tab_{action}", 88, 72, 16, UIColors.Transparent(UIColors.TabHover));
            // pivot을 정중앙으로. AnchorTL의 pivot(0,1)로 두면 hover scale이 우하단으로 커진다.
            btn.anchorMin = new Vector2(0, 1); btn.anchorMax = new Vector2(0, 1); btn.pivot = new Vector2(0.5f, 0.5f);
            btn.anchoredPosition = new Vector2(x + 88 / 2f, -(6 + 72 / 2f));
            var hit = btn.GetComponent<Image>();
            var img = IconChild(btn, icon, 34, 34, UIColors.TabIconInactive, "Icon");
            var fx = btn.gameObject.AddComponent<Btn>();
            fx.action = action;
            // 시작색은 hover와 같은 RGB의 투명색 — 알파만 0→1로 페이드시켜 중간에 색이 뜨지 않게.
            fx.Init(hit, UIColors.Transparent(UIColors.TabHover), UIColors.TabHover, UIColors.TabActive, btn, 1.1f, 0.94f, onClick);
            fxOut = fx;
            return img;
        }

        // ==================================================================
        //  위젯: 드롭다운
        // ==================================================================
        void AddDropdown(RectTransform slot, SettingId id)
        {
            var options = SettingsBinding.Options(id);

            var btn = Panel(slot, $"Dropdown_{id}", 460, 62, 31, UIColors.OffWhite);
            var dd = btn.gameObject.AddComponent<Dropdown>();
            dd.setting = id;
            btn.anchorMin = new Vector2(1, 0.5f); btn.anchorMax = new Vector2(1, 0.5f); btn.pivot = new Vector2(1, 0.5f);
            btn.anchoredPosition = Vector2.zero;
            dd.button = btn;
            dd.row = slot.parent as RectTransform;   // 팝업 열 때 이 행을 최상단으로 올리기 위한 참조
            dd.viewport = bodyViewport;              // 펼침 방향(아래/위) 판단 기준
            dd.valueLabel = Text(btn, dd.Current, 23, FontWeight.SemiBold, UIColors.TextDark, TextAlignmentOptions.Left);
            AnchorLeftMiddle(dd.valueLabel.rectTransform, 30, 380, 40);
            var chev = IconChild(btn, Icons.Chevron(), 22, 22, new Color(0.35f,0.35f,0.35f), "Icon_Chevron");
            var chevRT = (RectTransform)chev.transform;
            // 회전은 pivot 기준이라 AnchorRightMiddle의 pivot(1,0.5)로 두면 180° 돌 때
            // 아이콘이 축 반대편으로 넘어가 폭(22px)만큼 옆으로 튄다. pivot을 중앙으로 옮기고
            // 우측 여백 30px이 유지되도록 위치를 반폭만큼 보정한다.
            const float chevSize = 22f, chevRight = 30f;
            chevRT.anchorMin = new Vector2(1, 0.5f); chevRT.anchorMax = new Vector2(1, 0.5f);
            chevRT.pivot = new Vector2(0.5f, 0.5f);
            chevRT.sizeDelta = new Vector2(chevSize, chevSize);
            chevRT.anchoredPosition = new Vector2(-(chevRight + chevSize / 2f), 0);
            dd.chevron = chevRT;

            AddBtn(btn.gameObject, btn.GetComponent<Image>(), UIColors.OffWhite, UIColors.OffWhiteHover, UIColors.OffWhiteActive, null,
                   () => ToggleDropdown(dd), SettingsAction.ToggleDropdown);

            // 팝업
            var popup = Panel(btn, "Popup", 460, 8, 22, UIColors.OffWhite);
            popup.anchorMin = new Vector2(0.5f, 1); popup.anchorMax = new Vector2(0.5f, 1); popup.pivot = new Vector2(0.5f, 1);
            // 앵커는 버튼 윗변에 고정. pivot/위치는 열 때 Dropdown.SetOpen이 공간을 보고 아래/위로 정한다.
            popup.anchoredPosition = new Vector2(0, -70);
            var shadow = popup.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0,0,0,0.55f); shadow.effectDistance = new Vector2(0, -10);
            dd.popup = popup;
            dd.popupCG = popup.gameObject.AddComponent<CanvasGroup>();

            float oy = 8;
            const float optW = 460 - 16;
            bool first = true;
            foreach (var o in options)
            {
                // 옵션 사이 경계선. 항목 사이 4px 간격의 한가운데에 두어 호버/선택 배경과 겹치지 않는다.
                // 맨 위/맨 아래에는 넣지 않는다.
                if (!first)
                {
                    var sep = NewRect("OptionDivider", popup);
                    AnchorTL(sep, 8, oy - 2.5f, optW, 1);
                    var si = sep.gameObject.AddComponent<Image>();
                    si.color = UIColors.OptDivider; si.raycastTarget = false;
                }
                first = false;

                string cap = o;
                var opt = Panel(popup, $"Option_{o}", optW, 54, 15, UIColors.Transparent(UIColors.OptHover));
                AnchorTL(opt, 8, oy, 460 - 16, 54);
                var ot = Text(opt, o, 22, FontWeight.SemiBold, UIColors.TextDark, TextAlignmentOptions.Left);
                AnchorLeftMiddle(ot.rectTransform, 24, 400, 40);
                var oimg = opt.GetComponent<Image>();
                var ob = opt.gameObject.AddComponent<Btn>();
                ob.action = SettingsAction.SelectOption; ob.actionParam = dd.optionImages.Count;   // 이 옵션의 인덱스
                bool selected = o == dd.Current;
                ob.Init(oimg, selected ? UIColors.AccentYellowSoft : UIColors.Transparent(UIColors.OptHover), UIColors.OptHover, UIColors.OptActive, null, 1f, 1f, () => { dd.Commit(cap); CloseAll(); });
                dd.optionImages.Add(oimg);
                dd.optionButtons.Add(ob);
                oy += 54 + 4;
            }
            SetSize(popup, 460, oy);
            popup.gameObject.SetActive(false);
            dropdowns.Add(dd);
        }

        void ToggleDropdown(Dropdown dd)
        {
            bool open = !dd.isOpen;
            CloseAll();
            if (!open) return;
            dd.SetOpen(true);
            SettingsBinding.PlayClick();   // 구 UI에서 드롭다운 열 때 나던 소리
        }
        void CloseAll() { foreach (var d in dropdowns) d.SetOpen(false); }

        // ==================================================================
        //  위젯: 표시 모드 토글
        // ==================================================================
        void AddToggle(RectTransform slot)
        {
            var track = Panel(slot, "Toggle_DisplayMode", 460, 62, 31, UIColors.ToggleTrack);
            track.anchorMin = new Vector2(1, 0.5f); track.anchorMax = new Vector2(1, 0.5f); track.pivot = new Vector2(1, 0.5f);
            track.anchoredPosition = Vector2.zero;
            var knob = Panel(track, "ToggleKnob", 226, 54, 27, UIColors.ToggleKnob);
            AnchorTL(knob, 4, 4, 226, 54);
            var full = LocText(track, "전체 화면", 22, FontWeight.Bold, UIColors.TextDark, TextAlignmentOptions.Center);
            AnchorTL(full.rectTransform, 0, 11, 230, 40);
            var win = LocText(track, "창 모드", 22, FontWeight.Bold, UIColors.ToggleTextOff, TextAlignmentOptions.Center);
            AnchorTL(win.rectTransform, 230, 11, 230, 40);

            // 라벨 TMP는 raycastTarget=false라 클릭을 못 받는다.
            // 트랙 좌/우 절반을 덮는 투명 히트 영역을 얹어 세그먼트 전체(230x62)를 클릭 가능하게 한다.
            SegHit(track, 0, full, () => SetMode(DisplayMode.Full), SettingsAction.FullscreenOn);
            SegHit(track, 230, win, () => SetMode(DisplayMode.Window), SettingsAction.FullscreenOff);

            displayToggle = track.gameObject.AddComponent<SegToggle>();
            displayToggle.knob = knob; displayToggle.full = full; displayToggle.win = win;
            displayToggle.Apply(SettingsBinding.GetFullscreen() ? DisplayMode.Full : DisplayMode.Window, true);
        }
        void SegHit(RectTransform track, float x, TMP_Text label, Action onClick, SettingsAction action)
        {
            var hit = NewRect($"SegHit_{action}", track);
            AnchorTL(hit, x, 0, 230, 62);
            var img = hit.gameObject.AddComponent<Image>(); img.color = new Color(1, 1, 1, 0);
            var b = hit.gameObject.AddComponent<Btn>();
            b.action = action;
            b.InitTextButton(label, onClick);   // pressed 시 라벨 opacity 0.6 (스펙 5.2)
        }
        void SetMode(DisplayMode m) { SettingsBinding.SetFullscreen(m == DisplayMode.Full); displayToggle.Apply(m, false); }

        // ==================================================================
        //  위젯: 슬라이더 (+ 음소거)
        // ==================================================================
        void AddSlider(RectTransform slot, SettingId id,
                       bool hasMute, bool sensFmt, Sprite iconOn, Sprite iconOff)
        {
            var group = NewRect($"Slider_{id}", slot);
            group.anchorMin = new Vector2(1, 0.5f); group.anchorMax = new Vector2(1, 0.5f); group.pivot = new Vector2(1, 0.5f);
            SetSize(group, 660, 40); group.anchoredPosition = Vector2.zero;

            var s = group.gameObject.AddComponent<Slider3>();
            s.setting = id; s.hasMute = hasMute; s.sensFmt = sensFmt;

            // 아이콘/음소거 버튼 (좌)
            var iconWrap = NewRect("IconSlot", group);
            AnchorLeftMiddle(iconWrap, 0, 34, 34);
            var on = IconChild(iconWrap, iconOn, 30, 30, UIColors.TextValue, "Icon_On");
            s.iconOn = on.gameObject;
            if (hasMute && iconOff != null)
            {
                var off = IconChild(iconWrap, iconOff, 30, 30, Color.white, "Icon_Muted");
                s.iconOff = off.gameObject;
                // 아이콘 Image는 raycastTarget=false, iconWrap엔 Graphic이 없어서 클릭이 아예 안 들어왔다.
                // 투명 Image를 얹어 히트 영역을 만든다. (배경 하이라이트 없음 — 스펙 5.4)
                var hit = iconWrap.gameObject.AddComponent<Image>();
                hit.color = new Color(1, 1, 1, 0);
                var mb = iconWrap.gameObject.AddComponent<Btn>();
                mb.action = SettingsAction.ToggleMute;
                mb.InitIconButton(iconWrap, s.ToggleMute);   // 음소거 상태는 슬라이더가 직접 들고 있다
            }

            // 값 (우)
            float valW = sensFmt ? 66 : 56;
            var val = Text(group, "", 23, FontWeight.SemiBold, UIColors.TextValue, TextAlignmentOptions.Right);
            AnchorRightMiddle(val.rectTransform, 0, valW, 40);
            s.valueLabel = val;

            // 트랙 (중앙 flex)
            var track = NewRect("Track", group);
            track.anchorMin = new Vector2(0, 0.5f); track.anchorMax = new Vector2(1, 0.5f); track.pivot = new Vector2(0.5f, 0.5f);
            track.offsetMin = new Vector2(34 + 24, -11); track.offsetMax = new Vector2(-(valW + 24), 11);
            var trackHit = track.gameObject.AddComponent<Image>(); trackHit.color = new Color(0,0,0,0);
            var line = Panel(track, "TrackLine", 10, 4, 2, UIColors.SliderTrack);
            line.anchorMin = new Vector2(0,0.5f); line.anchorMax = new Vector2(1,0.5f); line.pivot = new Vector2(0.5f,0.5f);
            line.offsetMin = new Vector2(0,-2); line.offsetMax = new Vector2(0,2);
            var fill = Panel(track, "TrackFill", 10, 4, 2, new Color(0.94f,0.94f,0.94f,1));
            fill.anchorMin = new Vector2(0,0.5f); fill.anchorMax = new Vector2(0,0.5f); fill.pivot = new Vector2(0,0.5f);
            var handle = Panel(track, "TrackHandle", 17, 17, 9, Color.white);
            handle.anchorMin = new Vector2(0,0.5f); handle.anchorMax = new Vector2(0,0.5f); handle.pivot = new Vector2(0.5f,0.5f);
            var hsh = handle.gameObject.AddComponent<Shadow>(); hsh.effectColor = new Color(0,0,0,0.5f); hsh.effectDistance = new Vector2(0,-1);
            s.track = track; s.fill = fill; s.handle = handle;

            var ctl = track.gameObject.AddComponent<SliderInput>();
            ctl.Init(s);
            s.Refresh(false);
            sliders.Add(s);
        }

        // ==================================================================
        //  위젯: 키 바인딩
        // ==================================================================
        void AddKeyBind(RectTransform slot, int idx)
        {
            var btn = Panel(slot, $"Key_{SettingsBinding.ActionLabel(idx)}", 210, 60, 30, UIColors.KeyBG);
            btn.anchorMin = new Vector2(1, 0.5f); btn.anchorMax = new Vector2(1, 0.5f); btn.pivot = new Vector2(1, 0.5f);
            btn.anchoredPosition = Vector2.zero;
            AddOutline(btn.GetComponent<Image>(), UIColors.KeyBorder, 1);
            var t = Text(btn, SettingsBinding.KeyLabel(idx),
                         21, FontWeight.SemiBold, UIColors.TextValue, TextAlignmentOptions.Center);
            AnchorLeftMiddle(t.rectTransform, 0, 210, 40); t.alignment = TextAlignmentOptions.Center;
            var kr = btn.gameObject.AddComponent<KeyRow>();
            kr.actionIndex = idx;
            kr.bg = btn.GetComponent<Image>(); kr.label = t; kr.root = btn;
            kr.outline = btn.GetComponent<UnityEngine.UI.Outline>();
            var b = btn.gameObject.AddComponent<Btn>();
            b.action = SettingsAction.StartRebind; b.actionParam = idx;
            b.Init(btn.GetComponent<Image>(), UIColors.KeyBG, UIColors.KeyBGHover, UIColors.KeyBGActive, null, 1f, 1f, () => StartListening(idx));
            kr.hoverFx = b;
            keyRows.Add(kr);
        }
        void StartListening(int idx)
        {
            SettingsBinding.PlayClick();
            if (listening >= 0) keyRows[listening].SetListening(false);
            listening = idx; keyRows[idx].SetListening(true);
            listenFrame = Time.frameCount;
        }

        // ==================================================================
        //  버튼 디스패치 (씬에 배치된 버튼용)
        // ==================================================================
        // 코드로 만들 때는 Btn.onClick 람다가 직접 실행되고 여기로 오지 않는다.
        // 에디터로 구운 계층에서는 람다가 없으므로 Btn이 직렬화된 action을 들고 여기로 보낸다.
        public void Dispatch(SettingsAction action, int param, Btn source)
        {
            switch (action)
            {
                case SettingsAction.TabDisplay:  SwitchTab(SettingsTab.Display);  break;
                case SettingsAction.TabAudio:    SwitchTab(SettingsTab.Audio);    break;
                case SettingsAction.TabControls: SwitchTab(SettingsTab.Controls); break;

                case SettingsAction.Close:    RequestClose(); break;
                case SettingsAction.Apply:    OnApply();    break;
                case SettingsAction.ResetAll: OnReset();    break;
                case SettingsAction.MainMenu: OnMainMenu(); break;

                case SettingsAction.FullscreenOn:  SetMode(DisplayMode.Full);   break;
                case SettingsAction.FullscreenOff: SetMode(DisplayMode.Window); break;


                case SettingsAction.ToggleDropdown:
                {
                    var dd = source ? source.GetComponentInParent<Dropdown>() : null;
                    if (dd) ToggleDropdown(dd);
                    break;
                }
                case SettingsAction.SelectOption:
                {
                    var dd = source ? source.GetComponentInParent<Dropdown>() : null;
                    if (dd == null) break;
                    var opts = dd.Options;
                    if (param >= 0 && param < opts.Length) { dd.Commit(opts[param]); CloseAll(); }
                    break;
                }
                case SettingsAction.ToggleMute:
                {
                    var s = source ? source.GetComponentInParent<Slider3>() : null;
                    if (s) s.ToggleMute();
                    break;
                }
                case SettingsAction.StartRebind:
                    StartListening(param);
                    break;

                case SettingsAction.WarnApplyClose:   OnApply(); ClosePanel(); break;
                case SettingsAction.WarnDiscardClose:
                    SettingsBinding.DiscardChanges();   // 편집값 폐기 → 마지막 적용 상태로
                    ResetMuteStates(); RefreshAll();
                    ClosePanel();
                    break;
                case SettingsAction.WarnCancel: ShowWarning(false); break;
            }
        }

        // ==================================================================
        //  탭 전환
        // ==================================================================
        void SwitchTab(SettingsTab t, bool instant = false)
        {
            // 빌드가 예외로 중단된 상태에서 탭을 누르면 절반만 만들어진 참조들 때문에
            // 프레임마다 NPE가 쏟아진다. 원래 예외만 남기고 조용히 무시한다.
            if (!built) return;
            if (!instant) SettingsBinding.PlayTabClick();   // 최초 표시(instant)에는 울리지 않는다
            tab = t; CloseAll();
            for (int i = 0; i < 3; i++)
            {
                if (_tabIcons != null && i < _tabIcons.Length && _tabIcons[i])
                    _tabIcons[i].color = (int)t == i ? UIColors.TextDark : UIColors.TabIconInactive;
                // 활성 탭은 호버 배경을 끈다. 켜두면 노란 인디케이터 위에 회색막이 한 겹 덮인다.
                if (_tabBtns != null && i < _tabBtns.Length && _tabBtns[i])
                    _tabBtns[i].SetColorFeedback((int)t != i);
            }
            float x = 12 + (int)t * UIAnim.TabIndicatorStep;
            UITween.AnchorX(tabIndicator, x, instant ? 0 : UIAnim.TabIndicator, Ease.OutBack);

            ShowPanel(displayPanel, displayCG, t == SettingsTab.Display, instant);
            ShowPanel(audioPanel, audioCG, t == SettingsTab.Audio, instant);
            ShowPanel(controlsPanel, controlsCG, t == SettingsTab.Controls, instant);
            // parent.parent로 캐내면 계층이 한 단계만 바뀌어도 조용히 null이 된다.
            var sr = displayPanel.GetComponentInParent<ScrollRect>();
            if (sr) sr.content = t == SettingsTab.Display ? displayPanel : t == SettingsTab.Audio ? audioPanel : controlsPanel;

            controlsHint.SetActive(t == SettingsTab.Controls);
            if (listening >= 0) { keyRows[listening].SetListening(false); listening = -1; }
            Canvas.ForceUpdateCanvases();
            foreach (var s in sliders) s.Refresh(false);
        }
        void ShowPanel(RectTransform p, CanvasGroup cg, bool on, bool instant)
        {
            p.gameObject.SetActive(on);
            cg.interactable = on; cg.blocksRaycasts = on;
            if (!on) return;
            if (instant) { cg.alpha = 1; return; }
            cg.alpha = 0;
            var basePos = p.anchoredPosition;
            p.anchoredPosition = basePos + new Vector2(0, -14);
            UITween.Fade(cg, 1, UIAnim.PanelFade, Ease.OutQuad);
            UITween.AnchorY(p, basePos.y, UIAnim.PanelFade, Ease.OutQuad);
        }

        // ==================================================================
        //  하단 액션
        // ==================================================================
        // 초기화도 기존 시스템 규칙 그대로 — 폼(_pending)만 기본값으로 되돌리고,
        // 실제 엔진 반영/저장은 "설정 적용"을 눌러야 이루어진다.
        void OnReset()
        {
            CloseAll();
            if (listening >= 0) { keyRows[listening].SetListening(false); listening = -1; }
            SettingsBinding.ResetAll();
            ResetMuteStates();
            RefreshAll();
        }
        void RefreshAll()
        {
            RefreshTitleLabel();
            foreach (var d in dropdowns) d.SetValue(d.Current);   // _pending을 다시 읽어 표시
            if (displayToggle)
                displayToggle.Apply(SettingsBinding.GetFullscreen() ? DisplayMode.Full : DisplayMode.Window, false);
            foreach (var s in sliders) s.Refresh(false);
            for (int i = 0; i < keyRows.Count && i < SettingsBinding.ActionCount; i++)
                keyRows[i].SetKey(SettingsBinding.KeyLabel(i));
        }
        void OnApply()
        {
            SettingsBinding.Apply();   // _pending → _data 커밋 + settings.json 저장 + 엔진 반영
            // 적용 플래시
            UITween.Color(applyBG, UIColors.AccentYellow, 0f, Ease.Linear);
            UITween.Color(applyBG, UIColors.OffWhite, UIAnim.ApplyFlash, Ease.Linear);
        }
        void OnMainMenu() { SettingsBinding.QuitToMainMenu(); }   // 저장 + timeScale 정상화까지 매니저가 처리

        // ==================================================================
        //  키 감지
        // ==================================================================
        // 기존 시스템의 리바인드 후보와 동일한 범위: 키보드 + 마우스 좌/우/휠클릭만.
        // (조이스틱과 Mouse3~6은 KeyCode.Mouse0 이후 구간이라 통째로 건너뛴다)
        static KeyCode DetectKeyCode()
        {
            if (Input.GetMouseButtonDown(0)) return KeyCode.Mouse0;
            if (Input.GetMouseButtonDown(1)) return KeyCode.Mouse1;
            if (Input.GetMouseButtonDown(2)) return KeyCode.Mouse2;
            foreach (KeyCode kc in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (kc == KeyCode.None || kc >= KeyCode.Mouse0) continue;
                if (Input.GetKeyDown(kc)) return kc;
            }
            return KeyCode.None;
        }

        // ==================================================================
        //  생성 헬퍼 (RectTransform / TMP / Panel / Icon / 앵커)
        // ==================================================================
        RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            return rt;
        }
        void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
        void AnchorStretchTB(RectTransform rt, float top, float bottom)
        {
            rt.anchorMin = new Vector2(0,0); rt.anchorMax = new Vector2(1,1);
            rt.offsetMin = new Vector2(0, bottom); rt.offsetMax = new Vector2(0, -top);
        }
        void SetSize(RectTransform rt, float w, float h) { rt.sizeDelta = new Vector2(w, h); }
        // 상단-좌 기준 (부모 top-left 0,0, y는 아래로 음수)
        void AnchorTL(RectTransform rt, float x, float yDown, float w, float h)
        {
            rt.anchorMin = new Vector2(0,1); rt.anchorMax = new Vector2(0,1); rt.pivot = new Vector2(0,1);
            rt.anchoredPosition = new Vector2(x, -yDown); rt.sizeDelta = new Vector2(w,h);
        }
        void AnchorTop(RectTransform rt, float yDown) { rt.anchorMin = new Vector2(0.5f,1); rt.anchorMax = new Vector2(0.5f,1); rt.pivot = new Vector2(0.5f,1); rt.anchoredPosition = new Vector2(0,-yDown); }
        void AnchorTR(RectTransform rt, float right, float top, float w, float h) { rt.anchorMin = new Vector2(1,1); rt.anchorMax = new Vector2(1,1); rt.pivot = new Vector2(1,1); rt.anchoredPosition = new Vector2(-right,-top); rt.sizeDelta = new Vector2(w,h); }
        void AnchorLeftMiddle(RectTransform rt, float left, float w, float h) { rt.anchorMin = new Vector2(0,0.5f); rt.anchorMax = new Vector2(0,0.5f); rt.pivot = new Vector2(0,0.5f); rt.anchoredPosition = new Vector2(left,0); rt.sizeDelta = new Vector2(w,h); }
        void AnchorRightMiddle(RectTransform rt, float right, float w, float h) { rt.anchorMin = new Vector2(1,0.5f); rt.anchorMax = new Vector2(1,0.5f); rt.pivot = new Vector2(1,0.5f); rt.anchoredPosition = new Vector2(-right,0); rt.sizeDelta = new Vector2(w,h); }

        RectTransform Panel(RectTransform parent, string name, float w, float h, float radius, Color color)
        {
            var rt = NewRect(name, parent);
            SetSize(rt, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded.Get(Mathf.RoundToInt(radius));
            img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
            img.color = color;
            return rt;
        }
        void Divider(RectTransform parent, float x, float h)
        {
            var rt = NewRect("TabDivider", parent);
            AnchorTL(rt, x, (72 + 12 - h)/2, 1, h);
            var img = rt.gameObject.AddComponent<Image>(); img.color = UIColors.TabDivider;
        }
        Image IconChild(RectTransform parent, Sprite sprite, float w, float h, Color color, string name = "Icon")
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = new Vector2(0.5f,0.5f); rt.anchorMax = new Vector2(0.5f,0.5f); rt.pivot = new Vector2(0.5f,0.5f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(w,h);
            var img = rt.gameObject.AddComponent<Image>(); img.sprite = sprite; img.color = color; img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }
        // 계층에서 알아보기 쉽도록 텍스트 오브젝트 이름을 내용에서 만든다.
        // 리치텍스트 태그(<size=26> 등)와 줄바꿈은 이름에 섞이면 읽기 어려우니 걷어낸다.
        static string TextObjectName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Text";
            var sb = new System.Text.StringBuilder(s.Length);
            bool inTag = false;
            foreach (char c in s)
            {
                if (c == '<') { inTag = true; continue; }
                if (c == '>') { inTag = false; continue; }
                if (inTag) continue;
                sb.Append(c == '\n' || c == '\r' ? ' ' : c);
            }
            string clean = sb.ToString().Trim();
            if (clean.Length == 0) return "Text";
            if (clean.Length > 16) clean = clean.Substring(0, 16).TrimEnd() + "…";
            return "Text_" + clean;
        }

        void AddOutline(Image img, Color c, float px) { var o = img.gameObject.AddComponent<UnityEngine.UI.Outline>(); o.effectColor = c; o.effectDistance = new Vector2(px, px); o.useGraphicAlpha = false; }

        // Korean key를 Text()로 만들고 LocalizedLabel.SetKey()로 언어 변경 자동 구독.
        TMP_Text LocText(RectTransform parent, string korKey, float size, FontWeight weight, Color color, TextAlignmentOptions align)
        {
            var t = Text(parent, korKey, size, weight, color, align);
            if (!string.IsNullOrEmpty(korKey))
                t.gameObject.AddComponent<LocalizedLabel>().SetKey(korKey);
            return t;
        }

        void RefreshTitleLabel()
        {
            if (_titleTmp == null) return;
            if (Loc.CurrentLanguage == LanguageCode.KO)
                _titleTmp.text = "설정<space=14><size=26><cspace=0.02em><color=#8A8A8A>Settings</color></cspace></size>";
            else
                _titleTmp.text = Loc.Get("설정");
        }

        TMP_Text Text(RectTransform parent, string s, float size, FontWeight weight, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(TextObjectName(s), typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (koreanFont) t.font = koreanFont;
            t.text = s; t.fontSize = size; t.color = color; t.alignment = align;
            t.fontStyle = weight >= FontWeight.Bold ? FontStyles.Bold : FontStyles.Normal;
            t.fontWeight = weight;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
            // 이 UI는 목업 수치대로 절대 배치라 상자보다 넉넉한 폭 + Overflow를 전제로 한다.
            // 프로젝트의 TextAutoFit이 개입하면 글자를 강제로 줄여 디자인이 무너지므로 제외시킨다.
            go.AddComponent<TextAutoFitIgnore>();
            t.raycastTarget = false;
            return t;
        }
        // action은 씬에 구웠을 때를 위해 함께 기록해 둔다(코드 실행 중에는 onClick이 우선).
        void AddBtn(GameObject go, Image target, Color n, Color h, Color a, RectTransform scaleT, Action onClick,
                    SettingsAction action = SettingsAction.None, int param = 0)
        {
            var b = go.GetComponent<Btn>(); if (b == null) b = go.AddComponent<Btn>();
            b.action = action; b.actionParam = param;
            b.Init(target, n, h, a, scaleT, 1f, 1f, onClick);
        }
    }
}
