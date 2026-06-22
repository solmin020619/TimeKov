// =====================================================================
// ChestInteractable.cs
// 파밍 상자 — F로 "걸어두면" openTime초 뒤 '수령 가능'(자리 비워도 카운트),
// 돌아와서 F로 수령(전리품 패널). 기다리기 싫으면 G로 즉시완료(HP=시간 소모).
// 한 번 연 상자는 전리품이 남아있는 동안 F로 쿨 없이 즉시 다시 열린다.
// 다 비우면 다시 걸어둬야(쿨) 채워진다.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;

public class ChestInteractable : MonoBehaviour, IInstantInteractable
{
    [Header("드롭 설정")]
    [Tooltip("DropTable의 sourceId (예: LC_LOOT). sourceType=Chest 행과 매칭됨")]
    [SerializeField] private string sourceId = "LC_LOOT";

    [Header("비주얼 (선택)")]
    [SerializeField] private GameObject closedVisual;
    [SerializeField] private GameObject openedVisual;

    [Header("인터랙션")]
    [Tooltip("기지 결계 밖에서도 열 수 있는지")]
    [SerializeField] private bool requireBase = false;

    [Header("열기 / 즉시완료")]
    [Tooltip("F로 걸어두면 이 시간(초) 뒤 '수령 가능'이 된다. 자리를 비워도 카운트됨.")]
    [SerializeField] private float openTimeSeconds = 20f;
    [Tooltip("즉시완료(G) 시 소모 HP = 해금까지 '남은 시간' * 이 배율. 배율 2면 20초 남았을 때 40HP(=40초) 소모, 시간이 줄면 비용도 함께 줄어든다.")]
    [SerializeField] private float instantHpCostMultiplier = 2f;

    [Header("리젠 (파밍 후 사라졌다 재등장)")]
    [Tooltip("다 비운 상자가 사라진 뒤 이 시간(초) 후 새 상자로 재등장(Idle 복귀 - 처음부터 다시 까야 함).")]
    [SerializeField] private float respawnSeconds = 300f;

    [Header("프롬프트")]
    [SerializeField] private float promptRange = 2.6f;
    [Tooltip("범위 경계에서 들어왔다 나갔다 할 때 깜빡이는 것을 막기 위한 여유 거리(m). 한 번 '근접'으로 판정되면 promptRange + 이 값을 벗어나야 '근접 해제'로 바뀜.")]
    [SerializeField] private float nearExitBuffer = 0.4f;

    // ── 상태 ──────────────────────────────────────────────────────────
    // Idle=잠김(걸어두기 전) / Opening=자물쇠 따는 중 / Ready=수령 가능 / Opened=잠금해제(자유 개폐, 재굴림 안 함) / Depleted=다 비워 사라짐(리젠 대기)
    private enum State { Idle, Opening, Ready, Opened, Depleted }
    private State _state = State.Idle;
    private float _timer;
    private Collider[] _colliders;   // 리젠 동안 사라진 자리 통과 가능하게 토글
    private Renderer[] _renderers;   // 소멸 시 메시 숨김(closedVisual/openedVisual 미할당이어도 확실히 사라지게)

    // 공용 상자 인벤(ChestInstance)을 현재 점유한 상자. 다른 상자를 열면 소유권이 넘어간다.
    private static ChestInteractable _activeChest;
    // 이 상자가 1회 굴린 전리품(가져간 만큼 빠짐). null=아직 안 엶(잠김). 한 번 열면 재굴림 없이 이걸 표시.
    private System.Collections.Generic.List<(int itemId, int count)> _contents;

    // 즉시완료까지 '남은' 대기 시간(초). Idle=아직 안 걸어둠 → 전체 시간, Opening=현재 타이머 잔여.
    // 매초 단위(올림)로 계산 — 0.5초 같은 소수 단위가 아니라 정수 초 기준으로만 비용이 바뀐다.
    private float RemainingSeconds =>
        Mathf.Ceil(_state == State.Opening ? _timer : openTimeSeconds);
    // 즉시완료 비용 = 남은 시간 * 배율. 타이머가 줄면 비용도 비례해서 줄어든다(매 프레임 재계산).
    private float InstantCost => Mathf.Max(0f, RemainingSeconds * instantHpCostMultiplier);

    private Player  _player;

    [Header("강조 발광 (가까이 가면 상자가 노랗게 맥동 — 큰 오브젝트용)")]
    [SerializeField] private Color glowColor        = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private float glowMinIntensity = 0.12f;
    [SerializeField] private float glowMaxIntensity = 0.9f;
    [SerializeField] private float glowPulseSpeed   = 3.5f;

