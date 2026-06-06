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

    private PlayerStatComponent _stat;
    private Material _mat;
    private int _propId;
    private float _displayed = 1f;

    private void Awake()
    {
        _propId = Shader.PropertyToID(fillProperty);

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
    }

    private void Update()
    {
        if (_stat == null || _mat == null) return;

        float target = _stat.MaxStamina > 0f
            ? Mathf.Clamp01(_stat.CurrentStamina / _stat.MaxStamina)
            : 0f;

        _displayed = smoothSpeed > 0f
            ? Mathf.MoveTowards(_displayed, target, smoothSpeed * Time.deltaTime)
            : target;

        _mat.SetFloat(_propId, _displayed);
    }

    private void OnDestroy()
    {
        // 런타임에 복제한 머티리얼 정리 (누수 방지)
        if (_mat != null) Destroy(_mat);
    }
}
