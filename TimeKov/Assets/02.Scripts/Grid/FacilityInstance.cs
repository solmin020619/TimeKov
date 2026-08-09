using System.Collections.Generic;
using UnityEngine;

public class FacilityInstance : MonoBehaviour
{
    // 설치하는 순간 FacilityPlacer 가 Initialize(facilityId) 로 채운다.
    // 프리팹에 박아둔 값은 살아남지 못한다.
    //
    // ★[08-09] 설비 레벨 시스템 제거.
    //   FacilityLevelData 시트는 전 행 processTimeMultiplier=1 이라 효과가 0 이었고,
    //   레벨을 올리는 TryUpgrade 호출부가 한 곳도 없어서 모든 설비가 영원히 Lv.1 이었다.
    //   기획을 펼치던 시절의 잔해라 시트/코드/세이브 필드까지 같이 걷어냈다.
    //   공장 가동속도 조절은 우주선 수리(ShipRepairManager.FactorySpeedMultiplier)가 담당한다.
    //   되살릴 일이 생기면 그때는 우주선 쪽과 축이 겹치므로 설계부터 다시 해야 한다.
    [Header("Runtime Data")]
    [FilledBy("설치할 때 FacilityPlacer 가 지정 (설비 종류는 빌드 슬롯에서 고른다)")]
    [SerializeField] private int facilityId;

    private FacilityDataSheetData facilityData;

    public int FacilityId => facilityId;
    public FacilityDataSheetData FacilityData => facilityData;

    // facilityId 로 초기화. BuildManager 가 설치 직후 호출.
    public void Initialize(int newFacilityId)
    {
        facilityId = newFacilityId;
        RefreshCachedData();
    }

    // GameDataHolder 에서 최신 데이터를 다시 읽어 캐시 갱신.
    public void RefreshCachedData()
    {
        if (!GameDataHolder.I.FacilityData.TryGet(facilityId.ToString(), out facilityData))
        {
            Debug.LogError($"[FacilityInstance] FacilityData 없음. facilityId={facilityId}");
            facilityData = null;
        }
    }

    // 회전 가능 여부 (canRotate)
    public bool CanRotate()
    {
        if (facilityData == null) return false;
        return facilityData.canRotate;
    }


    public List<RecipeDataSheetData> GetAvailableRecipes()
    {
        return GameDataUtility.GetRecipesByFacilityId(facilityId);
    }

    // 우주선 수리 보상(공장 가동속도 전역 배수)을 곱한 실제 제작 시간. 우주선 없으면 1배.
    public float GetFinalProcessTime(float baseTime)
        => baseTime * ShipRepairManager.FactorySpeedMultiplier;
}