    private Material[] _glowMats;
    private Color[]    _glowOrigEmission;
    private bool       _glowOn;

    private void Awake()
    {
        _colliders = GetComponentsInChildren<Collider>(true);
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Start()
    {
        _player = FindAnyObjectByType<Player>();
        SetupGlow();
    }

    // 인벤토리 UI가 열려있거나 promptRange 밖이면 차단. 그 외엔 항시 F 가능(걸어두기/수령/즉시 재오픈).
    public bool CanInteract
    {
        get
        {
            var inv = InventoryUIController.Instance;
            if (inv != null && inv.IsOpen) return false;
            // 프롬프트 UI가 뜨는 거리와 F가 통하는 거리를 일치시킨다(물리 콜라이더 형태 때문에
            // OverlapSphere가 promptRange 밖에서도 감지하는 경우가 있어, 여기서 최종 거리 검증).
            return IsPlayerNear();
        }
    }

    // ── F 상호작용 ──────────────────────────────────────────────────────
    public void Interact(Player player)
    {
        if (player == null) return;

        switch (_state)
        {
            case State.Idle:
                if (requireBase && !player.Stat.IsInBase)
                {
                    Debug.Log("[Chest] 기지 내부에서만 열 수 있습니다.");
                    return;
                }
                _state = State.Opening;       // F로 걸어두기
                _timer = openTimeSeconds;
                SetVisual(false);
                GameSfx.Play(SfxId.ChestOpenStart, transform.position);   // 상자 열기 시작음(3D)
                break;

            case State.Opening:
                break;   // 여는 중 — 대기

            case State.Ready:
                Collect(player);             // 수령(롤 + 패널 열기)
                break;

            case State.Opened:
                if (HasItems())
                    ReopenPanel();           // 아이템 남아있을 때만 다시 열기
                break;
        }
        RefreshIndicator(IsPlayerNear());
    }

    // ── G 즉시완료 (IInstantInteractable) ───────────────────────────────
    public bool CanInstantComplete(Player player)
    {
        if (player == null) return false;
        if (_state != State.Idle && _state != State.Opening) return false;   // 걸어두기 전/도중만 즉시완료
        return player.Stat.CurrentHp > InstantCost; // 비용보다 많아야 (즉시완료로 죽지 않게)
    }

    public void OnInstantComplete(Player player)
    {
        if (player == null || !CanInstantComplete(player))
        {
            Debug.Log("[Chest] 즉시완료 불가 — 시간(HP) 부족.");
            return;
        }
        if (requireBase && !player.Stat.IsInBase) return;
        player.Stat.SpendHp(InstantCost);
        Collect(player);
    }

    // ── 매 프레임 ─────────────────────────────────────────────────────────
    private void Update()
    {
        if (_state == State.Opening)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) { _timer = 0f; _state = State.Ready; GameSfx.Play(SfxId.ChestOpenComplete, transform.position); }   // 상자 열기 완료음(잠금 풀림, 3D)
        }
        else if (_state == State.Opened && !HasItems()
                 && !(InventoryUIController.IsChestOpen && _activeChest == this))
        {
            // 다 비운 상자 -> 패널 닫은 뒤 사라지고 리젠 대기(한 번 판 상자는 그 자리서 소멸).
            EnterDepleted();
        }
        else if (_state == State.Depleted)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) Respawn();   // 새 상자로 재등장(Idle)
        }

        bool near = IsPlayerNear();
        RefreshIndicator(near);
        bool highlight = near && _state != State.Depleted && !(_state == State.Opened && !HasItems());
        // 해금 중엔 범위 밖이어도 연한 펄스로 표시 유지.
        bool openingRemote = _state == State.Opening && !near;
        UpdateGlow(highlight, openingRemote);
    }

    // ── 수령: 자물쇠 따서 전리품 1회만 굴림 + 패널. 이후 이 상자는 잠금해제(재굴림 없음). ──
    private void Collect(Player player)
    {
        if (InventoryUIController.IsChestOpen)
            InventoryUIController.IsChestOpen = false;

        var items = Roll();
        if (items.Count == 0)
            Debug.LogWarning($"[Chest] sourceId='{sourceId}' — DropTable에 Chest 항목 없음");
        _contents = items;   // 이 상자 전리품 저장(딱 한 번만 굴림. 이후 재굴림 X)

        _state = State.Opened;
        SetVisual(true);
        OpenWithMyContents();
    }

    // 이미 연(잠금해제) 상자를 재굴림 없이 다시 열기 (껐다 켜도 같은 내용, 가져간 만큼 빠진 채로)
    private void ReopenPanel()
    {
        OpenWithMyContents();
    }

    // 현재 표시중 내용을 그 상자에 보존 -> 내 전리품을 ChestInstance에 로드 -> 패널 열기
    private void OpenWithMyContents()
    {
        SaveActiveChestContents();   // 직전 활성 상자(또는 나 자신)의 가져간-반영 상태 보존

        _activeChest = this;
        InventoryUIController.IsChestOpen = true;   // 아이템 로드 전에 먼저 설정 (이벤트 타이밍 대비)

        var chestInv = InventoryManager.ChestInstance;
        if (chestInv != null)
        {
            chestInv.ClearAllItems();
            if (_contents != null)
                foreach (var (itemId, count) in _contents)
                    chestInv.AddItem(itemId, count);
        }

        InventoryUIController.Instance?.Open();
    }

    // ChestInstance의 현재 내용을 활성 상자의 _contents에 저장(플레이어가 가져간 만큼 반영). 재오픈 시 복제 방지.
    private static void SaveActiveChestContents()
    {
        if (_activeChest == null) return;
        var c = InventoryManager.ChestInstance;
        if (c == null) return;
        var list = new System.Collections.Generic.List<(int itemId, int count)>();
        foreach (var slot in c.GetSlots())
            if (!slot.IsEmpty) list.Add((slot.itemId, slot.amount));
        _activeChest._contents = list;
    }

    private void SetVisual(bool opened)
    {
        if (closedVisual != null) closedVisual.SetActive(!opened);
        if (openedVisual != null) openedVisual.SetActive(opened);
    }

    // 다 비운 상자: 사라짐(비주얼 둘 다 숨김 + 콜라이더 off로 자리 통과 가능) + 리젠 타이머 시작.
    private void EnterDepleted()
    {
        _state = State.Depleted;
        _timer = respawnSeconds;
        _contents = null;
        if (_activeChest == this) _activeChest = null;
        if (closedVisual != null) closedVisual.SetActive(false);
        if (openedVisual != null) openedVisual.SetActive(false);
        if (_colliders != null)
            foreach (var c in _colliders) if (c != null) c.enabled = false;
        if (_renderers != null)
            foreach (var r in _renderers) if (r != null) r.enabled = false;   // 메시 숨김(비주얼 미할당이어도 확실히 사라지게)
        UpdateGlow(false);
        ChestPromptUI.Instance?.HideIfOwner(this);
    }

    // 리젠: 새 상자로 다시 등장(Idle = 잠김). 처음부터 다시 까야 함 = 새로운 상자 개념.
    private void Respawn()
    {
        _state = State.Idle;
        _timer = 0f;
        _contents = null;
        if (_colliders != null)
            foreach (var c in _colliders) if (c != null) c.enabled = true;
        if (_renderers != null)
            foreach (var r in _renderers) if (r != null) r.enabled = true;   // 메시 복귀
        SetVisual(false);   // closedVisual on(잠긴 새 상자), openedVisual off
    }

    private void OnDisable()
    {
        // 씬 종료/비활성 중엔 GetOrCreate 로 새로 만들지 말 것(정리 안 됨 에러). 있을 때만 숨김.
        ChestPromptUI.Instance?.HideIfOwner(this);
        if (_glowOn) UpdateGlow(false);   // 발광 켜진 채 남지 않도록 원복
    }

    // ── 프롬프트 / 상태 표시 ─────────────────────────────────────────────
    private void RefreshIndicator(bool near)
    {
        // GameUIController.IsUIBlocking() = 설정/인벤/퀘스트/팩토리/빌드 등 모든 UI 상태 포함
        bool anyUIOpen = (GameUIController.Instance != null && GameUIController.Instance.IsUIBlocking())
                      || (InventoryUIController.Instance != null && InventoryUIController.Instance.IsOpen);
        var ui = ChestPromptUI.GetOrCreate();

        if (anyUIOpen) { ui.HideIfOwner(this); return; }

        switch (_state)
        {
            case State.Opening:
                if (!near) { ui.HideIfOwner(this); return; }
                float prog = openTimeSeconds > 0f ? 1f - _timer / openTimeSeconds : 1f;
                ui.ShowOpening(this, prog, _timer, InstantCost, () => OnInstantComplete(_player));
                break;
            case State.Ready:
                if (!near) { ui.HideIfOwner(this); return; }
                ui.ShowReady(this, () => Interact(_player));
                break;
            case State.Idle:
                if (!near) { ui.HideIfOwner(this); return; }
                ui.ShowIdle(this, InstantCost,
                    () => Interact(_player),
                    () => OnInstantComplete(_player));
                break;
            case State.Opened:
                if (!near || !HasItems()) { ui.HideIfOwner(this); return; }
                ui.ShowOpened(this, () => Interact(_player));
                break;
            default:
                ui.HideIfOwner(this);
                break;
        }
    }

    // ── 강조 발광 (B 방안) ────────────────────────────────────────────────
    // 가까이 가면 상자 메시가 노란빛으로 은은하게 맥동 → 큰 오브젝트에서 확실히 보임.

    private void SetupGlow()
    {
        var rends = GetComponentsInChildren<Renderer>(true);
        var mats  = new List<Material>();
        var orig  = new List<Color>();
        foreach (var r in rends)
        {
            if (r is ParticleSystemRenderer) continue;
            foreach (var m in r.materials)   // 인스턴스화 — 공유 재질/다른 상자에 영향 없음
            {
                if (m == null) continue;
                m.EnableKeyword("_EMISSION");
                mats.Add(m);
                orig.Add(m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black);
            }
        }
        _glowMats         = mats.ToArray();
        _glowOrigEmission = orig.ToArray();
    }

    private void UpdateGlow(bool show, bool pale = false)
    {
        if (_glowMats == null || _glowMats.Length == 0) return;

        if (show)
        {
            float t         = (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f;   // 0~1 맥동
            float intensity = Mathf.Lerp(glowMinIntensity, glowMaxIntensity, t);
            Color add       = glowColor * intensity;
            for (int i = 0; i < _glowMats.Length; i++)
                _glowMats[i].SetColor("_EmissionColor", _glowOrigEmission[i] + add);
            _glowOn = true;
        }
        else if (pale)
        {
            // 해금 중 범위 밖 — 맥동 없이 연하게 고정 발광.
            Color add = glowColor * (glowMinIntensity * 0.6f);
            for (int i = 0; i < _glowMats.Length; i++)
                _glowMats[i].SetColor("_EmissionColor", _glowOrigEmission[i] + add);
            _glowOn = true;
        }
        else if (_glowOn)
        {
            for (int i = 0; i < _glowMats.Length; i++)
                _glowMats[i].SetColor("_EmissionColor", _glowOrigEmission[i]);
            _glowOn = false;
        }
    }

    // 경계에서 미세하게 들어왔다 나갔다 해도 깜빡이지 않도록 히스테리시스 적용.
    // 이미 근접 상태면 promptRange + nearExitBuffer를 벗어나야 비로소 해제된다.
    private bool _near;

    private bool IsPlayerNear()
    {
        if (_player == null) { _near = false; return false; }
        float range = _near ? promptRange + nearExitBuffer : promptRange;
        _near = (_player.transform.position - transform.position).sqrMagnitude <= range * range;
        return _near;
    }

    // 현재 이 상자에 남아있는 아이템이 있는지 확인
    // _activeChest == this면 ChestInstance(실시간)를 직접 확인, 아니면 저장된 _contents 사용
    private bool HasItems()
    {
        if (_activeChest == this)
        {
            var ci = InventoryManager.ChestInstance;
            if (ci == null) return false;
            foreach (var slot in ci.GetSlots())
                if (!slot.IsEmpty) return true;
            return false;
        }
        return _contents != null && _contents.Count > 0;
    }

    // ── 드롭 롤 ───────────────────────────────────────────────────────
    private List<(int itemId, int count)> Roll()
    {
        var result  = new List<(int itemId, int count)>();
        string myId = (sourceId ?? "").Trim();
        if (myId.Length == 0) return result;

        // 각 행(아이템)을 독립적으로 굴린다 - dropChance% 로 드롭, 뜨면 개수 판정. 행끼리 서로 영향 없음.
        foreach (var row in GameDataHolder.I.DropTable.All)
        {
            string rowId = (row.sourceId ?? "").Trim();
            if (row.sourceType != SourceType.Chest || rowId != myId) continue;

            if (Random.Range(0, 100) >= row.dropChance) continue;   // 드롭 실패

            int itemId = ExtractItemId(row.SheetId);
            if (itemId <= 0) continue;
            if (GameDataUtility.GetItem(itemId) == null) continue;

            int count = GameDataUtility.RollDropCount(row.countDist);
            if (count > 0) result.Add((itemId, count));
        }
        return result;
    }

    private int ExtractItemId(DropTableSheetId sheetId)
    {
        string s = sheetId;
        int u = s.LastIndexOf('_');
        if (u < 0 || u + 1 >= s.Length) return 0;
        return int.TryParse(s.Substring(u + 1), out int id) ? id : 0;
    }
}
