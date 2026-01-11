using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.F;             // 파밍 키
    public float pickupRange = 2.0f;                    // 줍기 거리
    public LayerMask pickupLayerMask = ~0;              // 픽업 오브젝트 레이어(필요시 제한)

    private PlayerWeaponController weaponController;

    private void Awake()
    {
        weaponController = GetComponent<PlayerWeaponController>();
    }

    private void Update()
    {
        // F키 눌렀을떄만 체크
        if (!Input.GetKeyDown(interactKey))
            return;

        TryPickupWeapon();
    }

    void TryPickupWeapon()
    {
        // 콜라이더 주변에서 가장 가까운 WeaponPickup 찾기
        Collider[] hits = Physics.OverlapSphere(transform.position,pickupRange,pickupLayerMask);

        WeaponPickup nearest = null;
        float nearestDistSqr = float.MaxValue;

        foreach(var c in hits)
        {
            WeaponPickup pickup = c.GetComponentInParent<WeaponPickup>();
            
            if(pickup == null) continue;
            if(pickup.weaponData == null) continue;

            float d = (pickup.transform.position - transform.position).sqrMagnitude;

            if(d < nearestDistSqr)
            {
                nearestDistSqr = d;
                nearest = pickup;
            }
        }

        if (nearest == null)
        {
            Debug.Log("근처 무기 없음");
            return;
        }

        // 이미 무기 들고있으면 장착 선택
        // 교체 장착
        weaponController.EquipWeapon(nearest.weaponData);

        // 월드 오브젝트 제거 
        nearest.OnPickedUp();
        Debug.Log("무기 파밍 성공");
    }
}
