// BaseInteractable.cs (수정본: 런타임 Trigger 강제 + MonsterLoot 우선 -> LootContainer)
using UnityEngine;

public class BaseInteractable : MonoBehaviour
{
    public enum ActionType
    {
        OpenShop,
        OpenInventoryWarehouse,
        OpenLootContainer,
    }

    [Header("Action")]
    public ActionType action = ActionType.OpenShop;

    [Header("Input")]
    public KeyCode interactKey = KeyCode.F;

    [Header("Refs (optional)")]
    public UIStateManager uiStateManager;

    private bool playerInRange = false;

    void Reset()
    {
        // 에디터에서 컴포넌트 붙일 때 기본 트리거
        ForceTriggerOnSelfAndChildren();
    }

    void Awake()
    {
        // ✅ 핵심: 런타임에서도 트리거 강제 (기존 파밍상자 프리팹이 안 열리던 원인 해결)
        ForceTriggerOnSelfAndChildren();

        if (uiStateManager == null)
            uiStateManager = UIStateManager.Instance;
    }

    void Update()
    {
        if (!playerInRange) return;

        if (uiStateManager == null)
            uiStateManager = UIStateManager.Instance;

        if (!Input.GetKeyDown(interactKey)) return;

        switch (action)
        {
            case ActionType.OpenShop:
                if (uiStateManager == null) return;
                uiStateManager.ToggleShop();
                break;

            case ActionType.OpenInventoryWarehouse:
                if (uiStateManager == null) return;
                uiStateManager.ToggleInventory();
                break;

            case ActionType.OpenLootContainer:
                {
                    // ✅ 1) 시체 루팅(몬스터 드랍)
                    MonsterLoot monsterLoot = GetComponent<MonsterLoot>();
                    if (monsterLoot != null)
                    {
                        monsterLoot.Open();
                        return;
                    }

                    // ✅ 2) 기존 파밍 상자 루팅
                    LootContainer loot = GetComponent<LootContainer>();
                    if (loot != null)
                    {
                        loot.Open();
                        return;
                    }

                    Debug.LogWarning("[BaseInteractable] OpenLootContainer인데 MonsterLoot/LootContainer 둘 다 없음", gameObject);
                    break;
                }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = false;
    }

    private void ForceTriggerOnSelfAndChildren()
    {
        // 자기 자신 Collider
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // 자식 Collider까지 (상자 프리팹이 자식에 콜라이더 달린 구조일 때 대비)
        var childCols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childCols.Length; i++)
        {
            // 너무 큰 환경 콜라이더까지 건드리는 게 싫으면 조건 걸어도 되는데,
            // 지금은 "안 열림" 긴급 해결이 우선이라 전부 트리거로 강제.
            childCols[i].isTrigger = true;
        }
    }
}
