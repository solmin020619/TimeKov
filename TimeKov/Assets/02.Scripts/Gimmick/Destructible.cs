using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ── 파괴형: 여러 번 때리면 부서지는 오브젝트 ─────────────────────────────────────
// 플레이어가 공격으로 부수는 장애물/봉인. 부서지면 연결된 GimmickTarget 을 연다.
//
//   ★핵심: '공격력과 무관하게 타격 횟수'로만 깎인다(한 대 = HP 1). hitsToBreak 대 맞으면 파괴.
//     기존 전투 코드를 안 건드리려고 EnemyHealth 를 재활용한다 —
//       · 공격은 'Enemy' 레이어의 EnemyHealth 만 때리므로, 이 오브젝트를 Enemy 레이어에 둔다.
//       · maxHP 를 아주 크게 잡아 '실제 죽음(=적 처치 이벤트)'은 절대 안 나게 하고,
//         EnemyHealth.OnDamage(타격마다 1회)만 세서 우리 규칙(한 대=1)으로 파괴를 판정한다.
//       → 도감/퀘스트의 '처치 수'가 오염되지 않는다(진짜로 죽지 않으므로).
//
//   HP 바는 코드로 그린다(어두운 파란색, 텍스트 없음, 피격 시 표시). 몬스터 체력바와 무관.
// ★콜라이더는 반드시 이 오브젝트(EnemyHealth 와 같은 GameObject)에 있어야 한다 —
//   공격이 hit.TryGetComponent<EnemyHealth>() 로 '맞은 콜라이더와 같은 오브젝트'의 EnemyHealth 를 찾기 때문.
//   메시가 자식에 있으면 루트에도 콜라이더(박스 등)를 둬야 타격이 인식된다.
[RequireComponent(typeof(EnemyHealth), typeof(Collider))]
public class Destructible : MonoBehaviour
{
    [Header("파괴")]
    [Tooltip("부서지는 데 필요한 타격 횟수(공격력 무관, 한 대 = 1). 보통 5~6.")]
    [Min(1)] [SerializeField] private int hitsToBreak = 5;

    [Header("세이브")]
    [Tooltip("체크: 맞은 횟수를 저장해서 다음에 들어와도 그대로 둔다(부서진 것은 부서진 채로).\n" +
             "해제: 저장하지 않는다 — 들어올 때마다 멀쩡한 상태로 시작한다.")]
    [SerializeField] private bool persistState = true;
    [Tooltip("세이브에서 이 파괴물을 구분하는 id. 비우면 계층 경로로 자동 생성한다(대부분 비워두면 된다).\n" +
             "★오브젝트를 옮기거나 이름을 바꾸면 자동 id 가 바뀌어 초기화된다. 그게 곤란하면 여기에 직접 적는다.")]
    [SerializeField] private string saveId = "";

    [Header("연동 대상 (이 오브젝트만으로 문/다리 열 때)")]
    [Tooltip("부서지면 열 GimmickTarget 들. 여러 파괴물을 묶으려면 여기는 비우고 DestroyTrigger 를 쓴다.")]
    [SerializeField] private List<GimmickTarget> targets = new();

    [Header("연출 (선택)")]
    [Tooltip("한 대 맞을 때 생성할 타격 VFX(비우면 없음).")]
    [SerializeField] private GameObject hitVfxPrefab;
    [Tooltip("부서질 때 생성할 파괴 VFX(잔해/먼지 등, 비우면 없음).")]
    [SerializeField] private GameObject breakVfxPrefab;
    [Tooltip("한 대 맞을 때 재생.")]
    [SerializeField] private SfxId hitSfx = SfxId.None;
    [Tooltip("부서질 때 재생.")]
    [SerializeField] private SfxId breakSfx = SfxId.None;
    [Tooltip("부서진 뒤 오브젝트를 이 시간(초) 뒤 제거(VFX 재생 여유). 0이면 즉시.")]
    [SerializeField] private float destroyDelay = 0.1f;

