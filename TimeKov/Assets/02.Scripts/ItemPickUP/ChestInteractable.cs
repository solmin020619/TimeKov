// =====================================================================
// ChestInteractable.cs
// 씬에 배치하는 파밍 상자
// F키 꾹 눌러(openTime초) 열기 → DropTable(Chest) 롤 → 인벤토리 지급 → ChestOpenUI 팝업
// G키로 즉시완료(HP=시간 소모). 연 뒤 respawnTime초 후 재생성.
// 박스별 openTime/respawnTime/즉시비용 배율은 인스펙터에서 개별 조절.
// =====================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChestInteractable : MonoBehaviour, IHoldInteractable
{
    [Header("드롭 설정")]
    [Tooltip("DropTable의 sourceId (예: LC_LOOT). sourceType=Chest 행과 매칭됨")]
    [SerializeField] private string sourceId = "LC_LOOT";

    [Header("비주얼 (선택)")]
    [Tooltip("상자 열리기 전 모델/오브젝트")]
    [SerializeField] private GameObject closedVisual;
    [Tooltip("상자 열린 후 모델/오브젝트 (없으면 closedVisual 유지)")]
    [SerializeField] private GameObject openedVisual;

    [Header("인터랙션")]
    [Tooltip("기지 결계 밖에서도 열 수 있는지 여부")]
    [SerializeField] private bool requireBase = false;

    [Header("열기/즉시완료/리스폰")]
    [Tooltip("F를 꾹 눌러 여는 데 걸리는 시간(초). 0이면 즉시 열림(기존 방식).")]
    [SerializeField] private float openTimeSeconds = 5f;
    [Tooltip("즉시완료(G) 시 소모하는 HP = openTime * 이 배율. 2면 5초 상자 즉시열기에 10HP(=10초) 소모.")]
    [SerializeField] private float instantHpCostMultiplier = 2f;
    [Tooltip("연 뒤 다시 생성되기까지 시간(초). 0이면 재생성 안 함.")]
    [SerializeField] private float respawnTimeSeconds = 30f;

    [Header("진행 표시 (임시 — 디자인 링으로 교체 예정)")]
    [Tooltip("상자 위 진행 바 높이 오프셋(월드 단위)")]
    [SerializeField] private float progressHeight = 1.6f;
    [SerializeField] private float progressBarWidthPx = 120f;
    [SerializeField] private float progressBarHeightPx = 14f;

    // ── 상태 ──────────────────────────────────────────────────────────
    private bool _opened;   // 열린(고갈) 상태 — 리스폰 전까지 farm 불가
    private Coroutine _respawnCo;

    // 즉시완료 비용(HP = 시간)
    private float InstantCost => Mathf.Max(0f, openTimeSeconds * instantHpCostMultiplier);

    // 진행 바 런타임 오브젝트
    private RectTransform _progressRoot;
    private RectTransform _progressFill;
    private Transform _camTr;

    // 인벤토리 UI가 열려있거나 이미 연(고갈) 상태면 상호작용 차단
    public bool CanInteract
    {
        get
        {
            if (_opened) return false;
            var inv = InventoryUIController.Instance;
            if (inv == null) return true;
            return !inv.IsOpen;
        }
    }

    // ── IHoldInteractable ───────────────────────────────────────────────
    public float HoldDuration => openTimeSeconds;

    public bool CanInstantComplete(Player player)
    {
        if (player == null || _opened) return false;
        return player.Stat.CurrentHp > InstantCost;   // 비용보다 많아야 (즉시열기로 죽지 않게)
    }

    public void OnHoldProgress(float t01) => ShowProgress(t01);

    public void OnHoldComplete(Player player)
    {
        HideProgress();
        OpenChest(player);
    }

    public void OnHoldCancel() => HideProgress();

    public void OnInstantComplete(Player player)
    {
        HideProgress();
        if (player == null || _opened) return;
        if (!CanInstantComplete(player))
        {
            Debug.Log("[Chest] 즉시완료 불가 — 시간(HP)이 부족합니다.");
            return;
        }
        player.Stat.SpendHp(InstantCost);
        OpenChest(player);
    }

    // openTime이 0일 때 PlayerInteractComponent가 일반 경로로 호출 (즉시 열기)
    public void Interact(Player player) => OpenChest(player);

    // ── 상자 열기(롤+지급+UI) ───────────────────────────────────────────
    private void OpenChest(Player player)
    {
        if (player == null || _opened) return;

        if (requireBase && !player.Stat.IsInBase)
        {
            Debug.Log("[Chest] 기지 내부에서만 열 수 있습니다.");
            return;
        }

        // IsChestOpen이 stuck된 경우 안전 리셋
        if (InventoryUIController.IsChestOpen)
        {
            InventoryUIController.IsChestOpen = false;
            InventoryManager.ChestInstance?.ClearAllItems();
        }

        // ① 롤: DropTable(Chest) → 아이템 목록 생성
        List<(int itemId, int count)> items = Roll();

        if (items.Count == 0)
            Debug.LogWarning($"[Chest] sourceId='{sourceId}' — DropTable에 Chest 항목이 없거나 아이템 없음");

        // ② 상자 인벤토리 초기화 후 아이템 채우기
        var chestInv = InventoryManager.ChestInstance;
        if (chestInv != null)
        {
            chestInv.ClearAllItems();
            foreach (var (itemId, count) in items)
                chestInv.AddItem(itemId, count);
        }
        else
        {
            Debug.LogWarning("[Chest] ChestInstance 없음 — 씬에 Chest InventoryManager 오브젝트가 필요합니다.");
        }

        // ③ 창고 스타일로 인벤토리 UI 열기 (상자 패널 + 가방 패널)
        InventoryUIController.IsChestOpen = true;
        InventoryUIController.Instance?.Open();

        // ④ 비주얼 변경 (닫을 때 비주얼 복구는 리스폰에서 처리)
        if (closedVisual != null) closedVisual.SetActive(false);
        if (openedVisual != null) openedVisual.SetActive(true);

        // ⑤ 고갈 표시 + 리스폰 예약
        _opened = true;
        if (respawnTimeSeconds > 0f)
            _respawnCo = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnTimeSeconds);

        _respawnCo = null;
        _opened = false;
        if (closedVisual != null) closedVisual.SetActive(true);
        if (openedVisual != null) openedVisual.SetActive(false);
    }

    // 비활성화 중 코루틴이 멈춰 영구 고갈되는 것 방지 + 떠있는 진행바 정리
    private void OnDisable()
    {
        if (_respawnCo != null) { StopCoroutine(_respawnCo); _respawnCo = null; }
        HideProgress();
    }

    private void OnEnable()
    {
        // 열린 상태로 비활성화됐었다면 리스폰 타이머 재예약 (소프트락 방지)
        if (_opened && respawnTimeSeconds > 0f && _respawnCo == null)
            _respawnCo = StartCoroutine(RespawnRoutine());
    }

    // ── 진행 바 (임시 placeholder UI) ───────────────────────────────────
    private void ShowProgress(float t01)
    {
        EnsureProgressUI();
        if (_progressRoot == null) return;
        _progressRoot.gameObject.SetActive(true);
        if (_progressFill != null)
            _progressFill.sizeDelta = new Vector2(progressBarWidthPx * Mathf.Clamp01(t01), progressBarHeightPx);
    }

    private void HideProgress()
    {
        if (_progressRoot != null)
            _progressRoot.gameObject.SetActive(false);
    }

    private void EnsureProgressUI()
    {
        if (_progressRoot != null) return;

        var canvasGo = new GameObject("ChestHoldProgress");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = Vector3.up * progressHeight;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _progressRoot = (RectTransform)canvasGo.transform;
        _progressRoot.sizeDelta = new Vector2(progressBarWidthPx, progressBarHeightPx);
        _progressRoot.localScale = Vector3.one * 0.01f;   // 월드 1px = 0.01m

        // 배경 (다크 반투명)
        var bgGo = new GameObject("BG", typeof(RectTransform));
        bgGo.transform.SetParent(_progressRoot, false);
        var bgRt = (RectTransform)bgGo.transform;
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        var bg = bgGo.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.11f, 0.78f);
        bg.raycastTarget = false;

        // 채움 (골드, 왼쪽 정렬)
        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(_progressRoot, false);
        _progressFill = (RectTransform)fillGo.transform;
        _progressFill.anchorMin = new Vector2(0f, 0.5f);
        _progressFill.anchorMax = new Vector2(0f, 0.5f);
        _progressFill.pivot = new Vector2(0f, 0.5f);
        _progressFill.anchoredPosition = Vector2.zero;
        _progressFill.sizeDelta = new Vector2(0f, progressBarHeightPx);
        var fill = fillGo.AddComponent<Image>();
        fill.color = new Color(1f, 0.78f, 0.18f, 1f);
        fill.raycastTarget = false;

        _progressRoot.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        // 진행 바가 보일 때만 카메라를 바라보게(빌보드)
        if (_progressRoot == null || !_progressRoot.gameObject.activeSelf) return;
        if (_camTr == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            _camTr = cam.transform;
        }
        _progressRoot.forward = _camTr.forward;
    }

    // ── 드롭 롤 ───────────────────────────────────────────────────────

    private List<(int itemId, int count)> Roll()
    {
        var result  = new List<(int itemId, int count)>();
        string myId = (sourceId ?? "").Trim();
        if (myId.Length == 0) return result;

        // DropTable에서 Chest + sourceId 매칭 행 수집
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
                Debug.LogWarning($"[Chest] itemId 추출 실패 — SheetId='{picked.SheetId}'. DropTable의 itemId 컬럼 값 확인 필요");
                continue;
            }

            // ItemData에 존재하는지 검증
            if (GameDataUtility.GetItem(itemId) == null)
            {
                Debug.LogWarning($"[Chest] itemId={itemId} — ItemData에 없는 아이템. 시트 데이터 확인 필요");
                continue;
            }

            int count = Random.Range(picked.minCount, picked.maxCount + 1);
            if (count > 0)
            {
                result.Add((itemId, count));
                Debug.Log($"[Chest] 롤됨: itemId={itemId} x{count}");
            }
        }

        Debug.Log($"[Chest] sourceId='{sourceId}' → 총 {result.Count}종 아이템 롤 완료");
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

    // SheetId 복합키 "dropId_itemId" 에서 itemId 추출
    private int ExtractItemId(DropTableSheetId sheetId)
    {
        string s = sheetId;
        int u = s.LastIndexOf('_');
        if (u < 0 || u + 1 >= s.Length) return 0;
        return int.TryParse(s.Substring(u + 1), out int id) ? id : 0;
    }
}
