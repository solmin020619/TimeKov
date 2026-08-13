// =====================================================================
// BlueprintController.cs
// 청사진(복사-붙여넣기) 모드. 빌드 모드 안에서 N 키(또는 알약 버튼)로 켜고 끈다.
//
// [사용 흐름]
//   1) 윈도우 바탕화면처럼 얇은 사각형으로 드래그해 영역 선택
//   2) 놓는 순간 캡처 -> 원본과 똑같이 생긴 고스트가 커서를 따라다닌다.
//      R = 90도 회전, 좌클릭 = 도장(연속 가능)
//   3) N/슬롯키/E/X = 다른 모드로 전환, 우클릭/ESC/B = 빌드 모드 종료(기존 동작 그대로)
//
// [고스트는 "기존 미리보기"와 똑같이 보인다]
//   설비 = 수동 배치 프리뷰와 동일(프리팹 + 초록/빨강 틴트 + 포트 화살표(ShowGhostPortArrows)).
//   레일 = 레일 모드의 경로 프리뷰와 동일(실제 직선/코너 비주얼 + 고스트 머티리얼 스왑,
//   흐름 화살표 방향은 원본에서 복사). 설치 불가만 빨강.
//   "설치 전"이라는 상태가 기존 미리보기와 같은 문법으로 읽히게 한다(종욱 확정).
//
// [구조 원칙 - 지난 청사진이 죽은 이유를 막는 장치]
//   지난 버전(2026-06 삭제)은 프리뷰/배치/회전을 전부 자기 코드로 재구현해서 실제
//   배치와 어긋났다(색/방향/잔재 버그 반복). 이번 버전은 "새로 구현"이 없다:
//     배치        = BuildManager.PlaceFacilityTracked (수동 배치와 같은 경로, 상한 장부 포함)
//     레일        = PlaceRailImmediate + RestoreReflowAndValidate (세이브 복원과 같은 경로)
//     레일 고스트 = RailPiece.ApplyVisual + 실제 레일 프리팹 (실물과 같은 직선/코너/화살표)
//     벨트 연결   = BeltSegment.ReconnectAll (레일 형상 기반 감지라 장부 복구 불필요)
//     유효성      = 수동 배치의 canBuild 와 같은 검사(존/점유/레일/물리) + 설치 상한
//     회전 수학   = GridMath 의 순수 함수만 호출. 다른 회전 구현을 두지 않는다
//   - 프리뷰와 도장이 "같은 프레임에 계산한 같은 배치 계획(resolved)"을 소비한다.
//     프리뷰는 괜찮은데 설치는 실패하는 종류의 어긋남이 구조적으로 불가능하다.
//   - 고스트는 전부 루트 오브젝트 하나 밑에 만든다. Deactivate 가 루트를 지우므로
//     내부 리스트가 어떤 상태든 잔재가 남을 수 없다.
//
// [레일 고스트에서 스크립트를 끄지 않고 '제거'하는 이유]
//   레일 비주얼 프리팹에는 벨트 물류(BeltSegment 등)가 붙어 있을 수 있다. enabled=false 로
//   꺼도 전역 레지스트리(All)에 남아 ReconnectAll 스냅샷에 잡히면 고스트가 진짜 벨트
//   체인에 엮이는 사고가 난다. Destroy 는 OnDestroy 에서 등록 해제까지 타므로 안전하다.
//
// [건축 범위(BuildZone)와의 관계]
//   범위 판정은 매 프레임 owner.IsCellInBuildZone 라이브 검사다. 우주선 수리로 범위가
//   넓어지면 같은 청사진이 그 자리에서 설치 가능으로 바뀐다. 별도 갱신 코드가 필요 없다.
//
// [도장 직후의 벨트 색]
//   설비는 홀로그램 연출 뒤에 실물이 생기므로, 그 사이 포트에 닿은 벨트는 잠깐
//   미연결로 보일 수 있다. 연출이 끝난 뒤 FinalizeRoutine 이 reflow + ReconnectAll 을
//   한 번 더 돌려 정리한다(세이브 복원의 마무리와 같은 순서). 이 코루틴은 owner 에서
//   돌므로 도장 직후 모드를 나가도 끝까지 실행된다.
// =====================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BlueprintController
{
    private readonly BuildManager owner;

    public BlueprintController(BuildManager owner)
    {
        this.owner = owner;
    }

    // ── 상태 ─────────────────────────────────────────────────────────
    private enum State { Selecting, Pasting }
    private State state = State.Selecting;
    private bool isActive;

    private bool isDragging;
    private Vector2Int dragStartCell;
    private Vector2Int dragEndCell;
    private Vector2 dragStartScreen;   // 마퀴(스크린 사각형)용

    // ── 클립보드 (캡처 결과, 앵커 기준 상대 좌표) ────────────────────
    private struct FacilityEntry
    {
        public int facilityId;
        public Vector2Int offset;      // originCell - anchor (자체 회전이 이미 반영된 시작 셀)
        public Vector2Int placedSize;  // 배치 당시 footprint 크기 (자체 회전 반영)
        public int rotationY;          // 배치 당시 Y 회전 (0/90/180/270)
    }

    private struct RailEntry
    {
        public Vector2Int offset;
        public bool up, down, left, right;
        public Vector2Int flowDir;     // 원본의 흐름 방향(화살표). zero = 미지정
    }

    private readonly List<FacilityEntry> facilities = new();
    private readonly List<RailEntry> rails = new();
    private int pasteRotationY;

    // ── 배치 계획 (매 프레임 ResolveLayout 이 채우고, 도장이 그대로 소비) ──
    private struct ResolvedFacility
    {
        public int facilityId;
        public Vector3 worldPos;
        public Quaternion worldRot;
        public Vector2Int size;
        public List<Vector2Int> footprint;
        public bool valid;
    }

    private struct ResolvedRail
    {
        public Vector2Int cell;
        public bool up, down, left, right;
        public bool valid;
    }

    private readonly List<ResolvedFacility> resolvedFacilities = new();
    private readonly List<ResolvedRail> resolvedRails = new();
    private bool allValid;

    // ResolveLayout 작업용 (프레임마다 재사용)
    private readonly HashSet<Vector2Int> stampFacilityCells = new();
    private readonly Dictionary<int, int> stampValidCountByFacility = new();

    // 포트 앞칸 캐시 (레일이 설비 점유칸에 올 수 있는 유일한 예외 - CanUseCellAsRail 과 같은 규칙).
    private readonly HashSet<Vector2Int> portFrontCells = new();
    private int portFrontCacheFrame = -1;

    // ── 고스트 ───────────────────────────────────────────────────────
    private GameObject ghostRoot;                                  // 단일 부모. 정리는 이것만 지우면 끝
    private readonly List<GameObject> facilityGhosts = new();
    private readonly List<Renderer[]> facilityGhostRenderers = new();
    private readonly List<RailPiece> railGhostPieces = new();      // 실제 레일 비주얼을 입힌 고스트
    private readonly List<Renderer[]> railGhostRenderers = new();
    private int railGhostRotationBuilt = -1;                       // 이 회전값으로 레일 고스트 모양을 만들었나

    // ── 마퀴 (윈도우식 드래그 사각형, 스크린 스페이스 UI) ────────────
    private GameObject marqueeRoot;
    private RectTransform marqueeFill;
    private RectTransform[] marqueeBorders;   // left/right/bottom/top
    private const float MarqueeBorderPx = 2f;

    // 색: 수동 배치 프리뷰(UpdatePreview)와 동일 규칙 - 가능 = 초록 / 불가 = 빨강
    private static readonly Color TintValid = Color.green;
    private static readonly Color TintInvalid = Color.red;

    private static MaterialPropertyBlock _mpb;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int RailColorId = Shader.PropertyToID("_rail_color");   // 레일 셰이더 전용 색 키

    // ── 수명 ─────────────────────────────────────────────────────────

    public void Activate()
    {
        if (isActive) return;
        isActive = true;

        state = State.Selecting;
        isDragging = false;
        pasteRotationY = 0;
        railGhostRotationBuilt = -1;
        facilities.Clear();
        rails.Clear();

        // 레일 모드와 같은 이유로 작업 중엔 벨트 연결색 판정을 보류(작업 중 빨강 숨김)
        TIMEKOV.Factory.BeltSegment.SuppressConnectionColor = true;

        ghostRoot = new GameObject("[BlueprintGhosts]");

        ToastManager.Info(Loc.Get("청사진: 드래그로 복사할 영역을 선택하세요"));
    }

    public void Deactivate()
    {
        if (!isActive) return;
        isActive = false;

        isDragging = false;
        state = State.Selecting;
        facilities.Clear();
        rails.Clear();
        resolvedFacilities.Clear();
        resolvedRails.Clear();

        DestroyAllGhosts();

        // EndRailMode 와 같은 마무리 - 보류 해제 후 전체 재감지(안전망)
        TIMEKOV.Factory.BeltSegment.SuppressConnectionColor = false;
        TIMEKOV.Factory.BeltSegment.ReconnectAll();
    }

    // ── 프레임 ───────────────────────────────────────────────────────

    public void Tick()
    {
        if (!isActive || owner == null) return;

        // 튜토리얼 코치 중엔 입력 무시 (HandleBuild 와 동일 게이트)
        if (GameUIController.Instance != null && GameUIController.Instance.IsTutorialCoachActive)
            return;

        if (state == State.Selecting) TickSelecting();
        else TickPasting();
    }

    private void TickSelecting()
    {
        bool hasCursor = TryGetCursorCell(out Vector2Int cell);

        if (!isDragging)
        {
            if (hasCursor && Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            {
                isDragging = true;
                dragStartCell = cell;
                dragEndCell = cell;
                dragStartScreen = Input.mousePosition;
            }
            return;
        }

        // 드래그 중. 커서가 그리드 밖으로 나가면 마지막 유효 셀 유지.
        if (hasCursor)
            dragEndCell = cell;
        UpdateMarquee(dragStartScreen, Input.mousePosition);

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            HideMarquee();

            Vector2Int min = Vector2Int.Min(dragStartCell, dragEndCell);
            Vector2Int max = Vector2Int.Max(dragStartCell, dragEndCell);
            CaptureRegion(min, max);

            if (facilities.Count == 0 && rails.Count == 0)
            {
                ToastManager.Info(Loc.Get("선택 영역에 복사할 것이 없습니다"));
                return;   // Selecting 유지 - 다시 드래그
            }

            BuildGhosts();
            state = State.Pasting;
            ToastManager.Info(string.Format(Loc.Get("설비 {0}개, 레일 {1}칸 복사됨"), facilities.Count, rails.Count));
        }
    }

    private void TickPasting()
    {
        if (Input.GetKeyDown(KeyCode.R))
            pasteRotationY = (pasteRotationY + 90) % 360;

        // 회전이 바뀌면 레일 고스트의 직선/코너 모양을 다시 만든다 (설비 고스트는 회전만 하면 됨)
        if (railGhostRotationBuilt != pasteRotationY)
            RebuildRailGhostShapes();

        if (!TryGetCursorCell(out Vector2Int anchor))
        {
            if (ghostRoot != null) ghostRoot.SetActive(false);
            return;
        }
        if (ghostRoot != null && !ghostRoot.activeSelf) ghostRoot.SetActive(true);

        ResolveLayout(anchor);
        ApplyGhostTransformsAndTints();

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            if (allValid) Stamp();
            else ToastManager.Warning(Loc.Get("설치할 수 없는 위치가 있습니다"));
        }
    }

    // ── 캡처 ─────────────────────────────────────────────────────────

    private void CaptureRegion(Vector2Int min, Vector2Int max)
    {
        facilities.Clear();
        rails.Clear();

        // 1) 영역에 걸친 설비 수집 (한 칸이라도 걸치면 통째로 포함)
        var picked = new List<(int id, Vector2Int start, Vector2Int size, int rotY)>();
        int skippedStale = 0;
        bool skippedEdgePort = false;

        if (owner.BuildParent != null)
        {
            foreach (var pb in owner.BuildParent.GetComponentsInChildren<PlacedBuilding>())
            {
                if (pb == null || pb.occupiedCells == null || pb.occupiedCells.Count == 0) continue;

                bool inside = false;
                int cMinX = int.MaxValue, cMinY = int.MaxValue, cMaxX = int.MinValue, cMaxY = int.MinValue;
                foreach (var c in pb.occupiedCells)
                {
                    if (c.x >= min.x && c.x <= max.x && c.y >= min.y && c.y <= max.y) inside = true;
                    if (c.x < cMinX) cMinX = c.x;
                    if (c.y < cMinY) cMinY = c.y;
                    if (c.x > cMaxX) cMaxX = c.x;
                    if (c.y > cMaxY) cMaxY = c.y;
                }
                if (!inside) continue;

                // 시트/프리팹이 사라진 설비는 복사해봐야 붙일 수 없다 - 캡처에서 제외
                if (GetFacilityData(pb.facilityId) == null
                    || owner.PrefabDatabase == null || owner.PrefabDatabase.GetPrefab(pb.facilityId) == null)
                {
                    skippedStale++;
                    continue;
                }

                // 테두리 전용 모드가 켜져 있으면 창고 출력 포트는 자유 배치가 불가 - 제외
                if (owner.edgeOnlyStoragePort && pb.facilityId == FacilityBuildLimit.WarehousePortId)
                {
                    skippedEdgePort = true;
                    continue;
                }

                picked.Add((pb.facilityId,
                            new Vector2Int(cMinX, cMinY),
                            new Vector2Int(cMaxX - cMinX + 1, cMaxY - cMinY + 1),
                            GridMath.Normalize90(Mathf.RoundToInt(pb.transform.eulerAngles.y))));
            }
        }

        // 2) 영역 안 레일 수집
        var pickedRailCells = new HashSet<Vector2Int>();
        var railMap = owner.RailManager != null ? owner.RailManager.RailMap : null;
        if (railMap != null)
        {
            foreach (var kv in railMap)
            {
                Vector2Int c = kv.Key;
                if (kv.Value == null) continue;
                if (c.x < min.x || c.x > max.x || c.y < min.y || c.y > max.y) continue;
                pickedRailCells.Add(c);
            }
        }

        if (picked.Count == 0 && pickedRailCells.Count == 0)
        {
            // '데이터가 없는 설비'는 시트가 깨졌을 때만 생긴다. 플레이어가 읽어도 할 게 없어서
            // 화면에는 안 띄우고 로그로만 남긴다("복사할 게 없다"는 안내는 아래에서 따로 나간다).
            if (skippedStale > 0)
                Debug.LogWarning($"[Blueprint] 시트 데이터가 없는 설비 {skippedStale}개를 건너뛰었다");
            return;
        }

        // 3) 앵커 = 캡처 내용 전체 바운딩 박스의 중심 셀
        int bMinX = int.MaxValue, bMinY = int.MaxValue, bMaxX = int.MinValue, bMaxY = int.MinValue;
        foreach (var p in picked)
        {
            bMinX = Mathf.Min(bMinX, p.start.x);
            bMinY = Mathf.Min(bMinY, p.start.y);
            bMaxX = Mathf.Max(bMaxX, p.start.x + p.size.x - 1);
            bMaxY = Mathf.Max(bMaxY, p.start.y + p.size.y - 1);
        }
        foreach (var c in pickedRailCells)
        {
            bMinX = Mathf.Min(bMinX, c.x);
            bMinY = Mathf.Min(bMinY, c.y);
            bMaxX = Mathf.Max(bMaxX, c.x);
            bMaxY = Mathf.Max(bMaxY, c.y);
        }
        Vector2Int anchor = new Vector2Int((bMinX + bMaxX) / 2, (bMinY + bMaxY) / 2);

        foreach (var p in picked)
        {
            facilities.Add(new FacilityEntry
            {
                facilityId = p.id,
                offset = p.start - anchor,
                placedSize = p.size,
                rotationY = p.rotY,
            });
        }

        // 4) 레일 - 경계에서 잘린 연결만 정리한다.
        //    연결 bool 이 가리키는 이웃이
        //      a) 캡처된 레일        -> 유지 (묶음 안에서 이어짐)
        //      b) 캡처 안 된 레일    -> 제거 (경계에서 잘렸다. 남기면 허공을 가리키는 코너가 생긴다)
        //      c) 레일이 아닌 칸     -> 유지 (포트/설비를 향한 head-on 연결. 벨트 연결 감지가 이 모양을 본다)
        //    흐름 방향(flowFrom, 화살표)도 그대로 복사해 고스트가 원본과 같은 방향으로 보이게 한다.
        foreach (var c in pickedRailCells)
        {
            RailPiece piece = railMap[c];
            bool KeepDir(Vector2Int n) => pickedRailCells.Contains(n) || !railMap.ContainsKey(n);

            rails.Add(new RailEntry
            {
                offset = c - anchor,
                up    = piece.up    && KeepDir(c + Vector2Int.up),
                down  = piece.down  && KeepDir(c + Vector2Int.down),
                left  = piece.left  && KeepDir(c + Vector2Int.left),
                right = piece.right && KeepDir(c + Vector2Int.right),
                flowDir = piece.flowFrom,
            });
        }

        if (skippedStale > 0)
            Debug.LogWarning($"[Blueprint] 시트 데이터가 없는 설비 {skippedStale}개를 제외했다");
        if (skippedEdgePort)
            ToastManager.Info(Loc.Get("테두리 전용 설비는 청사진에서 제외했습니다"));
    }

    // ── 배치 계획 (프리뷰와 도장이 공유하는 유일한 계산) ─────────────

    private void ResolveLayout(Vector2Int anchor)
    {
        resolvedFacilities.Clear();
        resolvedRails.Clear();
        stampFacilityCells.Clear();
        stampValidCountByFacility.Clear();
        allValid = true;

        // 설비 먼저 - 레일 검증이 "이번 도장의 설비 칸" 집합을 쓴다
        foreach (var f in facilities)
        {
            int finalRot = GridMath.Normalize90(f.rotationY + pasteRotationY);
            Vector2Int start = anchor + GridMath.RotateRectStart(f.offset, f.placedSize, pasteRotationY);
            Vector2Int size = GridMath.RotatedSize(f.placedSize, pasteRotationY);
            List<Vector2Int> footprint = GridMath.Footprint(start, size);
            Vector3 pos = GridMath.StartCellToWorldCenter(start, size, owner.GridOriginPos, owner.cellSize, owner.fixedY);
            Quaternion rot = Quaternion.Euler(0f, finalRot, 0f);

            bool valid = ValidateFacility(f.facilityId, footprint, pos, size, rot, finalRot);
            if (valid)
            {
                stampValidCountByFacility.TryGetValue(f.facilityId, out int n);
                stampValidCountByFacility[f.facilityId] = n + 1;
            }
            else allValid = false;

            // 유효 여부와 무관하게 자기 칸으로 등록 - 캡처 시점에 합법이었던
            // "레일이 설비 칸(포트 앞칸)에 겹치는" 배치를 레일 검증이 허용하기 위함
            foreach (var c in footprint) stampFacilityCells.Add(c);

            resolvedFacilities.Add(new ResolvedFacility
            {
                facilityId = f.facilityId,
                worldPos = pos,
                worldRot = rot,
                size = size,
                footprint = footprint,
                valid = valid,
            });
        }

        foreach (var r in rails)
        {
            Vector2Int cell = anchor + GridMath.RotateCellOffset(r.offset, pasteRotationY);
            bool u = r.up, d = r.down, l = r.left, rr = r.right;
            GridMath.RotateRailDirs(pasteRotationY, ref u, ref d, ref l, ref rr);

            bool valid = ValidateRail(cell);
            if (!valid) allValid = false;

            resolvedRails.Add(new ResolvedRail { cell = cell, up = u, down = d, left = l, right = rr, valid = valid });
        }
    }

    private bool ValidateFacility(int facilityId, List<Vector2Int> footprint, Vector3 pos,
                                  Vector2Int size, Quaternion rot, int finalRot)
    {
        FacilityDataSheetData data = GetFacilityData(facilityId);
        if (data == null) return false;
        if (owner.PrefabDatabase == null || owner.PrefabDatabase.GetPrefab(facilityId) == null) return false;

        // 캡처 이후 시트에서 설비 크기가 바뀌었으면 캡처된 배치는 더 이상 맞지 않는다
        if (GridMath.RotatedSize(new Vector2Int(data.gridW, data.gridH), finalRot) != size) return false;

        foreach (var c in footprint)
        {
            if (!owner.IsCellInBuildZone(c)) return false;              // 건축 범위 (라이브 판정)
            if (owner.IsCellOccupied(c)) return false;                  // 기존 설비
            if (owner.RailManager != null && owner.RailManager.HasRailAt(c)) return false;   // 기존 레일
            if (stampFacilityCells.Contains(c)) return false;           // 이번 도장 안 겹침 (방어)
        }

        // 물리 겹침 - 수동 배치와 같은 검사. 고스트는 콜라이더가 없어 자기 자신에 안 걸린다
        if (owner.IsPlacementBlocked(pos, size, rot)) return false;

        // 설치 상한 - 배치 완료분 + 연출 진행분(owner 장부) + 이번 도장에서 이미 유효 판정된 것
        if (FacilityBuildLimit.HasLimit(facilityId))
        {
            stampValidCountByFacility.TryGetValue(facilityId, out int inStamp);
            if (owner.CountPlacedFacilities(facilityId) + inStamp >= FacilityBuildLimit.GetMax(facilityId))
                return false;
        }

        return true;
    }

    private bool ValidateRail(Vector2Int cell)
    {
        if (!owner.IsCellInBuildZone(cell)) return false;
        if (owner.RailManager == null) return false;
        if (owner.RailManager.HasRailAt(cell)) return false;

        // 기존 설비가 점유한 칸은 포트 앞칸만 허용 (CanUseCellAsRail 과 같은 규칙).
        // 이번 도장의 설비 칸은 캡처 시점에 합법이었던 조합이므로 허용된다
        // (아직 점유가 안 잡혀 IsCellOccupied 에 걸리지 않는다).
        if (owner.IsCellOccupied(cell) && !IsPortFrontCell(cell))
            return false;

        return true;
    }

    private bool IsPortFrontCell(Vector2Int cell)
    {
        if (portFrontCacheFrame != Time.frameCount)
        {
            portFrontCacheFrame = Time.frameCount;
            portFrontCells.Clear();
            foreach (var port in BuildPort.All)
                if (port != null) portFrontCells.Add(port.GetFrontCell());
        }
        return portFrontCells.Contains(cell);
    }

    // ── 도장 ─────────────────────────────────────────────────────────

    private void Stamp()
    {
        // resolved 리스트를 그대로 소비한다. 여기서 재계산하지 않는 것이
        // "프리뷰 = 실제 배치" 보장의 핵심이다.
        int i = 0;
        foreach (var rf in resolvedFacilities)
        {
            owner.StartCoroutine(owner.PlaceFacilityTracked(
                rf.facilityId, rf.worldPos, rf.worldRot, rf.footprint,
                startDelay: 0.04f * i, playSound: i == 0));
            i++;
        }

        if (owner.RailManager != null && resolvedRails.Count > 0)
        {
            foreach (var rr in resolvedRails)
                owner.RailManager.PlaceRailImmediate(rr.cell, rr.up, rr.down, rr.left, rr.right);

            // 즉시 배치된 레일의 흐름 화살표/포트 연결 재계산 (세이브 복원과 같은 마무리)
            owner.RailManager.RestoreReflowAndValidate();
        }

        TIMEKOV.Factory.BeltSegment.ReconnectAll();

        // 설비 실물은 홀로그램 연출 뒤에 생긴다. 그 다음 한 번 더 정리해야
        // 붙여넣은 설비의 포트에 닿은 벨트가 연결로 인정된다.
        float wait = owner.buildEffectDuration + 0.04f * resolvedFacilities.Count + 0.2f;
        owner.StartCoroutine(FinalizeRoutine(wait));

        // Pasting 유지 - 연속 도장. 방금 놓은 자리는 점유가 즉시 잡혀 다음 프레임부터 빨강.
    }

    // owner(BuildManager) 코루틴으로 돈다 - 도장 직후 모드를 나가도 마무리는 실행된다.
    // 고스트를 참조하지 않으므로 Deactivate 와 순서가 엉켜도 안전하다.
    private IEnumerator FinalizeRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        owner.RailManager?.RestoreReflowAndValidate();
        TIMEKOV.Factory.BeltSegment.ReconnectAll();
    }

    // ── 고스트 생성/표시 ─────────────────────────────────────────────

    private void BuildGhosts()
    {
        DestroyPasteGhosts();

        foreach (var f in facilities)
        {
            GameObject prefab = owner.PrefabDatabase != null ? owner.PrefabDatabase.GetPrefab(f.facilityId) : null;
            if (prefab == null) { facilityGhosts.Add(null); facilityGhostRenderers.Add(null); continue; }

            GameObject ghost = Object.Instantiate(prefab, ghostRoot.transform);
            ghost.name = $"BP_{f.facilityId}";
            FacilityPlacer.DisableGhostComponents(ghost, owner);

            // 틴트 대상 렌더러는 화살표를 붙이기 '전에' 캐시한다 - 수동 프리뷰(RefreshPreviewMarker)와
            // 같은 순서. 화살표는 고유색을 유지해야 하므로 틴트에서 제외된다.
            var rends = new List<Renderer>();
            foreach (var r in ghost.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.GetType().Name == "VFXRenderer") continue;   // VFX Graph 는 재질 접근 불가
                rends.Add(r);
            }

            // 수동 배치 프리뷰와 같은 입/출구 화살표 표시
            owner.RailManager?.ShowGhostPortArrows(ghost, f.placedSize);

            facilityGhosts.Add(ghost);
            facilityGhostRenderers.Add(rends.ToArray());
        }

        // 레일 고스트: 실제 레일과 같은 RailPiece + ApplyVisual 로 직선/코너/화살표를 그대로 만든다.
        // 모양은 회전값에 따라 달라지므로 RebuildRailGhostShapes 가 만든다.
        foreach (var _ in rails)
        {
            GameObject go = new GameObject("BP_Rail");
            go.transform.SetParent(ghostRoot.transform, false);
            railGhostPieces.Add(go.AddComponent<RailPiece>());
        }
        railGhostRotationBuilt = -1;   // 다음 Tick 에서 현재 회전으로 모양 생성
    }

    // 현재 청사진 회전(pasteRotationY)에 맞춰 레일 고스트의 직선/코너 비주얼을 다시 만든다.
    // R 를 누를 때만 호출된다 (모양은 회전에만 의존하고 위치는 매 프레임 따로 옮긴다).
    private void RebuildRailGhostShapes()
    {
        railGhostRotationBuilt = pasteRotationY;

        var rm = owner.RailManager;
        if (rm == null || rm.StraightRailPrefab == null || rm.CornerRailPrefab == null) return;

        for (int i = 0; i < rails.Count && i < railGhostPieces.Count; i++)
        {
            RailPiece piece = railGhostPieces[i];
            if (piece == null) continue;

            var r = rails[i];
            bool u = r.up, d = r.down, l = r.left, rr = r.right;
            GridMath.RotateRailDirs(pasteRotationY, ref u, ref d, ref l, ref rr);
            piece.up = u; piece.down = d; piece.left = l; piece.right = rr;
            // 화살표 방향도 원본에서 복사한 것을 회전해 적용 - 고스트가 원본과 같은 방향으로 보인다.
            // (도장 후 실제 레일은 reflow 가 체인 전체 기준으로 다시 확정한다)
            piece.flowFrom = r.flowDir == Vector2Int.zero
                ? Vector2Int.zero
                : GridMath.RotateCellOffset(r.flowDir, pasteRotationY);
            piece.pathIndex = 0;

            piece.ApplyVisual(rm.StraightRailPrefab, rm.CornerRailPrefab);
            StripGhostLogic(piece.gameObject);

            // 레일 모드의 경로 프리뷰와 같은 룩 - 고스트 머티리얼로 통째 스왑.
            // 직선/코너 판정은 ApplyVisual 과 같은 공식이다.
            int conn = (u ? 1 : 0) + (d ? 1 : 0) + (l ? 1 : 0) + (rr ? 1 : 0);
            bool straight = conn <= 1 || (u && d) || (l && rr);
            SwapToGhostMaterial(piece.gameObject,
                straight ? rm.GhostStraightRailMaterial : rm.GhostCornerRailMaterial);

            // 틴트용 렌더러 캐시. 직전 비주얼은 프레임 끝에 파괴돼 null 이 되므로 틴트 쪽에서 거른다.
            while (railGhostRenderers.Count <= i) railGhostRenderers.Add(null);
            railGhostRenderers[i] = piece.GetComponentsInChildren<Renderer>(true);
        }
    }

    // 레일 모드 ApplyGhostMaterial 과 같은 방식의 머티리얼 스왑 (_PathOffset 은 0 통일).
    // 인스턴스는 DestroyAllGhosts 의 렌더러 스윕이 회수한다.
    private static void SwapToGhostMaterial(GameObject ghost, Material ghostMat)
    {
        if (ghostMat == null) return;
        foreach (var r in ghost.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            Material[] mats = r.materials;
            for (int m = 0; m < mats.Length; m++)
                if (mats[m] != null) mats[m] = ghostMat;
            r.materials = mats;

            Material[] instanced = r.materials;
            foreach (var im in instanced)
                if (im != null && im.HasProperty("_PathOffset")) im.SetFloat("_PathOffset", 0f);
        }
    }

    // 고스트에서 물리/로직을 '제거'한다 (RailPiece 는 모양 재생성에 필요하므로 유지).
    // 끄기(enabled=false)가 아니라 제거인 이유는 파일 상단 주석 참조 - 벨트 전역 레지스트리 오염 방지.
    private static void StripGhostLogic(GameObject go)
    {
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null && !(mb is RailPiece)) Object.Destroy(mb);
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            if (col != null) Object.Destroy(col);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
            if (rb != null) Object.Destroy(rb);
    }

    private void ApplyGhostTransformsAndTints()
    {
        for (int i = 0; i < resolvedFacilities.Count && i < facilityGhosts.Count; i++)
        {
            var rf = resolvedFacilities[i];
            GameObject ghost = facilityGhosts[i];
            if (ghost == null) continue;

            ghost.transform.SetPositionAndRotation(rf.worldPos, rf.worldRot);

            // 수동 배치 프리뷰와 동일: 가능 = 초록 / 불가 = 빨강
            var rends = i < facilityGhostRenderers.Count ? facilityGhostRenderers[i] : null;
            if (rends != null)
                TintRenderersMPB(rends, rf.valid ? TintValid : TintInvalid);
        }

        float railY = owner.RailManager != null ? owner.RailManager.FixedYRail : owner.fixedY;
        for (int i = 0; i < resolvedRails.Count && i < railGhostPieces.Count; i++)
        {
            var rr = resolvedRails[i];
            RailPiece piece = railGhostPieces[i];
            if (piece == null) continue;

            Vector3 pos = GridMath.StartCellToWorldCenter(rr.cell, Vector2Int.one,
                owner.GridOriginPos, owner.cellSize, owner.fixedY);
            pos.y = railY;
            piece.transform.position = pos;

            var rends = i < railGhostRenderers.Count ? railGhostRenderers[i] : null;
            if (rends != null)
            {
                if (rr.valid) ClearRenderersMPB(rends);
                else TintRenderersMPB(rends, TintInvalid);
            }
        }
    }

    // MPB 틴트 - 머티리얼 인스턴스를 만들지 않아 누수가 없고, 블록 해제로 원색 복귀.
    // 레일 셰이더는 _rail_color 를 쓰므로 세 키를 전부 싣는다(셰이더에 없는 키는 무시됨).
    private static void TintRenderersMPB(Renderer[] renderers, Color c)
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(ColorId, c);
            _mpb.SetColor(RailColorId, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    private static void ClearRenderersMPB(Renderer[] renderers)
    {
        foreach (var r in renderers)
            if (r != null) r.SetPropertyBlock(null);
    }

    // ── 마퀴 (윈도우식 얇은 선택 사각형, 스크린 스페이스) ────────────

    private void EnsureMarquee()
    {
        if (marqueeRoot != null) return;

        marqueeRoot = new GameObject("BP_Marquee");
        marqueeRoot.transform.SetParent(ghostRoot.transform, false);

        var canvas = marqueeRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5600;   // HUD/토스트 위

        marqueeFill = CreateMarqueeRect("Fill", new Color(1f, 1f, 1f, 0.06f));
        marqueeBorders = new RectTransform[4];
        for (int i = 0; i < 4; i++)
            marqueeBorders[i] = CreateMarqueeRect("Border" + i, new Color(1f, 1f, 1f, 0.85f));
    }

    private RectTransform CreateMarqueeRect(string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(marqueeRoot.transform, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;   // 클릭을 가로채면 안 된다

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;   // 화면 좌하단 기준 픽셀 좌표 (Input.mousePosition 과 동일 기준)
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        return rt;
    }

    private void UpdateMarquee(Vector2 a, Vector2 b)
    {
        EnsureMarquee();
        marqueeRoot.SetActive(true);

        Vector2 min = Vector2.Min(a, b);
        Vector2 size = Vector2.Max(a, b) - min;
        float t = MarqueeBorderPx;

        Set(marqueeFill, min, size);
        Set(marqueeBorders[0], min, new Vector2(t, size.y));                                    // left
        Set(marqueeBorders[1], new Vector2(min.x + size.x - t, min.y), new Vector2(t, size.y)); // right
        Set(marqueeBorders[2], min, new Vector2(size.x, t));                                    // bottom
        Set(marqueeBorders[3], new Vector2(min.x, min.y + size.y - t), new Vector2(size.x, t)); // top

        static void Set(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }

    private void HideMarquee()
    {
        if (marqueeRoot != null)
            marqueeRoot.SetActive(false);
    }

    // ── 정리 ─────────────────────────────────────────────────────────

    // 붙여넣기 고스트만 파괴 (마퀴는 유지)
    private void DestroyPasteGhosts()
    {
        foreach (var g in facilityGhosts) if (g != null) Object.Destroy(g);
        foreach (var p in railGhostPieces) if (p != null) Object.Destroy(p.gameObject);
        facilityGhosts.Clear();
        facilityGhostRenderers.Clear();
        railGhostPieces.Clear();
        railGhostRenderers.Clear();
        railGhostRotationBuilt = -1;
    }

    private void DestroyAllGhosts()
    {
        // 레일 고스트 비주얼이 내부에서 만든 머티리얼 인스턴스(_PathOffset 처리)까지 회수
        if (ghostRoot != null)
        {
            foreach (var r in ghostRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.GetType().Name == "VFXRenderer") continue;
                foreach (var m in r.materials)
                    if (m != null) Object.Destroy(m);
            }
            Object.Destroy(ghostRoot);
        }
        ghostRoot = null;
        marqueeRoot = null;
        marqueeFill = null;
        marqueeBorders = null;

        facilityGhosts.Clear();
        facilityGhostRenderers.Clear();
        railGhostPieces.Clear();
        railGhostRenderers.Clear();
        railGhostRotationBuilt = -1;
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    private bool TryGetCursorCell(out Vector2Int cell)
    {
        cell = default;
        if (owner.mainCam == null) return false;

        Ray ray = owner.mainCam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, owner.rayDistance, owner.groundMask))
            return false;

        cell = GridMath.WorldToStartCellCentered(hit.point, Vector2Int.one, owner.GridOriginPos, owner.cellSize);
        return true;
    }

    private static FacilityDataSheetData GetFacilityData(int facilityId)
    {
        if (GameDataHolder.I.FacilityData.TryGet(facilityId.ToString(), out var data))
            return data;
        return null;
    }

    private static bool IsPointerOverUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
}