    [Header("HP 바")]
    [Tooltip("HP 바 채움 색(어두운 파란색 계열).")]
    [SerializeField] private Color barColor = new Color(0.13f, 0.32f, 0.72f, 1f);
    [Tooltip("HP 바 배경 색.")]
    [SerializeField] private Color barBackColor = new Color(0f, 0f, 0f, 0.6f);
    [Tooltip("오브젝트 위로 바를 띄우는 높이(m). 0=자동(렌더러 top + 0.4).")]
    [SerializeField] private float barHeightOverride = 0f;

    public bool IsBroken { get; private set; }
    public event Action<Destructible> OnBroken;

    private EnemyHealth _health;
    private int _hits;

    // 코드로 만든 월드 HP 바
    private GameObject _bar;
    private Image _fill;
    private float _barWorldY;
    private Camera _cam;
    private static Sprite _whiteSprite;
    private const float BarPixelW = 120f, BarPixelH = 14f, BarScale = 0.01f;

    // ── 세이브 ────────────────────────────────────────────────────────────
    // 맞은 횟수를 저장한다. 5대 중 3대를 때려 둔 뒤 나갔다 오면 3대 맞은 채로 돌아온다.
    //   이미 부서졌으면 아예 없던 것처럼 치운다(연출·소리 없이).
    //
    //   ★복원은 Awake 에서. DestroyTrigger 가 Start 에서 IsBroken 을 세어 처음 판정을 하므로
    //     그보다 먼저 확정돼 있어야 한다.
    private string SaveKey => GimmickSave.Key("brk", this, saveId);

    private void Awake()
    {
        if (!persistState) return;
        _hits = Mathf.Clamp(GimmickSave.GetInt(SaveKey), 0, hitsToBreak);
        if (_hits >= hitsToBreak) IsBroken = true;   // 이미 부서짐 — Start 에서 치운다
    }

    private void Start()
    {
        // 이미 부서진 채로 복원됐으면 여기서 끝. 타깃만 열어 두고 형체는 감춘다.
        //   ★instant — 이미 열려 있던 것이라 문이 다시 열리는 연출과 파괴음이 나면 안 된다.
        //   ★Destroy 하지 않는다. DestroyTrigger 가 Start 에서 이 컴포넌트의 IsBroken 을 세어
        //     "몇 개 부서졌는지"를 판정하는데, 지워 버리면 참조가 끊겨 '안 부서진 것'으로 세어
        //     3개 중 2개만 부순 판이 영영 안 열린다. 안 보이고 안 맞게만 만들어 둔다.
        if (IsBroken)
        {
            foreach (var t in targets)
                if (t != null) t.SetOpen(true, instant: true);
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            enabled = false;
            return;
        }

        _health = GetComponent<EnemyHealth>();

        // 공격이 때릴 수 있게 'Enemy' 레이어로. (없으면 경고 — 프로젝트 레이어 확인 필요)
        int el = LayerMask.NameToLayer("Enemy");
        if (el >= 0) gameObject.layer = el;
        else Debug.LogWarning("[Destructible] 'Enemy' 레이어가 없다. 공격이 이 오브젝트를 못 때린다.", this);

        // 실제 죽음(적 처치 이벤트)이 안 나도록 HP 를 아주 크게. 파괴는 타격 '횟수'로만 판정.
        _health.maxHP = 1e9f;
        _health.currentHP = 1e9f;
        _health.OnDamage += OnHit;

        BuildBar();

        // 복원된 타격 수만큼 바를 깎아 둔다(바는 피격 전까지 숨겨져 있어 화면엔 안 보인다).
        if (_hits > 0 && _fill != null)
            _fill.fillAmount = Mathf.Max(0f, hitsToBreak - _hits) / (float)hitsToBreak;
    }

