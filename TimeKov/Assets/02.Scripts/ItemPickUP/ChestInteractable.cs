// =====================================================================
// ChestInteractable.cs
// 파밍 상자 — F로 "걸어두면" openTime초 뒤 '수령 가능' (자리 비워도 카운트),
// 돌아와서 F로 수령. 기다리기 싫으면 G로 즉시완료(HP=시간 소모).
// 수령 후 respawnTime초 뒤 재생성. 값은 박스별 인스펙터 조절.
// =====================================================================

using System.Collections;
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

    [Header("열기/즉시완료/리스폰")]
    [Tooltip("F로 걸어두면 이 시간(초) 뒤 '수령 가능'이 된다. 자리를 비워도 카운트됨.")]
    [SerializeField] private float openTimeSeconds = 20f;
    [Tooltip("즉시완료(G) 시 소모 HP = openTime * 이 배율. 2면 20초 상자에 40HP(=40초) 소모.")]
    [SerializeField] private float instantHpCostMultiplier = 2f;
    [Tooltip("수령 후 재생성까지 시간(초). 0이면 재생성 안 함.")]
    [SerializeField] private float respawnTimeSeconds = 30f;

    [Header("상태 표시 (임시 — 디자인 나오면 교체)")]
    [SerializeField] private float indicatorHeight = 1.9f;
    [SerializeField] private float indicatorWidthPx = 170f;

    // ── 상태 ──────────────────────────────────────────────────────────
    private enum State { Idle, Opening, Ready, Depleted }
    private State _state = State.Idle;
    private float _timer;
    private Coroutine _respawnCo;

    private float InstantCost => Mathf.Max(0f, openTimeSeconds * instantHpCostMultiplier);

    // 상태 표시 UI
    private RectTransform _indRoot;
    private Image _indFill;
    private TMP_Text _indText;
    private Transform _camTr;

    // 인벤토리 UI 열려있거나 고갈 상태면 차단
    public bool CanInteract
    {
        get
        {
            if (_state == State.Depleted) return false;
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
                break;

            case State.Opening:
                // 이미 여는 중 — 그냥 대기 (아무것도 안 함)
                break;

            case State.Ready:
                Collect(player);             // 다시 와서 F → 수령
                break;
        }
        RefreshIndicator();
    }

    // ── G 즉시완료 (IInstantInteractable) ───────────────────────────────
    public bool CanInstantComplete(Player player)
    {
        if (player == null) return false;
        if (_state != State.Idle && _state != State.Opening) return false;
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

    // ── 시간 경과 (자리 비워도 카운트) ──────────────────────────────────
    private void Update()
    {
        if (_state != State.Opening) return;
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _timer = 0f;
            _state = State.Ready;
        }
        RefreshIndicator();
    }

    // ── 수령(롤+지급+UI) ────────────────────────────────────────────────
    private void Collect(Player player)
    {
        if (InventoryUIController.IsChestOpen)
        {
            InventoryUIController.IsChestOpen = false;
            InventoryManager.ChestInstance?.ClearAllItems();
        }

        List<(int itemId, int count)> items = Roll();
        if (items.Count == 0)
            Debug.LogWarning($"[Chest] sourceId='{sourceId}' — DropTable에 Chest 항목 없음");

        var chestInv = InventoryManager.ChestInstance;
        if (chestInv != null)
        {
            chestInv.ClearAllItems();
            foreach (var (itemId, count) in items)
                chestInv.AddItem(itemId, count);
        }

        InventoryUIController.IsChestOpen = true;
        InventoryUIController.Instance?.Open();

        if (closedVisual != null) closedVisual.SetActive(false);
        if (openedVisual != null) openedVisual.SetActive(true);

        _state = State.Depleted;
        HideIndicator();
        if (respawnTimeSeconds > 0f)
            _respawnCo = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnTimeSeconds);
        _respawnCo = null;
        _state = State.Idle;
        if (closedVisual != null) closedVisual.SetActive(true);
        if (openedVisual != null) openedVisual.SetActive(false);
    }

    private void OnDisable()
    {
        if (_respawnCo != null) { StopCoroutine(_respawnCo); _respawnCo = null; }
        HideIndicator();
    }

    private void OnEnable()
    {
        if (_state == State.Depleted && respawnTimeSeconds > 0f && _respawnCo == null)
            _respawnCo = StartCoroutine(RespawnRoutine());
    }

    // ── 상태 표시 (임시 placeholder) ────────────────────────────────────
    private void RefreshIndicator()
    {
        if (_state == State.Opening)
        {
            EnsureIndicator();
            _indRoot.gameObject.SetActive(true);
            if (_indFill != null) _indFill.fillAmount = openTimeSeconds > 0f ? 1f - _timer / openTimeSeconds : 1f;
            if (_indText != null) _indText.text = $"여는 중 {Mathf.CeilToInt(_timer)}초\nG 즉시 (HP -{Mathf.CeilToInt(InstantCost)})";
        }
        else if (_state == State.Ready)
        {
            EnsureIndicator();
            _indRoot.gameObject.SetActive(true);
            if (_indFill != null) _indFill.fillAmount = 1f;
            if (_indText != null) _indText.text = "F 로 수령";
        }
        else
        {
            HideIndicator();
        }
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
