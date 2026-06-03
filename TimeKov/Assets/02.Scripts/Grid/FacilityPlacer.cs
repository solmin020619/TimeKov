// =====================================================================
// FacilityPlacer.cs
// 설비 배치 "실행" 전담 - 홀로그램 연출 -> 실제 배치 -> 사운드/VFX/퀘스트 통지.
// 어떤 설비를 어디에 놓을지(선택/프리뷰)는 BuildManager 가 정하고, 그 결과(facilityId,
// 위치, 회전, footprint)를 받아 여기서 실제로 만든다.
// 코루틴은 IEnumerator 로 반환하고 BuildManager 가 StartCoroutine 으로 돌린다.
// 설정값은 BuildManager 인스펙터에 남겨두고 owner 통해 읽는다 (인스펙터 작업 불필요).
// =====================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FacilityPlacer
{
    private readonly BuildManager owner;
    private readonly GridOccupancy occupancy;

    public FacilityPlacer(BuildManager owner, GridOccupancy occupancy)
    {
        this.owner = owner;
        this.occupancy = occupancy;
    }

    // 홀로그램 연출을 거쳐 facilityId 설비를 배치. BuildManager 가 StartCoroutine 으로 호출.
    public IEnumerator PlaceRoutine(int facilityId, Vector3 position, Quaternion rotation, List<Vector2Int> footprintCells)
    {
        FacilityDataSheetData facility = GetFacilityData(facilityId);
        GameObject prefab = owner.PrefabDatabase != null ? owner.PrefabDatabase.GetPrefab(facilityId) : null;

        if (facility == null || prefab == null)
            yield break;

        occupancy.Occupy(footprintCells);
        PlayBuildStartSound();

        GameObject hologramObj = Object.Instantiate(prefab, position, rotation);
        ApplyHologramVisual(hologramObj);
        DisableGhostComponents(hologramObj, owner);

        yield return new WaitForSeconds(owner.buildEffectDuration);

        if (hologramObj != null)
            Object.Destroy(hologramObj);

        GameObject obj = Object.Instantiate(prefab, position, rotation, owner.BuildParent);

        PlacedBuilding placedBuilding = obj.GetComponent<PlacedBuilding>();
        if (placedBuilding == null)
            placedBuilding = obj.AddComponent<PlacedBuilding>();

        placedBuilding.facilityId = facilityId;
        placedBuilding.currentLevel = 1;
        placedBuilding.occupiedCells = new List<Vector2Int>(footprintCells);
        placedBuilding.originCell = footprintCells[0];
        placedBuilding.CacheRenderers();

        FacilityInstance facilityInstance = obj.GetComponent<FacilityInstance>();
        if (facilityInstance == null)
            facilityInstance = obj.AddComponent<FacilityInstance>();

        facilityInstance.Initialize(facilityId);

        placedBuilding.SetupLabel(facility.facilityName, facility.gridW, facility.gridH, owner.cellSize);

        if (!owner.IsTopViewMode)
            placedBuilding.HideLabel();

        if (owner.IsRailSubMode)
            owner.RailManager?.RefreshPortIndicators();

        PlayBuildCompleteSound();
        SpawnBuildCompleteEffect(position, rotation);

        // 퀘스트 시스템에 설치 완료 통지
        GameEvents.RaiseFacilityPlaced(facilityId);
    }

    private FacilityDataSheetData GetFacilityData(int facilityId)
    {
        if (GameDataHolder.I.FacilityData.TryGet(facilityId.ToString(), out var data))
            return data;
        return null;
    }

    private void ApplyHologramVisual(GameObject target)
    {
        if (target == null || owner.hologramMaterial == null)
            return;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;
            for (int j = 0; j < mats.Length; j++)
                mats[j] = owner.hologramMaterial;
            renderers[i].materials = mats;
        }
    }

    private void PlayBuildStartSound()
    {
        if (owner.audioSource == null || owner.buildStartClip == null) return;
        owner.audioSource.PlayOneShot(owner.buildStartClip, 0.1f);
    }

    private void PlayBuildCompleteSound()
    {
        if (owner.audioSource == null || owner.buildCompleteClip == null) return;
        owner.audioSource.PlayOneShot(owner.buildCompleteClip);
    }

    private void SpawnBuildCompleteEffect(Vector3 position, Quaternion rotation)
    {
        if (owner.buildCompleteEffectPrefab == null) return;
        Object.Instantiate(owner.buildCompleteEffectPrefab, position + owner.buildCompleteEffectOffset, rotation);
    }

    // 홀로그램/프리뷰 오브젝트의 물리·스크립트 비활성화 (충돌·동작 없이 보이기만).
    // 배치 코루틴과 BuildManager.RefreshPreviewMarker(프리뷰)가 공유하므로 static.
    // skip: 비활성화에서 제외할 MonoBehaviour (보통 BuildManager 자신).
    public static void DisableGhostComponents(GameObject obj, MonoBehaviour skip)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody[] rigidbodies = obj.GetComponentsInChildren<Rigidbody>();
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
        }

        MonoBehaviour[] behaviours = obj.GetComponentsInChildren<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null) continue; // 프리팹의 미싱 스크립트 보호
            if (behaviours[i] != skip)
                behaviours[i].enabled = false;
        }
    }
}
