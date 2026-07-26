// =====================================================================
// BuildZoneProgression.cs
// 건축 영역(BuildZone)의 현재 크기를 들고 있는 컴포넌트.
//
// ★[2026-07-22] 이 컴포넌트는 이제 "크기를 얼마로 할지" 를 정하지 않는다.
//   시키는 대로 키우기만 한다. 크기를 정하는 주인 = 우주선 수리(ShipRepairManager.LevelDef.zoneCells).
//
//   폐기 이력:
//     - 퀘스트 완료 트리거 (QuestManager 구독) : 진작 끊김
//     - 단계 리스트(stages) + 단계 인덱스      : 07-22 제거
//       "레벨 -> 단계번호 -> 칸수" 로 한 다리 건너가니 둘이 어긋났다.
//       실제로 씬의 단계 4개가 전부 38x38 로 같아져서 확장이 아예 안 먹고 있었다.
//     - 기지 업그레이드(BaseUpgradeManager)의 구역 확장 : 07-22 폐기(우주선으로 일원화)
//
// 사용법: ApplyCellSize(new Vector2Int(n, n)). 축소 요청은 무시된다.
//
// 크기 단위 = "설비 칸(cell) 개수". 실제 월드 크기는
//   cellSize(BuildManager 에서 런타임에 읽음) × 칸 개수 로 자동 환산.
//   → cellSize 값이 바뀌어도 코드 수정 불필요.
//
// 확장 방식:
//   - Grid_plane(gridPlane) 의 localScale 을 균등(uniform) 배율로 조정
//   - BuildZone 콜라이더는 Grid_plane 의 자식이므로 부모 스케일을 따라
//     자동으로 같이 커짐 (시각 바닥 + 판정 영역 항상 일치)
//   - Grid_plane 은 Hologram 셰이더(절차적 격자)라 스케일 키워도 안 깨짐
//
// 인스펙터 연결:
//   buildManager : 씬의 BuildManager (cellSize 참조용)
//   gridPlane    : Grid_plane Transform (BuildZone 의 부모)
//   zoneCollider : BuildZone 의 BoxCollider (현재 영역 bounds 조회용)
// =====================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

public class BuildZoneProgression : MonoBehaviour, ISaveable
{
    // ★[2026-07-22] 퀘스트 연동 단계(stages) 폐기.
    //   원래는 "퀘스트 클리어 -> 다음 단계 크기" 였는데 그 트리거는 진작 끊겼고,
    //   지금은 우주선 수리가 유일한 확장 수단이다(ShipRepairManager.LevelDef.zoneCells).
    //   단계 리스트를 남겨두면 "레벨 -> 단계번호 -> 칸수" 로 한 다리 건너가서 둘이 어긋난다.
    //   (실제로 씬의 단계 4개가 전부 38x38 로 같아져서 확장이 아예 안 먹고 있었다)
    //   -> 이 컴포넌트는 이제 "현재 칸 수" 하나만 안다. 크기를 정하는 건 우주선이다.

    [Header("시작 크기")]
    [Tooltip("게임 시작 시 적용할 한 변의 칸 수. 우주선이 Lv.1 값으로 곧 덮어쓰므로 안전망 역할이다.\n" +
             "우주선이 없는 씬에서도 영역이 0 이 되지 않게 한다.\n" +
             "★씬에 저장된 값이 이긴다 - 인스펙터의 Start Cells 도 같이 바꿔야 실제 반영된다.")]
    [SerializeField] private int startCells = 18;

    [Header("참조")]
    [Tooltip("cellSize 참조용. 비워두면 씬에서 자동 탐색.")]
    [SerializeField] private BuildManager buildManager;

    [Tooltip("Grid_plane Transform (BuildZone 의 부모). 스케일 조정 대상.")]
    [SerializeField] private Transform gridPlane;

    [Tooltip("BuildZone 의 BoxCollider. 현재 영역 월드 크기/bounds 조회용.")]
    [SerializeField] private BoxCollider zoneCollider;

    // 시작 시점 기준값 (배율 계산 기준)
    private Vector3 _baseGridScale;          // Grid_plane 초기 localScale
    private float _baseZoneWorldSize;        // 콜라이더 초기 월드 가로 크기 (배율 1 기준)
    private bool _captured;

    private int _currentStageIndex = -1;

    /// <summary>현재 적용된 확장 단계 인덱스 (세이브용). 미적용 상태면 -1.</summary>
    public int CurrentStageIndex => _currentStageIndex;

    // ── 초기화 ────────────────────────────────────────────────────

    private void Awake()
    {
        if (buildManager == null) buildManager = FindAnyObjectByType<BuildManager>();
        // CaptureBase() 는 여기서 부르지 않는다 — Awake 시점엔 부모 Transform 변경이
        // 자식 콜라이더 bounds 에 아직 반영 안 됐을 수 있음. 첫 ApplySize(Start) 때 캡처.

        SaveSlotManager.Instance?.Register(this);
    }

