using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 맵에 배치하는 설비 해금 픽업 오브젝트.
/// - 가까이 가면 hintUI(이름) 표시 + 아웃라인 ON
/// - 파밍 상자와 동일한 방식: F로 "걸어두면" openTime초 뒤 '수령 가능'(자리 비워도 카운트),
///   돌아와서 F로 해금. 기다리기 싫으면 G로 즉시완료(HP=시간 소모).
/// - 해금 시 깜빡임 → 스르륵 사라짐.
/// </summary>
public class FacilityUnlockPickup : MonoBehaviour, IInstantInteractable
{
    [Header("해금 설정")]
    [SerializeField] private int facilityId;

    [Header("힌트 UI")]
    [Tooltip("픽업 프리펩 자식의 Canvas UI 루트를 연결.")]
    [SerializeField] private GameObject hintUI;
    [Tooltip("힌트 UI / 아웃라인 표시 거리 (m)")]
    [SerializeField] private float hintRadius = 3f;
    [Tooltip("범위 경계에서 들어왔다 나갔다 할 때 깜빡이는 것을 막기 위한 여유 거리(m). 한 번 '근접'으로 판정되면 hintRadius + 이 값을 벗어나야 '근접 해제'로 바뀜.")]
    [SerializeField] private float nearExitBuffer = 0.4f;

    [Header("해금 대기 / 즉시완료 (파밍 상자와 동일)")]
    [Tooltip("F로 걸어두면 이 시간(초) 뒤 '해금 가능'이 된다. 자리를 비워도 카운트됨.")]
    [SerializeField] private float openTimeSeconds = 20f;
    [Tooltip("즉시완료(G) 시 소모 HP = 해금까지 '남은 시간' * 이 배율. 배율 2면 20초 남았을 때 40HP(=40초) 소모, 시간이 줄면 비용도 함께 줄어든다.")]
    [SerializeField] private float instantHpCostMultiplier = 2f;

    [Header("깜빡임 효과")]
    [SerializeField] private float flashDuration = 0.4f;
    [SerializeField] private int   flashCount    = 3;

