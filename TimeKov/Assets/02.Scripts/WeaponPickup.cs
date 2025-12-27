using UnityEngine;

public class WeaponPickup : MonoBehaviour
{
    [Header("Weapon Data")]
    public WeaponData weaponData;       // 이 오브젝트를 주우면 장착될 무기

    [Header("DestroySettings")]
    public GameObject destroyTarget;
    
    public void OnPickedUp()
    {
        // destroyTarget이 지정되어있으면 그것을 삭제
        // 없으면 이 오브젝트의 최상위 부모를 삭제
        GameObject target = destroyTarget != null ? destroyTarget : transform.root.gameObject;
        Destroy(target);
    }
}
