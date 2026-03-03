// BaseInteractable.cs
// 기능 유지: F키 상호작용 + ActionType 분기 + MonsterLoot 우선 -> LootContainer
// 변경: 기존 콜라이더 전부 Trigger로 강제하던 방식(ForceTriggerOnSelfAndChildren)을 "옵션"으로 내리고,
//      기본은 '상호작용 전용 Trigger 콜라이더(BoxCollider)'를 자동으로 만들어서 사용.

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

    [Header("Interaction Trigger Settings")]
    [Tooltip("켜면: 기존 콜라이더들은 건드리지 않고, 상호작용 범위용 Trigger(BoxCollider)를 따로 만들어 사용합니다. (추천)")]
    public bool useDedicatedInteractionTrigger = true;

    [Tooltip("전용 트리거 자동 생성")]
    public bool autoCreateTriggerIfMissing = true;

    [Tooltip("전용 트리거 콜라이더 크기(BoxCollider size)")]
    public Vector3 triggerSize = new Vector3(2.0f, 2.0f, 2.0f);

    [Tooltip("전용 트리거 콜라이더 중심(BoxCollider center)")]
    public Vector3 triggerCenter = new Vector3(0f, 1.0f, 0f);

    [Header("Legacy (Not recommended)")]
    [Tooltip("예전 방식: 이 오브젝트/자식의 Collider를 전부 Trigger로 강제합니다. (환경/충돌에 영향 줄 수 있음)")]
    public bool forceAllCollidersToTrigger_Legacy = false;

    private bool playerInRange = false;
    private Collider interactionTrigger; // 전용 트리거 참조(있으면 이 콜라이더로만 범위 판정)

    void Reset()
    {
        // Reset에서는 기본값 세팅만. (원래처럼 강제 트리거는 끔)
        useDedicatedInteractionTrigger = true;
        autoCreateTriggerIfMissing = true;
        forceAllCollidersToTrigger_Legacy = false;
    }

    void Awake()
    {
        // UIStateManager 자동 참조
        if (uiStateManager == null)
            uiStateManager = UIStateManager.Instance;

        // 1) 레거시 모드면 기존 동작 유지 (원래 기능을 옵션으로 남겨둠)
        if (forceAllCollidersToTrigger_Legacy)
        {
            ForceTriggerOnSelfAndChildren();
        }

        // 2) 추천 모드: 전용 트리거 확보 (기존 "막는 콜라이더"는 그대로 둠)
        if (useDedicatedInteractionTrigger)
        {
            SetupDedicatedTrigger();
        }
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
        // 전용 트리거를 쓰는 경우:
        // "상호작용 트리거 콜라이더"에 들어온 트리거 이벤트만 인정하고 싶다면,
        // other는 플레이어 콜라이더라서 여기서 구분이 어려움.
        // 대신 "내 오브젝트에 어떤 트리거로 들어왔는지"는 Unity 이벤트 구조상 직접 못 받으니,
        // 전용 트리거를 이 스크립트가 붙은 동일 오브젝트에 두는 방식으로 통일함(SetupDedicatedTrigger가 그렇게 함).

        if (other.CompareTag("Player"))
            playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = false;
    }

    private void SetupDedicatedTrigger()
    {
        // 전용 트리거는 "이 컴포넌트가 붙은 같은 GameObject"에 두는 걸 기본으로 함.
        // (그래야 OnTriggerEnter/Exit가 확실히 이 스크립트로 들어옴)
        interactionTrigger = GetComponent<Collider>();

        // 같은 오브젝트에 콜라이더가 이미 있고 그게 Trigger라면 그냥 사용
        if (interactionTrigger != null && interactionTrigger.isTrigger)
            return;

        // 같은 오브젝트에 콜라이더가 있는데 Trigger가 아니면 (막는 콜라이더)
        // -> 전용 트리거(BoxCollider)를 "추가"로 만든다.
        // 단, 이미 BoxCollider가 2개 이상 붙어도 Unity는 문제없음.
        if (interactionTrigger == null || !interactionTrigger.isTrigger)
        {
            // 이미 존재하는 "Trigger BoxCollider"가 있으면 그걸 사용
            var allCols = GetComponents<Collider>();
            for (int i = 0; i < allCols.Length; i++)
            {
                if (allCols[i] != null && allCols[i].isTrigger)
                {
                    interactionTrigger = allCols[i];
                    return;
                }
            }

            if (!autoCreateTriggerIfMissing) return;

            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = triggerSize;
            bc.center = triggerCenter;
            interactionTrigger = bc;
        }
    }

    private void ForceTriggerOnSelfAndChildren()
    {
        // 자기 자신 Collider
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // 자식 Collider까지
        var childCols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childCols.Length; i++)
        {
            childCols[i].isTrigger = true;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 값이 이상하게 들어가면 최소 보정
        triggerSize.x = Mathf.Max(0.1f, triggerSize.x);
        triggerSize.y = Mathf.Max(0.1f, triggerSize.y);
        triggerSize.z = Mathf.Max(0.1f, triggerSize.z);
    }
#endif
}