    [Header("사라짐 효과")]
    [SerializeField] private float vanishDuration = 0.7f;
    [SerializeField] private AnimationCurve vanishCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("상태 표시 (임시 — 디자인 나오면 교체)")]
    [SerializeField] private float indicatorHeight = 1.9f;
    [SerializeField] private float indicatorWidthPx = 180f;

    [Header("시야 체크")]
    [Tooltip("플레이어 눈높이 오프셋 (m)")]
    [SerializeField] private float playerEyeHeight = 1.4f;
    [Tooltip("이 거리(m)보다 가까우면 시야 체크를 건너뛴다. 너무 가까이서 레이가 오브젝트/바닥에 막혀 힌트가 꺼지는 것 방지.")]
    [SerializeField] private float lineOfSightSkipDistance = 1.5f;

    // ─────────────────────────────────────────────────────────────

    // 파밍 상자와 동일한 상태머신
    private enum State { Idle, Opening, Ready }
    private State _state = State.Idle;
    private float _timer;

    private bool              _collected    = false;
    private bool              _interacting  = false;  // 최종 해금 연출 중
    private bool              _playerNearby = false;
    private bool              _tmpInitialized = false;
    private Outline[]         _outlines;
    private FacilitySelectRow _row;
    private Transform         _playerTransform;
    private Player            _player;

    // 즉시완료까지 '남은' 대기 시간(초). Idle=아직 안 걸어둠 → 전체 시간, Opening=현재 타이머 잔여.
    // 매초 단위(올림)로 계산 — 0.5초 같은 소수 단위가 아니라 정수 초 기준으로만 비용이 바뀐다.
    private float RemainingSeconds =>
        Mathf.Ceil(_state == State.Opening ? _timer : openTimeSeconds);
    // 즉시완료 비용 = 남은 시간 * 배율. 타이머가 줄면 비용도 비례해서 줄어든다(매 프레임 재계산).
    private float InstantCost => Mathf.Max(0f, RemainingSeconds * instantHpCostMultiplier);

    // 진행 상태 표시 UI (런타임 생성)
    private RectTransform _indRoot;
    private Image         _indFill;
    private TMP_Text      _indText;
    private Transform     _camTr;

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
        if (player != null) { _player = player; _playerTransform = player.transform; }
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

    private bool _arrowShown = false;

    private void Update()
    {
        if (_collected || _interacting || _playerTransform == null) return;

        // 해금 퀘스트가 이 설비를 가리키는 동안 위치 화살표 안내 (거리 무관 — 멀리서도 찾게)
        UpdateUnlockArrow();

        // ── 해금 타이머 진행 (자리 비워도 카운트) ─────────────────────
        if (_state == State.Opening)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = 0f;
                _state = State.Ready;
            }
        }

        // 다른 UI가 열려 있으면 강제 숨김
        bool uiBlocking = GameUIController.Instance != null
                       && GameUIController.Instance.IsUIBlocking();

        float distToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        // 경계에서 미세하게 들어왔다 나갔다 해도 깜빡이지 않도록 히스테리시스 적용.
        float effectiveRadius = _playerNearby ? hintRadius + nearExitBuffer : hintRadius;
        bool nearby = !uiBlocking
                   && distToPlayer <= effectiveRadius
                   // 아주 가까우면 시야 체크 생략 (근접 시 레이가 막혀 힌트가 꺼지는 문제 방지)
                   && (distToPlayer <= lineOfSightSkipDistance || HasLineOfSight());

        bool nearbyChanged = nearby != _playerNearby;
        _playerNearby = nearby;

        RefreshIndicator();

        if (!nearbyChanged) return;

        SetOutlineEnabled(nearby);

        if (nearby)
        {
            if (hintUI != null)
            {
                // TMP Awake()가 최초 1회만 실행되도록 처음엔 강제 활성화 후 즉시 숨김
                if (!_tmpInitialized)
                {
                    hintUI.SetActive(true);
                    ApplyFacilityName();
                    _tmpInitialized = true;
                    hintUI.SetActive(false);
                }
            }
        }
        else
        {
            if (hintUI != null) hintUI.SetActive(false);
        }
    }

    // ── IInteractable (F 상호작용) ────────────────────────────────
    // 해금 안 됐고 해금 연출 중이 아니면 항상 상호작용 가능 (Opening 중에도 G 디스패치 위해 true 유지)
    // 주의: 퀘스트 화살표(UpdateUnlockArrow)도 이 값을 쓰며 "거리 무관 — 멀리서도 찾게" 동작해야
    // 하므로 여기엔 거리 조건을 넣지 않는다. F/G의 실제 거리 게이트는 Interact/OnInstantComplete에서 처리.
    public bool CanInteract =>
        !_collected && !_interacting
        && FacilityUnlockManager.Instance != null
        && !FacilityUnlockManager.Instance.IsUnlocked(facilityId);

    public void Interact(Player player)
    {
        if (!CanInteract || player == null) return;
        // F가 통하는 거리와 힌트 UI가 뜨는 거리를 일치시킨다(물리 콜라이더 형태 때문에
        // PlayerInteractComponent의 OverlapSphere가 hintRadius 밖에서도 감지하는 경우 방지).
        if (!_playerNearby) return;

        switch (_state)
        {
            case State.Idle:
                _state = State.Opening;   // F로 걸어두기
                _timer = openTimeSeconds;
                ToastManager.Info("해금을 시작합니다.");
                GameSfx.Play(SfxId.FacilityUnlockStart, transform.position);   // 설비 해금 시작음(3D)
                RefreshIndicator();
                break;

            case State.Opening:
                // 이미 여는 중 — 그냥 대기 (아무것도 안 함)
                break;

            case State.Ready:
                BeginUnlock();            // 다시 와서 F → 해금
                break;
        }
    }

    // ── IInstantInteractable (G 즉시완료) ─────────────────────────
    public bool CanInstantComplete(Player player)
    {
        if (player == null) return false;
        if (_state != State.Idle && _state != State.Opening) return false;
        if (!_playerNearby) return false;   // F와 동일하게 힌트 UI 표시 거리에 맞춰 게이트
        return player.Stat.CurrentHp > InstantCost; // 비용보다 많아야 (즉시완료로 죽지 않게)
    }

    public void OnInstantComplete(Player player)
    {
        if (player == null || !CanInstantComplete(player))
        {
            return;
        }
        player.Stat.SpendHp(InstantCost);
        BeginUnlock();
    }

    // ── 해금 시작 (깜빡임 → 해금 → 사라짐) ────────────────────────
    private void BeginUnlock()
    {
        if (_interacting) return;
        _interacting = true;
        GameSfx.Play(SfxId.FacilityUnlockComplete, transform.position);   // 설비 해금 완료음(F/G 둘 다 여기 경유, 3D)
        _arrowShown = false;
        TimeKov.UI.HintArrowManager.I?.Hide("facility_pickup_" + facilityId);
        HideIndicator();
        FacilityPromptUI.Instance?.Hide();
        if (hintUI != null) hintUI.SetActive(false);
        StartCoroutine(FlashThenUnlock());
    }

    // ── 해금 퀘스트 화살표 ────────────────────────────────────────
    private void UpdateUnlockArrow()
    {
        var mgr = TimeKov.UI.HintArrowManager.I;
        if (mgr == null) return;
        bool show = CanInteract && IsUnlockQuestActive();
        if (show == _arrowShown) return;
        _arrowShown = show;
        string arrowId = "facility_pickup_" + facilityId;
        if (show) mgr.Show(arrowId, transform, 0f);
        else       mgr.Hide(arrowId);
    }

    private bool IsUnlockQuestActive()
    {
        var qm = QuestManager.Instance;
        if (qm == null || !qm.IsReady) return false;
        foreach (var rt in qm.Runtimes)
        {
            if (rt?.activeObjectives == null) continue;
            foreach (var obj in rt.activeObjectives)
                if (obj is FacilityUnlockObjective u && !u.IsCompleted
                    && (u.facilityId == 0 || u.facilityId == facilityId))
                    return true;
        }
        return false;
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

    // ── 진행 상태 표시 ────────────────────────────────────────────
    private void RefreshIndicator()
    {
        var ui = FacilityPromptUI.GetOrCreate();

        if (!_playerNearby) { ui.HideIfOwner(this); HideIndicator(); return; }

        switch (_state)
        {
            case State.Idle:
                ui.ShowIdle(this, InstantCost,
                    () => Interact(_player),
                    () => OnInstantComplete(_player));
                HideIndicator();
                break;
            case State.Opening:
            {
                float prog = openTimeSeconds > 0f ? 1f - _timer / openTimeSeconds : 1f;
                ui.ShowOpening(this, prog, _timer, InstantCost, () => OnInstantComplete(_player));
                HideIndicator();
                break;
            }
            case State.Ready:
                ui.ShowReady(this, () => Interact(_player));
                HideIndicator();
                break;
        }
    }

    private void HideIndicator()
    {
        if (_indRoot != null) _indRoot.gameObject.SetActive(false);
    }

    private void EnsureIndicator()
    {
        if (_indRoot != null) return;

        // 설비를 작게(0.1~0.2) 스케일해 배치해도 인디케이터가 그 스케일을 물려받아
        // 설비 안에 묻히지 않도록, 픽업의 자식이 아니라 월드 공간 독립 오브젝트로 만든다.
        // (위치/회전은 LateUpdate에서 픽업 위에 직접 고정, 파괴 시 OnDestroy에서 정리)
        var go = new GameObject("UnlockStatusUI");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _indRoot = (RectTransform)go.transform;
        _indRoot.sizeDelta = new Vector2(indicatorWidthPx, 64f);
        _indRoot.localScale = Vector3.one * 0.01f;   // 부모 스케일 비상속 → 항상 일정 크기
        _indRoot.position = transform.position + Vector3.up * indicatorHeight;

        // 텍스트 (위)
        var txtGo = new GameObject("Text", typeof(RectTransform));
        var trt = (RectTransform)txtGo.transform;
        trt.SetParent(_indRoot, false);
        trt.anchorMin = new Vector2(0, 0); trt.anchorMax = new Vector2(1, 1);
        trt.offsetMin = new Vector2(0, 14); trt.offsetMax = Vector2.zero;
        _indText = txtGo.AddComponent<TextMeshProUGUI>();
        _indText.alignment = TextAlignmentOptions.Bottom;
        _indText.fontSize = 16f;
        _indText.color = new Color(1f, 0.86f, 0.4f, 1f);
        _indText.fontStyle = FontStyles.Bold;
        _indText.raycastTarget = false;

        // 진행 바 (아래)
        var bgGo = new GameObject("BarBG", typeof(RectTransform));
        var brt = (RectTransform)bgGo.transform;
        brt.SetParent(_indRoot, false);
        brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0); brt.pivot = new Vector2(0.5f, 0f);
        brt.sizeDelta = new Vector2(-10, 10); brt.anchoredPosition = Vector2.zero;
        var bg = bgGo.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.07f, 0.1f, 0.8f);
        bg.raycastTarget = false;

        var fillGo = new GameObject("BarFill", typeof(RectTransform));
        var frt = (RectTransform)fillGo.transform;
        frt.SetParent(bgGo.transform, false);
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        _indFill = fillGo.AddComponent<Image>();
        _indFill.sprite = UISpriteFactory.RoundedRect(16, 4);
        _indFill.type = Image.Type.Filled;
        _indFill.fillMethod = Image.FillMethod.Horizontal;
        _indFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _indFill.color = new Color(1f, 0.78f, 0.18f, 1f);
        _indFill.fillAmount = 0f;
        _indFill.raycastTarget = false;

        _indRoot.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_indRoot == null || !_indRoot.gameObject.activeSelf) return;

        // 독립 오브젝트라 위치를 매 프레임 픽업 위에 직접 고정 (월드 높이 — 스케일 무관)
        _indRoot.position = transform.position + Vector3.up * indicatorHeight;

        if (_camTr == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            _camTr = cam.transform;
        }
        _indRoot.forward = _camTr.forward;
    }

    private void OnDestroy()
    {
        // 인디케이터는 픽업의 자식이 아니므로(스케일 비상속) 직접 파괴해 잔존 방지
        if (_indRoot != null) Destroy(_indRoot.gameObject);
        // 씬 종료 중엔 GetOrCreate 로 새로 만들지 말 것(정리 안 됨 에러). 있을 때만 숨김.
        FacilityPromptUI.Instance?.HideIfOwner(this);
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
