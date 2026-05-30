// =====================================================================
// BuildZoneProgression.cs
// 퀘스트 완료에 따라 건축 영역(BuildZone)을 단계적으로 확장한다.
//
// 동작:
//   - 게임 시작 시 stages[0] 크기로 영역 초기화
//   - QuestManager.OnQuestCompleted 구독 → 완료된 퀘스트 id 가
//     어떤 stage 의 questId 와 일치하면 그 stage 크기로 확장
//
// 기획자 워크플로우 (코드 수정 없음):
//   - 인스펙터의 stages 리스트에서 단계 추가/삭제
//   - 각 단계에 연동할 questId(문자열) + 크기(셀 개수) 입력
//   - 단계 개수·순서·크기·연동 퀘스트 전부 인스펙터에서 조정
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

public class BuildZoneProgression : MonoBehaviour
{
    [Serializable]
    public class Stage
    {
        [Tooltip("이 퀘스트(QuestSO.id)가 완료되면 아래 크기로 확장.\n" +
                 "비워두면 '시작 단계'로 취급 (게임 시작 시 적용).")]
        public string questId;

        [Tooltip("건축 영역 크기 (가로 칸 수 × 세로 칸 수). 예: (2,2), (3,3), (4,4).\n" +
                 "실제 월드 크기 = 칸 수 × cellSize 로 자동 환산됨.")]
        public Vector2Int sizeInCells = new Vector2Int(2, 2);
    }

    [Header("단계 정의 (기획자 편집)")]
    [Tooltip("건축 영역 확장 단계 목록. 위에서부터 순서대로 진행.\n" +
             "0번 = 시작 크기, 이후 각 단계의 questId 완료 시 그 크기로 확장.")]
    [SerializeField] private List<Stage> stages = new List<Stage>();

    [Header("참조")]
    [Tooltip("cellSize 참조용. 비워두면 씬에서 자동 탐색.")]
    [SerializeField] private BuildManager buildManager;

    [Tooltip("Grid_plane Transform (BuildZone 의 부모). 스케일 조정 대상.")]
    [SerializeField] private Transform gridPlane;

    [Tooltip("BuildZone 의 BoxCollider. 현재 영역 월드 크기/bounds 조회용.")]
    [SerializeField] private BoxCollider zoneCollider;

    [Header("디버그")]
    [Tooltip("단계 변경 로그 출력")]
    [SerializeField] private bool logStageChanges = true;

    // 시작 시점 기준값 (배율 계산 기준)
    private Vector3 _baseGridScale;          // Grid_plane 초기 localScale
    private float _baseZoneWorldSize;        // 콜라이더 초기 월드 가로 크기 (배율 1 기준)
    private bool _captured;

    private int _currentStageIndex = -1;

    // ── 초기화 ────────────────────────────────────────────────────

    private void Awake()
    {
        if (buildManager == null) buildManager = FindAnyObjectByType<BuildManager>();
        // CaptureBase() 는 여기서 부르지 않는다 — Awake 시점엔 부모 Transform 변경이
        // 자식 콜라이더 bounds 에 아직 반영 안 됐을 수 있음. 첫 ApplySize(Start) 때 캡처.
    }

    private void OnEnable()
    {
        // QuestManager 가 아직 준비 안 됐을 수 있으니 가드. 준비됐으면 즉시 구독.
        TrySubscribe();
    }

    private void Start()
    {
        // Awake 순서 문제로 OnEnable 때 QuestManager 가 없었을 경우 한 번 더 시도
        TrySubscribe();

        // 시작 단계(questId 비어있는 첫 stage, 없으면 stages[0]) 적용
        ApplyInitialStage();
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
    }

    private bool _subscribed;
    private void TrySubscribe()
    {
        if (_subscribed) return;
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.OnQuestCompleted += HandleQuestCompleted;
        _subscribed = true;
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

    // ── 퀘스트 완료 콜백 ──────────────────────────────────────────

    private void HandleQuestCompleted(CategoryRuntime rt, QuestSO quest)
    {
        if (quest == null || string.IsNullOrEmpty(quest.id)) return;

        for (int i = 0; i < stages.Count; i++)
        {
            var s = stages[i];
            if (s == null || string.IsNullOrEmpty(s.questId)) continue;
            if (s.questId == quest.id)
            {
                ApplyStage(i);
                return;
            }
        }
    }

    // ── 단계 적용 ─────────────────────────────────────────────────

    private void ApplyInitialStage()
    {
        if (stages == null || stages.Count == 0) return;

        // questId 가 비어있는 첫 stage 를 시작 단계로. 없으면 0번.
        int startIdx = 0;
        for (int i = 0; i < stages.Count; i++)
        {
            if (stages[i] != null && string.IsNullOrEmpty(stages[i].questId)) { startIdx = i; break; }
        }
        ApplyStage(startIdx);
    }

    /// <summary>지정 단계 크기로 영역을 확장한다. 이미 더 큰(또는 같은) 단계면 무시.</summary>
    public void ApplyStage(int index)
    {
        if (stages == null || index < 0 || index >= stages.Count) return;

        // 뒤로 가기(축소) 방지 — 더 낮은 단계 요청은 무시
        if (index <= _currentStageIndex) return;

        var stage = stages[index];
        if (stage == null) return;

        _currentStageIndex = index;
        ApplySize(stage.sizeInCells);

        if (logStageChanges)
            Debug.Log($"[BuildZoneProgression] 단계 {index} 적용 — {stage.sizeInCells.x}x{stage.sizeInCells.y} 칸" +
                      (string.IsNullOrEmpty(stage.questId) ? " (시작)" : $" (quest: {stage.questId})"));
    }

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
        if (stages != null)
            foreach (var s in stages)
                if (s != null)
                {
                    s.sizeInCells.x = Mathf.Max(1, s.sizeInCells.x);
                    s.sizeInCells.y = Mathf.Max(1, s.sizeInCells.y);
                }
    }

    // 컴포넌트 처음 부착 시 기본 단계값을 채워준다. (기획자가 빈 리스트부터 안 만들어도 되게)
    // 실제 questId/크기는 인스펙터에서 자유롭게 수정·추가·삭제 가능.
    private void Reset()
    {
        if (stages != null && stages.Count > 0) return;

        stages = new List<Stage>
        {
            new Stage { questId = "",                                       sizeInCells = new Vector2Int(20, 20) }, // 시작
            new Stage { questId = "quest_tutorial_14_place_bio_extractor",  sizeInCells = new Vector2Int(30, 30) }, // 첫 설비 설치
            new Stage { questId = "quest_tutorial_18_place_bio_injector",   sizeInCells = new Vector2Int(45, 45) }, // 두 번째 설비
            new Stage { questId = "quest_tutorial_22_use_healing_ampoule",  sizeInCells = new Vector2Int(60, 60) }, // 튜토리얼 완주
        };
    }
#endif
}
