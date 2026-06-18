using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyWorldUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("Display Settings")]
    [Tooltip("좌우 미세 보정용. Y는 머리 위 자동 배치라 무시됨(Renderer 없을 때만 폴백).")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);
    [SerializeField] private float showDistance = 20f;
    [SerializeField] private bool alwaysShowBossLike = false;

    [Header("크기 통일 (몬스터 프리팹 스케일 무시)")]
    [Tooltip("월드 기준 체력바/이름 크기. 모든 몬스터가 이 값으로 통일된다. 프리팹을 몇 배로 키우든 영향 없음.")]
    [SerializeField] private float worldScale = 0.01f;

    [Header("머리 위 배치 (스케일/크기 무관 통일)")]
    [Tooltip("몬스터 실제 메쉬 윗부분에서 이만큼(월드 단위) 위에 띄운다. 모든 몬스터 동일 간격.")]
    [SerializeField] private float headGap = 0.4f;

    private EnemyHealth targetHealth;
    private Transform targetTransform;
    private Camera cam;

    private float _headWorldOffset;   // 몬스터 pivot -> 메쉬 top 까지 높이 + headGap (스폰 시 1회 계산)
    private bool _headOffsetReady;
    private bool _scaleReady;          // 부모 스케일 상쇄값 1회 계산 여부 (스폰 후 부모 스케일 불변 가정)

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (hpSlider == null)
            hpSlider = GetComponentInChildren<Slider>(true);

        if (nameText == null)
            nameText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    public void Initialize(EnemyHealth health, string enemyName)
    {
        targetHealth = health;
        targetTransform = health.transform;
        cam = Camera.main;

        CacheHeadOffset();   // 몬스터 실제 높이 기준으로 머리 위 위치 1회 계산

        if (nameText != null)
            nameText.text = enemyName;

        RefreshHP();

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        targetHealth.OnDamage += RefreshHP;
        targetHealth.OnDeath += HandleDeath;
    }

    private void LateUpdate()
    {
        if (targetTransform == null)
            return;

        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return;

        // 거리 컬링: showDistance 밖이면 어차피 안 보이므로(alpha 0) 빌보드/스케일/위치 계산을 통째로 스킵.
        // (안 보이는 HP바는 시각 차이 없음. 22마리 매 프레임 연산 절약. Distance 대신 sqr 비교로 sqrt 제거.)
        if (!alwaysShowBossLike)
        {
            float sqrDist = (cam.transform.position - targetTransform.position).sqrMagnitude;
            if (sqrDist > showDistance * showDistance)
            {
                if (canvasGroup != null && canvasGroup.alpha != 0f) canvasGroup.alpha = 0f;
                return;
            }
        }
        if (canvasGroup != null && canvasGroup.alpha != 1f) canvasGroup.alpha = 1f;

        // 부모(몬스터) 스케일 상쇄 — 스폰 후 스케일이 안 변하므로 1회만 계산해 캐싱.
        if (!_scaleReady) NormalizeScale();

        // 머리 위 통일 배치: 몬스터 실제 메쉬 윗부분 + headGap. 스케일/기본키 달라도 항상 머리 바로 위.
        if (!_headOffsetReady) CacheHeadOffset();
        Vector3 p = targetTransform.position;
        transform.position = new Vector3(p.x + worldOffset.x, p.y + _headWorldOffset, p.z + worldOffset.z);

        transform.forward = cam.transform.forward;
    }

    // 월드 기준 lossyScale 을 worldScale 로 고정 (부모 스케일이 얼마든 결과 동일)
    private void NormalizeScale()
    {
        _scaleReady = true;   // 부모 스케일은 스폰 후 불변 가정 -> 1회만 계산
        var parent = transform.parent;
        if (parent == null)
        {
            transform.localScale = Vector3.one * worldScale;
            return;
        }

        Vector3 ps = parent.lossyScale;
        transform.localScale = new Vector3(
            Mathf.Approximately(ps.x, 0f) ? worldScale : worldScale / ps.x,
            Mathf.Approximately(ps.y, 0f) ? worldScale : worldScale / ps.y,
            Mathf.Approximately(ps.z, 0f) ? worldScale : worldScale / ps.z);
    }

    // 몬스터 실제 메쉬 윗부분까지 높이를 1회 계산 -> 모든 몬스터를 머리 바로 위로 통일.
    // (고정 오프셋이나 스케일 비례 오프셋은 몬스터마다 키/스케일이 달라 제각각이 됨 -> 실제 bounds 기준이 정답)
    private void CacheHeadOffset()
    {
        _headOffsetReady = true;
        _headWorldOffset = worldOffset.y;   // Renderer 못 찾을 때 폴백

        if (targetTransform == null) return;

        bool has = false;
        Bounds b = default;
        var renderers = targetTransform.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (r is ParticleSystemRenderer) continue;            // VFX는 범위 튀니 제외
            if (transform != null && r.transform.IsChildOf(transform)) continue;   // 체력바 UI 자신 제외
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }

        if (has)
            _headWorldOffset = (b.max.y - targetTransform.position.y) + headGap;
    }

    private void RefreshHP()
    {
        if (targetHealth == null || hpSlider == null)
            return;

        hpSlider.maxValue = targetHealth.maxHP;
        hpSlider.value = targetHealth.currentHP;
    }

    private void HandleDeath()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (targetHealth == null)
            return;

        targetHealth.OnDamage -= RefreshHP;
        targetHealth.OnDeath -= HandleDeath;
    }
}
