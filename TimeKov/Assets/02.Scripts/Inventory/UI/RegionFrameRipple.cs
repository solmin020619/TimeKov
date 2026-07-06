using UnityEngine;
using UnityEngine.UI;

// 범위 강조 바깥 라인 물결 애니 (엔필 느낌).
// region 경계 밖(reach px)에서 안쪽 고정선(rippleDepth)까지 잔잔하게 수렴하는 다겹 캐스케이드.
// [07-06 v4 설계]
//  - 겹들은 "오버레이"에 그린다 = 최상위 클리퍼(Mask/RectMask2D) 패널의 형제(바로 위 순서) =
//    패널 밖으로 나가도 안 잘리고(엔필 동일), 패널/블러 바로 위에 그려져 블러에 안 먹힘.
//    이 컴포넌트가 붙은 Image 자체는 템플릿으로만 쓰고 렌더는 끈다. 오버레이가 매 프레임 원본
//    rect 를 따라와 패널 슬라이드 중에도 정렬 유지.
//  - 아래쪽은 reachBottom(작게)만 나감 = 하단 삐져나옴 방지, 물결이 위에서 아래로 들어오는 인상.
//  - 알파 = 멀리서 진하고 서서히 연해지되 경계선(안쪽 고정 프레임)에 닿을 때까지 보이고, 닿는 순간 소멸.
//  - 필드 reach/cycle 은 씬 베이크 안 함(코드 기본값 지배 = 튜닝은 코드+Play 만).
[RequireComponent(typeof(Image))]
public class RegionFrameRipple : MonoBehaviour
{
    [Tooltip("안쪽 고정선 위치 = 프레임 경계서 이만큼 안으로(px). 물결의 도착점.")]
    public float rippleDepth = 1f;
    [Tooltip("물결 시작 거리(좌/우/상) = 프레임 밖 이만큼서 출발(px).")]
    public float reach = 34f;
    [Tooltip("아래쪽 시작 거리(px). 하단은 삐져나오면 안 돼서 거의 0.")]
    public float reachBottom = 2f;
    [Tooltip("한 겹이 출발->도착까지 걸리는 시간(초). 잔잔한 호수 느낌 = 길게.")]
    public float cycle = 2.8f;
    [Tooltip("물결 겹 수. 위상 균등 분산 = 간격 두고 연속 수렴. 선 간격 = (reach+rippleDepth)/layers.")]
    public int layers = 6;
    [Tooltip("겹당 알파 배율(여러 겹 겹침 밝기 보정).")]
    [Range(0.2f, 1f)] public float layerAlpha = 0.85f;

    private RectTransform _src;        // 따라갈 원본 rect(= 이 오브젝트의 부모 = region 루트)
    private RectTransform _overlay;    // 루트 캔버스 직속(마스크 밖) 컨테이너
    private RectTransform[] _rts;
    private Image[] _imgs;
    private float _baseAlpha = 1f;
    private float _t;
    private readonly Vector3[] _wc = new Vector3[4];

