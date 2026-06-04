// =====================================================================
// ChestOpenUI.cs
// 상자 오픈 팝업 — 획득한 아이템 목록 표시
// 아이템은 ChestInteractable에서 이미 인벤토리에 지급된 상태
// 이 UI는 "무엇을 얻었는지" 알려주는 알림 역할
// =====================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChestOpenUI : MonoBehaviour
{
    // ── 싱글톤 ────────────────────────────────────────────────────────
    public static ChestOpenUI Instance { get; private set; }

    // ── 루트 ──────────────────────────────────────────────────────────
    [Header("루트 패널")]
    [SerializeField] private GameObject panelRoot;

    // ── 아이템 목록 ───────────────────────────────────────────────────
    [Header("아이템 목록")]
    [SerializeField] private Transform     rowContainer;   // VerticalLayoutGroup
    [SerializeField] private ChestItemRow  rowPrefab;      // 행 프리팹

    // ── 텍스트 / 버튼 ─────────────────────────────────────────────────
    [Header("버튼")]
    [SerializeField] private Button closeButton;

    // ── 내부 ──────────────────────────────────────────────────────────
    private readonly List<ChestItemRow> _rows = new();

    // ── 라이프사이클 ──────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        closeButton?.onClick.AddListener(Close);
        panelRoot?.SetActive(false);
    }

    private void Update()
    {
        // ESC 또는 F키로 닫기
        if (IsPanelOpen() && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    // ── 공개 API ──────────────────────────────────────────────────────

    /// <summary>상자 오픈 팝업 표시. 아이템은 이미 인벤토리에 지급된 상태.</summary>
    public void Open(List<(int itemId, int count)> items)
    {
        ClearRows();

        if (rowPrefab != null && rowContainer != null)
        {
            foreach (var (itemId, count) in items)
            {
                ChestItemRow row = Instantiate(rowPrefab, rowContainer);
                row.Set(itemId, count);
                _rows.Add(row);
            }

            // 레이아웃 즉시 재계산
            if (rowContainer is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        panelRoot?.SetActive(true);
    }

    public void Close()
    {
        ClearRows();
        panelRoot?.SetActive(false);
        GameUIController.Instance?.CloseChestUI();
    }

    public void HidePanel() => panelRoot?.SetActive(false);

    // ── 내부 ──────────────────────────────────────────────────────────

    private void ClearRows()
    {
        foreach (var r in _rows)
            if (r != null) Destroy(r.gameObject);
        _rows.Clear();
    }

    private bool IsPanelOpen() => panelRoot != null && panelRoot.activeSelf;
}
