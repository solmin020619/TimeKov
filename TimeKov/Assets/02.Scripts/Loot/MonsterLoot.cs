using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Linq;
using UnityEngine;

public class MonsterLoot : MonoBehaviour
{
    [Header("Drop Info")]
    public string monsterType;
    public string sourceType = "monster";
    public int dropTier = 0;

    [Header("UI")]
    [SerializeField] private GameObject lootSlotPrefab;

    private RectTransform _content;
    private GameObject _lootPanel;

    private List<GameObject> _spawnedSlots = new List<GameObject>();
    private List<LootData> _rolledLoot = new List<LootData>();


    public class LootData
    {
        public int itemId;
        public int count;

        public LootData(int id, int cnt)
        {
            itemId = id;
            count = cnt;
        }
    }

    void Awake()
    {
        FindUI();
    }

    void FindUI()
    {
        // 기존에는 Resources.FindObjectsOfTypeAll<Transform>()로 Inventory를 찾았는데,
        // 이 방식은 프리팹 에셋까지 검색해서 잘못된 Content를 잡을 수 있음.
        // 그 결과 loot slot 생성 시 persistent parent 에러가 발생할 수 있음,
        // 씬에 로드된 오브젝트만 대상으로 UI를 찾도록 수정.
        // 보고 이해 안 되면 한종욱에게 질문하십쇼 안승현씨

        Transform[] allSceneTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Transform inventory = allSceneTransforms.FirstOrDefault(t =>
            t.name == "Inventory" &&
            t.gameObject.scene.IsValid() &&
            t.gameObject.scene.isLoaded);

        if (inventory == null)
        {
            Debug.LogError("[MonsterLoot] 씬에서 Inventory 못찾음");
            return;
        }

        Transform panel = inventory.Find("RightPanel/LootPanel");
        if (panel == null)
        {
            Debug.LogError("[MonsterLoot] LootPanel 못찾음");
            return;
        }

        _lootPanel = panel.gameObject;

        Transform content = panel.Find("Scroll View/Viewport/Content");
        if (content == null)
        {
            Debug.LogError("[MonsterLoot] Content 못찾음");
            return;
        }

        _content = content.GetComponent<RectTransform>();
        if (_content == null)
        {
            Debug.LogError("[MonsterLoot] Content에 RectTransform 없음");
        }
    }

    public void Open()
    {
        if (_content == null || _lootPanel == null)
        {
            FindUI();
            if (_content == null) return;
        }

       
        if (UIStateManager.Instance != null &&
            UIStateManager.Instance.GetCurrentState() == UIStateManager.UIState.Loot)
        {
            UIStateManager.Instance.SetState(UIStateManager.UIState.None);
            return;
        }

        EnsureDataStoreLoaded();

        ClearSlots();
        _rolledLoot.Clear();

        RollLoot();
        BuildSlots();

        if (UIStateManager.Instance != null)
            UIStateManager.Instance.ToggleLoot(_lootPanel);
        else
            _lootPanel.SetActive(true);
    }

    public void Close()
    {
        if (UIStateManager.Instance != null)
            UIStateManager.Instance.SetState(UIStateManager.UIState.None);
        else if (_lootPanel != null)
            _lootPanel.SetActive(false);

        ClearSlots();
    }

    void BuildSlots()
    {
        // UI 참조 누락 상태에서 슬롯 생성 시 null 관련 오류를 막기 위한 방어 코드

        if (lootSlotPrefab == null)
        {
            Debug.LogError("[MonsterLoot] lootSlotPrefab이 비어 있음", this);
            return;
        }

        if (_content == null)
        {
            Debug.LogError("[MonsterLoot] _content가 비어 있음", this);
            return;
        }

        foreach (var data in _rolledLoot)
        {
            GameObject slot = Instantiate(lootSlotPrefab, _content);

            var slotInfo = slot.GetComponent<SlotInfo>();
            if (slotInfo != null)
            {
                slotInfo.ownerType = SlotInfo.SlotOwnerType.Loot;
                slotInfo.SetSlot(data.itemId, data.count);
            }

            _spawnedSlots.Add(slot);
        }
    }

    void ClearSlots()
    {
        if (_content == null) return;

        for (int i = _content.childCount - 1; i >= 0; i--)
        {
            Destroy(_content.GetChild(i).gameObject);
        }

        _spawnedSlots.Clear();
    }

    void RollLoot()
    {
        var rows = GetMatchedDropRows();

        if (rows == null || rows.Count == 0)
            return;

        int pickCount = Mathf.Max(1, rows[0].pickCount);
        List<DropRow> pool = new List<DropRow>(rows);

        for (int i = 0; i < pickCount; i++)
        {
            if (pool.Count == 0) break;

            int index = Random.Range(0, pool.Count);
            var picked = pool[index];

            int count = Random.Range(picked.minCount, picked.maxCount + 1);
            _rolledLoot.Add(new LootData(picked.itemId, count));

            pool.RemoveAt(index);
        }
    }

    List<DropRow> GetMatchedDropRows()
    {
        foreach (var kv in DataStore.DropRowsByDropId)
        {
            var rows = kv.Value;
            if (rows == null || rows.Count == 0) continue;

            var head = rows[0];

            if (!head.sourceType.Equals(sourceType, System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (!head.sourceId.Equals(monsterType, System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (head.dropTier != dropTier)
                continue;

            return new List<DropRow>(rows);
        }

        return null;
    }

    void EnsureDataStoreLoaded()
    {
        if (!DataStore.IsLoaded)
            DataStore.LoadAll();
    }
}