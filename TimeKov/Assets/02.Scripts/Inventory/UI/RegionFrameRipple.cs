using UnityEngine;
using UnityEngine.UI;

// 범위 강조 바깥 라인 물결 애니 (엔필 느낌).
// region 경계 밖(reach px)에서 안쪽 고정선(rippleDepth)까지 잔잔하게 수렴하는 다겹 캐스케이드.
// [07-06 v5 설계]
//  - 겹들은 "오버레이"에 그린다 = 최상위 클리퍼(Mask/RectMask2D) 패널의 형제(바로 위 순서) =
//    패널 밖으로 나가도 안 잘리고(엔필 동일), 패널/블러 바로 위에 그려져 블러에 안 먹힘.
//    이 컴포넌트가 붙은 Image 자체는 템플릿으로만 쓰고 렌더는 끈다. 오버레이가 매 프레임 원본
//    rect 를 따라와 패널 슬라이드 중에도 정렬 유지.
//  - "열차" 모델: 촘촘한 선 묶음이 통째로 출발점->강조선까지 쭉 이동, 안쪽 선부터 순서대로
//    닿고 사라지고, 꼬리까지 끝나면 새 묶음 출발. (위상 균등 분산 = 제자리 꿈틀 착시라 폐기)
//  - 아래쪽은 reachBottom(작게)만 나감 = 하단 삐져나옴 방지, 물결이 위에서 아래로 들어오는 인상.
//  - 필드 reach/cycle 은 씬 베이크 안 함(코드 기본값 지배 = 튜닝은 코드+Play 만).
[RequireComponent(typeof(Image))]
public class RegionFrameRipple : MonoBehaviour
{
    // ★전부 [NonSerialized] = 씬/프리팹에 절대 안 박힘. 코드 기본값이 항상 지배(튜닝 = 여기 숫자 + Play).
    //   (직렬화 필드였을 때 종욱이 씬 저장하는 순간 그 시점 값이 베이크돼 이후 코드 튜닝이 무시되는
    //    사고가 2번 반복 -> 영구 차단. 공장 팩토리의 런타임 코드 세팅은 그대로 작동.)
    [System.NonSerialized] public float rippleDepth = 1f;   // 안쪽 고정선 위치 = 물결 도착점(px)
    [System.NonSerialized] public float reach = 12f;        // 물결 시작 거리(좌/우/상, px). 프레임에 바짝
    [System.NonSerialized] public float reachBottom = 2f;   // 아래쪽 시작 거리(px). 하단 삐져나옴 방지 = 거의 0
    [System.NonSerialized] public float cycle = 2.8f;       // 한 묶음 출발->꼬리 도착 시간(초). 잔잔하게 = 길게
    [System.NonSerialized] public int layers = 4;           // 한 묶음의 선 개수
    [System.NonSerialized] public float lineGap = 7f;       // 묶음 안 선 간격(px)
    [System.NonSerialized] public float layerAlpha = 0.85f; // 전체 알파 배율

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

    // "열차" 모델: 촘촘한 선 묶음이 통째로 바깥(reach) -> 강조선(rippleDepth)까지 이동.
    // 안쪽 선부터 순서대로 강조선에 닿아 사라지고, 꼬리까지 다 도착하면 새 묶음이 바깥서 출발.
    // (옛 위상 균등 분산 = 선들이 제자리서 꿈틀대는 착시(위치 고정처럼 보임)라 폐기 - 종욱 지적)
    private void Drive()
    {
        if (_rts == null) return;
        int n = _rts.Length;
        float span = reach + rippleDepth;                    // 선 하나의 총 이동 거리(측면 기준)
        float total = span + (n - 1) * lineGap;              // 머리 출발 -> 꼬리 도착까지 스윕 거리
        float head = -reach + Mathf.Repeat(_t / Mathf.Max(0.01f, cycle), 1f) * total;
        for (int i = 0; i < n; i++)
        {
            if (_rts[i] == null || _imgs[i] == null) continue;
            float inset = head - i * lineGap;                // 선 i 의 측면 inset (i=0 이 선두)
            float q = (inset + reach) / Mathf.Max(1f, span); // 0=출발점, 1=강조선 도착
            if (q <= 0f || q >= 1f)                          // 아직 출발 전 / 이미 닿고 소멸
            {
                var hc = _imgs[i].color; hc.a = 0f; _imgs[i].color = hc;
                continue;
            }
            float side = Mathf.Lerp(-reach, rippleDepth, q);
            float bottom = Mathf.Lerp(-reachBottom, rippleDepth, q);  // 하단: 살짝만(삐져나옴 방지)
            _rts[i].offsetMin = new Vector2(side, bottom);
            _rts[i].offsetMax = new Vector2(-side, -side);

            // 출발 직후 4px 페이드인 + 이동 중 서서히 연해짐 + 강조선 앞 4px 에서 닿으며 소멸
            float a = Mathf.Clamp01(q * span / 4f)
                    * Mathf.Lerp(1f, 0.55f, q)
                    * Mathf.Clamp01((1f - q) * span / 4f);
            var c = _imgs[i].color;
            c.a = _baseAlpha * layerAlpha * a;
            _imgs[i].color = c;
        }
    }
}