    private void OnDestroy()
    {
        if (_health != null) _health.OnDamage -= OnHit;
    }

    private void OnHit()
    {
        if (IsBroken) return;
        _hits++;

        GameSfx.Play(hitSfx, transform.position);
        if (hitVfxPrefab != null) Instantiate(hitVfxPrefab, BarWorldPos(), Quaternion.identity);

        ShowBar();
        int remain = Mathf.Max(0, hitsToBreak - _hits);
        if (_fill != null) _fill.fillAmount = remain / (float)hitsToBreak;

        // ★한 대마다 파일을 쓰지 않는다(SetDeferred). 세이브 저장은 등록된 모든 시스템을 훑고
        //   파일 두 개를 통째로 다시 쓰는 작업이라, 연타로 때리는 중에 부르면 화면이 끊긴다.
        //   때린 횟수는 잃어도 자원 손해가 없어서 자동저장에 맡겨도 된다.
        if (persistState) GimmickSave.SetDeferred(SaveKey, _hits);
        if (_hits >= hitsToBreak) Break();
    }

    private void Break()
    {
        if (IsBroken) return;
        IsBroken = true;

        // 부서진 건 되돌릴 수 없으니 여기서는 파일까지 확실히 쓴다.
        //   ★DestroyTrigger 없이 targets 를 직접 연결한 배선도 있어서, 트리거의 저장에
        //     기대면 안 된다.
        if (persistState) GimmickSave.Set(SaveKey, _hits);

        GameSfx.Play(breakSfx, transform.position);
        if (breakVfxPrefab != null) Instantiate(breakVfxPrefab, BarWorldPos(), Quaternion.identity);

        foreach (var t in targets)
            if (t != null) t.SetOpen(true);
        OnBroken?.Invoke(this);

        if (_bar != null) _bar.SetActive(false);

        // 더 이상 안 맞게 콜라이더 끄고, 잠깐 뒤 오브젝트 제거(부서짐).
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Destroy(gameObject, Mathf.Max(0f, destroyDelay));
    }

    // ── 월드 HP 바 (코드 생성, 텍스트 없음) ───────────────────────────────────────
    private void BuildBar()
    {
        _cam = Camera.main;

        // 렌더러 top 기준으로 바 높이 산출(1회).
        _barWorldY = barHeightOverride > 0f ? barHeightOverride : ComputeTopGap();

        _bar = new GameObject("BreakHPBar", typeof(RectTransform), typeof(Canvas));
        _bar.transform.SetParent(transform, false);
        var canvas = _bar.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var crt = (RectTransform)_bar.transform;
        crt.sizeDelta = new Vector2(BarPixelW, BarPixelH);
        _bar.transform.localScale = Vector3.one * BarScale;

        MakeImage("BG", barBackColor, Image.Type.Simple);
        _fill = MakeImage("Fill", barColor, Image.Type.Filled);
        _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _fill.fillAmount = 1f;

        _bar.SetActive(false);   // 피격 전에는 숨김
    }

    private Image MakeImage(string n, Color c, Image.Type type)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_bar.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = WhiteSprite();
        img.type = type;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    private void ShowBar()
    {
        if (_bar != null && !_bar.activeSelf) _bar.SetActive(true);
    }

    private void LateUpdate()
    {
        if (_bar == null || !_bar.activeSelf) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        _bar.transform.position = BarWorldPos();
        _bar.transform.forward = _cam.transform.forward;   // 카메라를 향하도록
    }

    private Vector3 BarWorldPos() => transform.position + Vector3.up * _barWorldY;

    private float ComputeTopGap()
    {
        float top = transform.position.y + 1.5f;   // 폴백
        bool has = false; Bounds b = default;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        if (has) top = b.max.y;
        return (top - transform.position.y) + 0.4f;
    }

    private static Sprite WhiteSprite()
    {
        if (_whiteSprite == null)
        {
            var tex = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return _whiteSprite;
    }
}