    private void Awake()
    {
        _src = transform.parent as RectTransform;
        var template = GetComponent<Image>();
        if (template != null) _baseAlpha = template.color.a;

        // 오버레이 = "최상위 클리퍼(Mask/RectMask2D) 패널"의 부모에 형제로 = 마스크 밖(안 잘림)이면서
        // 같은 캔버스 순서 문맥(패널/블러 바로 위에 그림 = 블러에 먹히거나 순서 꼬임 방지).
        // 클리퍼가 없으면 루트 캔버스 폴백.
        Transform topClipper = null;
        for (var t = transform.parent; t != null; t = t.parent)
            if (t.GetComponent<UnityEngine.UI.Mask>() != null || t.GetComponent<RectMask2D>() != null)
                topClipper = t;
        Transform overlayParent;
        if (topClipper != null && topClipper.parent != null) overlayParent = topClipper.parent;
        else
        {
            var canvas = GetComponentInParent<Canvas>();
            overlayParent = canvas != null ? canvas.rootCanvas.transform : transform.parent;
        }
        var og = new GameObject("RippleOverlay", typeof(RectTransform));
        _overlay = (RectTransform)og.transform;
        _overlay.SetParent(overlayParent, false);
        _overlay.SetAsLastSibling();
        _overlay.anchorMin = _overlay.anchorMax = _overlay.pivot = new Vector2(0.5f, 0.5f);

        int n = Mathf.Max(1, layers);
        _rts = new RectTransform[n];
        _imgs = new Image[n];
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("RippleLayer" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_overlay, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            if (template != null)
            {
                img.sprite = template.sprite; img.type = template.type;
                img.pixelsPerUnitMultiplier = template.pixelsPerUnitMultiplier;
                img.color = template.color;
            }
            img.raycastTarget = false;
            _rts[i] = rt; _imgs[i] = img;
        }

        // 자기 Image 는 템플릿 전용(렌더는 오버레이 겹들이 담당)
        if (template != null) template.enabled = false;

        _overlay.gameObject.SetActive(isActiveAndEnabled);
    }

    private void OnEnable()
    {
        _t = 0f;
        if (_overlay != null)
        {
            _overlay.gameObject.SetActive(true);
            _overlay.SetAsLastSibling();   // 켜질 때마다 최상단(중간에 생긴 다른 UI 위로)
            SyncOverlay();
            Drive();
        }
    }

    private void OnDisable()
    {
        if (_overlay != null) _overlay.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_overlay != null) Destroy(_overlay.gameObject);
    }

    private void Update()
    {
        if (cycle <= 0f || _overlay == null) return;
        _t += Time.unscaledDeltaTime;
        SyncOverlay();   // 패널 슬라이드 등 이동 추적
        Drive();
    }

    // 오버레이 rect 를 원본(region 루트) 월드 rect 에 일치시킴(같은 캔버스 스케일 전제).
    private void SyncOverlay()
    {
        if (_src == null || _overlay == null) return;
        _src.GetWorldCorners(_wc);
        _overlay.position = (_wc[0] + _wc[2]) * 0.5f;
        var ps = _overlay.parent != null ? _overlay.parent.lossyScale : Vector3.one;
        _overlay.sizeDelta = new Vector2(
            (_wc[2].x - _wc[0].x) / Mathf.Max(0.0001f, ps.x),
            (_wc[2].y - _wc[0].y) / Mathf.Max(0.0001f, ps.y));
    }

    private void Drive()
    {
        if (_rts == null) return;
        int n = _rts.Length;
        float mul = n > 1 ? layerAlpha : 1f;
        for (int i = 0; i < n; i++)
        {
            if (_rts[i] == null) continue;
            float k = Mathf.Repeat(_t / Mathf.Max(0.01f, cycle) + (float)i / n, 1f);   // 겹마다 위상 균등 분산
            float side = Mathf.Lerp(-reach, rippleDepth, k);          // 좌/우/상: 멀리서 수렴
            float bottom = Mathf.Lerp(-reachBottom, rippleDepth, k);  // 하단: 살짝만(삐져나옴 방지)
            _rts[i].offsetMin = new Vector2(side, bottom);
            _rts[i].offsetMax = new Vector2(-side, -side);
            if (_imgs[i] != null)
            {
                // 멀리서 진하고 들어올수록 서서히 연해지되, 경계선에 "닿을 때까지" 보이다가
                // 닿는 순간(마지막 7%)에 소멸. (이전 1-k 감쇠는 도착 전에 다 죽어서 폐기)
                float a = Mathf.Clamp01(k / 0.10f)
                        * Mathf.Lerp(1f, 0.45f, k)
                        * Mathf.Clamp01((1f - k) / 0.07f);
                var c = _imgs[i].color;
                c.a = _baseAlpha * mul * a;
                _imgs[i].color = c;
            }
        }
    }
}
