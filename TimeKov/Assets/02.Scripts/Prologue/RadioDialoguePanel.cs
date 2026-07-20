using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// Attach to an empty UI GameObject (RectTransform) inside a Canvas.
// Builds the radio dialogue panel (blinking status dot, speaker name,
// dialogue text, next-arrow button, CRT scanline + static noise overlay)
// entirely from code — no prefab needed.
[RequireComponent(typeof(RectTransform))]
public class RadioDialoguePanel : MonoBehaviour
{
    [Header("Content")]
    public string speakerName = "지휘관 리안";
    [TextArea] public string dialogueText = "전 병력 청취, 여기는 사령부. 목표 지점 확보 후 즉시 보고 바람.";

    [Header("Layout")]
    public float panelWidth = 400f;
    public float panelHeight = 100f;

    [Header("Colors")]
    public Color panelBg = new Color(0f, 0f, 0f, 0.88f);
    public Color borderColor = new Color(1f, 1f, 1f, 0.35f);
    public Color textColor = Color.white;
    public Color dialogueColor = new Color(1f, 1f, 1f, 0.82f);

    [Header("Events")]
    public UnityEvent onNextPressed;

    private RawImage noiseImage;
    private Texture2D noiseTex;
    private const int NOISE_W = 140, NOISE_H = 70;
    private float noiseTimer;

    void Start()
    {
        Build();
    }

    void Build()
    {
        RectTransform root = GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(panelWidth, panelHeight);

        // background
        Image bg = gameObject.GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = panelBg;

        // border: Shadow으로 대체 (Outline이 URP Outline과 이름 충돌)
        Shadow shadow = gameObject.AddComponent<Shadow>();
        shadow.effectColor = borderColor;
        shadow.effectDistance = new Vector2(1, -1);

        // --- CRT overlay: scanlines + static noise (masked to bounds) ---
        GameObject overlay = new GameObject("Overlay", typeof(RectTransform));
        overlay.transform.SetParent(transform, false);
        RectTransform overlayRt = overlay.GetComponent<RectTransform>();
        StretchFull(overlayRt);
        Mask mask = overlay.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        Image overlayImg = overlay.AddComponent<Image>();
        overlayImg.color = new Color(1, 1, 1, 0.001f); // required for Mask

        GameObject noiseGo = new GameObject("Noise", typeof(RectTransform));
        noiseGo.transform.SetParent(overlay.transform, false);
        StretchFull(noiseGo.GetComponent<RectTransform>());
        noiseImage = noiseGo.AddComponent<RawImage>();
        noiseImage.color = new Color(1, 1, 1, 0.06f);
        noiseTex = new Texture2D(NOISE_W, NOISE_H, TextureFormat.RGBA32, false);
        noiseTex.filterMode = FilterMode.Point;
        noiseImage.texture = noiseTex;

        // --- content column ---
        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(transform, false);
        RectTransform contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 0);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.offsetMin = new Vector2(18, 14);
        contentRt.offsetMax = new Vector2(-40, -14);

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        // header row: dot + name
        GameObject header = new GameObject("Header", typeof(RectTransform));
        header.transform.SetParent(content.transform, false);
        HorizontalLayoutGroup hlg = header.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        LayoutElement headerLe = header.AddComponent<LayoutElement>();
        headerLe.preferredHeight = 16;

        GameObject dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
        dot.transform.SetParent(header.transform, false);
        dot.GetComponent<RectTransform>().sizeDelta = new Vector2(6, 6);
        dot.GetComponent<Image>().color = Color.white;
        BlinkDot blink = dot.AddComponent<BlinkDot>();

        GameObject nameGo = new GameObject("SpeakerName", typeof(RectTransform));
        nameGo.transform.SetParent(header.transform, false);
        Text nameText = nameGo.AddComponent<Text>();
        nameText.text = speakerName;
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = 15;
        nameText.fontStyle = FontStyle.Bold;
        nameText.color = textColor;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameGo.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 18);
        LayoutElement nameLe = nameGo.AddComponent<LayoutElement>();
        nameLe.preferredWidth = 200;
        nameLe.preferredHeight = 18;

        // dialogue text
        GameObject dialogueGo = new GameObject("DialogueText", typeof(RectTransform));
        dialogueGo.transform.SetParent(content.transform, false);
        Text dialogue = dialogueGo.AddComponent<Text>();
        dialogue.text = dialogueText;
        dialogue.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        dialogue.fontSize = 15;
        dialogue.color = dialogueColor;
        dialogue.alignment = TextAnchor.UpperLeft;
        dialogue.horizontalOverflow = HorizontalWrapMode.Wrap;
        dialogue.verticalOverflow = VerticalWrapMode.Overflow;
        LayoutElement dialogueLe = dialogueGo.AddComponent<LayoutElement>();
        dialogueLe.preferredHeight = panelHeight - 50;
        dialogueLe.flexibleWidth = 1;

        // next-arrow button
        GameObject btnGo = new GameObject("NextButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(transform, false);
        RectTransform btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = btnRt.anchorMax = new Vector2(1, 0.5f);
        btnRt.pivot = new Vector2(1, 0.5f);
        btnRt.anchoredPosition = new Vector2(-8, 0);
        btnRt.sizeDelta = new Vector2(32, 32);
        Image btnImg = btnGo.GetComponent<Image>();
        btnImg.color = Color.black;
        Shadow btnShadow = btnGo.AddComponent<Shadow>();
        btnShadow.effectColor = Color.white;
        btnShadow.effectDistance = new Vector2(1.5f, -1.5f);
        Button btn = btnGo.GetComponent<Button>();
        btn.onClick.AddListener(() => onNextPressed?.Invoke());

        GameObject arrowGo = new GameObject("Arrow", typeof(RectTransform));
        arrowGo.transform.SetParent(btnGo.transform, false);
        Text arrow = arrowGo.AddComponent<Text>();
        arrow.text = ">";
        arrow.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        arrow.fontSize = 18;
        arrow.fontStyle = FontStyle.Bold;
        arrow.color = Color.white;
        arrow.alignment = TextAnchor.MiddleCenter;
        StretchFull(arrowGo.GetComponent<RectTransform>());
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void Update()
    {
        // regenerate static noise a few times a second (cheap CRT flicker)
        noiseTimer += Time.deltaTime;
        if (noiseTimer < 0.08f || noiseTex == null) return;
        noiseTimer = 0f;
        Color32[] pixels = new Color32[NOISE_W * NOISE_H];
        for (int i = 0; i < pixels.Length; i++)
        {
            byte v = (byte)Random.Range(0, 256);
            byte a = (byte)Random.Range(0, 256);
            pixels[i] = new Color32(v, v, v, a);
        }
        noiseTex.SetPixels32(pixels);
        noiseTex.Apply(false);
    }
}

// Simple opacity blink, matches the CSS keyframe on the status dot.
public class BlinkDot : MonoBehaviour
{
    public float period = 1.1f;
    private Graphic graphic;
    private float t;

    void Awake() { graphic = GetComponent<Graphic>(); }

    void Update()
    {
        t += Time.deltaTime;
        bool on = (t % period) < period * 0.5f;
        Color c = graphic.color;
        c.a = on ? 1f : 0.25f;
        graphic.color = c;
    }
}
