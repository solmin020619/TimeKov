using System.Collections;
using UnityEngine;

public class Map1TestGun : MonoBehaviour
{
    [SerializeField] private PlayerWeaponController weaponCtrl;
    [SerializeField] private int fallbackWeaponId = 1201;

    private IEnumerator Start()
    {
        if (weaponCtrl == null)
            weaponCtrl = GetComponent<PlayerWeaponController>();

        if (weaponCtrl == null)
        {
            Debug.LogError("[Map1TestGun] PlayerWeaponController를 찾지 못함.");
            yield break;
        }

        // 한 프레임 말고 조금 더 기다려서
        // 다른 초기화가 끝난 뒤 마지막에 다시 장착
        yield return new WaitForSeconds(0.5f);

        int currentId = weaponCtrl.GetEquippedItemId();

        if (currentId <= 0)
            currentId = fallbackWeaponId;

        weaponCtrl.EquipByItemId(currentId);

        Debug.Log($"[Map1TestGun] 테스트용 탄창 채움. weaponId={currentId}");
    }
}