using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TIMEKOV.Factory;

/// <summary>
/// 여러 설비가 근처에 있을 때 표시되는 선택 패널.
/// DropPickupPanel과 동일한 구조 — rowPrefab을 동적 생성해 목록을 구성한다.
/// </summary>
public class FacilitySelectPanel : MonoBehaviour
{
    [SerializeField] private GameObject      panelRoot;
    [SerializeField] private Transform       rowContainer;
    [SerializeField] private FacilitySelectRow rowPrefab;

    private readonly List<FacilitySelectRow> _rows = new();
    private int _selectedIndex = 0;

    public bool        IsShowing           => panelRoot != null && panelRoot.activeSelf;
    public MachineBase SelectedMachine     => InRange(_selectedIndex) ? _rows[_selectedIndex].Machine     : null;
    public string      SelectedMachineName => InRange(_selectedIndex) ? _rows[_selectedIndex].MachineName : "";

    private bool InRange(int i) => _rows.Count > 0 && i >= 0 && i < _rows.Count;

    // ─────────────────────────────────────────────────────────────────────

    void Awake() => Hide();

    /// <summary>패널을 표시한다. 목록이 바뀔 때마다 재호출.</summary>
    public void Show(List<(MachineBase machine, string name)> machines)
    {
        ClearRows();
        _selectedIndex = 0;

        foreach (var (machine, name) in machines)
        {
            FacilitySelectRow row = Instantiate(rowPrefab, rowContainer);
            row.Set(machine, name);
            _rows.Add(row);
        }

        if (_rows.Count > 0) _rows[0].SetSelected(true);

        panelRoot.SetActive(true);

        if (rowContainer is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    public void Hide()
    {
        ClearRows();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>
    /// 마우스 휠로 선택 인덱스 이동.
    /// delta = +1 → 아래 항목 선택, delta = -1 → 위 항목 선택.
    /// </summary>
    public void ScrollSelection(int delta)
    {
        if (_rows.Count == 0) return;
        _rows[_selectedIndex].SetSelected(false);
        _selectedIndex = (_selectedIndex + delta + _rows.Count) % _rows.Count;
        _rows[_selectedIndex].SetSelected(true);
    }

    /// <summary>선택된 행을 flashCount회 깜빡인다. MachineInteraction에서 yield return으로 사용.</summary>
    public IEnumerator FlashSelected(float totalDuration = 0.4f, int flashCount = 3)
    {
        if (!InRange(_selectedIndex)) yield break;

        FacilitySelectRow row = _rows[_selectedIndex];
        float interval = totalDuration / (flashCount * 2f);

        // 선택된 큰 크기는 유지하고 색만 깜빡인다 (크기 깜빡임 방지).
        for (int i = 0; i < flashCount; i++)
        {
            row.SetFlashHighlight(false);
            yield return new WaitForSecondsRealtime(interval);
            row.SetFlashHighlight(true);
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    /// <summary>LootBoxScanner처럼 박스(설비) 월드 위치를 화면 좌표로 받아 패널을 이동한다.</summary>
    public void SetScreenPosition(Vector3 screenPos)
    {
        if (panelRoot != null)
            panelRoot.transform.position = new Vector3(screenPos.x, screenPos.y, 0f);
    }

    // ─────────────────────────────────────────────────────────────────────

    private void ClearRows()
    {
        foreach (var row in _rows)
            if (row != null) Destroy(row.gameObject);
        _rows.Clear();
    }
}
