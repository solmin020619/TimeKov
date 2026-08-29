using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스태미나 바 채움 연동.
/// 셰이더(S_UI mask slider)의 _Slider(0~1) 프로퍼티에 CurrentStamina/MaxStamina 비율을 넣어준다.
/// 스태미나 바 Image(Stemina Slider 머티리얼)에 부착하고 barImage 연결 (비우면 자기 자신 Image 사용).
/// </summary>
public class StaminaBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image barImage;   // Stemina Slider 머티리얼이 붙은 Image

    [Header("Shader")]
    [SerializeField] private string fillProperty = "_Slider";  // 셰이더의 0~1 채움 프로퍼티

    [Header("Smoothing")]
    [Tooltip("바가 따라오는 속도. 0이면 즉시.")]
    [SerializeField] private float smoothSpeed = 10f;

    // ── 자동 숨김 ────────────────────────────────────────────────────────
    // 스태미나는 대부분의 시간 동안 가득 차 있어서, 늘 띄워 두면 화면만 차지하고
    // 정작 줄어드는 순간에 눈이 안 간다. 쓰거나 차오르는 동안에만 보여 준다.
    [Header("자동 숨김")]
    [Tooltip("가득 차면 서서히 감춘다. 끄면 항상 보인다(예전 동작).")]
    [SerializeField] private bool autoHide = true;

    [Tooltip("감출 대상. 비우면 '한 단계 위 부모'를 감춘다.\n" +
             "★이 컴포넌트는 채움(파란 바) 이미지에 붙는데, 테두리·배경은 대개 형제 오브젝트다.\n" +
             "  자기 자신만 감추면 파란 부분만 사라지고 회색 틀이 남는다. 그래서 부모가 기본값이다.\n" +
             "너무 많이 사라지면(HUD 전체 등) 여기에 바 컨테이너를 직접 지정한다.")]
    [SerializeField] private Transform fadeRoot;

    [Tooltip("가득 찬 뒤 사라지기 시작할 때까지 기다리는 시간(초).\n" +
             "0 이면 다 차자마자 사라져서, 회복이 끝나는 순간이 눈에 안 띈다.")]
    [Range(0f, 3f)] [SerializeField] private float hideDelay = 0.6f;

    [Tooltip("나타나는 시간(초). 짧아야 쓰기 시작한 순간 바로 보인다.")]
    [Range(0.01f, 1f)] [SerializeField] private float fadeInTime = 0.1f;

    [Tooltip("사라지는 시간(초). 길수록 부드럽게 없어진다.")]
    [Range(0.05f, 3f)] [SerializeField] private float fadeOutTime = 0.6f;

    [Tooltip("가득 찬 것으로 보는 오차. 0.1이면 최대치-0.1 이상이면 가득 참으로 친다.\n" +
             "0 으로 두면 부동소수 오차 때문에 영영 '가득 참'이 안 될 수 있다.")]
    [Range(0f, 2f)] [SerializeField] private float fullThreshold = 0.1f;

    private PlayerStatComponent _stat;
    private Material _mat;
    private int _propId;
    private float _displayed = 1f;

    private CanvasGroup _group;
    private float _hideTimer;

    private void Awake()
    {
        _propId = Shader.PropertyToID(fillProperty);

        if (autoHide)
        {
            // 지정이 없으면 부모를 감춘다 — 채움 이미지 하나만 감추면 회색 틀만 남기 때문.
            Transform t = fadeRoot != null ? fadeRoot
                        : (transform.parent != null ? transform.parent : transform);

            _group = t.GetComponent<CanvasGroup>();
            if (_group == null) _group = t.gameObject.AddComponent<CanvasGroup>();
        }

        if (barImage == null)
            barImage = GetComponent<Image>();

        if (barImage != null)
        {
            // 공유 머티리얼 에셋을 직접 안 건드리게 인스턴스 복제 후 교체
            _mat = Instantiate(barImage.material);
            barImage.material = _mat;
        }
        else
        {
            Debug.LogWarning("[StaminaBarUI] barImage 없음 - 채움 동작 안 함");
        }
    }

    private void Start()
    {
        var player = FindAnyObjectByType<Player>();
        if (player != null) _stat = player.Stat;
        if (_stat == null)
            Debug.LogWarning("[StaminaBarUI] PlayerStat 못 찾음 - 씬에 Player 있는지 확인");

        // 시작 상태를 곧바로 맞춘다. 안 그러면 게임에 들어서자마자 가득 찬 바가
        // 한 번 보였다가 사라지는 게 눈에 띈다.
        if (_group != null)
            _group.alpha = (_stat == null || !IsFull()) ? 1f : 0f;
    }

    private bool IsFull()
    {
        if (_stat == null || _stat.MaxStamina <= 0f) return false;
        return _stat.CurrentStamina >= _stat.MaxStamina - fullThreshold;
    }

    private void Update()
    {
        if (_stat == null) return;

        if (_mat != null)
        {
            float target = _stat.MaxStamina > 0f
                ? Mathf.Clamp01(_stat.CurrentStamina / _stat.MaxStamina)
                : 0f;

            _displayed = smoothSpeed > 0f
                ? Mathf.MoveTowards(_displayed, target, smoothSpeed * Time.deltaTime)
                : target;

            _mat.SetFloat(_propId, _displayed);
        }

        UpdateVisibility();
    }

    /// <summary>쓰거나 차오르는 동안만 보이게 한다.
    ///
    /// 스태미나가 최대치가 아니라는 건 곧 '지금 쓰고 있거나 회복 중'이라는 뜻이므로,
    /// 그것 하나만 보면 두 경우가 다 걸린다.
    /// ★가득 찬 뒤 곧바로 지우지 않고 hideDelay 만큼 붙잡는다. 회복이 끝나는 순간이
    ///   화면에서 사라지면, 다 찼는지 확인할 틈이 없다.
    /// ★나타날 때는 빠르게, 사라질 때는 느리게. 쓰기 시작한 순간은 즉시 보여야 하고,
    ///   사라지는 건 서서히여야 눈에 안 거슬린다.</summary>
    private void UpdateVisibility()
    {
        if (!autoHide || _group == null) return;

        bool full = IsFull();
        if (!full) _hideTimer = hideDelay;
        else if (_hideTimer > 0f) _hideTimer -= Time.deltaTime;

        float want = (!full || _hideTimer > 0f) ? 1f : 0f;
        float seconds = want > _group.alpha ? fadeInTime : fadeOutTime;

        _group.alpha = Mathf.MoveTowards(_group.alpha, want,
                                         Time.deltaTime / Mathf.Max(0.01f, seconds));
    }

    private void OnDestroy()
    {
        // 런타임에 복제한 머티리얼 정리 (누수 방지)
        if (_mat != null) Destroy(_mat);
    }
}