    private void Start()
    {
        // 안전망 크기. 우주선이 Lv.1 값(30칸)으로 곧 덮어쓴다.
        ApplyCellSize(new Vector2Int(startCells, startCells));

        // 저장된 크기가 더 크면 그쪽으로. (우주선 레벨 복원이 또 호출해도 축소 방지가 걸려 안전)
        RestoreFromSave();
    }

    private void OnDestroy()
    {
        SaveSlotManager.Instance?.Unregister(this);
    }

    // ── 세이브/로드 (ISaveable) ───────────────────────────────────────────

    // 저장하는 값 = "적용된 한 변의 칸 수". 예전엔 단계 인덱스였다(폐기).
    // 우주선 레벨만으로도 복원되지만, 우주선이 없는 저장본이나 미래의 다른 확장원을 위해 크기 자체를 남긴다.
    public void Capture(GameSaveData data)
    {
        data.buildZoneCells = _appliedCells;
    }

    private void RestoreFromSave()
    {
        if (SaveSlotManager.Instance == null || !SaveSlotManager.Instance.HasActiveSlot) return;

        int saved = SaveSlotManager.Instance.Data.buildZoneCells;
        if (saved > 0)
            ApplyCellSize(new Vector2Int(saved, saved));
    }

    private void CaptureBase()
    {
        if (_captured) return;

        if (gridPlane != null)
            _baseGridScale = gridPlane.localScale;
        else
            _baseGridScale = Vector3.one;

        // 콜라이더의 현재 월드 bounds 가로 크기를 배율 1 기준으로 캡처.
        // bounds 는 부모 스케일·회전이 모두 반영된 월드 AABB 라 회전(-90°) 신경 안 써도 됨.
        if (zoneCollider != null)
            _baseZoneWorldSize = Mathf.Max(zoneCollider.bounds.size.x, zoneCollider.bounds.size.z);
        else
            _baseZoneWorldSize = 1f;

        if (_baseZoneWorldSize < 0.0001f) _baseZoneWorldSize = 1f;

        _captured = true;
    }

    // ── 크기 적용 ─────────────────────────────────────────────────

    /// <summary>
    /// 칸 수를 직접 지정해 영역을 확장한다. 단계(stage) 인덱스를 안 거치는 경로.
    ///
    /// ★우주선 수리가 이 경로를 쓴다. 단계 리스트를 따로 관리하면
    ///   "우주선 레벨 -> 단계 번호 -> 칸 수" 로 한 다리 건너가서, 둘이 어긋나면
    ///   기획서 표와 실제 크기가 조용히 달라진다(실제로 stages 4개가 전부 38x38 로
    ///   같아져서 확장이 아예 안 먹고 있었다). 레벨 정의에 칸 수를 직접 적는 게 낫다.
    ///
    /// 축소는 무시한다 - 이미 설치된 설비가 영역 밖으로 밀려나면 안 된다.
    /// </summary>
    public void ApplyCellSize(Vector2Int sizeInCells)
    {
        int cells = Mathf.Max(1, Mathf.Max(sizeInCells.x, sizeInCells.y));
        if (cells <= _appliedCells) return;   // 뒤로 가기 방지

        _appliedCells = cells;
        ApplySize(new Vector2Int(cells, cells));
    }

    /// <summary>현재 적용된 한 변의 칸 수. 0 이면 아직 미적용.</summary>
    public int AppliedCells => _appliedCells;

    private int _appliedCells;

    private void ApplySize(Vector2Int sizeInCells)
    {
        CaptureBase(); // 안전 가드

        float cellSize = buildManager != null ? buildManager.cellSize : 1f;

        // 목표 월드 가로 크기 (가장 큰 칸 변 기준 — 정사각 영역 가정).
        int cells = Mathf.Max(1, Mathf.Max(sizeInCells.x, sizeInCells.y));
        float targetWorld = cells * cellSize;

        // 배율 = 목표 월드 크기 / 배율 1 기준 콜라이더 월드 크기
        float factor = targetWorld / _baseZoneWorldSize;

        // Grid_plane 균등 스케일 — 자식 BuildZone 콜라이더도 함께 커짐.
        // (균등 배율이라 회전 -90° 와 무관하게 안전)
        if (gridPlane != null)
            gridPlane.localScale = _baseGridScale * factor;
    }

    // ── 외부 조회 (BuildManager 판정용) ───────────────────────────

    /// <summary>현재 건축 영역의 월드 AABB. 콜라이더 미연결이면 false.</summary>
    public bool TryGetZoneBounds(out Bounds bounds)
    {
        if (zoneCollider != null)
        {
            bounds = zoneCollider.bounds;
            return true;
        }
        bounds = default;
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        startCells = Mathf.Max(1, startCells);
    }
#endif
}
