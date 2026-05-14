// BaseInteractable.cs
// 기능 유지: F키 상호작용 + ActionType 분기 + MonsterLoot 우선 -> LootContainer
// 변경(최적화): GetComponent 반복 캐싱, 입력 처리 분리, 리스트 제거 중복 정리

using System.Collections.Generic;
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
    private Collider interactionTrigger;

    //private MonsterLoot cachedMonsterLoot; //수정
    private LootContainer cachedLootContainer;

    //  동시에 여러 상자 범위에 있을 때 입력을 1개만 처리하기 위한 전역 상태
    private static readonly List<BaseInteractable> _inRangeList = new List<BaseInteractable>(64);
    private static Transform _playerTransform;
    private static int _lastConsumeFrame = -1;

    void Reset()
    {
        useDedicatedInteractionTrigger = true;
        autoCreateTriggerIfMissing = true;
        forceAllCollidersToTrigger_Legacy = false;
    }

    void Awake()
    {
        ResolveRefs();

        if (forceAllCollidersToTrigger_Legacy)
            ForceTriggerOnSelfAndChildren();

        if (useDedicatedInteractionTrigger)
            SetupDedicatedTrigger();
    }

    void OnDisable()
    {
        RemoveFromRangeList();
        playerInRange = false;
    }

    void Update()
    {
        if (!playerInRange)
            return;

        if (!Input.GetKeyDown(interactKey))
            return;

        if (Time.frameCount == _lastConsumeFrame)
            return;

        if (_playerTransform != null)
        {
            BaseInteractable nearest = GetNearestInRange(_playerTransform.position);
            if (nearest != this)
                return;
        }

        _lastConsumeFrame = Time.frameCount;
        ExecuteAction();
    }

    private void ResolveRefs()
    {
        if (uiStateManager == null)
            uiStateManager = UIStateManager.Instance;

        //if (cachedMonsterLoot == null) //수정
        //    cachedMonsterLoot = GetComponent<MonsterLoot>(); //수정

        if (cachedLootContainer == null)
            cachedLootContainer = GetComponent<LootContainer>();
    }

    private void ExecuteAction()
    {
        if (uiStateManager == null)
            uiStateManager = UIStateManager.Instance;

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
                //if (cachedMonsterLoot != null) //수정
                //{ //수정
                //    cachedMonsterLoot.Open(); //수정
                //    return; //수정
                //} //수정

                if (cachedLootContainer != null)
                {
                    cachedLootContainer.Open();
                    return;
                }

                Debug.LogWarning("[BaseInteractable] OpenLootContainer인데 LootContainer 없음", gameObject); //수정
                break;
        }
    }

    private BaseInteractable GetNearestInRange(Vector3 playerPos)
    {
        BaseInteractable nearest = null;
        float best = float.MaxValue;

        for (int i = _inRangeList.Count - 1; i >= 0; i--)
        {
            var it = _inRangeList[i];
            if (it == null)
            {
                _inRangeList.RemoveAt(i);
                continue;
            }

            if (!it.playerInRange)
                continue;

            float d = (it.transform.position - playerPos).sqrMagnitude;
            if (d < best)
            {
                best = d;
                nearest = it;
            }
        }

        return nearest;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = true;
        _playerTransform = other.transform;
        AddToRangeList();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = false;
        RemoveFromRangeList();
    }

    private void AddToRangeList()
    {
        if (!_inRangeList.Contains(this))
            _inRangeList.Add(this);
    }

    private void RemoveFromRangeList()
    {
        if (_inRangeList.Contains(this))
            _inRangeList.Remove(this);
    }

    private void SetupDedicatedTrigger()
    {
        interactionTrigger = GetComponent<Collider>();

        if (interactionTrigger != null && interactionTrigger.isTrigger)
            return;

        if (interactionTrigger == null || !interactionTrigger.isTrigger)
        {
            var allCols = GetComponents<Collider>();
            for (int i = 0; i < allCols.Length; i++)
            {
                if (allCols[i] != null && allCols[i].isTrigger)
                {
                    interactionTrigger = allCols[i];
                    return;
                }
            }

            if (!autoCreateTriggerIfMissing)
                return;

            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = triggerSize;
            bc.center = triggerCenter;
            interactionTrigger = bc;
        }
    }

    private void ForceTriggerOnSelfAndChildren()
    {
        var cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null) continue;
            cols[i].isTrigger = true;
        }
    }

    private void OnValidate()
    {
        triggerSize.x = Mathf.Max(0.1f, triggerSize.x);
        triggerSize.y = Mathf.Max(0.1f, triggerSize.y);
        triggerSize.z = Mathf.Max(0.1f, triggerSize.z);
    }
}