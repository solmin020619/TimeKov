using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Claude 디자인 tk-rise CSS 키프레임을 Unity UI 1:1 변환.
/// 매 프레임 frame.rect.size로 스케일 재계산 → 해상도/CanvasScaler 변경에도 자동 대응.
/// 750×416 design frame 기준 translateY(-460px) + translateX(+20px) + alpha 0/0.7@10%/0.5@90%/0.
/// </summary>
public class TimekovParticles : MonoBehaviour
{
    [SerializeField] private RectTransform frame;       // 디자인 프레임 (스케일 기준)
    [SerializeField] private GameObject particlePrefab; // Image + CanvasGroup
    [SerializeField] private int count = 18;

    private const float DESIGN_W = 750f;
    private const float DESIGN_H = 416f;
    private const float RISE_PX = 460f;
    private const float DRIFT_PX = 20f;

    private struct P
    {
        public RectTransform rt;
        public CanvasGroup cg;
        public float baseX, baseY;
        public float duration;
        public float elapsed;
    }
    private P[] particles;

    void Start()
    {
        if (frame == null || particlePrefab == null) return;

        particles = new P[count];
        Vector2 frameSize = frame.rect.size;
        float sx = frameSize.x / DESIGN_W;
        float sy = frameSize.y / DESIGN_H;

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(particlePrefab, frame);
            go.SetActive(true);
            var rt = go.GetComponent<RectTransform>();
            var cg = go.GetComponent<CanvasGroup>();

            float leftPct = (i * 137.5f) % 100f;
            float delay = (i * 1.31f) % 8f;
            float dur = 14f + ((i * 7) % 10);
            float sizePx = 1f + (i % 3) * 0.5f;

            rt.sizeDelta = new Vector2(sizePx * sx, sizePx * sy);

            float baseXPx = (leftPct / 100f) * DESIGN_W;
            float baseYPx = -5f;

            particles[i] = new P
            {
                rt = rt,
                cg = cg,
                baseX = baseXPx * sx,
                baseY = baseYPx * sy,
                duration = dur,
                elapsed = -delay
            };
            cg.alpha = 0;
        }
    }

    void Update()
    {
        if (particles == null) return;

        Vector2 frameSize = frame.rect.size;
        float sx = frameSize.x / DESIGN_W;
        float sy = frameSize.y / DESIGN_H;

        for (int i = 0; i < count; i++)
        {
            ref var p = ref particles[i];
            p.elapsed += Time.deltaTime;
            if (p.elapsed < 0) { p.cg.alpha = 0; continue; }

            float t = (p.elapsed % p.duration) / p.duration;

            // 위치: CSS translateY(-460px) translateX(+20px). UGUI는 Y+가 위라 부호 그대로.
            float dx = DRIFT_PX * t * sx;
            float dy = RISE_PX * t * sy;
            p.rt.anchoredPosition = new Vector2(p.baseX + dx, p.baseY + dy);

            // 알파: 0%→0, 10%→0.7, 90%→0.5, 100%→0
            float a;
            if (t < 0.1f) a = Mathf.Lerp(0f, 0.7f, t / 0.1f);
            else if (t < 0.9f) a = Mathf.Lerp(0.7f, 0.5f, (t - 0.1f) / 0.8f);
            else a = Mathf.Lerp(0.5f, 0f, (t - 0.9f) / 0.1f);

            p.cg.alpha = a;
        }
    }
}
