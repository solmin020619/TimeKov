// =====================================================================
// QuickSlotIconUI.cs
// 퀵슬롯 한 칸 — 블루 입체 스킨(림/이너/브래킷/틱/자물쇠/설비) 절차 생성.
// 상태: 잠김(회색+자물쇠) / 해금(시안+설비) / 선택(시안 글로우 강). 강한 해금 연출.
// 스킨은 기존 'Quick Slot' 프레임 안에 살짝 inset해서 얹음 → 슬롯 사이 간격 유지.
// 번호(1~8)/레일 E는 프리팹 그대로 슬롯 '아래'에 표시(통일).
// =====================================================================

using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuickSlotIconUI : MonoBehaviour
{
    [Tooltip("(구) 설비 아이콘 Image — 새 스킨에서 비활성화하고 자체 생성한 아이콘을 씀")]
    [SerializeField] private Image iconImage;

    [Tooltip("(구) 슬롯 번호 텍스트 — 슬롯 아래 표시로 유지(레일 E와 통일)")]
    [SerializeField] private TextMeshProUGUI slotNumber;

    // ── 색 ───────────────────────────────────────────────────────────
    static readonly Color Cyan1   = new Color32(111, 210, 255, 255);
    static readonly Color Cyan2   = new Color32( 47, 155, 224, 255);
    static readonly Color CyanHi  = new Color32(159, 230, 255, 255);
    static readonly Color GrayRim = new Color32(110, 124, 140, 255);
    static readonly Color32 NavyTop = new Color32(23, 39, 60, 255);
    static readonly Color32 NavyBot = new Color32(11, 20, 34, 255);

    // ── 상태 ───────────────────────────────────────────────────────────
    private enum SlotState { Locked, Active }
    private SlotState _state = SlotState.Locked;
    private bool _selected;
    private bool _pending;
    private bool _isRail;
    private int  _facilityId;
    private Sprite _facilitySprite;

    // ── 스킨 ───────────────────────────────────────────────────────────
    private bool _built;
    private RectTransform _skin, _fxRoot;
    private Image _glow, _rim, _inner, _highlight, _facility, _lock;
    private Image[] _brackets;
    private Image[] _ticks;
    private float _eff = 92f;   // inset 적용 후 실제 스킨 크기
    private Coroutine _unlockCo;

    private RectTransform SpotlightRect
    {
        get
        {
            var parent = transform.parent;
            if (parent != null)
            {
                var frame = parent.Find("Quick Slot") as RectTransform;
                if (frame != null) return frame;
                return parent as RectTransform;
            }
            return transform as RectTransform;
        }
    }

    private void Awake()
    {
        if (iconImage == null) iconImage = GetComponent<Image>();
    }

    private void Start()
    {
        EnsureBuilt();
        ApplyState();
    }

    // ── Public API ──────────────────────────────────────────────────
    public void SetFacility(int facilityId)
    {
        _facilityId = facilityId;
        _facilitySprite = FacilityIconDatabase.Instance != null ? FacilityIconDatabase.Instance.GetIcon(facilityId) : null;
        _state = SlotState.Active;
        _pending = false;
        EnsureBuilt();
        ApplyState();
        TutorialOverlay.RegisterTarget("quick_slots", SpotlightRect);
    }

    public void Clear()
    {
        _state = SlotState.Locked;
        _pending = false;
        EnsureBuilt();
        ApplyState();
        TutorialOverlay.UnregisterTarget("quick_slots", SpotlightRect);
    }

    // 해금됐지만 다음 건설모드 진입 때 '팡' 연출로 공개 — 그 전까진 잠긴 모습
    public void SetUnlockPending(int facilityId)
    {
        _facilityId = facilityId;
        _facilitySprite = FacilityIconDatabase.Instance != null ? FacilityIconDatabase.Instance.GetIcon(facilityId) : null;
        _pending = true;
        _state = SlotState.Locked;
        EnsureBuilt();
        ApplyState();
    }

    public bool HasPendingUnlock => _pending;

    public void PlayUnlockSequence(float delay = 0f)
    {
        if (!_pending) return;
        EnsureBuilt();
        if (_unlockCo != null) StopCoroutine(_unlockCo);
        ResetAnimTransforms();   // 재진입 시 이전 연출 잔여 트윈/변형 제거 (각도 고정 버그 방지)
        _unlockCo = StartCoroutine(UnlockRoutine(delay));
    }

    public void SetSelected(bool sel)
    {
        if (_selected == sel) return;
        _selected = sel;
        if (_built) ApplyState();
    }

    public void SetSlotNumber(int oneBased)
    {
        if (slotNumber != null) slotNumber.text = oneBased.ToString();
    }

    // 레일 E 슬롯 — 항상 활성, 기존 아이콘(레일) 유지, 번호는 프리팹 'E'를 그대로 둠
    public void SetRail()
    {
        _isRail = true;
        EnsureBuilt();
        _state = SlotState.Active;
        _pending = false;
        ApplyState();
    }

    private void OnDestroy()
    {
        TutorialOverlay.UnregisterTarget("quick_slots", SpotlightRect);
    }

    private void OnEnable()
    {
        // 비활성화 중 마무리된 상태(또는 외부 변경)를 다시 보이게 반영
        if (_built) ApplyState();
    }

    private void OnDisable()
    {
        // 해금 연출 도중 퀵슬롯 바가 꺼지면(빌드모드 B 토글 등) DOTween 트윈이 끊겨
        // _skin이 회전/스케일/위치가 틀어진 채 고정되는 버그 방지.
        // 진행 중이던 연출은 즉시 해금 완료 상태로 스냅한다.
        if (_unlockCo != null)
        {
            StopCoroutine(_unlockCo);
            _unlockCo = null;
            _state = SlotState.Active;
            _pending = false;
        }
        ResetAnimTransforms();
    }

    // 해금 연출이 건드리는 트윈/트랜스폼을 전부 죽이고 기본 상태로 되돌린다.
    private void ResetAnimTransforms()
    {
        if (_skin != null)
        {
            _skin.DOKill();
            _skin.localRotation   = Quaternion.identity;
            _skin.localScale      = Vector3.one;
            _skin.anchoredPosition = Vector2.zero;
        }
        if (_lock != null)
        {
            _lock.transform.DOKill();
            _lock.DOKill();
            _lock.transform.localScale    = Vector3.one;
            _lock.transform.localRotation = Quaternion.identity;
            var c = _lock.color; c.a = 1f; _lock.color = c;
        }
        if (_facility != null)
        {
            _facility.transform.DOKill();
            _facility.transform.localScale = Vector3.one;
        }
    }

    // ── 스킨 생성 ───────────────────────────────────────────────────────
    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;

        var frame = transform.parent != null ? transform.parent.Find("Quick Slot") as RectTransform : null;
        RectTransform host = frame != null ? frame : (transform.parent as RectTransform) ?? (transform as RectTransform);

        // 레일은 기존 아이콘 스프라이트를 캡처해서 그대로 보여줌 (FacilityIconDatabase에 없음)
        if (_isRail && iconImage != null && iconImage.sprite != null)
            _facilitySprite = iconImage.sprite;

        // 구 프레임/아이콘 끄기 (번호는 슬롯 '아래' 표시로 유지 → 끄지 않음)
        if (frame != null) { var fi = frame.GetComponent<Image>(); if (fi != null) fi.enabled = false; }
        if (iconImage != null) iconImage.enabled = false;

        float full = host.rect.width;
        if (full < 1f) full = host.sizeDelta.x;
        if (full < 1f) full = 104f;

        float inset = Mathf.Max(7f, full * 0.085f);   // 슬롯 사이 간격 확보
        _eff = full - inset * 2f;
        float S = _eff;

        // 스킨 루트 — host 안에 inset
        _skin = NewRT("QSSkin", host);
        Stretch(_skin, inset, inset, inset, inset);

        // 선택 글로우 (뒤)
        _glow = NewImg("Glow", _skin, UISpriteFactory.Disc(96));
        Stretch(_glow.rectTransform, -S * 0.18f, -S * 0.18f, -S * 0.18f, -S * 0.18f);
        _glow.color = new Color(Cyan1.r, Cyan1.g, Cyan1.b, 0f);

        // 림(테두리)
        _rim = NewImg("Rim", _skin, UISpriteFactory.RoundedRect(64, 16));
        Stretch(_rim.rectTransform, 0, 0, 0, 0);

        // 이너(다크 그라디언트)
        _inner = NewImg("Inner", _skin, UISpriteFactory.RoundedRectVGrad(NavyTop, NavyBot, 64, 14));
        Stretch(_inner.rectTransform, 2, 2, 2, 2);

        // 상단 하이라이트(베벨)
        _highlight = NewImg("Highlight", _skin, UISpriteFactory.RoundedRect(64, 14));
        var hr = _highlight.rectTransform;
        hr.anchorMin = new Vector2(0, 1); hr.anchorMax = new Vector2(1, 1); hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(-8, S * 0.34f); hr.anchoredPosition = new Vector2(0, -3);
        _highlight.color = new Color(CyanHi.r, CyanHi.g, CyanHi.b, 0.12f);

        // 설비 아이콘 (슬롯의 ~80% 차게)
        _facility = NewImg("Facility", _skin, null);
        Center(_facility.rectTransform, S * 0.8f, S * 0.8f);
        _facility.preserveAspect = true;

        // 자물쇠
        _lock = NewImg("Lock", _skin, UISpriteFactory.Lock(96));
        Center(_lock.rectTransform, S * 0.5f, S * 0.5f);

        // 하단 틱 (시간 모티프)
        _ticks = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            var t = NewImg($"Tick{i}", _skin, null);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(2f, (i == 2) ? 6f : 4f);
            rt.anchoredPosition = new Vector2((i - 2) * 5f, S * 0.07f);
            _ticks[i] = t;
        }

        // 코너 브래킷
        _brackets = new Image[8];
        float bl = S * 0.16f, bt = 2f, off = S * 0.06f;
        BuildBracket(0, 0, 1, off, bl, bt); // TL
        BuildBracket(2, 1, 1, off, bl, bt); // TR
        BuildBracket(4, 0, 0, off, bl, bt); // BL
        BuildBracket(6, 1, 0, off, bl, bt); // BR

        // FX 루트 (최상단)
        _fxRoot = NewRT("FX", _skin); Stretch(_fxRoot, 0, 0, 0, 0);
        _fxRoot.SetAsLastSibling();
    }

    private void BuildBracket(int idx, int xCorner, int yCorner, float off, float len, float thick)
    {
        var h = NewImg("BrkH", _skin, null);
        var hr = h.rectTransform;
        hr.anchorMin = hr.anchorMax = new Vector2(xCorner, yCorner); hr.pivot = new Vector2(xCorner, yCorner);
        hr.sizeDelta = new Vector2(len, thick);
        hr.anchoredPosition = new Vector2(xCorner == 0 ? off : -off, yCorner == 0 ? off : -off);
        _brackets[idx] = h;

        var v = NewImg("BrkV", _skin, null);
        var vr = v.rectTransform;
        vr.anchorMin = vr.anchorMax = new Vector2(xCorner, yCorner); vr.pivot = new Vector2(xCorner, yCorner);
        vr.sizeDelta = new Vector2(thick, len);
        vr.anchoredPosition = new Vector2(xCorner == 0 ? off : -off, yCorner == 0 ? off : -off);
        _brackets[idx + 1] = v;
    }

    // ── 상태 반영 ───────────────────────────────────────────────────────
    private void ApplyState()
    {
        if (!_built) return;
        bool locked = _state == SlotState.Locked;

        _rim.color = locked ? GrayRim : (_selected ? CyanHi : Cyan2);
        _inner.color = locked ? new Color(0.55f, 0.58f, 0.62f, 1f) : Color.white;
        _highlight.color = new Color(CyanHi.r, CyanHi.g, CyanHi.b, locked ? 0.05f : 0.13f);

        Color brk = locked ? new Color(0.6f, 0.64f, 0.69f, 0.5f) : (_selected ? CyanHi : Cyan1);
        if (_brackets != null) foreach (var b in _brackets) if (b != null) b.color = brk;

        Color tick = new Color(Cyan1.r, Cyan1.g, Cyan1.b, locked ? 0.25f : 0.55f);
        if (_ticks != null) foreach (var t in _ticks) if (t != null) t.color = tick;

        bool showLock = locked;
        _lock.enabled = showLock;
        _lock.color = Color.white;
        _facility.enabled = !showLock && _facilitySprite != null;
        _facility.sprite = _facilitySprite;

        float ga = (_selected && !locked) ? 0.75f : 0f;
        DOTween.Kill(_glow);
        _glow.DOFade(ga, 0.15f).SetUpdate(true);
    }

    // ── 해금 연출 ───────────────────────────────────────────────────────
    private IEnumerator UnlockRoutine(float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        _skin.DOShakeAnchorPos(0.36f, new Vector2(7f, 3f), 20, 90, false, true).SetUpdate(true);
        _skin.DOShakeRotation(0.36f, new Vector3(0, 0, 10f), 20, 90).SetUpdate(true);
        yield return new WaitForSecondsRealtime(0.4f);

        _rim.color = CyanHi;
        _skin.DOPunchScale(Vector3.one * 0.18f, 0.5f, 6, 0.6f).SetUpdate(true);
        PlayUnlockFx();
        if (_lock != null)
        {
            _lock.transform.DOScale(1.6f, 0.3f).SetUpdate(true);
            _lock.transform.DORotate(new Vector3(0, 0, 16f), 0.3f).SetUpdate(true);
            _lock.DOFade(0f, 0.3f).SetUpdate(true);
        }
        yield return new WaitForSecondsRealtime(0.26f);

        _state = SlotState.Active;
        _pending = false;
        ApplyState();
        if (_facility != null && _facilitySprite != null)
        {
            _facility.transform.localScale = Vector3.one * 1.9f;
            _facility.transform.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetUpdate(true);
        }
        TutorialOverlay.RegisterTarget("quick_slots", SpotlightRect);
        yield return new WaitForSecondsRealtime(0.45f);

        if (_lock != null)
        {
            _lock.transform.localScale = Vector3.one;
            _lock.transform.localRotation = Quaternion.identity;
            var c = _lock.color; c.a = 1f; _lock.color = c;
        }
        _unlockCo = null;
    }

    private void PlayUnlockFx()
    {
        if (_fxRoot == null) return;
        float S = _eff;

        var flash = NewImg("Flash", _fxRoot, UISpriteFactory.Disc(96));
        Center(flash.rectTransform, S * 0.2f, S * 0.2f);
        flash.color = CyanHi;
        flash.rectTransform.DOSizeDelta(new Vector2(S * 1.7f, S * 1.7f), 0.5f).SetUpdate(true).SetEase(Ease.OutCubic);
        flash.DOFade(0f, 0.5f).SetUpdate(true).OnComplete(() => { if (flash) Destroy(flash.gameObject); });

        var ring = NewImg("Ring", _fxRoot, UISpriteFactory.Ring(96, 5f));
        Center(ring.rectTransform, S * 0.25f, S * 0.25f);
        ring.color = CyanHi;
        ring.rectTransform.DOScale(3.4f, 0.55f).SetUpdate(true).SetEase(Ease.OutCubic);
        ring.DOFade(0f, 0.55f).SetUpdate(true).OnComplete(() => { if (ring) Destroy(ring.gameObject); });

        for (int i = 0; i < 10; i++)
        {
            float a = (i / 10f) * Mathf.PI * 2f;
            float r = S * (0.42f + (i % 2) * 0.14f);
            var sp = NewImg($"Spark{i}", _fxRoot, UISpriteFactory.Disc(32));
            Center(sp.rectTransform, 5f, 5f);
            sp.color = CyanHi;
            sp.rectTransform.DOAnchorPos(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r), 0.55f).SetUpdate(true).SetEase(Ease.OutCubic);
            sp.rectTransform.DOScale(0.4f, 0.55f).SetUpdate(true);
            sp.DOFade(0f, 0.55f).SetUpdate(true).OnComplete(() => { if (sp) Destroy(sp.gameObject); });
        }
    }

    // ── 헬퍼 ───────────────────────────────────────────────────────────
    private static RectTransform NewRT(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        return rt;
    }

    private static Image NewImg(string name, Transform parent, Sprite sprite)
    {
        var rt = NewRT(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        return img;
    }

    private static void Stretch(RectTransform rt, float l, float t, float r, float b)
    {
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
    }

    private static void Center(RectTransform rt, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
    }
}
