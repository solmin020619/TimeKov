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

    [Tooltip("0=자동(콜라이더/메시 top 기준). >0 이면 이 높이(원점 기준 월드 단위)로 고정한다.\n" +
             "자이언트웜처럼 콜라이더가 지하로 길게 뻗어 자동 계산 top 이 지면에 박히는 몹용 수동 보정.")]
    [SerializeField] private float headOffsetOverride = 0f;

    [Tooltip("설정하면 이 이름의 본(머리 등)을 따라 HP바가 움직인다 — 원점 고정 대신. 애니메이션으로 머리가 크게\n" +
             "흔들리는 몹(웜처럼 몸을 휘두르는 공격)용. 비우면 기존 방식(원점 + 자동/override 높이).")]
    [SerializeField] private string followBoneName = "";
    [Tooltip("따라갈 본 위로 이만큼(월드 단위) 띄운다.")]
    [SerializeField] private float followBoneGap = 1.5f;

    [Header("이름 가독성 (밝은 배경 대비)")]
    [Tooltip("이름 글자가 흰색이라 눈밭/밝은 배경에서 안 보임. 검은 외곽선으로 어디서나 읽히게. 폭 0 = 끔.")]
    [SerializeField] private Color nameOutlineColor = Color.black;
    [Range(0f, 0.5f)]
    [SerializeField] private float nameOutlineWidth = 0.2f;

    private EnemyHealth targetHealth;
    private Transform targetTransform;
    private Camera cam;

    private float _headWorldOffset;   // 몬스터 pivot -> 메쉬 top 까지 높이 + headGap (스폰 시 1회 계산)
    private bool _headOffsetReady;
    private Transform _followBone;    // followBoneName 설정 시 이 본을 따라감(원점 고정 대신)
    private bool _scaleReady;          // 부모 스케일 상쇄값 1회 계산 여부 (스폰 후 부모 스케일 불변 가정)

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (hpSlider == null)
            hpSlider = GetComponentInChildren<Slider>(true);

        if (nameText == null)
            nameText = GetComponentInChildren<TextMeshProUGUI>(true);

        ApplyNameOutline();   // 이름에 검은 외곽선(밝은 배경 가독성)
    }

    // 이름 글자(흰색)가 눈밭/밝은 배경에서 안 보이는 문제. 검은 외곽선으로 어디서나 읽히게 한다.
    //   머티리얼을 몹마다 인스턴스로 만들면 배칭이 깨져 드로우콜이 는다(성능 병목). 외곽선 머티리얼을
    //   딱 하나만(static) 만들어 전 몬스터 이름이 공유 = 배칭 유지(밝은 배경 몹 여럿 떠도 드로우콜 안 늚).
    private static Material _nameOutlineMat;

    private void ApplyNameOutline()
    {
        if (nameText == null || nameOutlineWidth <= 0f) return;
        if (_nameOutlineMat == null)
        {
            _nameOutlineMat = new Material(nameText.fontSharedMaterial);
            _nameOutlineMat.SetColor(ShaderUtilities.ID_OutlineColor, nameOutlineColor);
            _nameOutlineMat.SetFloat(ShaderUtilities.ID_OutlineWidth, nameOutlineWidth);
        }
        nameText.fontSharedMaterial = _nameOutlineMat;
        nameText.UpdateMeshPadding();   // 외곽선 폭만큼 메쉬 패딩 재계산(안 하면 외곽선이 잘림)
    }

    public void Initialize(EnemyHealth health, string enemyName)
    {
        targetHealth = health;
        targetTransform = health.transform;
        cam = Camera.main;

        CacheHeadOffset();   // 몬스터 실제 높이 기준으로 머리 위 위치 1회 계산

        // 따라갈 본 지정 시 1회 탐색(웜 머리 등). 못 찾으면 null -> 기존 원점 방식으로 폴백.
        if (!string.IsNullOrEmpty(followBoneName))
            _followBone = FindBone(targetTransform, followBoneName);

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

        // 땅속에 숨은 동안은 통째로 감춘다(위치 갱신도 필요 없다).
        if (_forceHidden)
        {
            if (canvasGroup != null && canvasGroup.alpha != 0f) canvasGroup.alpha = 0f;
            return;
        }

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
        if (_followBone != null)
        {
            // 따라갈 본(움직이는 머리)을 따라간다. 몸을 휘두르는 공격에도 바가 머리에 붙어 있게.
            Vector3 b = _followBone.position;
            transform.position = new Vector3(b.x + worldOffset.x, b.y + followBoneGap, b.z + worldOffset.z);
        }
        else
        {
            Vector3 p = targetTransform.position;
            transform.position = new Vector3(p.x + worldOffset.x, p.y + _headWorldOffset, p.z + worldOffset.z);
        }

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

    // 몬스터 머리 위까지의 높이를 1회 계산 -> 모든 몬스터를 머리 바로 위로 통일.
    //
    // ★1순위 = 콜라이더. 2순위 = 메시 bounds.
    //   메시 bounds 를 1순위로 쓰다가 몹마다 높이가 제각각이 됐다. 이유:
    //   (a) SkinnedMeshRenderer 의 bounds 는 FBX 에 구워진 값이라 실제 포즈를 안 따라간다.
    //       납품사마다 여유를 다르게 잡아서(날개 편 포즈, 꼬리 세운 포즈 등) 같은 키라도 값이 딴판이다.
    //   (b) 이 계산은 Awake 시점에 도는데, 그때는 애니메이터가 아직 포즈를 잡기 전이다.
    //   반면 콜라이더 높이는 빌더/프리팹에서 몹마다 직접 정한 값(bodyHeight)이라 의도가 분명하고,
    //   충돌 판정과도 일치해서 "몸 위"라는 말이 실제로 맞는다.
    private void CacheHeadOffset()
    {
        _headOffsetReady = true;
        _headWorldOffset = worldOffset.y;   // 아무것도 못 찾을 때 폴백

        // 수동 보정: 콜라이더가 지하로 뻗어 자동 top 이 지면에 박히는 몹(웜 등)은 이 값으로 고정.
        if (headOffsetOverride > 0f) { _headWorldOffset = headOffsetOverride; return; }

        if (targetTransform == null) return;

        // 1순위: 몸통 콜라이더(트리거 제외 - 감지용 트리거는 몸집과 무관하게 크다)
        foreach (var col in targetTransform.GetComponents<Collider>())
        {
            if (col == null || col.isTrigger) continue;
            _headWorldOffset = (col.bounds.max.y - targetTransform.position.y) + headGap;
            return;
        }

        // 2순위: 메시 bounds
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

    // 이름으로 본 Transform 찾기. 정확 매칭 우선, 실패 시 부분 매칭(네임스페이스 접두 "rig:Head" 등 대비).
    private static Transform FindBone(Transform root, string boneName)
    {
        if (root == null || string.IsNullOrEmpty(boneName)) return null;
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in all) if (t.name == boneName) return t;
        foreach (var t in all) if (t.name.Contains(boneName)) return t;
        return null;
    }

    // ── 강제 숨김 ──
    // 몹이 땅속으로 숨는 패턴(헬버그 잠복) 처럼 몸이 사라졌는데 체력바만 허공에 남는 걸 막는다.
    // ★몸을 숨길 때 쓰는 GetComponentsInChildren<Renderer>() 에는 UI 가 안 걸린다.
    //   UI 그래픽은 Renderer 가 아니라 CanvasRenderer 라서다. 그래서 따로 불러줘야 한다.
    private bool _forceHidden;

    public void SetHidden(bool hidden)
    {
        _forceHidden = hidden;
        if (canvasGroup != null) canvasGroup.alpha = hidden ? 0f : 1f;
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
