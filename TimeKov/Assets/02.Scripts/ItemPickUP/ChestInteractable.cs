// =====================================================================
// ChestInteractable.cs
// 파밍 상자 — F로 "걸어두면" openTime초 뒤 '수령 가능'(자리 비워도 카운트),
// 돌아와서 F로 수령(전리품 패널). 기다리기 싫으면 G로 즉시완료(HP=시간 소모).
// 한 번 연 상자는 전리품이 남아있는 동안 F로 쿨 없이 즉시 다시 열린다.
// 다 비우면 다시 걸어둬야(쿨) 채워진다.
// =====================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [Tooltip("즉시완료(G) 시 소모 HP = openTime * 이 배율. 2면 20초 상자에 40HP(=40초) 소모.")]
    [SerializeField] private float instantHpCostMultiplier = 2f;

    [Header("프롬프트 / 상태 표시 (임시 — 디자인 나오면 교체)")]
    [Tooltip("플레이어가 이 거리(m) 안이면 'F 눌러서 열기' 프롬프트를 띄운다.")]
    [SerializeField] private float promptRange = 2.6f;
    [SerializeField] private float indicatorHeight = 1.9f;
    [SerializeField] private float indicatorWidthPx = 170f;

    // ── 상태 ──────────────────────────────────────────────────────────
    // Idle=잠김(걸어두기 전) / Opening=자물쇠 따는 중 / Ready=수령 가능 / Opened=잠금해제(자유 개폐, 재굴림 안 함)
    private enum State { Idle, Opening, Ready, Opened }
    private State _state = State.Idle;
    private float _timer;

    // 공용 상자 인벤(ChestInstance)을 현재 점유한 상자. 다른 상자를 열면 소유권이 넘어간다.
    private static ChestInteractable _activeChest;
    // 이 상자가 1회 굴린 전리품(가져간 만큼 빠짐). null=아직 안 엶(잠김). 한 번 열면 재굴림 없이 이걸 표시.
    private System.Collections.Generic.List<(int itemId, int count)> _contents;

    private float InstantCost => Mathf.Max(0f, openTimeSeconds * instantHpCostMultiplier);

    // 상태 표시 UI
    private RectTransform _indRoot;
    private Image _indFill;
    private GameObject _indBar;
    private TMP_Text _indText;
    private Transform _camTr;
    private Player _player;

    // 인벤토리 UI 열려있을 때만 차단. 그 외엔 항시 F 가능(걸어두기/수령/즉시 재오픈).
    public bool CanInteract
    {
        get
        {
            var inv = InventoryUIController.Instance;
            return inv == null || !inv.IsOpen;
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
                break;

            case State.Opening:
                break;   // 여는 중 — 대기

            case State.Ready:
                Collect(player);             // 수령(롤 + 패널 열기)
                break;

            case State.Opened:
                ReopenPanel();               // 이미 연 상자 — 쿨 없이 즉시 다시 열기
                break;
        }
        RefreshIndicator();
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
            if (_timer <= 0f) { _timer = 0f; _state = State.Ready; }
        }
        // Opened(잠금해제)는 절대 Idle로 안 돌아간다 = 재굴림 없음(다 비우면 빈 채로 유지).
        RefreshIndicator();
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

        var chestInv = InventoryManager.ChestInstance;
        if (chestInv != null)
        {
            chestInv.ClearAllItems();
            if (_contents != null)
                foreach (var (itemId, count) in _contents)
                    chestInv.AddItem(itemId, count);
        }
        _activeChest = this;

        InventoryUIController.IsChestOpen = true;
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

    private void OnDisable()
    {
        HideIndicator();
    }

    // ── 프롬프트 / 상태 표시 ─────────────────────────────────────────────
    private void RefreshIndicator()
    {
        var inv = InventoryUIController.Instance;
        bool invOpen = inv != null && inv.IsOpen;

        if (_state == State.Opening)
        {
            ShowIndicator(true);
            if (_indFill != null) _indFill.fillAmount = openTimeSeconds > 0f ? 1f - _timer / openTimeSeconds : 1f;
            if (_indText != null) _indText.text = $"여는 중 {Mathf.CeilToInt(_timer)}초\nG 즉시 (HP -{Mathf.CeilToInt(InstantCost)})";
            return;
        }
        if (_state == State.Ready)
        {
            ShowIndicator(true);
            if (_indFill != null) _indFill.fillAmount = 1f;
            if (_indText != null) _indText.text = "F 로 수령";
            return;
        }

        // Idle / Opened = 'F 눌러서 열기' 프롬프트 (근접 시에만, 진행바 없음)
        if (invOpen || !IsPlayerNear())
        {
            HideIndicator();
            return;
        }
        ShowIndicator(false);
        if (_indText != null) _indText.text = "F 눌러서 열기";
    }

    private bool IsPlayerNear()
    {
        if (_player == null) _player = FindAnyObjectByType<Player>();
        if (_player == null) return false;
        return (_player.transform.position - transform.position).sqrMagnitude <= promptRange * promptRange;
    }

    private void ShowIndicator(bool withBar)
    {
        EnsureIndicator();
        _indRoot.gameObject.SetActive(true);
        if (_indBar != null) _indBar.SetActive(withBar);
    }

    private void HideIndicator()
    {
        if (_indRoot != null) _indRoot.gameObject.SetActive(false);
    }

    private void EnsureIndicator()
    {
        if (_indRoot != null) return;

        var go = new GameObject("ChestStatusUI");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * indicatorHeight;
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _indRoot = (RectTransform)go.transform;
        _indRoot.sizeDelta = new Vector2(indicatorWidthPx, 64f);
        _indRoot.localScale = Vector3.one * 0.01f;

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

        // 진행 바 (아래) — 걸어두기/수령 때만 표시, 프롬프트일 땐 숨김
        var bgGo = new GameObject("BarBG", typeof(RectTransform));
        var brt = (RectTransform)bgGo.transform;
        brt.SetParent(_indRoot, false);
        brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0); brt.pivot = new Vector2(0.5f, 0f);
        brt.sizeDelta = new Vector2(-10, 10); brt.anchoredPosition = Vector2.zero;
        var bg = bgGo.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.07f, 0.1f, 0.8f);
        bg.raycastTarget = false;
        _indBar = bgGo;

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
        if (_camTr == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            _camTr = cam.transform;
        }
        _indRoot.forward = _camTr.forward;
    }

    // ── 드롭 롤 ───────────────────────────────────────────────────────
    private List<(int itemId, int count)> Roll()
    {
        var result  = new List<(int itemId, int count)>();
        string myId = (sourceId ?? "").Trim();
        if (myId.Length == 0) return result;

        var pool = new List<DropTableSheetData>();
        foreach (var row in GameDataHolder.I.DropTable.All)
        {
            string rowId = (row.sourceId ?? "").Trim();
            if (row.sourceType == SourceType.Chest && rowId == myId)
                pool.Add(row);
        }
        if (pool.Count == 0) return result;

        int pickCount = Mathf.Max(1, pool[0].pickCount);
        var available = new List<DropTableSheetData>(pool);

        for (int p = 0; p < pickCount && available.Count > 0; p++)
        {
            DropTableSheetData picked = WeightedPick(available);
            available.Remove(picked);

            int itemId = ExtractItemId(picked.SheetId);
            if (itemId <= 0)
            {
                Debug.LogWarning($"[Chest] itemId 추출 실패 — SheetId='{picked.SheetId}'.");
                continue;
            }
            if (GameDataUtility.GetItem(itemId) == null)
            {
                Debug.LogWarning($"[Chest] itemId={itemId} — ItemData에 없음.");
                continue;
            }

            int count = Random.Range(picked.minCount, picked.maxCount + 1);
            if (count > 0) result.Add((itemId, count));
        }
        return result;
    }

    private DropTableSheetData WeightedPick(List<DropTableSheetData> pool)
    {
        int total = 0;
        foreach (var r in pool) total += Mathf.Max(0, r.dropWeight);
        if (total <= 0) return pool[0];

        int rand = Random.Range(0, total);
        int acc  = 0;
        foreach (var r in pool)
        {
            acc += Mathf.Max(0, r.dropWeight);
            if (rand < acc) return r;
        }
        return pool[pool.Count - 1];
    }

    private int ExtractItemId(DropTableSheetId sheetId)
    {
        string s = sheetId;
        int u = s.LastIndexOf('_');
        if (u < 0 || u + 1 >= s.Length) return 0;
        return int.TryParse(s.Substring(u + 1), out int id) ? id : 0;
    }
}
