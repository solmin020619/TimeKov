using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 명조(Wuthering Waves)식 가로 5칸 스킬바. 왼쪽부터 [퀵슬롯][대쉬][Q][E][R].
// 쿨다운 = 테두리 시계방향 색 링 + (스킬만) 남은 초 숫자. 대쉬 = 숫자 없이 깜빡임, 퀵슬롯 = 개수.
// PlayerHudUI가 런타임 생성한다. 디자인 스프라이트 = Resources/SkillBar, 없으면 절차 생성 폴백.
public class SkillBarUI : MonoBehaviour
{
    enum Kind { Skill, Dash, Quick }

    class Slot
    {
        public Kind kind;
        public SkillSheetId skillId;
        public RectTransform circle;
        public Image icon, ring, flash;
        public TMP_Text seconds, count;
        public Image countBadge;
        public int shownItemId = int.MinValue;
        public bool wasReady = true;   // 직전 프레임 준비완료 여부 (쿨 완료 순간 감지용)
        public TMP_Text keyLabel;          // 슬롯 아래 키 텍스트 ("Q"/"E"/"R"/"V"/"RMB" 등)
        public System.Func<KeyCode> liveKey; // 설정에서 리바인딩된 실제 키를 매 갱신마다 다시 읽기 위한 getter
    }

    private Player _player;
    private readonly List<Slot> _slots = new();
    private readonly List<(string id, RectTransform rt)> _spotlights = new();
    private TMP_FontAsset _font;   // HUD 폰트 (PlayerHudUI에서 넘겨받아 통일)
    private Slot _quickSlot;
    private Slot _dashSlot;
    private Coroutine _flashCo;
    private Sprite _quickEmptyIcon;   // 빈 퀵슬롯에 흐릿하게 깔 소모품 표시 아이콘

    // 대쉬 잠금/해금 연출. 0강=자물쇠+쇠사슬 오버레이, 1강 되는 순간 흔들 -> 팡 풀림 + 말풍선.
    private RectTransform _dashLockOverlay;
    private bool _dashUnlockPending;   // 해금 이벤트 수신 -> 막는 UI 닫힌 순간 연출 발동(트리거)
    private bool _dashUnlocking;       // 풀림 연출 진행 중(중복/재표시 방지)
    private bool _registerFlashPending;   // 퀵슬롯 등록 수신 -> 인벤 닫혀 바 보이는 순간 칸 강조(트리거)
    private bool _returnStoneRevealPending;   // 귀환석 해금/상승 수신 -> 막는 UI 닫힌 순간 바를 띄움(대쉬와 동일)

    // 표시/숨김 자가 관리(외부 페이더 의존 X). HUD와 동일 규칙: state==None일 때만 표시,
    // 결계 안에선 전투/대쉬/퀵슬롯/피격 시 잠깐 떴다가 다시 숨김, 결계 밖이면 항상.
    private CanvasGroup _cg;
    private float _showTimer;
    private float _lastHp = float.NaN;
    private float _lastMaxHp = float.NaN;
    private const float ShowHold = 3.5f;

    private static readonly Color SkillRing = new Color32(80, 200, 235, 255);
    private static readonly Color DashRing  = new Color32(255, 168, 56, 255);   // 앰버 - 스킬 시안 림과 대비되게(돌아가는 게 잘 보이도록)
    private static readonly Color QuickTint = new Color32(150, 232, 188, 255);   // 퀵슬롯 프레임만 살짝 초록(소모품 구분)
    private static readonly Color DashIcon  = new Color32(190, 205, 220, 255);
    private static readonly Color TextMain  = new Color32(232, 240, 247, 255);

    private const float SkillSize = 90f;
    private const float UtilSize  = 78f;

    private static Sprite Load(string n) => Resources.Load<Sprite>("SkillBar/" + n);

