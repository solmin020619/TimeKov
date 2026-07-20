using UnityEngine;
using UnityEngine.UI;

// Attach to an empty UI GameObject (RectTransform) inside a Canvas.
// Auto-builds N bar Images as children and animates their height like
// an audio waveform. Bars are white with a soft glow (Outline/Shadow optional).
[RequireComponent(typeof(RectTransform))]
public class WaveformBars : MonoBehaviour
{
    [Header("Layout")]
    public int barCount = 20;
    public float barWidth = 4f;
    public float gap = 4f;
    public float minHeight = 6f;
    public float maxHeight = 26f;

    [Header("Color")]
    public Color barColor = Color.white;

    [Header("Animation")]
    public float updateInterval = 0.09f; // seconds between height changes
    public float noiseSpeed = 0.55f;

    private RectTransform[] bars;
    private float timer;
    private float phase;

    void Start()
    {
        BuildBars();
    }

    void BuildBars()
    {
        bars = new RectTransform[barCount];
        RectTransform rt = GetComponent<RectTransform>();

        HorizontalLayoutGroup layout = gameObject.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.LowerCenter;
        layout.spacing = gap;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        for (int i = 0; i < barCount; i++)
        {
            GameObject go = new GameObject("Bar_" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            Image img = go.GetComponent<Image>();
            img.color = barColor;
            RectTransform barRt = go.GetComponent<RectTransform>();
            barRt.sizeDelta = new Vector2(barWidth, minHeight);
            bars[i] = barRt;
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer < updateInterval) return;
        timer = 0f;
        phase += 1f;

        for (int i = 0; i < barCount; i++)
        {
            // deterministic pseudo-random per bar/phase, same shape as the source design
            float seed = Mathf.Sin(i * 12.9898f + phase * noiseSpeed) * 43758.5453f;
            float r = seed - Mathf.Floor(seed);
            float h = Mathf.Lerp(minHeight, maxHeight, r);
            bars[i].sizeDelta = new Vector2(barWidth, h);
        }
    }
}
