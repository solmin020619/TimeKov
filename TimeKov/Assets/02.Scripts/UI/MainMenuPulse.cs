using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 요소 알파를 ease in/out으로 펄스(tk-pulse 2.4s).
/// 우선순위: CanvasGroup → TMP_Text → Graphic. 그룹 전체 펄스는 CanvasGroup 부착.
/// </summary>
public class MainMenuPulse : MonoBehaviour
{
    [Tooltip("펄스 한 사이클 길이(초)")]
    [SerializeField] private float period = 2.4f;
    [SerializeField] private float minAlpha = 0.55f;
    [SerializeField] private float maxAlpha = 1.0f;

    private CanvasGroup _cg;
    private Graphic _graphic;
    private TMP_Text _tmp;

    void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
        if (_cg == null)
        {
            _tmp = GetComponent<TMP_Text>();
            if (_tmp == null) _graphic = GetComponent<Graphic>();
        }
    }

    void Update()
    {
        // ease in/out: 0.5 + 0.5 * sin(t) → 0~1. 그 다음 min~max로 매핑.
        float t = (Mathf.Sin(Time.time / period * Mathf.PI * 2f) + 1f) * 0.5f;
        // smoothstep 으로 ease-in-out 강조
        t = t * t * (3f - 2f * t);
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);

        if (_cg != null)
        {
            _cg.alpha = alpha;
        }
        else if (_tmp != null)
        {
            var c = _tmp.color;
            c.a = alpha;
            _tmp.color = c;
        }
        else if (_graphic != null)
        {
            var c = _graphic.color;
            c.a = alpha;
            _graphic.color = c;
        }
    }
}
