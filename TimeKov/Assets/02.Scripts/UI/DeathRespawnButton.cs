// DeathRespawnButton.cs
// 사망 화면 "시간 되감기" 버튼의 호버/누름 인터랙션(시각 피드백) 전용 컴포넌트.
// 클릭 판정/콜백은 UnityEngine.UI.Button 이 담당하고, 여기서는 스케일·색·글로우만 부드럽게 제어한다.
// (Button.transition = None 으로 두고 이 컴포넌트가 비주얼을 전담)

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class DeathRespawnButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    // 외부(빌더)에서 배선
    public Button   button;      // interactable 상태 참조
    public Image    background;  // 베이스 판
    public Image    border;      // 테두리(호버 시 밝아짐)
    public Image    glow;        // 호버 글로우(뒤에 깔리는 부드러운 빛)
    public TMP_Text label;

    // 모서리 브래킷(평소 숨김 → 호버 시 바깥에서 코너로 모아지며 계속 미세하게 움직임)
    [System.Serializable]
    public struct CornerBracket
    {
        public RectTransform rt;      // 코너에 고정된 컨테이너
        public CanvasGroup   cg;      // 투명도 제어
        public Vector2       outDir;  // 코너 바깥 방향(정규화)
    }
    public CornerBracket[] brackets;
    public float bracketOutDist  = 18f;   // 숨김/시작 시 바깥 오프셋
    public float bracketRestDist = 9f;    // 호버 시 코너에서 살짝 떨어진 정착 오프셋

    // 색 팔레트(빌더에서 세팅)
    public Color baseColor   = new Color(0.10f, 0.02f, 0.03f, 0.55f);
    public Color hoverColor  = new Color(0.55f, 0.06f, 0.10f, 0.75f);
    public Color borderBase  = new Color(1f, 0.18f, 0.24f, 0.55f);
    public Color borderHover = new Color(1f, 0.30f, 0.36f, 1f);
    public Color labelBase   = new Color(1f, 0.72f, 0.74f, 1f);
    public Color labelHover  = new Color(1f, 0.96f, 0.96f, 1f);

    private RectTransform _rt;
    private bool _hover;
    private bool _pressed;
    private float _glowT;      // 0=off, 1=full (호버 글로우/색 보간용)
    private float _pressT;     // 0=풀림, 1=눌림
    private float _idle;       // 준비 완료 시 은은한 호흡용

    void Awake() => _rt = (RectTransform)transform;

    bool Usable => button == null || button.interactable;

    public void OnPointerEnter(PointerEventData e) { if (Usable) _hover = true; }
    public void OnPointerExit(PointerEventData e)  { _hover = false; _pressed = false; }
    public void OnPointerDown(PointerEventData e)  { if (Usable) _pressed = true; }
    public void OnPointerUp(PointerEventData e)    { _pressed = false; }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _idle += dt;

        bool usable = Usable;
        float glowTarget  = (usable && _hover) ? 1f : 0f;
        float pressTarget = (usable && _pressed) ? 1f : 0f;

        // 부드러운 추종(호버 인/아웃, 누름)
        _glowT  = Mathf.MoveTowards(_glowT,  glowTarget,  dt / 0.12f);
        _pressT = Mathf.MoveTowards(_pressT, pressTarget, dt / 0.06f);

        // 준비 완료(호버 아님) 시 아주 은은한 호흡 — "되감기 대기" 느낌
        float breathe = (usable && !_hover) ? (Mathf.Sin(_idle * 2.2f) * 0.5f + 0.5f) * 0.12f : 0f;

        // 스케일: 호버 시 살짝 커지고, 누르면 다시 줄어듦
        float scale = 1f + _glowT * 0.05f - _pressT * 0.06f;
        if (_rt != null) _rt.localScale = new Vector3(scale, scale, 1f);

        // 색 보간
        float on = Mathf.Max(_glowT, breathe);
        if (background != null) background.color = Color.Lerp(baseColor,   hoverColor,  _glowT);
        if (border     != null) border.color     = Color.Lerp(borderBase,  borderHover, on);
        if (label      != null) label.color      = Color.Lerp(labelBase,   labelHover,  _glowT);
        if (glow       != null)
        {
            var c = glow.color; c.a = on * 0.55f; glow.color = c;
            float gs = 1f + on * 0.10f;
            glow.rectTransform.localScale = new Vector3(gs, gs, 1f);
        }

        UpdateBrackets(_glowT);
    }

    // 브래킷: 호버(_glowT)에 따라 바깥→코너로 모아지고, 호버 중엔 계속 미세하게 진동.
    void UpdateBrackets(float hoverT)
    {
        if (brackets == null) return;
        // 호버할수록 바깥 거리 → 코너 근처로 수렴
        float dist = Mathf.Lerp(bracketOutDist, bracketRestDist, hoverT);
        // 계속 조금씩 움직이는 미세 진동(호버 시에만 보임)
        float wob = Mathf.Sin(_idle * 3.4f) * 1.6f * hoverT;
        for (int i = 0; i < brackets.Length; i++)
        {
            var b = brackets[i];
            if (b.rt != null) b.rt.anchoredPosition = b.outDir * (dist + wob);
            if (b.cg != null) b.cg.alpha = hoverT;
        }
    }

    /// <summary>비활성(카운트다운 중)일 때 시각 상태 리셋.</summary>
    public void ResetVisual()
    {
        _hover = _pressed = false;
        _glowT = _pressT = 0f;
        if (_rt != null) _rt.localScale = Vector3.one;
        if (background != null) background.color = baseColor;
        if (border     != null) border.color     = borderBase;
        if (label      != null) label.color      = labelBase;
        if (glow       != null) { var c = glow.color; c.a = 0f; glow.color = c; }
        if (brackets != null)
            foreach (var b in brackets)
            {
                if (b.cg != null) b.cg.alpha = 0f;
                if (b.rt != null) b.rt.anchoredPosition = b.outDir * bracketOutDist;
            }
    }
}
