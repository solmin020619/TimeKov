using UnityEngine;
using UnityEngine.UI;

// HP 바 시각 피드백 모음.
// (1) 상태별 색: 평소 파랑 / 저체력 빨강 / 체력 안 닳는 상태(기지 안) 회색
//     우선순위 = 안 닳음(회색) > 저체력(빨강) > 평소(파랑)
// (2) 저체력일 때 깜빡임
// (3) 피격 시 HP 바 떨림 (세기/시간 인스펙터 조절)
// PlayerHudUI 와 별개로, HP 슬라이더의 Fill Image 와 떨릴 RectTransform 만 연결하면 동작.
public class HpBarFeedback : MonoBehaviour
{
    [SerializeField] private PlayerStatComponent playerStat;

    [Header("대상")]
    [Tooltip("HP 슬라이더의 Fill Image (색을 바꿀 대상)")]
    [SerializeField] private Image hpFillImage;
    [Tooltip("피격 시 떨릴 RectTransform (보통 HP 바 루트). 비워두면 떨림 생략")]
    [SerializeField] private RectTransform shakeTarget;

    [Header("색")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.55f, 1f, 1f);   // 평소 파랑
    [SerializeField] private Color lowColor    = new Color(0.9f, 0.15f, 0.15f, 1f); // 저체력 빨강
    [SerializeField] private Color frozenColor = new Color(0.6f, 0.6f, 0.6f, 1f);   // 안 닳을 때 회색
    [Range(0f, 1f)]
    [Tooltip("이 비율 이하이면 저체력(빨강)")]
    [SerializeField] private float lowThreshold = 0.3f;
    [Tooltip("색 전환 속도 (클수록 빠르게 바뀜)")]
    [SerializeField] private float colorLerpSpeed = 8f;

    [Header("저체력 깜빡")]
    [SerializeField] private bool blinkOnLow = true;
    [SerializeField] private float blinkSpeed = 6f;
    [Range(0f, 1f)]
    [Tooltip("깜빡일 때 가장 옅어지는 알파")]
    [SerializeField] private float blinkMinAlpha = 0.35f;

    [Header("피격 떨림")]
    [SerializeField] private bool shakeOnHurt = true;
    [SerializeField] private float shakeDuration = 0.2f;
    [Tooltip("떨림 크기 (픽셀)")]
    [SerializeField] private float shakeMagnitude = 6f;

    private float _shakeTimer;
    private Vector2 _shakeHome;
    private bool _homeCaptured;

    private void Start()
    {
        if (playerStat == null)
        {
            var p = FindAnyObjectByType<Player>();
            if (p != null) playerStat = p.GetComponent<PlayerStatComponent>();
        }

        if (playerStat != null)
            playerStat.OnHurt += OnHurt;

        if (shakeTarget != null)
        {
            _shakeHome = shakeTarget.anchoredPosition;
            _homeCaptured = true;
        }
    }

    private void OnDestroy()
    {
        if (playerStat != null)
            playerStat.OnHurt -= OnHurt;
    }

    private void OnHurt()
    {
        if (shakeOnHurt) _shakeTimer = shakeDuration;
    }

    private void Update()
    {
        if (playerStat == null) return;
        UpdateColor();
        UpdateShake();
    }

    private void UpdateColor()
    {
        if (hpFillImage == null) return;

        float ratio = playerStat.MaxHp > 0f ? playerStat.CurrentHp / playerStat.MaxHp : 0f;
        bool frozen = playerStat.IsInBase;   // 체력이 안 닳는 상태
        bool low = ratio <= lowThreshold;

        // 우선순위: 회색(안 닳음) > 빨강(저체력) > 파랑(평소)
        Color target = frozen ? frozenColor : (low ? lowColor : normalColor);

        // 저체력 깜빡 — 투명도(alpha)가 아니라 색 밝기(RGB)로 펄스.
        // alpha 를 건드리면 Fill 이 불투명/투명해지며 위에 깔린 텍스트를 가리므로.
        if (blinkOnLow && low && !frozen)
        {
            float t = (Mathf.Sin(Time.unscaledTime * blinkSpeed) + 1f) * 0.5f;
            Color dim = target * blinkMinAlpha;
            target = Color.Lerp(dim, target, t);
        }

        Color cur = hpFillImage.color;
        Color lerped = Color.Lerp(cur, target, Time.deltaTime * colorLerpSpeed);
        lerped.a = cur.a; // 원래 Fill 알파 유지 (텍스트 가림 방지)
        hpFillImage.color = lerped;
    }

    private void UpdateShake()
    {
        if (shakeTarget == null || !_homeCaptured) return;

        if (_shakeTimer > 0f)
        {
            _shakeTimer -= Time.deltaTime;
            float damp = Mathf.Clamp01(_shakeTimer / shakeDuration);
            Vector2 off = new Vector2(
                (Mathf.PerlinNoise(Time.unscaledTime * 40f, 0f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0f, Time.unscaledTime * 40f) - 0.5f) * 2f
            ) * shakeMagnitude * damp;
            shakeTarget.anchoredPosition = _shakeHome + off;
        }
        else
        {
            shakeTarget.anchoredPosition = Vector2.Lerp(shakeTarget.anchoredPosition, _shakeHome, Time.deltaTime * 12f);
        }
    }
}
