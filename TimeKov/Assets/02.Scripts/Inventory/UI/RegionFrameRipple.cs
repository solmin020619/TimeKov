using UnityEngine;
using UnityEngine.UI;

// 범위 강조 바깥 라인 물결 애니.
// region 경계(가장자리)에서 안쪽 고정선(inner)까지 반복 수렴 + 페이드. (inner 고정 / outer만 움직임)
// DropRegion이 드래그 중에만 활성화되므로, 켜질 때마다 처음부터 돈다.
[RequireComponent(typeof(Image))]
public class RegionFrameRipple : MonoBehaviour
{
    [Tooltip("안쪽 고정선 위치 = 프레임 경계서 이만큼 안으로(px). 물결의 도착점.")]
    public float rippleDepth = 4f;
    [Tooltip("물결 시작 거리 = 프레임 경계서 이만큼 바깥(px). 클수록 멀리서 온다.")]
    public float startOut = 14f;
    [Tooltip("한 번 들어오는 주기(초). 작을수록 빠르게 반복.")]
    public float period = 1.3f;

    private RectTransform _rt;
    private Image _img;
    private float _baseAlpha = 1f;
    private float _t;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        _img = GetComponent<Image>();
        if (_img != null) _baseAlpha = _img.color.a;
    }

    private void OnEnable() { _t = 0f; }   // 영역 켜질(드래그 시작) 때마다 처음부터

    private void Update()
    {
        if (period <= 0f) return;
        _t += Time.unscaledDeltaTime;
        float k = (_t % period) / period;               // 0..1 반복
        float inset = Mathf.Lerp(-startOut, rippleDepth, k);   // 바깥(startOut px) -> 안쪽(inner)으로 수렴
        _rt.offsetMin = new Vector2(inset, inset);
        _rt.offsetMax = new Vector2(-inset, -inset);
        if (_img != null)
        {
            var c = _img.color;
            c.a = _baseAlpha * Mathf.Sin(k * Mathf.PI);   // 가장자리/안쪽서 0, 중간 최대 (물결 느낌)
            _img.color = c;
        }
    }
}
