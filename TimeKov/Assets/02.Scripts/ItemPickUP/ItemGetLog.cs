// =====================================================================
// ItemGetLog.cs
// 아이템 획득 로그 (엔드필드식) — "획득 [아이콘] 아이템명 x개"가
// 좌측에 쌓였다가 잠시 후 자동으로 사라진다.
//
// 동작:
//   - GameEvents.OnItemAcquired(itemId, count) 구독
//   - 획득 시 DropPickupRow 한 줄을 생성해 위로 쌓음 (기존 줍기 UI 행 재사용)
//   - displayDuration 후 fadeDuration 동안 페이드아웃 → 제거
//   - 같은 아이템이 아직 살아있으면 개수만 합산 + 타이머 리셋 (mergeSameItem)
//
// 토스트/보상 등 별도 알림 없이 이 로그 하나로 통일 가능.
// (퀘스트 보상도 결국 OnItemAcquired 로 들어오면 여기 같이 뜸)
//
// UI 계층 (인스펙터 작업):
//   ItemGetLog (RectTransform, 좌측 중앙 정도 — 스탯/퀘스트와 안 겹치게)
//   └── rowContainer (VerticalLayoutGroup + ContentSizeFitter)
//         └── [DropPickupRow 들이 런타임 생성]
//
// 인스펙터 연결:
//   rowPrefab     : DropPickupRow 컴포넌트 붙은 행 프리팹 (PickupRow.prefab 재사용)
//   rowContainer  : 행이 쌓일 부모 (VerticalLayoutGroup 권장)
//   tierColors    : 등급별 색 (DropPickupPanel 과 동일하게 채우면 됨)
// =====================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ItemGetLog : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("DropPickupRow 컴포넌트가 붙은 행 프리팹 (줍기 UI의 PickupRow 재사용 가능)")]
    [SerializeField] private DropPickupRow rowPrefab;

    [Tooltip("행이 쌓일 부모. VerticalLayoutGroup + ContentSizeFitter 권장.")]
    [SerializeField] private Transform rowContainer;

    [Tooltip("아이템 등급(itemGrade)별 색. 배열 인덱스 = 등급 번호. DropPickupPanel 과 동일하게.")]
    [SerializeField] private Color[] tierColors;

    [Tooltip("연료 전용 색. 등급 색 대신 이 색으로 표시해 '다른 종류'임을 인지시킴.\n" +
             "연료 판별은 FuelConfig.fuelItemId 기준. 알파(A)가 0이면 미설정으로 보고 등급 색을 그대로 씀.")]
    [SerializeField] private Color fuelColor = new Color(0.23f, 0.51f, 0.96f, 1f);

    [Tooltip("'획득' 같은 고정 헤더 오브젝트. 로그가 하나라도 떠 있으면 표시, 없으면 자동 숨김.\n" +
             "rowContainer 위에 두면 목록 맨 위에 보임. 비워두면 헤더 없이 동작.")]
    [SerializeField] private GameObject headerObject;

    [Header("동작")]
    [Tooltip("한 줄이 유지되는 시간(초). 이후 페이드아웃 시작.")]
    [SerializeField] private float displayDuration = 3f;

    [Tooltip("페이드아웃 시간(초).")]
    [SerializeField] private float fadeDuration = 0.5f;

    [Tooltip("동시에 표시할 최대 줄 수. 초과 시 가장 오래된 줄부터 제거.")]
    [SerializeField] private int maxRows = 6;

    [Tooltip("같은 아이템이 아직 떠 있으면 새 줄 만들지 않고 개수만 합산 + 타이머 리셋.")]
    [SerializeField] private bool mergeSameItem = true;

    [Tooltip("일시정지(timeScale=0) 중에도 로그가 사라지게 할지. true면 unscaled 시간 사용.")]
    [SerializeField] private bool useUnscaledTime = true;

    // 살아있는 줄 추적
    private class Entry
    {
        public int itemId;
        public int count;
        public DropPickupRow row;
        public CanvasGroup group;
        public float age;       // 생성/갱신 후 경과
        public bool fading;
    }

    private readonly List<Entry> _entries = new List<Entry>();

    // ── 초기화 ──────────────────────────────────────────────────────

    private void Awake()
    {
        // 표시 전용 로그 — 절대 클릭을 가로채지 않게 (머신 UI 화살표 등 뒤쪽 UI 클릭이 먼저 먹도록).
        // 루트 CanvasGroup.blocksRaycasts=false 면 자식 행들까지 전부 비차단(자식이 ignoreParentGroups 안 켜는 한).
        var cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    // ── 구독 ──────────────────────────────────────────────────────

    private void OnEnable()
    {
        // InventoryManager.AddItem 단일 입구에서 발생 — 모든 획득 경로
        // (필드 드롭/공장 수령/벨트 자동/퀘스트 보상 등) 빠짐없이 커버.
        InventoryManager.OnItemAddedToInventory += HandleAcquired;

        // 시작 시 헤더 숨김 (로그가 떠야 보임)
        RefreshHeader();
    }

    private void OnDisable()
    {
        InventoryManager.OnItemAddedToInventory -= HandleAcquired;
    }

    // ── 획득 콜백 ─────────────────────────────────────────────────

    private void HandleAcquired(int itemId, int count)
    {
        if (count <= 0 || rowPrefab == null || rowContainer == null) return;

        // 합산 모드 — 같은 아이템이 아직 살아있고 페이드 전이면 개수만 더함
        if (mergeSameItem)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.itemId == itemId && !e.fading)
                {
                    e.count += count;
                    e.age = 0f;                         // 타이머 리셋
                    if (e.group != null) e.group.alpha = 1f;
                    e.row.Set(e.itemId, e.count, GetTierColor(e.itemId));
                    e.row.transform.SetAsLastSibling(); // 맨 아래(최신)로
                    return;
                }
            }
        }

        SpawnRow(itemId, count);
    }

    private void SpawnRow(int itemId, int count)
    {
        // 최대 줄 수 초과 시 가장 오래된(맨 위) 줄 제거
        while (_entries.Count >= maxRows && _entries.Count > 0)
        {
            var oldest = _entries[0];
            _entries.RemoveAt(0);
            if (oldest.row != null) Destroy(oldest.row.gameObject);
        }

        DropPickupRow row = Instantiate(rowPrefab, rowContainer);
        row.Set(itemId, count, GetTierColor(itemId));
        row.transform.SetAsLastSibling();

        var group = row.GetComponent<CanvasGroup>();
        if (group == null) group = row.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 1f;

        _entries.Add(new Entry
        {
            itemId = itemId,
            count = count,
            row = row,
            group = group,
            age = 0f,
            fading = false
        });

        RefreshHeader();
    }

    // '획득' 헤더는 로그가 하나라도 있을 때만 표시.
    private void RefreshHeader()
    {
        if (headerObject != null)
            headerObject.SetActive(_entries.Count > 0);
    }

    // ── 수명 관리 ─────────────────────────────────────────────────

    private void Update()
    {
        if (_entries.Count == 0) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        bool removed = false;

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            if (e.row == null) { _entries.RemoveAt(i); removed = true; continue; }

            e.age += dt;

            if (!e.fading)
            {
                if (e.age >= displayDuration)
                {
                    e.fading = true;
                    e.age = 0f; // 페이드 경과 재사용
                }
            }
            else
            {
                float t = fadeDuration > 0f ? (e.age / fadeDuration) : 1f;
                if (e.group != null) e.group.alpha = Mathf.Clamp01(1f - t);

                if (t >= 1f)
                {
                    Destroy(e.row.gameObject);
                    _entries.RemoveAt(i);
                    removed = true;
                }
            }
        }

        // 줄이 제거되어 0개가 되면 헤더도 숨김
        if (removed) RefreshHeader();
    }

    // ── 등급 색 ───────────────────────────────────────────────────

    private Color GetTierColor(int itemId)
    {
        // [연료 전용 색] FuelConfig.fuelItemId 와 일치하면 등급 색 대신 연료색 사용.
        // fuelColor 알파가 0이면 미설정으로 보고 기존 등급 색으로 폴백 (안 깨짐).
        var fuelCfg = FuelConfig.Instance;
        if (fuelCfg != null && fuelCfg.fuelItemId == itemId && fuelColor.a > 0f)
            return fuelColor;

        ItemDataSheetData item = GameDataUtility.GetItem(itemId);
        int tier = item != null ? (int)item.itemGrade : 0;
        if (tierColors != null && tier >= 0 && tier < tierColors.Length)
            return tierColors[tier];
        return Color.white;
    }
}