    public void Setup(RectTransform canvasParent, Player player, Sprite[] skillIcons, TMP_FontAsset font)
    {
        _player = player;
        _font = font;

        var frameSp = Load("slot_frame_circle");
        var ringSp  = Load("cooldown_ring");   // 디자인 링 사용(절차 링은 모양이 어색해서 되돌림)
        var countBadge = Load("count_badge");
        var keyBadge   = Load("key_badge");
        var dashIcon   = Load("Skill_Icon_Dash");
        _quickEmptyIcon = Load("item_marker");   // 빈 퀵슬롯 placeholder(소모품 표시). 없으면 빈칸.

        var rt = (RectTransform)transform;
        rt.SetParent(canvasParent, false);

        _cg = gameObject.AddComponent<CanvasGroup>();
        _cg.blocksRaycasts = false;   // 표시 전용 - 클릭 가로채지 않음
        _cg.interactable = false;
        _showTimer = ShowHold;        // 시작은 보이게

        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-44f, 46f);   // 우하단. 위치는 인스펙터/씬에서 미세조정 가능.

        var hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 18f;
        hlg.childAlignment = TextAnchor.LowerCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        var fit = gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 왼쪽부터: 퀵슬롯, 대쉬, Q, E, R
        // 라벨은 리바인딩 직후에도 맞아야 하므로 고정 문자열이 아니라 KeyBindings를 직접 읽는 getter로 채운다.
        BuildSlot(Kind.Quick, default, UtilSize, frameSp, ringSp, null, countBadge, keyBadge, () => KeyBindings.QuickSlot, QuickTint, null);
        BuildSlot(Kind.Dash,  default, UtilSize, frameSp, ringSp, dashIcon, null, keyBadge, () => KeyBindings.Dash, Color.white, null);
        BuildSlot(Kind.Skill, SkillSheetId.Skill1, SkillSize, frameSp, ringSp, Pick(skillIcons, 0), null, keyBadge, () => KeyBindings.Skill1, Color.white, "skill_q");
        // E/R 스왑: PlayerSkillComponent.Update()에서 Skill2Pressed(KeyBindings.Skill2)가 SkillSheetId.Skill3을,
        // Skill3Pressed(KeyBindings.Skill3)가 SkillSheetId.Skill2(궁극기)를 실행하도록 디스패치가 교차되어 있다.
        // 라벨도 "실제로 눌러야 하는 키"를 보여줘야 하므로 liveKey를 디스패치와 똑같이 교차해서 읽는다.
        BuildSlot(Kind.Skill, SkillSheetId.Skill3, SkillSize, frameSp, ringSp, Pick(skillIcons, 2), null, keyBadge, () => KeyBindings.Skill2, Color.white, "skill_e");
        BuildSlot(Kind.Skill, SkillSheetId.Skill2, SkillSize, frameSp, ringSp, Pick(skillIcons, 1), null, keyBadge, () => KeyBindings.Skill3, Color.white, "skill_r");

        _quickSlot = _slots.Find(s => s.kind == Kind.Quick);
        if (player.QuickSlot != null)
        {
            player.QuickSlot.OnUsed += HandleQuickUsed;
            player.QuickSlot.OnChanged += HandleQuickChanged;   // 등록/해제/재등록 시 바를 잠깐 띄움
            GameEvents.OnQuickSlotRegistered += HandleQuickRegistered;   // 등록 순간 -> 인벤 닫혀 바 보일 때 칸 강조
        }

        _dashSlot = _slots.Find(s => s.kind == Kind.Dash);
        PlayerDashComponent.OnDashUnlocked += HandleDashUnlocked;   // 코어 1강 대쉬 해금 순간 아이콘 팝
        PlayerDashComponent.OnDashBlocked += HandleDashBlocked;     // 잠긴 대쉬 시도 시 바 띄우고 잠금 덜컹
        GameEvents.OnReturnStoneChanged += HandleReturnStoneChanged;   // 귀환석 해금/상승 순간 바를 띄워 변화 인지

