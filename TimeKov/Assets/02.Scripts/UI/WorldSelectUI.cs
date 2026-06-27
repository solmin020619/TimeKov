// =====================================================================
// WorldSelectUI.cs
// 타이틀 화면에서 띄우는 월드(세이브 슬롯) 선택 패널.
// 목록 표시 -> 슬롯 선택/생성 -> SaveSlotManager에 활성 슬롯 확정 -> 로딩 씬으로 전환.
// =====================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldSelectUI : MonoBehaviour
{
    [SerializeField] GameObject panelRoot;
    [SerializeField] Transform rowContainer;
    [SerializeField] WorldSelectRow rowPrefab;
    [SerializeField] TMP_InputField newWorldNameInput;
    [SerializeField] Button newWorldButton;
    [SerializeField] Button backButton;

    [Tooltip("슬롯 확정 후 로딩을 거쳐 진입할 실제 플레이 씬 이름.")]
    [SerializeField] string gameplaySceneName = "World";

    readonly List<WorldSelectRow> _spawnedRows = new();

    void Awake()
    {
        // panelRoot는 보통 이 컴포넌트와 같은 GameObject — 에디터 빌더가 이미 비활성 상태로
        // 저장해두므로 여기서 다시 SetActive(false)하면 Show()가 막 활성화한 직후 자기 자신을
        // 도로 꺼버리는 꼴이 된다. 그래서 Awake에서는 건드리지 않는다.
        if (newWorldButton != null) newWorldButton.onClick.AddListener(OnClickCreateNewWorld);
        if (backButton != null) backButton.onClick.AddListener(Hide);
    }

    public void Show()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void Refresh()
    {
        foreach (var row in _spawnedRows)
            if (row != null) Destroy(row.gameObject);
        _spawnedRows.Clear();

        if (SaveSlotManager.Instance == null || rowPrefab == null || rowContainer == null) return;

        foreach (var meta in SaveSlotManager.Instance.ListSlots())
        {
            string slotId = meta.slotId; // 클로저 캡처용 로컬 복사
            var row = Instantiate(rowPrefab, rowContainer);
            row.Set(meta, () => EnterSlot(slotId), () => DeleteSlot(slotId));
            _spawnedRows.Add(row);
        }
    }

    void OnClickCreateNewWorld()
    {
        if (SaveSlotManager.Instance == null) return;

        string name = newWorldNameInput != null ? newWorldNameInput.text : null;
        var meta = SaveSlotManager.Instance.CreateSlot(name);
        if (newWorldNameInput != null) newWorldNameInput.text = string.Empty;

        EnterSlot(meta.slotId);
    }

    void EnterSlot(string slotId)
    {
        if (SaveSlotManager.Instance == null || !SaveSlotManager.Instance.LoadSlot(slotId)) return;
        CoreUtilities.LoadViaLoading(gameplaySceneName);
    }

    void DeleteSlot(string slotId)
    {
        SaveSlotManager.Instance?.DeleteSlot(slotId);
        Refresh();
    }
}
