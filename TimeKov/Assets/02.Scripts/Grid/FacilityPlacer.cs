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
    // startDelay: 홀로그램 시작을 늦춘다(청사진이 여러 개를 도미노처럼 세울 때). 셀 점유는
    //   지연 없이 즉시 잡는다 - 기다리는 동안 같은 자리에 다른 배치가 끼어들면 안 되므로.
    // playSound: 청사진이 수십 개를 한 번에 놓을 때 사운드가 겹쳐 터지는 것 방지(첫 개만 재생).
    public IEnumerator PlaceRoutine(int facilityId, Vector3 position, Quaternion rotation, List<Vector2Int> footprintCells,
                                    float startDelay = 0f, bool playSound = true)
    {
        FacilityDataSheetData facility = GetFacilityData(facilityId);
        GameObject prefab = owner.PrefabDatabase != null ? owner.PrefabDatabase.GetPrefab(facilityId) : null;

        if (facility == null || prefab == null)
            yield break;

        occupancy.Occupy(footprintCells);

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        if (playSound)
            PlayBuildStartSound();

        GameObject hologramObj = Object.Instantiate(prefab, position, rotation);
        ApplyHologramVisual(hologramObj);
        DisableGhostComponents(hologramObj, owner);

        yield return new WaitForSeconds(owner.buildEffectDuration);

        if (hologramObj != null)
            Object.Destroy(hologramObj);

        GameObject obj = Object.Instantiate(prefab, position, rotation, owner.BuildParent);

        // 배치 직후 아웃라인 OFF — 플레이어가 가까이 가야만 MachineInteraction이 켜줌
        foreach (var outline in obj.GetComponentsInChildren<Outline>(true))
            outline.enabled = false;

        PlacedBuilding placedBuilding = obj.GetComponent<PlacedBuilding>();
        if (placedBuilding == null)
            placedBuilding = obj.AddComponent<PlacedBuilding>();

        placedBuilding.facilityId = facilityId;
        placedBuilding.occupiedCells = new List<Vector2Int>(footprintCells);
        placedBuilding.originCell = footprintCells[0];
        placedBuilding.CacheRenderers();

        // 몬스터(NavMeshAgent)가 설비를 뚫고 지나가지 못하도록 NavMesh 카빙 장애물 추가 (#15).
        // 플레이어는 물리 콜라이더로, 몬스터는 NavMesh 카빙으로 막힌다(우회 경로 탐색).
        // 런타임 배치 설비는 베이크가 불가하므로 NavMeshObstacle 카빙이 유일한 방법.
        AddNavMeshObstacle(obj, facility, owner.cellSize);

        // 설치 위치에 플레이어가 있으면 배치를 막지 않고 밀어낸다(#7, 엔드필드 방식).
        PushPlayerOutOfBuilding(obj, owner.PlayerRigidbody);

        FacilityInstance facilityInstance = obj.GetComponent<FacilityInstance>();
        if (facilityInstance == null)
            facilityInstance = obj.AddComponent<FacilityInstance>();

        facilityInstance.Initialize(facilityId);

        placedBuilding.SetupLabel(facility.facilityName, facility.gridW, facility.gridH, owner.cellSize);

        if (!owner.IsTopViewMode)
            placedBuilding.HideLabel();

        if (owner.IsRailSubMode)
            owner.RailManager?.RefreshPortIndicators();

        if (playSound)
            PlayBuildCompleteSound();
        SpawnBuildCompleteEffect(position, rotation);

        // 퀘스트 시스템에 설치 완료 통지 (사운드와 달리 개수 집계라 항상 개별 통지)
        GameEvents.RaiseFacilityPlaced(facilityId);
    }

    // 세이브 복원 전용 — 홀로그램 연출/사운드/VFX/퀘스트 알림 없이 즉시 배치한다.
    // PlaceRoutine과 달리 코루틴이 아니라 동기 호출이다.
    // 반환값: 배치된 GameObject(실패 시 null) — 호출부가 설비 내부 생산 상태(MachineBase)를
    // 추가로 복원할 때 컴포넌트를 꺼내 쓸 수 있게.
    public GameObject PlaceInstant(int facilityId, Vector3 position, Quaternion rotation, List<Vector2Int> footprintCells)
    {
        FacilityDataSheetData facility = GetFacilityData(facilityId);
        GameObject prefab = owner.PrefabDatabase != null ? owner.PrefabDatabase.GetPrefab(facilityId) : null;

        if (facility == null || prefab == null)
        {
            Debug.LogWarning($"[FacilityPlacer] 복원 실패 — facilityId={facilityId} 데이터/프리팹 없음");
            return null;
        }

        occupancy.Occupy(footprintCells);

        GameObject obj = Object.Instantiate(prefab, position, rotation, owner.BuildParent);

        foreach (var outline in obj.GetComponentsInChildren<Outline>(true))
            outline.enabled = false;

        PlacedBuilding placedBuilding = obj.GetComponent<PlacedBuilding>();
        if (placedBuilding == null)
            placedBuilding = obj.AddComponent<PlacedBuilding>();

        placedBuilding.facilityId = facilityId;
        placedBuilding.occupiedCells = new List<Vector2Int>(footprintCells);
        placedBuilding.originCell = footprintCells[0];
        placedBuilding.CacheRenderers();

        AddNavMeshObstacle(obj, facility, owner.cellSize);
        PushPlayerOutOfBuilding(obj, owner.PlayerRigidbody);

        FacilityInstance facilityInstance = obj.GetComponent<FacilityInstance>();
        if (facilityInstance == null)
            facilityInstance = obj.AddComponent<FacilityInstance>();

        facilityInstance.Initialize(facilityId);

        placedBuilding.SetupLabel(facility.facilityName, facility.gridW, facility.gridH, owner.cellSize);

        if (!owner.IsTopViewMode)
            placedBuilding.HideLabel();

        return obj;
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
            // VFX Graph 렌더러는 재질 교체가 막혀 있다. 건드려봐야 효과 없이 경고만 남는다.
            if (renderers[i] == null || renderers[i].GetType().Name == "VFXRenderer") continue;
            Material[] mats = renderers[i].materials;
            for (int j = 0; j < mats.Length; j++)
                mats[j] = owner.hologramMaterial;
            renderers[i].materials = mats;
        }
    }

    private void PlayBuildStartSound()   => GameSfx.Play(SfxId.BuildStart);      // 볼륨은 GameSfxConfig 에서(0.1)

    private void PlayBuildCompleteSound() => GameSfx.Play(SfxId.BuildComplete);

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

    // 배치된 설비에 NavMesh 카빙 장애물 추가 — 몬스터가 설비를 못 뚫고 우회하게 한다.
    static void AddNavMeshObstacle(GameObject obj, FacilityDataSheetData facility, float cellSize)
    {
        if (obj.GetComponent<UnityEngine.AI.NavMeshObstacle>() != null) return; // 중복 방지

        var obstacle = obj.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true; // 설비는 안 움직임 → 카빙 비용 절감

        float w = Mathf.Max(1f, facility.gridW) * cellSize;
        float d = Mathf.Max(1f, facility.gridH) * cellSize;
        // 높이는 넉넉히 — 설비 원점이 베이스/중앙 어디든 NavMesh와 겹쳐 확실히 카빙되도록.
        obstacle.center = Vector3.zero;
        obstacle.size   = new Vector3(w, 6f, d);
    }

    // 새로 놓인 설비와 겹친 플레이어를 수평으로 밀어낸다(#7, 엔드필드 방식).
    // ComputePenetration 으로 정확한 분리 벡터를 구해 즉시 빼낸다(물리 depenetration의
    // 거친 튕김 대신 제어된 1회 이동).
    static void PushPlayerOutOfBuilding(GameObject building, Rigidbody playerRb)
    {
        if (playerRb == null || building == null) return;
        var playerCol = playerRb.GetComponent<Collider>();
        if (playerCol == null) return;

        foreach (var col in building.GetComponentsInChildren<Collider>())
        {
            if (col == null || col.isTrigger) continue;

            if (!Physics.ComputePenetration(
                    playerCol, playerCol.transform.position, playerCol.transform.rotation,
                    col, col.transform.position, col.transform.rotation,
                    out Vector3 dir, out float dist))
                continue;

            // 위로 솟구치지 않게 수평 성분만 사용
            Vector3 horiz = new Vector3(dir.x, 0f, dir.z);
            if (horiz.sqrMagnitude < 1e-4f)
                horiz = playerRb.position - col.bounds.center;
            horiz.y = 0f;
            if (horiz.sqrMagnitude < 1e-4f) horiz = Vector3.right; // 정중앙 폴백
            horiz.Normalize();

            playerRb.position += horiz * (dist + 0.15f);
            playerRb.linearVelocity = Vector3.zero;
        }
    }
}
