using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 맵에 배치하는 설비 해금 픽업 오브젝트.
/// - 가까이 가면 hintUI 표시 + 아웃라인 ON
/// - 다른 UI(설정·인벤 등) 열리면 hintUI 즉시 숨김
/// - F키 → 깜빡임 → 해금 → 스르륵 사라짐
/// </summary>
public class FacilityUnlockPickup : MonoBehaviour, IInteractable
{
    [Header("해금 설정")]
    [SerializeField] private int facilityId;

    [Header("힌트 UI")]
    [Tooltip("픽업 프리펩 자식의 Canvas UI 루트를 연결.")]
    [SerializeField] private GameObject hintUI;
    [Tooltip("힌트 UI / 아웃라인 표시 거리 (m)")]
    [SerializeField] private float hintRadius = 3f;

    [Header("깜빡임 효과")]
    [SerializeField] private float flashDuration = 0.4f;
    [SerializeField] private int   flashCount    = 3;

    [Header("사라짐 효과")]
    [SerializeField] private float vanishDuration = 0.7f;
    [SerializeField] private AnimationCurve vanishCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("시야 체크")]
    [Tooltip("플레이어 눈높이 오프셋 (m)")]
    [SerializeField] private float playerEyeHeight = 1.4f;

    // ─────────────────────────────────────────────────────────────

    private bool              _collected    = false;
    private bool              _interacting  = false;
    private bool              _playerNearby = false;
    private bool              _tmpInitialized = false;  // TMP Awake() 실행 여부
    private Outline[]         _outlines;
    private FacilitySelectRow _row;
    private Transform         _playerTransform;

    private void Awake()
    {
        _outlines = GetComponentsInChildren<Outline>(true);
        SetOutlineEnabled(false);

        if (hintUI != null)
        {
            _row = hintUI.GetComponentInChildren<FacilitySelectRow>(true);
            hintUI.SetActive(false);
        }
    }

    private void Start()
    {
        var player = FindFirstObjectByType<Player>();
        if (player != null) _playerTransform = player.transform;
    }

    private void ApplyFacilityName()
    {
        if (hintUI == null) return;

        // DataBoot 완료 후 호출되므로 정상 조회 가능
        string facilityName = null;
        if (GameDataHolder.I != null &&
            GameDataHolder.I.FacilityData.TryGet(facilityId.ToString(), out var data))
            facilityName = data.facilityName;

        if (string.IsNullOrEmpty(facilityName))
        {
            Debug.LogWarning($"[FacilityUnlockPickup] facilityId={facilityId} 이름 조회 실패.");
            return;
        }

        // ① FacilitySelectRow 경유
        if (_row != null) _row.Set(null, facilityName);

        // ② "Name" 자식 TMP에 직접 세팅 (① 실패 보험)
        foreach (var tmp in hintUI.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.gameObject.name == "Name")
            {
                tmp.text = facilityName;
                break;
            }
        }
    }

    private void Update()
    {
        if (_collected || _interacting || _playerTransform == null) return;

        // 다른 UI가 열려 있으면 강제 숨김
        bool uiBlocking = GameUIController.Instance != null
                       && GameUIController.Instance.IsUIBlocking();

        bool nearby = !uiBlocking
                   && Vector3.Distance(transform.position, _playerTransform.position) <= hintRadius
                   && HasLineOfSight();

        if (nearby == _playerNearby) return;
        _playerNearby = nearby;

        SetOutlineEnabled(nearby);

        if (nearby)
        {
            if (hintUI != null)
            {
                // TMP Awake()가 최초 1회만 실행되도록 처음엔 강제 활성화
                if (!_tmpInitialized)
                {
                    hintUI.SetActive(true);
                    _tmpInitialized = true;
                }
                // 공유 UI에 이 오브젝트의 이름을 항상 다시 세팅 (다른 픽업이 덮어쓸 수 있으므로)
                ApplyFacilityName();
                hintUI.SetActive(true);
            }
            _row?.SetSelected(true);
        }
        else
        {
            if (hintUI != null) hintUI.SetActive(false);
        }
    }

    // ── IInteractable ─────────────────────────────────────────────

    public bool CanInteract =>
        !_collected && !_interacting
        && FacilityUnlockManager.Instance != null
        && !FacilityUnlockManager.Instance.IsUnlocked(facilityId);

    public void Interact(Player player)
    {
        if (!CanInteract) return;
        _interacting = true;
        StartCoroutine(FlashThenUnlock());
    }

    // ── 깜빡임 → 해금 ────────────────────────────────────────────

    private IEnumerator FlashThenUnlock()
    {
        float interval = flashDuration / (flashCount * 2f);

        for (int i = 0; i < flashCount; i++)
        {
            _row?.SetSelected(false);
            SetOutlineEnabled(false);
            yield return new WaitForSeconds(interval);

            _row?.SetSelected(true);
            SetOutlineEnabled(true);
            yield return new WaitForSeconds(interval);
        }

        _collected = true;
        _row?.SetSelected(false);
        SetOutlineEnabled(false);
        if (hintUI != null) hintUI.SetActive(false);

        FacilityUnlockManager.Instance?.TryUnlock(facilityId);
        StartCoroutine(VanishRoutine());
    }

    // ── 사라짐 애니메이션 ─────────────────────────────────────────

    private IEnumerator VanishRoutine()
    {
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        Vector3 originalScale = transform.localScale;
        float   elapsed       = 0f;

        while (elapsed < vanishDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / vanishDuration);
            transform.localScale = originalScale * vanishCurve.Evaluate(t);
            yield return null;
        }

        Destroy(gameObject);
    }

    // ── 시야 체크 ────────────────────────────────────────────────

    /// <summary>
    /// 플레이어 눈높이 → 픽업 중심까지 레이캐스트.
    /// 이 오브젝트(또는 자식) 외에 뭔가 맞으면 시야 차단 → false.
    /// </summary>
    private bool HasLineOfSight()
    {
        Vector3 origin = _playerTransform.position + Vector3.up * playerEyeHeight;
        Vector3 target = transform.position + Vector3.up * 0.5f;
        Vector3 dir    = target - origin;
        float   dist   = dir.magnitude;

        if (dist < 0.01f) return true;

        // 트리거 제외, 전체 레이어 대상으로 레이캐스트
        // (SerializeField 기본값 직렬화 문제를 피하기 위해 코드에서 직접 지정)
        if (Physics.Raycast(origin, dir / dist, out RaycastHit hit, dist,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            // 맞은 콜라이더가 이 오브젝트(또는 자식)이면 시야 확보
            return hit.collider.transform.IsChildOf(transform)
                || hit.collider.transform == transform;
        }
        return true; // 아무것도 안 맞음 = 시야 확보
    }

    // ── 아웃라인 ─────────────────────────────────────────────────

    private void SetOutlineEnabled(bool active)
    {
        foreach (var o in _outlines)
            if (o != null) o.enabled = active;
    }

    // ── 에디터 기즈모 ────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
        Gizmos.DrawSphere(transform.position, hintRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, hintRadius);
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1.5f,
            $"Unlock facilityId = {facilityId}");
    }
#endif
}
