using System.Collections;
using UnityEngine;

public class Map1TestGun : MonoBehaviour
{
    // Base에서 Map1 으로 넘어와서 싸우는거 테스트하고싶으면 혹시 모르니 이거 비활성화 해서 써
    // 세션 데이터(PlayerSessionData)가 무기 탄약을 덮어써서 테스트용 임시방편 스크립트
    // Inspector에서 직접 연결하는 게 가장 안전함
    [SerializeField] private PlayerWeaponController weaponCtrl;

    // 현재 무기가 없을 경우 사용할 기본 무기 ID
    [SerializeField] private int fallbackWeaponId = 1201;

    private IEnumerator Start()
    {
        // Inspector에서 연결 안 되어 있으면 자동으로 찾아옴
        if (weaponCtrl == null)
            weaponCtrl = GetComponent<PlayerWeaponController>();

        // 그래도 못 찾으면 에러 출력하고 종료
        if (weaponCtrl == null)
        {
            Debug.LogError("[Map1TestGun] PlayerWeaponController를 찾지 못함.");
            yield break;
        }

        // 한 프레임 기다림
        //  PlayerSessionData 로드 , 무기 자동 장착
        // 이런 초기화가 먼저 끝나게 하기 위해
        yield return null;

        // 현재 장착된 무기 ID 가져오기
        int currentId = weaponCtrl.GetEquippedItemId();

        // 무기가 없으면 fallback 무기 사용
        if (currentId <= 0)
            currentId = fallbackWeaponId;

        // 무기를 다시 장착하면 내부에서 탄창이 최대치로 채워짐
        // 테스트용으로 시작할 때 탄창을 채우는 방식
        weaponCtrl.EquipByItemId(currentId);

        Debug.Log($"[Map1TestGun] 테스트용 탄창 채움. weaponId={currentId}");
    }
}