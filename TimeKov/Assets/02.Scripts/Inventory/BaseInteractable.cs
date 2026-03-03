// BaseInteractable.cs
// 기능 유지: F키 상호작용 + ActionType 분기 + MonsterLoot 우선 -> LootContainer
// 변경(추가): 플레이어가 여러 Interactable 트리거에 동시에 들어가 있을 때
//            F 입력이 "여러 개에 동시에 처리"되어 LootContainer.Open()이 중복 호출되는 문제를 방지.
//            - 한 프레임에 입력은 1개만 소비
//            - 소비 주체는 "플레이어와 가장 가까운 Interactable" 1개

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

    // ✅ [추가] 동시에 여러 상자 범위에 있을 때 입력을 1개만 처리하기 위한 전역 상태
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
        if (uiStateManager == null)
            uiStateManager = UIStateManager.Instance;

        if (forceAllCollidersToTrigger_Legacy)
        {
            ForceTriggerOnSelfAndChildren();
        }

        if (useDedicatedInteractionTrigger)
        {
            SetupDedicatedTrigger();
        }
    }

    void OnDisable()
    {
        if (_inRangeList.Contains(this))
            _inRangeList.Remove(this);
        playerInRange = false;
    }

    void Update()
    {
        if (!playerInRange) return;

        if (uiStateManager == null)
            uiStateManager = UIStateManager.Instance;

        if (!Input.GetKeyDown(interactKey)) return;

        // ✅ [추가] 한 프레임에 입력은 1개만 소비
        if (Time.frameCount == _lastConsumeFrame) return;

        // ✅ [추가] 동시에 여러 Interactable이 범위 안이면, "가장 가까운 1개"만 실행
        if (_playerTransform != null)
        {
            BaseInteractable nearest = GetNearestInRange(_playerTransform.position);
            if (nearest != this) return;
        }

        _lastConsumeFrame = Time.frameCount;

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
                    MonsterLoot monsterLoot = GetComponent<MonsterLoot>();
                    if (monsterLoot != null)
                    {
                        monsterLoot.Open();
                        return;
                    }

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
            if (!it.playerInRange) continue;

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
        if (!other.CompareTag("Player")) return;

        playerInRange = true;
        _playerTransform = other.transform;

        if (!_inRangeList.Contains(this))
            _inRangeList.Add(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = false;

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
        var cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null) continue;
            cols[i].isTrigger = true;
        }
    }

    private void OnValidate()
    { 
        // 값이 이상하게 들어가면 최소 보정 
        triggerSize.x = Mathf.Max(0.1f, triggerSize.x); 
        triggerSize.y = Mathf.Max(0.1f, triggerSize.y); 
        triggerSize.z = Mathf.Max(0.1f, triggerSize.z); 
    }
 }