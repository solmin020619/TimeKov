using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TIMEKOV.Factory
{
    public class BeltSegment : MonoBehaviour
    {
        [Header("벨트 시각화")]
        public GameObject beltItemPrefab;
        [Tooltip("벨트 한 칸을 지나는 데 걸리는 시간(초). 칸 진입(절반)+방출(절반)으로 나눠 쓴다.")]
        public float travelTimePerSegment = 0.5f;

        [HideInInspector] public BeltSegment prevSegment;
        [HideInInspector] public BeltSegment nextSegment;
        [HideInInspector] public MachineBase sourceM;
        [HideInInspector] public MachineBase targetM;

        // 이 세그먼트가 "직접" 감지한 설비 (체인 전파값과 구분 — 체인 방향 판정에 사용)
        private MachineBase _localSource;
        private MachineBase _localTarget;

        public bool IsHead => prevSegment == null && sourceM != null;
        public bool IsReady => sourceM != null && targetM != null;

        [Header("앞뒤 감지 포인트")]
        public Transform endpointFront;
        public Transform endpointBack;

        [Header("감지 반경")]
        public float detectRadius = 0.6f;

        [Header("감지 레이어")]
        public LayerMask detectMask;

        [Header("연결 상태 색상")]
        [Tooltip("연결이 확정적으로 안 됐을 때 벨트 글로우 색 (HDR). 빨강.\n" +
                 "연결되면 원래 머티리얼 색(파랑)으로 자동 복귀.")]
        [ColorUsage(true, true)]
        public Color disconnectedColor = new Color(5f, 0f, 0f, 0f);

        [Tooltip("레일 빌드 모드 중(연결 작업 중) 아직 연결 안 된 벨트의 중립 글로우 색.\n" +
                 "값을 1 미만으로 두면 블룸(과한 빛번짐) 없이 고스트처럼 부드러운 매트 흰색이 된다.\n" +
                 "작업이 끝나기 전까진 빨강/파랑 판정을 보류하고 이 색으로 둔다.")]
        [ColorUsage(true, true)]
        public Color buildingColor = new Color(0.4f, 0.4f, 0.4f, 0f);

        [Tooltip("작업 중 레일 바닥 틴트 (중립). 1 미만 권장.")]
        [ColorUsage(true, true)]
        public Color buildingRailColor = new Color(0.6f, 0.6f, 0.6f, 0f);

        [Tooltip("작업 중(흰색) 상태의 밝기 배수(_intelsity). 머티리얼 기본값은 15라 그대로면\n" +
                 "화살표가 과하게 빛난다. 1~2 정도로 낮추면 눈 안 아프게 부드러워짐.")]
        public float buildingIntensity = 1.5f;

        [Tooltip("해제(철거) 모드에서 마우스를 올렸을 때의 글로우 색 (HDR). 매우 강한 빨강.\n" +
                 "이미 빨강(미연결)인 벨트 위에서도 호버가 또렷이 구분되도록 미연결보다 훨씬 진하게.")]
        [ColorUsage(true, true)]
        public Color demolishHighlightColor = new Color(12f, 0f, 0f, 0f);

        [Tooltip("해제 호버 글로우 밝기 배수(_intelsity). 미연결(8)보다 크게 잡아 더 강렬하게.")]
        public float demolishHighlightIntensity = 18f;

        [Tooltip("연결이 끊긴 뒤 빨강으로 바꾸기까지의 유예 시간(초).\n" +
                 "다른 레일 철거 시 재감지로 잠깐 연결이 비는 동안 깜빡이지 않게 함.")]
        public float disconnectColorGrace = 0.3f;

        /// <summary>씬에 활성화된 BeltSegment 전체 목록. 그리드 연결 감지에 사용.</summary>
        public static readonly List<BeltSegment> All = new();

        // ── 그리드 셀 캐시 ───────────────────────────────────────────────
        private Vector2Int _beltCell;   // 이 벨트 자신의 셀
        private Vector2Int _frontCell;  // beltCell + 앞 방향 (다음 연결 대상 셀)
        private Vector2Int _backCell;   // beltCell + 뒤 방향 (이전 연결 대상 셀)
        private bool       _cellsInitialized;

        private void OnEnable()
        {
            All.Add(this);

            // 새로 생성/활성화될 때 빌드 중(또는 해제 모드)이면 즉시 흰색을 적용한다.
            // OnEnable 은 Instantiate 중 렌더 전에 동기 실행되므로, 첫 프레임에 기본 머티리얼
            // (파랑)이 잠깐 비쳐 깜빡이는 것을 막는다 — 특히 경로 끝 코너(rail_turn)가
            // 매 경로계산마다 재생성될 때, 또는 해제 시 양옆 벨트가 재생성될 때 파랑으로
            // 깜빡이던 문제 해결.
            if (SuppressConnectionColor || DemolishModeActive)
                ApplyColorState(ColorState.Building);
        }

        private void Start() => StartCoroutine(DetectNextFrame());

        /// <summary>레일 빌드 모드 중 연결 색상 판정을 보류할지. RailBuildManager 가 토글.
        /// 보류 중엔 연결된 건 파랑, 아직 안 된 건 빨강 대신 중립(흰색)으로 둔다.</summary>
        public static bool SuppressConnectionColor;

        /// <summary>해제(철거) 모드 진입 여부. BuildManager 가 토글.
        /// 진입 중엔 연결 상태와 무관하게 모든 벨트를 흰색(작업 중 색)으로 둔다.
        /// 단, 마우스 호버한 벨트만 빨강(Demolish)으로 표시.</summary>
        public static bool DemolishModeActive;

        // 벨트 색상 상태. Connected=파랑(원래색), Disconnected=빨강(확정 미연결),
        // Building=흰색(작업 중 보류), Demolish=강렬한 빨강(해제 모드 호버).
        private enum ColorState { Connected, Disconnected, Building, Demolish }

        // 해제(철거) 모드에서 마우스를 올렸을 때 — 연결 상태와 무관하게 강한 빨강으로 표시.
        private bool _demolishHighlighted;

        /// <summary>해제 모드 호버 시 BuildDemolisher 가 토글. true면 벨트 자체 텍스처를
        /// 강한 빨강(미연결 색)으로 칠해 철거 대상을 표시한다. 호버 해제 시 원래 색 복귀.</summary>
        public void SetDemolishHighlight(bool on) => _demolishHighlighted = on;

        // 연결 상태가 바뀌는 경로가 여러 곳(감지/전파/재연결/철거)이라
        // 매 프레임 IsReady 를 폴링해 색을 갱신한다. 상태가 그대로면 즉시 반환해 비용 거의 없음.
        private void Update()
        {
            if (_demolishHighlighted)
            {
                // 해제 모드 호버 중 — 매우 강렬한 빨강 고정 (연결/미연결 판정 보류)
                _disconnectTimer = 0f;
                ApplyColorState(ColorState.Demolish);
            }
            else if (DemolishModeActive)
            {
                // 해제 모드 진입 — 호버 안 한 벨트는 전부 흰색(작업 중 색)으로
                _disconnectTimer = 0f;
                ApplyColorState(ColorState.Building);
            }
            else if (IsReady)
            {
                // 연결됨 → 즉시 파랑
                _disconnectTimer = 0f;
                ApplyColorState(ColorState.Connected);
            }
            else if (SuppressConnectionColor)
            {
                // 레일 빌드 모드 중(작업 중) + 아직 연결 안 됨 → 판정 보류, 중립(흰색).
                // 빨강은 작업이 끝난(빌드 모드 종료) 뒤에만 표시.
                _disconnectTimer = 0f;
                ApplyColorState(ColorState.Building);
            }
            else
            {
                // 작업 끝났는데 미연결 → 유예 후 빨강. 유예는 재감지 빈틈 깜빡임 방지용.
                _disconnectTimer += Time.deltaTime;
                if (_disconnectTimer >= disconnectColorGrace)
                    ApplyColorState(ColorState.Disconnected);
            }
        }

        // ── 연결 상태 색상 표시 ─────────────────────────────────────────
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private ColorState _appliedState;
        private bool _colorInitialized;
        private float _disconnectTimer;  // 연결 끊긴 지속 시간 (유예 판정용)

        private static readonly int FresnelColorId = Shader.PropertyToID("_Fresnel_color");
        private static readonly int RailColorId     = Shader.PropertyToID("_rail_color");
        private static readonly int BaseColorId     = Shader.PropertyToID("_Base_Color");
        private static readonly int IntensityId     = Shader.PropertyToID("_intelsity");
        // 코너(rail_turn) 전용 노이즈 스케일 — 직선 레일 셰이더엔 없음(무시됨).
        // 흰색/빨강 등 오버라이드 상태에서 0으로 눌러 코너의 얼룩(노이즈)을 없애 직선과 같게 만든다.
        private static readonly int NoiseScaleId    = Shader.PropertyToID("_noise_scale");

        // 빨강(미연결) 상태 밝기. 5~8 정도가 눈 안 아프면서 또렷하게 보임.
        private const float DisconnectedIntensity = 8f;

        /// <summary>벨트 색을 상태에 맞게 적용. 같은 상태면 즉시 반환.</summary>
        private void ApplyColorState(ColorState state)
        {
            if (_colorInitialized && _appliedState == state) return;

            // 코너 교체(RailPiece.ApplyVisual) 후 캐시된 렌더러가 파괴돼 있으면 재쿼리.
            // 비주얼이 RailPiece 의 자식(BeltSegment 의 형제)일 수 있으므로
            // RailPiece 루트 기준으로 탐색해 확실히 찾는다.
            if (_renderers == null || (_renderers.Length > 0 && _renderers[0] == null))
            {
                Component searchRoot = RailPieceRef != null ? (Component)RailPieceRef : (Component)this;
                _renderers = searchRoot.GetComponentsInChildren<Renderer>(true);
            }
            _mpb ??= new MaterialPropertyBlock();

            foreach (var r in _renderers)
            {
                if (r == null) continue;

                if (state == ColorState.Connected)
                {
                    // 오버라이드 제거 → 머티리얼 원래 색(파랑)으로 복귀
                    r.SetPropertyBlock(null);
                    continue;
                }

                Color glow = state switch
                {
                    ColorState.Demolish     => demolishHighlightColor,
                    ColorState.Disconnected => disconnectedColor,
                    _                       => buildingColor,
                };
                Color rail = state switch
                {
                    ColorState.Demolish     => new Color(0.55f, 0.02f, 0.02f, 0f),  // 강렬한 빨강 레일 바닥
                    ColorState.Disconnected => new Color(0.30f, 0.05f, 0.05f, 0f),  // 빨강 레일 바닥
                    _                       => buildingRailColor,                    // 중립 레일 바닥
                };
                // 작업 중(흰색)은 밝기를 크게 낮춰 블룸/눈부심 제거, 빨강/해제는 또렷하게 유지.
                float intensity = state switch
                {
                    ColorState.Demolish     => demolishHighlightIntensity,
                    ColorState.Disconnected => DisconnectedIntensity,
                    _                       => buildingIntensity,
                };

                _mpb.Clear();   // 이전 상태의 잔여 오버라이드 제거 후 깨끗이 설정
                _mpb.SetColor(FresnelColorId, glow);   // 글로우/프레넬
                _mpb.SetColor(RailColorId, rail);      // 레일 바닥 틴트
                _mpb.SetFloat(IntensityId, intensity); // 밝기 배수 (화살표 눈부심 제어)
                _mpb.SetFloat(NoiseScaleId, 0f);       // 코너 노이즈 제거 → 직선과 동일한 매끈한 색
                r.SetPropertyBlock(_mpb);
            }

            _appliedState     = state;
            _colorInitialized = true;
        }

        private void OnDisable()
        {
            All.Remove(this);
            // 이 칸에 아이템이 올라와 있으면(잼 상태 등) 먼저 창고로 옮기고 비주얼을 제거한다.
            // Unity는 StopAllCoroutines/파괴 시 코루틴 finally 실행을 보장하지 않으므로 여기서 명시적으로 처리
            // (벨트 철거 시 아이템 오브젝트가 씬에 남던 문제 방지). OnDisable은 파괴 직전에도 호출된다.
            RescueOccupantToStorage();
            StopAllCoroutines();
        }

        // 이 칸의 점유 아이템을 창고로 옮기고 비주얼을 제거한다. 점유가 없으면 아무것도 안 함(멱등).
        // 정상 전달/핸드오프는 이미 _occupant 를 비워두므로 여기서 중복 회수되지 않는다.
        private void RescueOccupantToStorage()
        {
            if (_occupant == null) return;
            var token = _occupant;
            var v     = _occupantVisual;
            _occupant = null; _occupantVisual = null;
            InventoryManager.StorageInstance?.AddItem(token.itemId, token.amount);
            if (v != null) Destroy(v);
        }

        private IEnumerator DetectNextFrame()
        {
            yield return null;
            DetectConnections();
            yield return null;
            PropagateChain();
        }

        // ── 그리드 셀 초기화 ─────────────────────────────────────────────

        private void InitCells()
        {
            if (endpointFront == null && endpointBack == null)
            {
                Debug.LogWarning("[BeltSegment] endpointFront/Back 모두 null → 물리 감지로 대체합니다.", this);
                return;
            }

            var bm = FindFirstObjectByType<BuildManager>();
            if (bm == null)
            {
                Debug.LogWarning("[BeltSegment] BuildManager를 찾을 수 없습니다. 물리 감지로 대체합니다.", this);
                return;
            }

            Vector3 origin   = bm.GridOriginPos;
            float   cellSize = bm.cellSize > 0f ? bm.cellSize : 1f;

            // 이 벨트 자신의 셀 (transform 기준, FloorToInt로 확정)
            _beltCell = new Vector2Int(
                Mathf.FloorToInt((transform.position.x - origin.x) / cellSize),
                Mathf.FloorToInt((transform.position.z - origin.z) / cellSize));

            // endpoint가 어느 방향에 있는지로 인접 셀 계산
            // → 코너 피스처럼 front/back 모두 같은 셀 내에 있어도 정확히 처리
            if (endpointFront != null)
                _frontCell = _beltCell + WorldDirToGridDir(endpointFront.position - transform.position);

            if (endpointBack != null)
                _backCell  = _beltCell + WorldDirToGridDir(endpointBack.position  - transform.position);

            _cellsInitialized = true;
        }

        /// <summary>
        /// 월드 벡터를 가장 가까운 그리드 방향 (±1, 0) 또는 (0, ±1)로 변환한다.
        /// </summary>
        private static Vector2Int WorldDirToGridDir(Vector3 dir)
        {
            float ax = Mathf.Abs(dir.x);
            float az = Mathf.Abs(dir.z);
            if (ax >= az)
                return new Vector2Int(dir.x >= 0 ? 1 : -1, 0);
            else
                return new Vector2Int(0, dir.z >= 0 ? 1 : -1);
        }

        // ── 수동 재연결 (Inspector 우클릭 또는 인게임 호출) ──────────────

        /// <summary>기존 연결/소스/타깃을 모두 끊고 셀 캐시를 무효화한다(감지 전 단계).</summary>
        private void ClearConnections()
        {
            if (prevSegment != null && prevSegment.nextSegment == this) prevSegment.nextSegment = null;
            if (nextSegment != null && nextSegment.prevSegment == this) nextSegment.prevSegment = null;
            sourceM?.RemoveOutputBelt(this);

            prevSegment       = null;
            nextSegment       = null;
            sourceM           = null;
            targetM           = null;
            _localSource      = null;
            _localTarget      = null;
            _cellsInitialized = false;
        }

        [ContextMenu("연결 재감지 (Reconnect)")]
        public void Reconnect()
        {
            ClearConnections();

            // 단일 세그먼트 재연결(인스펙터/개별 호출): 다른 세그먼트가 준비될 시간을 위해 비동기.
            StartCoroutine(DetectNextFrame());
        }

        /// <summary>
        /// 씬의 모든 벨트 연결을 다시 감지한다.
        /// 설비를 짓거나 철거한 직후 호출 — 벨트를 먼저 깔고 설비를 나중에 지어도 연결된다.
        ///
        /// 한 프레임 뒤로 미뤄서(코얼레싱) 실제 감지를 수행한다. 레일을 새로 깔거나 철거하면
        /// RailPiece.ApplyVisual 이 이웃 벨트의 비주얼을 Destroy 후 재생성하는데,
        /// Destroy 는 프레임 끝에 반영되므로 호출 즉시점엔 같은 칸에 '곧 파괴될 옛 세그먼트'와
        /// '새 세그먼트'가 All 에 함께 들어 있다. 이때 바로 감지하면 옛 세그먼트에 잘못 엮였다가
        /// 프레임 끝 파괴로 체인이 끊겨, 끊었다 다시 이어도 연결이 안 되던(빨강 유지) 문제가 났다.
        /// 한 프레임 기다린 뒤 죽은 세그먼트가 All 에서 빠지고 나서 감지하면 깔끔히 엮인다.
        /// </summary>
        public static void ReconnectAll()
        {
            BeltReconnectRunner.Schedule();
        }

        /// <summary>실제 재감지 — 모든 셀을 먼저 초기화한 뒤 동기 3단계로 처리해
        /// 실행 순서와 무관하게 항상 완전히 엮이게 한다. (BeltReconnectRunner 가 한 프레임 뒤 호출)</summary>
        internal static void ReconnectAllNow()
        {
            var snapshot = new List<BeltSegment>(All);

            // 1) 전부 연결 해제 + 셀 재초기화
            foreach (var seg in snapshot)
            {
                if (seg == null) continue;
                seg.ClearConnections();
                seg.InitCells();
            }

            // 2) 연결 감지 (모든 셀이 초기화된 상태라 실행 순서 무관)
            foreach (var seg in snapshot)
                if (seg != null) seg.DetectConnections();

            // 3) 체인 전파 → source/target 확정
            foreach (var seg in snapshot)
                if (seg != null) seg.PropagateChain();
        }

        // ── 연결 감지 (그리드 기반) ──────────────────────────────────────

        public void DetectConnections()
        {
            if (!_cellsInitialized) InitCells();

            if (_cellsInitialized)
                GridDetectConnections();
            else
                LegacyDetectConnections(); // BuildManager 없을 때 폴백
        }

        private void GridDetectConnections()
        {
            // ── 설비 감지: 포트 그리드 매칭 (결정적) ─────────────────────
            // 포트의 front cell == 이 벨트의 셀이고, 이 레일이 포트를 향해(head-on) 연결돼
            // 있을 때만 source/target 으로 인정한다. "포트를 향한 연결"이란 레일이 포트 방향
            // 쪽 칸(접근 칸)으로 연결돼 있다는 뜻 — 포트 앞 칸을 수직으로 그냥 통과(스쳐
            // 지나감)하는 레일은 그 방향 연결이 없어 제외된다.
            // (connectionCount 같은 장부 대신 실제 레일 형상으로 판정 → cell-start·gap-fill 등
            //  어떤 방식으로 연결해도 동일하게 인정됨.)
            foreach (var port in BuildPort.All)
            {
                if (port == null || port.OwnerBuilding == null) continue;
                if (port.GetFrontCell() != _beltCell) continue;
                if (!RailConnectsToward(port.GetWorldDirection())) continue; // head-on 연결만 인정

                var machine = port.GetComponentInParent<MachineBase>();
                if (machine == null) continue;

                if (port.portType == PortType.Output) { sourceM = machine; _localSource = machine; }
                else                                  { targetM = machine; _localTarget = machine; }
            }

            // ── 벨트↔벨트 감지: 그리드 기반 ─────────────────────────────
            // A.frontCell == B.beltCell → A 다음에 B.
            // 단, 실제 레일(RailPiece)이 서로 연결돼 있어야만 체인으로 엮는다.
            // (셀만 인접한 다른 라인이나, 중간이 끊긴 칸끼리 잘못 엮이는 것 방지)
            foreach (var other in All)
            {
                if (other == null || other == this || !other._cellsInitialized) continue;
                if (!RailLinked(other)) continue;   // 실제 레일 연결이 없으면 체인 안 엮음

                if (_frontCell == other._beltCell)
                {
                    if (nextSegment == null)       nextSegment       = other;
                    if (other.prevSegment == null) other.prevSegment = this;
                }

                if (other._frontCell == _beltCell)
                {
                    if (prevSegment == null)       prevSegment       = other;
                    if (other.nextSegment == null) other.nextSegment  = this;
                }
            }
        }

        // 이 벨트의 RailPiece (시각 프리팹의 부모). 레일의 실제 연결 방향 판정에 사용.
        private RailPiece _railPiece;
        private bool _railPieceResolved;
        private RailPiece RailPieceRef
        {
            get
            {
                if (!_railPieceResolved)
                {
                    _railPiece = GetComponentInParent<RailPiece>();
                    _railPieceResolved = true;
                }
                return _railPiece;
            }
        }

        /// <summary>두 벨트의 RailPiece 가 서로 마주보는 방향으로 실제 레일 연결돼 있는지.
        /// RailPiece 가 없으면(레거시 배치) 기존 동작 유지 위해 true.</summary>
        private bool RailLinked(BeltSegment other)
        {
            RailPiece a = RailPieceRef;
            RailPiece b = other.RailPieceRef;
            if (a == null || b == null) return true;

            Vector2Int d = b.cell - a.cell;
            if (d == Vector2Int.up)    return a.up    && b.down;
            if (d == Vector2Int.down)  return a.down  && b.up;
            if (d == Vector2Int.left)  return a.left  && b.right;
            if (d == Vector2Int.right) return a.right && b.left;
            return false;   // 인접하지 않음 → 연결 아님
        }

        /// <summary>이 레일(RailPiece)이 지정한 방향으로 실제 연결돼 있는지.
        /// 포트를 향한(head-on) 연결인지 판정하는 데 사용. dir = 포트가 바라보는 방향.
        /// RailPiece 가 없으면(레거시) 기존 동작 유지 위해 true.</summary>
        private bool RailConnectsToward(Vector2Int dir)
        {
            RailPiece rp = RailPieceRef;
            if (rp == null) return true;

            // 포트 축 방향으로 연결돼 있어야 하고(스쳐 지나감 제외), 동시에 포트 축에
            // 수직으로 꺾이면(코너) 안 된다. front cell 이 직선이어야 설비(포트) 쪽을 향한
            // endpoint 가 생겨 head-on 으로 물린다. front cell 에서 바로 꺾이는 코너는
            // 설비를 관통/우회하는 잘못된 연결이라 제외한다. (출력에서 옆으로 새는 버그 방지)
            if (dir == Vector2Int.up)    return rp.up    && !rp.left && !rp.right;
            if (dir == Vector2Int.down)  return rp.down  && !rp.left && !rp.right;
            if (dir == Vector2Int.left)  return rp.left  && !rp.up   && !rp.down;
            if (dir == Vector2Int.right) return rp.right && !rp.up   && !rp.down;
            return false;
        }

        // ── 레거시 물리 감지 (폴백) ──────────────────────────────────────

        private void LegacyDetectConnections()
        {
            if (endpointBack  != null) LegacyDetectAt(endpointBack.position,  false);
            if (endpointFront != null) LegacyDetectAt(endpointFront.position, true);
        }

        private void LegacyDetectAt(Vector3 pos, bool isFront)
        {
            var hits = Physics.OverlapSphere(pos, detectRadius, detectMask);
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                var machine = hit.GetComponentInParent<MachineBase>();
                if (machine != null)
                {
                    bool nearOutput = machine.outputPort != null && machine.inputPort != null
                        ? Vector3.Distance(pos, machine.outputPort.position) <
                          Vector3.Distance(pos, machine.inputPort.position)
                        : machine.outputPort != null;

                    if (nearOutput) { sourceM = machine; _localSource = machine; }
                    else            { targetM = machine; _localTarget = machine; }
                    continue;
                }

                var seg = hit.GetComponentInParent<BeltSegment>();
                if (seg == null || seg == this) continue;

                if (isFront)
                {
                    bool neighborBackIsClose = seg.endpointBack != null &&
                        Vector3.Distance(pos, seg.endpointBack.position) <
                        Vector3.Distance(pos, seg.endpointFront != null
                            ? seg.endpointFront.position : seg.transform.position);

                    if (neighborBackIsClose)
                    {
                        if (nextSegment == null)      nextSegment      = seg;
                        if (seg.prevSegment == null)  seg.prevSegment  = this;
                    }
                    else
                    {
                        if (prevSegment == null)      prevSegment      = seg;
                        if (seg.nextSegment == null)  seg.nextSegment  = this;
                    }
                }
                else
                {
                    bool neighborFrontIsClose = seg.endpointFront != null &&
                        Vector3.Distance(pos, seg.endpointFront.position) <
                        Vector3.Distance(pos, seg.endpointBack != null
                            ? seg.endpointBack.position : seg.transform.position);

                    if (neighborFrontIsClose)
                    {
                        if (prevSegment == null)      prevSegment      = seg;
                        if (seg.nextSegment == null)  seg.nextSegment  = this;
                    }
                    else
                    {
                        if (nextSegment == null)      nextSegment      = seg;
                        if (seg.prevSegment == null)  seg.prevSegment  = this;
                    }
                }
            }
        }

        public BeltSegment GetChainHead()
        {
            BeltSegment cur = this;
            int safety = 100;
            while (cur.prevSegment != null && safety-- > 0)
                cur = cur.prevSegment;
            if (safety <= 0)
                Debug.LogWarning($"[BeltSegment] GetChainHead: 순환 참조 감지! ({gameObject.name})", this);
            return cur;
        }

        private void PropagateChain() => GetChainHead().TraverseAndSet();

        private void TraverseAndSet()
        {
            var chain = new List<BeltSegment>();
            var cur = this;
            int safety = 200;
            while (cur != null && safety-- > 0) { chain.Add(cur); cur = cur.nextSegment; }

            // 후보 설비 수집 (방향 판정용 임시값)
            MachineBase anySrc = null, anyTgt = null;
            foreach (var seg in chain)
            {
                if (anySrc == null && seg.sourceM != null) anySrc = seg.sourceM;
                if (seg.targetM != null) anyTgt = seg.targetM;
            }
            if (anySrc == null || anyTgt == null || anySrc == anyTgt) return;

            // ── 체인 방향 확정 ───────────────────────────────────────────
            // 출력 포트를 "직접" 감지한 세그먼트가 머리(head)여야 한다.
            // 양끝의 직접 감지 결과로 판정하고, 둘 다 모호할 때만 거리 휴리스틱.
            var head = chain[0];
            var tail = chain[chain.Count - 1];
            bool reverse;
            if      (tail._localSource != null && head._localSource == null) reverse = true;
            else if (head._localSource != null && tail._localSource == null) reverse = false;
            else if (head._localTarget != null && tail._localTarget == null) reverse = true;
            else if (tail._localTarget != null && head._localTarget == null) reverse = false;
            else
                reverse = Vector3.Distance(head.transform.position, anyTgt.transform.position) <
                          Vector3.Distance(head.transform.position, anySrc.transform.position);

            if (reverse)
            {
                chain.Reverse();
                for (int i = 0; i < chain.Count; i++)
                {
                    chain[i].prevSegment = i > 0 ? chain[i - 1] : null;
                    chain[i].nextSegment = i < chain.Count - 1 ? chain[i + 1] : null;
                }
            }

            // ── 출발/도착 설비 선정 ──────────────────────────────────────
            // src는 머리 쪽부터, tgt는 꼬리 쪽부터 탐색 — 레일이 다른 설비
            // 옆을 지나가며 생긴 중간 오감지가 양끝 연결을 덮어쓰지 못하게 한다.
            MachineBase src = null, tgt = null;
            for (int i = 0; i < chain.Count && src == null; i++)
                src = chain[i].sourceM;
            for (int i = chain.Count - 1; i >= 0 && tgt == null; i--)
                tgt = chain[i].targetM;

            if (src == null || tgt == null || src == tgt) return;

            // 같은 체인의 세그먼트들이 각자 PropagateChain을 호출하므로,
            // 실제로 연결 상태가 바뀐 첫 호출에서만 대기 발송을 시작한다 (연속 발송 방지)
            bool changed = false;
            foreach (var seg in chain)
            {
                if (seg.sourceM != src || seg.targetM != tgt) changed = true;
                seg.sourceM = src;
                seg.targetM = tgt;
            }
            src.AddOutputBelt(chain[0]);

            // 벨트가 연결될 때 OutputBuffer에 이미 대기 중인 아이템이 있으면 첫 발송을 시도한다.
            // 여러 세그먼트가 각자 PropagateChain을 호출해 changed가 여러 번 true가 돼도,
            // TryDispatchPendingOutput은 머리 칸이 비었을 때만 1개를 올리므로 중복 발송되지 않는다.
            if (changed)
                src.TryDispatchPendingOutput();
        }

        // =====================================================================
        // 칸 점유(occupancy) 기반 운송 — 벨트 1칸당 아이템 1개.
        // 각 세그먼트가 자기 칸의 아이템 하나를 소유하고, 다음 칸이 비면 한 칸씩 넘긴다(hand-off).
        // 다음 칸/설비가 못 받으면 그 자리에 머물러(잼) 뒤로 역압이 전파된다.
        // =====================================================================

        /// <summary>이동 중인 아이템 하나를 식별하는 토큰.</summary>
        private class InFlightItem
        {
            public int itemId;
            public int amount;
        }

        // 이 칸이 들고 있는 아이템(없으면 null)과 그 시각 오브젝트.
        private InFlightItem _occupant;
        private GameObject   _occupantVisual;

        /// <summary>이 벨트 칸에 아이템이 올라와 있으면 true.</summary>
        public bool IsOccupied => _occupant != null;

        private void OnDestroy()
        {
            // ── 이웃 세그먼트 참조 정리 ─────────────────────────────────────
            // 재연결 시 DetectConnections가 null 체크로 자리를 채울 수 있도록
            // 이웃이 나를 가리키는 참조를 미리 끊어둔다.
            if (prevSegment != null && prevSegment.nextSegment == this)
                prevSegment.nextSegment = null;
            if (nextSegment != null && nextSegment.prevSegment == this)
                nextSegment.prevSegment = null;

            // 소스 머신의 outputBelts 목록에서 이 세그먼트 제거
            sourceM?.RemoveOutputBelt(this);

            // 점유 아이템/비주얼 정리는 OnDisable 의 RescueOccupantToStorage 가 담당한다.
            // (OnDisable 은 파괴 직전에도 호출되므로, 벨트 철거 시 올라와 있던 아이템이
            //  모두 창고로 옮겨지고 비주얼이 제거된다.)
        }

        // ── 소스 → 머리 칸 진입 ───────────────────────────────────────────

        /// <summary>소스 설비가 이 벨트(체인 머리)로 아이템을 올린다.
        /// 칸이 비어 있고 연결돼 있을 때만 성공한다. 차 있으면 false → 아이템은 소스 출력 버퍼에 남아 역압.</summary>
        public bool TryTransport(int itemId, int amount)
        {
            if (!IsReady) return false;
            if (_occupant != null) return false;

            var token  = new InFlightItem { itemId = itemId, amount = amount };
            var visual = SpawnVisual(itemId, amount, BackPoint());   // 진입 지점(소스 출력 쪽 stub)에서 시작
            AcceptOccupant(token, visual);
            return true;
        }

        /// <summary>이전 칸(또는 소스)에서 이 칸으로 아이템을 넘겨받아 이동을 시작한다.</summary>
        private void AcceptOccupant(InFlightItem token, GameObject visual)
        {
            _occupant       = token;
            _occupantVisual = visual;
            StartCoroutine(RunOccupant());
        }

        private IEnumerator RunOccupant()
        {
            var token  = _occupant;
            var visual = _occupantVisual;
            try
            {
                float half = Mathf.Max(0.01f, travelTimePerSegment * 0.5f);

                // 1) 진입: back(칸 경계) → center(칸 중앙). 이 칸은 이미 우리가 점유 중이라 항상 진행.
                yield return MoveVisual(visual, BackPoint(), transform.position, half);

                // 2) 칸 중앙에서 대기하다가, 다음 칸/설비가 받으면 내보낸다(못 받으면 잼).
                while (true)
                {
                    // 양끝(소스·타깃) 모두 끊긴 고립 칸 → 창고로 구조
                    if (sourceM == null && targetM == null)
                    {
                        RescueAndClear(token, visual, notify: false);
                        yield break;
                    }

                    var next = nextSegment;
                    if (next == null)
                    {
                        // 이 칸이 꼬리 — 설비로 전달 시도
                        var tgt = targetM;
                        if (tgt == null)
                        {
                            // 도착 설비가 사라짐(철거) → 창고로 구조
                            RescueAndClear(token, visual, notify: false);
                            yield break;
                        }
                        if (!tgt.CanReceive(token.itemId))
                        {
                            // 레시피 불일치 등 — 받을 때까지 이 칸에 머문다(잼 = 역압). 창고로 안 보낸다.
                            yield return null;
                            continue;
                        }
                        // 전달: center → 설비 입구
                        yield return MoveVisual(visual, transform.position, FrontPoint(), half);
                        _occupant = null; _occupantVisual = null;
                        tgt.Receive(token.itemId, token.amount);
                        GameEvents.RaiseRailItemMoved(tgt.FacilityId, token.itemId, token.amount);   // 튜토: 레일 자동 이동 성공
                        if (visual != null) Destroy(visual);
                        NotifyFreed();
                        yield break;
                    }
                    else
                    {
                        if (next.IsOccupied)
                        {
                            // 다음 칸이 차 있음 → 비워질 때까지 이 칸에서 대기(역압)
                            yield return null;
                            continue;
                        }
                        // 방출: center → front(= 다음 칸 back). 같은 토큰/비주얼을 그대로 넘긴다(연속 이동).
                        yield return MoveVisual(visual, transform.position, FrontPoint(), half);
                        _occupant = null; _occupantVisual = null;
                        next.AcceptOccupant(token, visual);
                        NotifyFreed();
                        yield break;
                    }
                }
            }
            finally
            {
                // 보조 안전망: 정상 경로(전달/핸드오프/구조)와 철거(OnDisable)는 이미 _occupant 를 비운다.
                // 여기까지 아직 들고 있다면 예기치 못한 예외 등으로 중단된 경우 → 창고에 구조하고 비주얼 제거.
                // (Unity는 파괴/StopAllCoroutines 시 finally 실행을 보장하지 않으므로 이건 어디까지나 보조다.)
                if (_occupant == token)
                {
                    var v = _occupantVisual;
                    _occupant = null; _occupantVisual = null;
                    InventoryManager.StorageInstance?.AddItem(token.itemId, token.amount);
                    if (v != null) Destroy(v);
                }
            }
        }

        // 머리 칸이 비면 소스가 다음 아이템을 올릴 수 있게 알린다.
        // (중간/꼬리 칸이 비는 건 직전 칸 RunOccupant 가 매 프레임 next.IsOccupied 를 폴링해
        //  알아서 전진하므로 별도 통지가 필요 없다.)
        private void NotifyFreed()
        {
            if (prevSegment == null)
                sourceM?.TryDispatchPendingOutput();
        }

        // 아이템을 창고로 구조하고 이 칸의 점유를 해제한다(경로 소실 전용 — 레시피 잼은 여기로 오지 않는다).
        private void RescueAndClear(InFlightItem token, GameObject visual, bool notify)
        {
            _occupant = null; _occupantVisual = null;
            InventoryManager.StorageInstance?.AddItem(token.itemId, token.amount);
            if (notify) ToastManager.Warning("가동 중 철거 - 아이템 창고로 회수");
            if (visual != null) Destroy(visual);
        }

        private GameObject SpawnVisual(int itemId, int amount, Vector3 pos)
        {
            if (beltItemPrefab == null) return null;
            var v = Instantiate(beltItemPrefab, pos, beltItemPrefab.transform.rotation);
            if (v.TryGetComponent<BeltItemVisual>(out var vis)) vis.Setup(itemId, amount);
            return v;
        }

        // 비주얼을 from→to 로 dur 초 동안 직선 이동. visual 이 null 이어도 시간은 흐른다(타이밍 보존).
        private static IEnumerator MoveVisual(GameObject visual, Vector3 from, Vector3 to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                if (visual != null)
                    visual.transform.position = Vector3.Lerp(from, to, t / dur);
                t += Time.deltaTime;
                yield return null;
            }
            if (visual != null) visual.transform.position = to;
        }

        // ── 이 칸의 진입/방출 기준점 ─────────────────────────────────────
        // back  = 이전 칸과의 경계(없으면 소스 진입 stub), front = 다음 칸과의 경계(없으면 설비 입구 stub).
        // 아이템은 back→center→front 로 지나가고, 막히면 center(칸 중앙)에 머문다 → 칸마다 1개씩 균일 간격.

        // stub(머리/꼬리에서 칸 밖으로 뻗는 길이) = 이웃까지 거리의 절반(없으면 0.5).
        private float StubLen()
        {
            if (nextSegment != null) return Vector3.Distance(transform.position, nextSegment.transform.position) * 0.5f;
            if (prevSegment != null) return Vector3.Distance(transform.position, prevSegment.transform.position) * 0.5f;
            return 0.5f;
        }

        private Vector3 BackPoint()
        {
            if (prevSegment != null) return (prevSegment.transform.position + transform.position) * 0.5f;
            Vector3 dir = endpointBack != null ? (endpointBack.position - transform.position) : -transform.forward;
            return transform.position + dir.normalized * StubLen();
        }

        private Vector3 FrontPoint()
        {
            if (nextSegment != null) return (transform.position + nextSegment.transform.position) * 0.5f;
            Vector3 dir = endpointFront != null ? (endpointFront.position - transform.position) : transform.forward;
            return transform.position + dir.normalized * StubLen();
        }

        private void OnDrawGizmosSelected()
        {
            if (_cellsInitialized)
            {
                // 자신의 셀 (파란색)
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(transform.position, Vector3.one * 0.4f);
                // 앞 방향 연결 셀 (초록)
                Gizmos.color = Color.green;
                if (endpointFront != null) Gizmos.DrawWireCube(endpointFront.position, Vector3.one * 0.3f);
                // 뒤 방향 연결 셀 (빨강)
                Gizmos.color = Color.red;
                if (endpointBack != null) Gizmos.DrawWireCube(endpointBack.position, Vector3.one * 0.3f);
            }
            else
            {
                Gizmos.color = Color.green;
                if (endpointFront != null) Gizmos.DrawWireSphere(endpointFront.position, detectRadius);
                Gizmos.color = Color.red;
                if (endpointBack  != null) Gizmos.DrawWireSphere(endpointBack.position,  detectRadius);
            }
        }
    }
}