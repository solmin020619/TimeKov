using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class DroppedItem : MonoBehaviour, IInteractable
{
    [Header("Item")]
    [Tooltip("DataStore의 ItemRow id")]
    public int itemId;
    public int count = 1;

    [Header("Pickup")]
    [Tooltip("플레이어가 주울 수 있는지")]
    public bool canPickup = true;

    //[Tooltip("Player Layer (인스펙터에서 Player 레이어 체크)")]
    //public LayerMask playerMask = ~0;

    private Rigidbody _rb;
    private float _spawnTime;
    private float _pickupDelay;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _spawnTime = Time.time;
    }

    public void Initialize(int id, int amount, bool pickup, float pickupDelay)
    {
        itemId = id;
        count = amount;
        canPickup = pickup;
        _pickupDelay = pickupDelay;
        _spawnTime = Time.time;
    }

    public void Launch(Vector3 force, float spinTorque, float gravityScale)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();

        _rb.useGravity = gravityScale > 0f;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.AddForce(force, ForceMode.Impulse);

        if (spinTorque > 0f)
        {
            Vector3 randomTorque = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ) * spinTorque;
            _rb.AddTorque(randomTorque, ForceMode.Impulse);
        }

        if (gravityScale != 1f && gravityScale > 0f)
        {
            _rb.mass = Mathf.Max(0.0001f, _rb.mass / gravityScale);
        }
    }

    // IInteractable 구현
    // 픽업 가능 여부 체크 (스폰 후 딜레이 지났는지 포함)
    public bool CanInteract => canPickup &&
                               Time.time - _spawnTime >= _pickupDelay;

    // F키로 상호작용 시 호출
    public void Interact(Player player)
    {
        InventoryManager inv = FindPlayerInventory();
        if (inv == null) return;

        int remaining = inv.TryAddItemFromLoot(itemId, count);
        if (remaining <= 0)
        {
            Destroy(gameObject);
            return;
        }

        count = remaining;
    }

    private InventoryManager FindPlayerInventory()
    {
        InventoryManager[] all = FindObjectsByType<InventoryManager>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].ownerType == InventoryManager.InventoryOwnerType.Player)
                return all[i];
        }
        return null;
    }

    // 자동 획득 비활성화 (F키 방식으로 변경)
    // private void OnTriggerEnter(Collider other)
    // {
    //     TryPickup(other);
    // }

    // private void OnCollisionEnter(Collision collision)
    // {
    //     TryPickup(collision.collider);
    // }

    // private void TryPickup(Collider other)
    // {
    //     if (!canPickup) return;
    //     if (Time.time - _spawnTime < _pickupDelay) return;

    //     if (((1 << other.gameObject.layer) & playerMask.value) == 0)
    //     {
    //         if (!other.CompareTag("Player")) return;
    //     }

    //     InventoryManager inv = FindPlayerInventory();
    //     if (inv == null) return;

    //     inv.AddItem(itemId, count);
    //     Destroy(gameObject);
    // }
}