        GlobalSettingsManager.OnKeyBindingsChanged += RefreshKeyLabels;   // 설정창에서 "설정 적용" 누른 직후 라벨 갱신
    }

    // 설정에서 리바인딩 적용 시 호출 — 5개 슬롯 키 라벨을 KeyBindings 최신 값으로 다시 그림.
    void RefreshKeyLabels()
    {
        foreach (var s in _slots)
            if (s.keyLabel != null && s.liveKey != null)
                s.keyLabel.text = KeyLabel(s.liveKey());
    }

    static Sprite Pick(Sprite[] arr, int i) => (arr != null && i < arr.Length) ? arr[i] : null;

    void BuildSlot(Kind kind, SkillSheetId skillId, float size, Sprite frameSp, Sprite ringSp,
                   Sprite iconSp, Sprite countBadge, Sprite keyBadge, System.Func<KeyCode> liveKey, Color frameColor,
                   string spotlightId)
    {
        float colH = size + 12f + 22f;   // 원 + 간격 + 키 배지

        var colRt = NewChild(kind + "Slot", (RectTransform)transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, colH));
        var le = colRt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = size; le.minWidth = size;
        le.preferredHeight = colH; le.minHeight = colH;

        // 원형 슬롯 (프레임 = 이 오브젝트의 Image)
        var circle = NewChild("Circle", colRt, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(size, size));
        var frame = circle.gameObject.AddComponent<Image>();
        frame.sprite = frameSp;
        frame.type = Image.Type.Simple;
        frame.preserveAspect = true;
        frame.color = frameColor;
        frame.raycastTarget = false;

        // 아이콘. 스킬 아트는 꽉 차서 0.58로 충분하지만, 퀵슬롯(소모품)/대쉬 아이콘은 스프라이트
        // 내부 여백이 커서 같은 비율이면 작아 보인다 -> 더 키워 스킬과 시각 크기를 맞춤.
        float iconScale = (kind == Kind.Skill) ? 0.58f : 0.82f;
        var icon = NewImage("Icon", circle, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * iconScale, size * iconScale));
        icon.preserveAspect = true;
        if (iconSp != null)
        {
            icon.sprite = iconSp;
            if (kind == Kind.Dash) icon.color = DashIcon;
        }
        else icon.enabled = false;

        // 쿨다운 링 (스킬/대쉬만)
        Image ring = null;
        TMP_Text seconds = null;
        if (kind != Kind.Quick)
        {
            ring = NewImage("Ring", circle, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            ring.sprite = ringSp;
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 1f;
            ring.color = (kind == Kind.Dash) ? DashRing : SkillRing;

            if (kind == Kind.Skill)
            {
                seconds = NewText("Sec", circle, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), size * 0.4f);
                seconds.fontStyle = FontStyles.Bold;
                seconds.text = "";
            }
        }

        // 개수 배지 + 사용 플래시 (퀵슬롯만)
        TMP_Text count = null;
        Image countBadgeImg = null;
        Image flashImg = null;
        if (kind == Kind.Quick)
        {
            if (countBadge != null)
            {
                countBadgeImg = NewImage("CountBadge", circle, new Vector2(1f, 0f), new Vector2(5f, -3f), new Vector2(34f, 22f));
                countBadgeImg.sprite = countBadge;
            }
            count = NewText("Count", circle, new Vector2(1f, 0f), new Vector2(5f, -3f), new Vector2(36f, 22f), 16f);
            count.fontStyle = FontStyles.Bold;

            // 사용 시 번쩍이는 오버레이(맨 위, 평소 투명)
            flashImg = NewImage("Flash", circle, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            flashImg.sprite = UISpriteFactory.Disc(128);
            flashImg.color = new Color(1f, 1f, 1f, 0f);
        }

        // 키 배지 (원 아래)
        if (keyBadge != null)
            NewImage("KeyBadge", colRt, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(48f, 22f)).sprite = keyBadge;
        var keyT = NewText("Key", colRt, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(50f, 22f), 14f);
        keyT.text = KeyLabel(liveKey());

        if (!string.IsNullOrEmpty(spotlightId))
        {
            TutorialOverlay.RegisterTarget(spotlightId, circle);
            _spotlights.Add((spotlightId, circle));
        }

        // 인트로 투어에서 스킬 3칸(Q/E/R)을 한 번에 강조하기 위한 합집합 타깃.
        if (kind == Kind.Skill)
        {
            TutorialOverlay.RegisterTarget("skills_qer", circle);
            _spotlights.Add(("skills_qer", circle));
        }

        if (kind == Kind.Dash) BuildDashLockOverlay(circle, size);

        _slots.Add(new Slot { kind = kind, skillId = skillId, circle = circle, icon = icon, ring = ring, flash = flashImg, seconds = seconds, count = count, countBadge = countBadgeImg, keyLabel = keyT, liveKey = liveKey });
    }

    // 대쉬 슬롯 위에 얹는 잠금 오버레이 — 어두운 디스크 + 쇠사슬 X자 + 자물쇠. 해금되면 코루틴이 팡 풀고 끈다.
    void BuildDashLockOverlay(RectTransform circle, float size)
    {
        var ov = NewChild("DashLock", circle, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        ov.SetAsLastSibling();   // 아이콘/링 위에

        var designed = Load("dash_lock_chain");   // 디자인된 잠금 오버레이(자물쇠+쇠사슬 PNG). 있으면 통째 교체.
        if (designed != null)
        {
            var img = ov.gameObject.AddComponent<Image>();
            img.sprite = designed;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }
        else
        {
            // 폴백(절차 생성) — Resources/SkillBar/dash_lock PNG 들어오면 위 분기가 대체.
            var dim = NewImage("Dim", ov, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.96f, size * 0.96f));
            dim.sprite = UISpriteFactory.Circle(96);
            dim.color = new Color(0.04f, 0.05f, 0.07f, 0.5f);

            // 쇠사슬 2개 X자(금속 그라디언트 바)
            Color32 chainTop = new Color32(158, 166, 176, 255);
            Color32 chainBot = new Color32(74, 80, 88, 255);
            for (int i = 0; i < 2; i++)
            {
                var ch = NewImage("Chain" + i, ov, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.18f, size * 0.22f));
                ch.sprite = UISpriteFactory.RoundedRectVGrad(chainTop, chainBot, 48, 10);
                ch.type = Image.Type.Simple;
                ch.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 34f : -34f);
            }

            // 자물쇠(중앙)
            var lk = NewImage("Lock", ov, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(size * 0.5f, size * 0.5f));
            lk.sprite = UISpriteFactory.Lock(96);
            lk.color = new Color32(236, 233, 227, 255);
            lk.preserveAspect = true;
        }

        ov.gameObject.SetActive(!PlayerDashComponent.IsDashUnlocked);
        _dashLockOverlay = ov;
    }

    private void Update()
    {
        if (_player == null) return;
        UpdateVisibility();
        for (int i = 0; i < _slots.Count; i++) UpdateSlot(_slots[i]);

        // 대쉬 해금 연출 — 막는 UI(코어 패널 등) 없는 순간 바를 띄우며 발동. 코어 패널 닫힌 직후 인지.
        if (_dashUnlockPending && !_dashUnlocking)
        {
            var gui = GameUIController.Instance;
            bool anyUI = gui != null && gui.GetCurrentState() != GameUIController.UIState.None;
            if (!anyUI)
            {
                _dashUnlockPending = false;
                _showTimer = Mathf.Max(_showTimer, 5f);   // 말풍선 동안 바 유지
                StartCoroutine(DashUnlockCelebration());
            }
        }

        // 귀환석 해금/상승 — 사용뿐 아니라 '해금'도 변화라, 막는 UI(전송 패널 등) 닫힌 순간 바를 띄워 보여준다.
        if (_returnStoneRevealPending)
        {
            var guiR = GameUIController.Instance;
            bool anyUIR = guiR != null && guiR.GetCurrentState() != GameUIController.UIState.None;
            if (!anyUIR)
            {
                _returnStoneRevealPending = false;
                _showTimer = Mathf.Max(_showTimer, 5f);
            }
        }

        // 퀵슬롯 등록 직후 — 인벤 닫혀 바가 보이는 순간 칸을 한 번 강조(등록됐음을 인지).
        if (_registerFlashPending)
        {
            var gui2 = GameUIController.Instance;
            bool anyUI2 = gui2 != null && gui2.GetCurrentState() != GameUIController.UIState.None;
            if (!anyUI2)
            {
                _registerFlashPending = false;
                _showTimer = Mathf.Max(_showTimer, ShowHold);   // 강조 동안 바 유지
                if (_quickSlot != null && _quickSlot.flash != null)
                {
                    if (_flashCo != null) StopCoroutine(_flashCo);
                    _flashCo = StartCoroutine(FlashRoutine(_quickSlot));
                }
            }
        }
    }

    // 플레이어 HUD와 동일하게: 다른 UI(인벤/공장/건축/퀘스트/설정/코어/상자)가 열리면 숨김.
    // state==None(평소 게임플레이)에선 결계 밖이면 항상, 결계 안이면 전투/대쉬/퀵슬롯/피격 때만 잠깐 표시.
    void UpdateVisibility()
    {
        if (_cg == null) return;

        var gui = GameUIController.Instance;
        bool anyUI = gui != null && gui.GetCurrentState() != GameUIController.UIState.None;

        if (_player.Skill != null && _player.Skill.IsExecuting) _showTimer = ShowHold;
        if (_player.Dash != null && _player.Dash.IsDashing) _showTimer = ShowHold;
        if (_player.Stat != null && (_player.Stat.CurrentHp != _lastHp || _player.Stat.MaxHp != _lastMaxHp))
        {
            _showTimer = ShowHold;
            _lastHp = _player.Stat.CurrentHp;
            _lastMaxHp = _player.Stat.MaxHp;
        }
        if (_showTimer > 0f) _showTimer -= Time.deltaTime;

        bool outside = _player.Stat == null || !_player.Stat.IsInBase;
        bool coachmark = TutorialOverlay.HasInstance && TutorialOverlay.I.IsActive;
        bool show = !anyUI && !DeathOverlayUI.IsOpen && (outside || coachmark || _showTimer > 0f);

        // 페이드 없이 즉시 on/off (화면전환 느낌 - 딱딱). 부드러운 페이드는 의도적으로 안 씀.
        _cg.alpha = show ? 1f : 0f;
    }

    void UpdateSlot(Slot s)
    {
        if (s.kind == Kind.Skill)
        {
            if (_player.Skill == null) return;
            float readiness = Mathf.Clamp01(_player.Skill.GetGauge(s.skillId) / 100f);
            bool ready = readiness >= 0.999f;
            if (ready && !s.wasReady) _showTimer = ShowHold;   // 쿨다운 완료 순간 띄움(모션<쿨 스킬도 READY 알림)
            s.wasReady = ready;
            if (s.ring != null) s.ring.fillAmount = readiness;
            if (s.icon != null && s.icon.enabled) SetAlpha(s.icon, ready ? 1f : 0.22f);
            if (s.seconds != null)
            {
                float rem = _player.Skill.GetCooldown(s.skillId);
                s.seconds.text = rem > 0.05f ? Mathf.CeilToInt(rem).ToString() : "";
            }
        }
        else if (s.kind == Kind.Dash)
        {
            if (_player.Dash == null) return;

            // 코어 0강 = 우클릭 대쉬 잠김: 자물쇠+쇠사슬 오버레이로 표시(회색처리 대신). 아이콘은 살짝만 어둡게.
            if (!PlayerDashComponent.IsDashUnlocked)
            {
                if (_dashLockOverlay != null && !_dashUnlocking && !_dashLockOverlay.gameObject.activeSelf)
                    _dashLockOverlay.gameObject.SetActive(true);
                if (s.ring != null) { s.ring.color = new Color(0.5f, 0.52f, 0.56f, 0.5f); s.ring.fillAmount = 1f; }
                if (s.icon != null && s.icon.enabled) s.icon.color = new Color(0.62f, 0.64f, 0.68f, 0.85f);
                return;
            }

            float max = _player.Dash.MaxCooldown;
            float rem = _player.Dash.CooldownRemaining;
            float readiness = max > 0f ? Mathf.Clamp01(1f - rem / max) : 1f;
            bool ready = !_player.Dash.IsOnCooldown && !_player.Dash.IsDashing;
            if (ready && !s.wasReady) _showTimer = ShowHold;   // 대쉬 쿨 완료 순간(쿨>표시시간으로 커져도 안전)
            s.wasReady = ready;
            if (s.ring != null) { s.ring.color = DashRing; s.ring.fillAmount = readiness; }   // 해금 후 색 복구
            if (s.icon != null && s.icon.enabled) { Color c = DashIcon; c.a = ready ? 1f : 0.4f; s.icon.color = c; }
        }
        else // Quick
        {
            var qs = _player.QuickSlot;
            if (qs == null) return;
            int id = qs.RegisteredItemId;
            int cnt = qs.Count;
            bool has = id > 0;

            if (id != s.shownItemId)
            {
                s.shownItemId = id;
                Sprite sp;
                if (has)
                {
                    var data = ItemDatabase.GetItem(id);
                    sp = data != null ? ItemDatabase.GetIcon(data.iconKey) : null;
                }
                else sp = _quickEmptyIcon;   // 빈 슬롯 = 소모품 표시 placeholder
                if (s.icon != null) { s.icon.sprite = sp; s.icon.enabled = sp != null; }
            }

            if (s.icon != null && s.icon.enabled)
            {
                if (!has) s.icon.color = new Color(0.78f, 0.95f, 0.84f, 0.32f);   // 빈칸: 소모품 톤으로 흐릿하게(힌트)
                else s.icon.color = new Color(1f, 1f, 1f, cnt > 0 ? 1f : 0.4f);
            }
            if (s.countBadge != null) s.countBadge.enabled = has;   // 빈 슬롯엔 개수 배지 숨김
            if (s.count != null)
            {
                s.count.text = has ? cnt.ToString() : "";
                s.count.color = cnt > 0 ? TextMain : new Color32(220, 110, 110, 255);
            }
        }
    }

    private void OnDestroy()
    {
        if (_player != null && _player.QuickSlot != null)
        {
            _player.QuickSlot.OnUsed -= HandleQuickUsed;
            _player.QuickSlot.OnChanged -= HandleQuickChanged;
        }
        GameEvents.OnQuickSlotRegistered -= HandleQuickRegistered;   // 정적 이벤트 누수 방지(조건 밖에서 항상 해제)
        PlayerDashComponent.OnDashUnlocked -= HandleDashUnlocked;
        PlayerDashComponent.OnDashBlocked -= HandleDashBlocked;
        GameEvents.OnReturnStoneChanged -= HandleReturnStoneChanged;
        GlobalSettingsManager.OnKeyBindingsChanged -= RefreshKeyLabels;
        foreach (var (id, rt) in _spotlights) TutorialOverlay.UnregisterTarget(id, rt);
        _spotlights.Clear();
    }

    // 퀵슬롯 사용 성공 시 슬롯에 흰 빛 번쩍(숫자 안 봐도 "썼다"가 보이게).
    void HandleQuickUsed()
    {
        _showTimer = ShowHold;   // V로 썼을 때 바가 페이드 중이면 다시 띄움
        if (_quickSlot == null || _quickSlot.flash == null) return;
        if (_flashCo != null) StopCoroutine(_flashCo);
        _flashCo = StartCoroutine(FlashRoutine(_quickSlot));
    }

    // 퀵슬롯 등록/해제 등 내용 변경 시 바를 잠깐 띄움(아이콘/배지 변화를 보이게).
    void HandleQuickChanged() => _showTimer = ShowHold;

    // 퀵슬롯 등록 순간 — 이때는 인벤이 열려 바가 숨겨져 있으니 즉시 연출 안 함.
    // 인벤이 닫혀 바가 보이는 순간(Update)에 칸을 한 번 강조한다(등록됐음을 인지).
    void HandleQuickRegistered(int itemId) => _registerFlashPending = true;

    // 코어 1강으로 우클릭 대쉬가 해금되는 순간 — 즉시 연출하지 않고 대기 플래그만 세움.
    // 보통 이 순간 코어 패널이 떠 있어 바가 가려지므로, 패널 닫혀 바가 보일 때 Update가 발동(트리거).
    void HandleDashUnlocked() => _dashUnlockPending = true;
    void HandleReturnStoneChanged(int level) => _returnStoneRevealPending = true;

    // 잠긴 대쉬를 시도한 순간 — 바를 잠깐 띄우고 잠금 오버레이를 덜컹(왜 안 되는지=잠김을 보게).
    void HandleDashBlocked()
    {
        _showTimer = Mathf.Max(_showTimer, 2.5f);
        if (_dashLockOverlay != null && _dashLockOverlay.gameObject.activeSelf && !_dashUnlocking)
            StartCoroutine(LockNudge(_dashLockOverlay));
    }

    IEnumerator LockNudge(RectTransform ov)
    {
        Vector2 basePos = ov.anchoredPosition;
        float t = 0f; const float dur = 0.3f;
        while (t < dur && ov != null)
        {
            t += Time.unscaledDeltaTime;
            float dx = Mathf.Sin(t * 58f) * 4f * (1f - t / dur);
            ov.anchoredPosition = basePos + new Vector2(dx, 0f);
            yield return null;
        }
        if (ov != null) ov.anchoredPosition = basePos;
    }

    // 잠금 풀림 풀세트: 쇠사슬 흔들 -> 팡(버스트 링 + 오버레이 스케일/페이드) -> 아이콘 팝 -> 말풍선.
    IEnumerator DashUnlockCelebration()
    {
        _dashUnlocking = true;
        var circle = _dashSlot != null ? _dashSlot.circle : null;

        if (_dashLockOverlay != null && _dashLockOverlay.gameObject.activeSelf)
        {
            var ov = _dashLockOverlay;

            // 1) 쇠사슬 흔들림(잠깐 덜컹)
            Vector2 basePos = ov.anchoredPosition;
            float t = 0f; const float shakeDur = 0.34f;
            while (t < shakeDur && ov != null)
            {
                t += Time.unscaledDeltaTime;
                float dx = Mathf.Sin(t * 72f) * 5f * (1f - t / shakeDur);
                ov.anchoredPosition = basePos + new Vector2(dx, 0f);
                yield return null;
            }
            if (ov != null) ov.anchoredPosition = basePos;

            // 2) 팡 풀림 — 버스트 링 + 오버레이 스케일업/페이드아웃
            if (circle != null) SpawnBurstRing(circle);
            var cg = ov.GetComponent<CanvasGroup>();
            if (cg == null) cg = ov.gameObject.AddComponent<CanvasGroup>();
            float u = 0f; const float popDur = 0.36f;
            while (u < popDur && ov != null)
            {
                u += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(u / popDur);
                float sc = 1f + 0.5f * k;
                ov.localScale = new Vector3(sc, sc, 1f);
                cg.alpha = 1f - k;
                yield return null;
            }
            if (ov != null)
            {
                ov.gameObject.SetActive(false);
                ov.localScale = Vector3.one;
                cg.alpha = 1f;
            }
        }

        // 3) 대쉬 아이콘 팝(강조)
        if (circle != null) StartCoroutine(DashUnlockPop(circle));

        // 4) 말풍선 안내
        ShowDashUnlockBubble();

        _dashUnlocking = false;
    }

    // 대쉬 슬롯 둘레로 퍼지는 버스트 링(해금 임팩트). 한 번 커지며 사라지고 제거.
    void SpawnBurstRing(RectTransform circle)
    {
        var rt = NewChild("UnlockBurst", circle, new Vector2(0.5f, 0.5f), Vector2.zero, circle.sizeDelta);
        rt.SetAsLastSibling();
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = UISpriteFactory.Ring(96, 5f);
        img.color = DashRing;
        img.raycastTarget = false;
        StartCoroutine(BurstRoutine(img));
    }

    IEnumerator BurstRoutine(Image img)
    {
        var rt = img.rectTransform;
        float t = 0f; const float dur = 0.5f;
        while (t < dur && img != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float sc = 0.6f + 1.8f * k;
            rt.localScale = new Vector3(sc, sc, 1f);
            var c = img.color; c.a = 1f - k; img.color = c;
            yield return null;
        }
        if (img != null) Destroy(img.gameObject);
    }

    // 대쉬 슬롯 위 말풍선 "이제 대쉬를 사용할 수 있습니다!" — 페이드인 -> 유지 -> 페이드아웃 -> 제거.
    void ShowDashUnlockBubble()
    {
        if (_dashSlot == null || _dashSlot.circle == null) return;
        var circle = _dashSlot.circle;

        var old = circle.Find("DashUnlockBubble");
        if (old != null) Destroy(old.gameObject);

        // 슬롯 위로 띄움. pivot 아래중앙 -> 슬롯 상단 기준 위로 자람. 꼬리는 아래 중앙.
        var bubble = NewChild("DashUnlockBubble", circle, new Vector2(0.5f, 1f), new Vector2(0f, 14f), new Vector2(300f, 58f));
        bubble.pivot = new Vector2(0.5f, 0f);
        var cg = bubble.gameObject.AddComponent<CanvasGroup>();

        var bg = bubble.gameObject.AddComponent<Image>();
        bg.sprite = UISpriteFactory.RoundedRect(64, 18);
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.10f, 0.13f, 0.17f, 0.96f);
        bg.raycastTarget = false;
        var outline = bubble.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = DashRing;
        outline.effectDistance = new Vector2(2f, -2f);

        // 꼬리(tail)는 절차 생성으론 테두리가 안 따라붙어 어두운 자국처럼 보여 제거. 깔끔한 꼬리는 디자인 스프라이트로 대체 예정.
        var msg = NewText("Msg", bubble, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(282f, 50f), 17f);
        msg.text = Loc.Get("이제 대쉬를 사용할 수 있습니다!");
        msg.fontStyle = FontStyles.Bold;
        msg.color = TextMain;

        StartCoroutine(BubbleRoutine(bubble, cg));
    }

    IEnumerator BubbleRoutine(RectTransform bubble, CanvasGroup cg)
    {
        cg.alpha = 0f;
        bubble.localScale = new Vector3(0.85f, 0.85f, 1f);

        float t = 0f; const float inDur = 0.25f;
        while (t < inDur && bubble != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / inDur);
            cg.alpha = k;
            float s = 0.85f + 0.15f * k;
            bubble.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (bubble != null) { cg.alpha = 1f; bubble.localScale = Vector3.one; }

        yield return new WaitForSecondsRealtime(3.2f);

        t = 0f; const float outDur = 0.5f;
        while (t < outDur && bubble != null)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = 1f - Mathf.Clamp01(t / outDur);
            yield return null;
        }
        if (bubble != null) Destroy(bubble.gameObject);
    }

    IEnumerator DashUnlockPop(RectTransform rt)
    {
        float t = 0f; const float dur = 0.55f;
        while (t < dur && rt != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float scale = 1f + 0.4f * Mathf.Sin(k * Mathf.PI);   // 1 -> 1.4 -> 1 한 번 팝
            rt.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    IEnumerator FlashRoutine(Slot s)
    {
        var flash = s.flash;
        if (flash == null) yield break;
        var frt = flash.rectTransform;
        float dur = 0.32f, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            frt.localScale = Vector3.one * (1f + 0.5f * k);
            var c = flash.color; c.a = 0.75f * (1f - k); flash.color = c;
            yield return null;
        }
        var cc = flash.color; cc.a = 0f; flash.color = cc;
        frt.localScale = Vector3.one;
        _flashCo = null;
    }

    // ── 생성 헬퍼 ──
    static RectTransform NewChild(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Image NewImage(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = NewChild(name, parent, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    TMP_Text NewText(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize)
    {
        var rt = NewChild(name, parent, anchor, pos, size);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.fontSize = fontSize;
        t.alignment = TextAlignmentOptions.Center;
        t.color = TextMain;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    static void SetAlpha(Graphic g, float a)
    {
        var c = g.color;
        if (!Mathf.Approximately(c.a, a)) { c.a = a; g.color = c; }
    }

    static string KeyLabel(KeyCode key)
    {
        // 마우스 버튼은 좁은 원형 슬롯 아래에 "Mouse0/1/2"로 박히면 너무 길고 안 읽혀서 약어로.
        switch (key)
        {
            case KeyCode.Mouse0: return "LMB";
            case KeyCode.Mouse1: return "RMB";
            case KeyCode.Mouse2: return "MMB";
        }
        string s = key.ToString();
        if (s.StartsWith("Alpha")) s = s.Substring(5);
        return s;
    }
}
