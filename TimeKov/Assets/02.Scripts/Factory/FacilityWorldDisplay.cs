// =====================================================================
// FacilityWorldDisplay.cs
// 엔드필드 스타일 설비 월드 표시 (플레이어 3인칭 카메라 뷰 기준).
//  1) 제작 중 아이템 아이콘 — 설비 가운데에 BG와 함께, 항상 카메라를 향함(빌보드).
//  2) 설비 이름 — 플레이어 근접 시에만, 사각형 4면 중 카메라가 보는 면에 납작하게 고정 표시(빌보드 아님).
//  3) 금지 표시 — (a) 아이템이 도착했으나 타깃이 못 받아 실제로 정체(잼)된 순간, 또는
//     (b) 타깃이 이 설비 산출물을 못 받는 '잘못된 연결'(연결 즉시 예측). 위치는 레일이 설비에 물리는 지점.
// ProcessingMachine 이 런타임에 자동 부착한다(프리팹 편집 불필요).
// =====================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TIMEKOV.Factory
{
    [DisallowMultipleComponent]
    public class FacilityWorldDisplay : MonoBehaviour
    {
        // 설비 UI(MachineUI) 등이 열린 동안 모든 월드 표시(이름표/제작아이콘/막힘) 억제.
        // onTop 머티리얼(_ZTestMode=8)이라 패널 블러 위로 뚫고 올라오므로, 패널 열 때 true.
        public static bool SuppressWorldLabels;

        [Header("공통")]
        [Tooltip("월드 캔버스 스케일 (픽셀→월드 변환 배수). 0.01이면 100px = 1m.")]
        public float worldScale = 0.01f;
        [Tooltip("이 거리(m)보다 멀면 모두 숨김. 0이면 무제한.")]
        public float maxVisibleDistance = 60f;
        [Tooltip("요소를 설비 표면보다 카메라 쪽으로 살짝 당겨 가림(occlusion) 방지하는 거리(m).")]
        public float cameraPullOffset = 0.15f;

        [Header("제작 아이템 아이콘 (설비 정중앙, 안쪽)")]
        [Tooltip("아이템 이미지 크기(px). BG와 비슷하게 두면 아이콘이 배지 밖으로 살짝 튀어나와 입체적으로 보인다.")]
        public float craftIconPixels = 86f;
        [Tooltip("원형 BG 크기(px). 아이템보다 약간 작게 두면 아이콘이 튀어나온 느낌.")]
        public float craftBgPixels   = 78f;
        [Tooltip("설비 정중앙에서 위로 미세 조정(m). 0이면 정확히 중심.")]
        public float craftYOffset = 0f;
        [Tooltip("원형 BG 채움색 (탑뷰 설비 배지와 동일한 어두운 색).")]
        public Color craftBgColor = new Color(0.05f, 0.06f, 0.08f, 0.85f);
        [Tooltip("원형 BG 테두리(링) 두께(px, 128 스프라이트 기준). 테두리 색은 아이템 레어도색.")]
        public float craftRingThickness = 4f;

        [Header("설비 이름 (정면 고정, 텍스트만)")]
        public float nameFontSize = 30f;
        public Color nameTextColor = Color.white;
        [Tooltip("설비(몸체) 높이 대비 이름 높이 비율(0=바닥, 1=상단). 벨트 연결 검은 받침 위로 올리려면 키운다.")]
        [Range(0f, 1f)] public float nameHeightFrac = 0.34f;
        [Tooltip("설비 시각 표면에서 바깥으로 더 띄우는 거리(m). 텍스트가 몸체에 묻히면 키운다.")]
        public float nameOutwardOffset = 0.3f;
        [Tooltip("플레이어가 설비 표면에서 이 거리(m) 안에 들어왔을 때만 이름 표시.")]
        public float nameVisibleDistance = 4.5f;

        [Header("금지 표시 (막힘)")]
        public float blockPixels = 60f;
        [Tooltip("레일이 거부 설비에 연결된 지점(체인 꼬리 칸)에서 위로 올리는 높이(m).")]
        public float blockYOffset = 1.2f;
        public Color blockColor = new Color(1f, 0.22f, 0.2f, 0.95f);

        // ── 런타임 ────────────────────────────────────────────────────────
        private MachineBase _machine;
        private ProcessingMachine _pm;
        private Camera _cam;
        private Transform _player;

        private Collider[] _cols;      // 콜라이더 바운즈(몸체) — 위치/근접 판정 기준
        private Renderer[] _rends;     // 콜라이더 없을 때 폴백용
        private Bounds _bounds;        // 콜라이더(몸체) 바운즈
        private Vector3 _boundsCenter;
        private float  _boundsBaseY, _boundsTopY, _extX, _extZ;

        // 깊이를 무시하고 항상 위에 그리는 UI 머티리얼 (메시 안쪽/뒤에 있어도 보이게).
        private static Material _onTopMat;
        private static Material OnTopMat
        {
            get
            {
                if (_onTopMat == null)
                {
                    _onTopMat = new Material(Shader.Find("UI/Default"));
                    _onTopMat.SetInt("unity_GUIZTestMode",
                        (int)UnityEngine.Rendering.CompareFunction.Always);
                }
                return _onTopMat;
            }
        }

        // 제작 아이콘 요소
        private GameObject _craftRoot;
        private Image      _craftIcon;
        private Image      _craftRing;   // 레어도색 테두리
        private int        _craftShownItemId = -1;

        // 이름 요소
        private GameObject _nameRoot;
        private TextMeshProUGUI _nameText;

        // 금지 표시 요소
        private GameObject _blockRoot;
        private System.Collections.Generic.List<int> _outIds;   // 이 설비 산출물 id 캐시(연결 즉시 예측 경고용)
        private int _cachedLockedIdx = int.MinValue;

        private void Awake()
        {
            _machine = GetComponent<MachineBase>();
            _pm = _machine as ProcessingMachine;
        }

        private void Start()
        {
            _cam = Camera.main;
            var p = FindFirstObjectByType<Player>();
            if (p != null) _player = p.transform;
            BuildElements();
        }

        private void OnDestroy()
        {
            // 최상위(독립) 오브젝트라 명시적으로 정리.
            if (_craftRoot != null) Destroy(_craftRoot);
            if (_nameRoot  != null) Destroy(_nameRoot);
            if (_blockRoot != null) Destroy(_blockRoot);
        }

        // ── 생성 ────────────────────────────────────────────────────────
        private void BuildElements()
        {
            BuildCraftIcon();
            BuildNameLabel();
            BuildBlockIcon();

            HideAll();
        }

        private void BuildCraftIcon()
        {
            _craftRoot = CreateWorldCanvas("_FacilityCraftIcon");

            // 어두운 원형 BG (탑뷰 설비 배지와 동일 스타일). 설비 안쪽에 둬도 보이게 onTop.
            AddImage(_craftRoot.transform, UISpriteFactory.Circle(128),
                     craftBgColor, new Vector2(craftBgPixels, craftBgPixels), onTop: true);

            // 원형 테두리(링) — 색은 제작 아이템 레어도에 맞춰 런타임에 설정
            _craftRing = AddImage(_craftRoot.transform, UISpriteFactory.Ring(128, craftRingThickness),
                                  Color.white, new Vector2(craftBgPixels, craftBgPixels), onTop: true);

            // 제작 아이템 아이콘
            _craftIcon = AddImage(_craftRoot.transform, null, Color.white,
                                  new Vector2(craftIconPixels, craftIconPixels), onTop: true);
            _craftIcon.preserveAspect = true;
        }

        private void BuildNameLabel()
        {
            _nameRoot = CreateWorldCanvas("_FacilityNameLabel");

            var txtGo = new GameObject("Name", typeof(RectTransform));
            var trt = (RectTransform)txtGo.transform;
            trt.SetParent(_nameRoot.transform, false);
            trt.anchoredPosition = Vector2.zero;
            _nameText = txtGo.AddComponent<TextMeshProUGUI>();
            if (FacilityIconDatabase.Instance != null && FacilityIconDatabase.Instance.labelFont != null)
                _nameText.font = FacilityIconDatabase.Instance.labelFont;
            _nameText.fontSize = nameFontSize;
            _nameText.color = nameTextColor;
            _nameText.fontStyle = FontStyles.Bold;
            _nameText.alignment = TextAlignmentOptions.Center;
            _nameText.textWrappingMode = TextWrappingModes.NoWrap;
            _nameText.overflowMode = TextOverflowModes.Overflow;
            _nameText.raycastTarget = false;
            _nameText.outlineWidth = 0.18f;
            _nameText.outlineColor = new Color(0f, 0f, 0f, 1f);
            _nameText.text = GetDisplayName();

            LayoutNameLabel();
        }

        // 표시 이름: FacilityData 시트(facilityName)를 우선 사용 - 시트에서 개명하면 자동 반영된다.
        //  (프리팹 루트 이름은 개명해도 안 바뀌어 스테일. 예: 프리팹 "화학 정제기" -> 시트 "시간에너지 합성기".)
        //  시트 조회 실패(데이터 미로드/미매핑/facilityId 0)면 프리팹 루트 이름으로 폴백.
        //  (F-프롬프트/도감도 같은 GameDataUtility.GetFacility 경로라 이제 표시가 일관된다.)
        private string GetDisplayName()
        {
            if (_machine != null)
            {
                var fd = GameDataUtility.GetFacility(_machine.FacilityId);
                if (fd != null && !string.IsNullOrEmpty(fd.facilityName))
                    return fd.GetLocalizedName();
            }

            string n = gameObject.name ?? "";
            int idx = n.IndexOf("(Clone)", System.StringComparison.Ordinal);
            if (idx >= 0) n = n.Substring(0, idx);
            return n.TrimEnd();
        }

        // 이름 텍스트 폭에 맞춰 rect 크기 갱신 (BG/아이콘 없이 텍스트만).
        private void LayoutNameLabel()
        {
            if (_nameText == null) return;
            _nameText.text = GetDisplayName();
            _nameText.ForceMeshUpdate();

            var trt = (RectTransform)_nameText.transform;
            trt.sizeDelta = new Vector2(_nameText.preferredWidth + 8f, nameFontSize + 12f);
            trt.anchoredPosition = Vector2.zero;

            // 큰 설비 몸체에 묻혀도 항상 위에 그림(깊이 무시). 메시 갱신 후 재적용해 확실히 유지.
            // CompareFunction.Always = 8
            _nameText.fontMaterial.SetFloat("_ZTestMode", 8f);
        }

        private void BuildBlockIcon()
        {
            _blockRoot = CreateWorldCanvas("_FacilityBlockIcon");
            AddImage(_blockRoot.transform, UISpriteFactory.NoEntry(96),
                     blockColor, new Vector2(blockPixels, blockPixels));
        }

        // ── 매 프레임 갱신 ───────────────────────────────────────────────
        private void LateUpdate()
        {
            if (SuppressWorldLabels) { HideAll(); return; }   // 설비 패널 열림 = 월드 표시 끔(블러 위로 뚫고 나오는 것 차단)
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }
            UpdateBounds();   // 매 프레임 갱신 — 첫 프레임 바운즈 오류/늦은 로드에도 견고

            // 거리 컬링
            if (maxVisibleDistance > 0f)
            {
                float distSqr = (_cam.transform.position - _boundsCenter).sqrMagnitude;
                if (distSqr > maxVisibleDistance * maxVisibleDistance)
                {
                    HideAll();
                    return;
                }
            }

            UpdateCraftIcon();
            UpdateNameLabel();
            UpdateBlockIcon();
        }

        private void UpdateCraftIcon()
        {
            bool show = _pm != null
                     && _machine.Status == MachineStatus.Processing
                     && _pm.ActiveRecipe != null
                     && _pm.ActiveRecipe.outputs != null
                     && _pm.ActiveRecipe.outputs.Length > 0;

            if (!show) { if (_craftRoot.activeSelf) _craftRoot.SetActive(false); return; }

            int itemId = _pm.ActiveRecipe.outputs[0].itemId;
            if (itemId != _craftShownItemId)
            {
                _craftShownItemId = itemId;
                var item = GameDataUtility.GetItem(itemId);
                _craftIcon.sprite = item != null ? ItemDatabase.GetIcon(item.iconKey) : null;
                _craftIcon.enabled = _craftIcon.sprite != null;
                // 테두리 색 = 아이템 레어도색
                _craftRing.color = item != null ? GradeVisual.GetColor(item.itemGrade) : Color.white;
            }

            if (!_craftRoot.activeSelf) _craftRoot.SetActive(true);

            // 설비 정중앙(안쪽)에 배치. onTop 머티리얼이라 메시에 묻혀도 보인다.
            _craftRoot.transform.position = _boundsCenter + new Vector3(0f, craftYOffset, 0f);
            _craftRoot.transform.forward = _cam.transform.forward;
        }

        private void UpdateNameLabel()
        {
            // 플레이어가 설비 표면 근처(nameVisibleDistance)에 들어왔을 때만 표시.
            if (_player == null)
            {
                var p = FindFirstObjectByType<Player>();
                if (p != null) _player = p.transform;
            }
            bool near = _player != null
                     && _bounds.SqrDistance(_player.position) <= nameVisibleDistance * nameVisibleDistance;
            if (!near) { if (_nameRoot.activeSelf) _nameRoot.SetActive(false); return; }

            if (!_nameRoot.activeSelf) _nameRoot.SetActive(true);

            // 데이터 로드가 늦어 이름이 나중에 확정될 수 있으므로 변경 시 재배치
            if (_nameText.text != GetDisplayName())
                LayoutNameLabel();

            // 플레이어(카메라)가 있는 쪽 면을 골라 그 면에 표시 → 항상 플레이어를 향한다.
            // ±X/±Z 중 하나로 스냅(4방향). 글자는 그 면 법선에 납작하게 고정(빌보드 아님).
            // 중심은 콜라이더(몸체) 기준 — 파티클/이펙트로 왜곡되는 시각 바운즈를 쓰지 않는다.
            Vector3 center = _boundsCenter;
            Vector3 toCam = _cam.transform.position - center;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 1e-4f) toCam = Vector3.forward;

            Vector3 faceDir;
            float ext;
            if (Mathf.Abs(toCam.x) >= Mathf.Abs(toCam.z))
            {
                faceDir = new Vector3(Mathf.Sign(toCam.x), 0f, 0f);
                ext = _extX;
            }
            else
            {
                faceDir = new Vector3(0f, 0f, Mathf.Sign(toCam.z));
                ext = _extZ;
            }

            // 몸체 밖으로 확실히 내보낸다(ext = 그 면까지 반폭, +offset 으로 표면 바깥). clamp 없음.
            float outward = ext + nameOutwardOffset;

            // 높이 base는 설비 배치 원점(transform.position.y = 지면) 기준 — 콜라이더가 origin 중심이라
            // 지하로 내려가거나(5x5) 시각 모델이 지면 아래로 뻗어도(생체 배양기) 영향 없음.
            float span = Mathf.Max(0.1f, _boundsTopY - _boundsBaseY);
            float y = transform.position.y + nameHeightFrac * span;
            Vector3 pos = new Vector3(center.x, y, center.z)
                        + faceDir * outward;

            _nameRoot.transform.position = pos;
            // 캔버스 글자는 forward(+Z)가 카메라 반대(설비 안쪽)를 향할 때 정상으로 읽힌다.
            // 카메라는 faceDir 쪽(바깥)에 있으므로 forward = -faceDir 로 둔다. 4개 고정 방향 중 하나, 카메라 추적 X.
            _nameRoot.transform.rotation = Quaternion.LookRotation(-faceDir, Vector3.up);
        }

        private void UpdateBlockIcon()
        {
            // 막힘 표시 = 두 경우. 위치는 둘 다 레일이 설비에 물리는 꼬리 칸(실제 지점).
            //  (a) 실제 잼: 아이템이 타깃 입구까지 도착했는데 못 받아 물리적으로 정체.
            //  (b) 연결 즉시 예측: 타깃이 이 설비 산출물을 못 받는 '잘못된 연결' — 아이템이 흐르기 전에 미리 경고.
            //      (플레이어가 안 맞는 연결을 헷갈리지 않게. 재원의 '실제 위치 표시'는 유지하고 예측 경고만 복원.)
            BeltSegment tail = FindBlockedTail();
            if (tail == null) { if (_blockRoot.activeSelf) _blockRoot.SetActive(false); return; }
            if (!_blockRoot.activeSelf) _blockRoot.SetActive(true);

            Vector3 pos = tail.transform.position + Vector3.up * blockYOffset;
            pos -= _cam.transform.forward * cameraPullOffset;

            _blockRoot.transform.position = pos;
            _blockRoot.transform.forward = _cam.transform.forward;
        }

        // ── 막힘 판정 (실제 잼 + 연결 즉시 예측) ───────────────────────────
        // 이 설비의 출력 벨트 체인들 중, 꼬리 칸이 아래 둘 중 하나면 그 꼬리를 반환(없으면 null):
        //  (a) IsJammedAtTarget = 아이템이 도착했으나 타깃이 못 받아 물리적으로 정체.
        //  (b) 타깃이 이 설비의 현재 산출물을 하나라도 못 받음 = 잘못된 연결(예측, 아이템 안 흘러도).
        // outputBelts 에 담긴 건 체인 머리라 nextSegment 로 꼬리까지 따라간다.
        private BeltSegment FindBlockedTail()
        {
            if (_machine.outputBelts == null || _machine.outputBelts.Count == 0)
                return null;

            var outIds = _pm != null ? GetOutputItemIds() : null;   // 예측용 산출물 id(현재 레시피)

            foreach (var belt in _machine.outputBelts)
            {
                if (belt == null || !belt.IsReady) continue;

                BeltSegment tail = belt;
                int guard = 256;
                while (tail.nextSegment != null && guard-- > 0) tail = tail.nextSegment;
                if (tail == null) continue;

                // (a) 실제 물리 잼
                if (tail.IsJammedAtTarget) return tail;

                // (b) 연결 즉시 예측: 타깃이 산출물을 하나라도 못 받으면 잘못된 연결
                var tgt = tail.targetM;
                if (tgt != null && tgt != _machine && outIds != null)
                    foreach (var id in outIds)
                        if (!tgt.CanReceive(id)) return tail;
            }
            return null;
        }

        // 현재 선택된(잠긴) 레시피, 없으면 첫 레시피의 산출물 id 목록 (변경 시에만 재계산).
        private System.Collections.Generic.List<int> GetOutputItemIds()
        {
            if (_pm.Recipes == null || _pm.Recipes.Count == 0) return null;

            int idx = _pm.LockedRecipeIndex;
            if (idx == _cachedLockedIdx && _outIds != null) return _outIds;
            _cachedLockedIdx = idx;

            var recipe = (idx >= 0 && idx < _pm.Recipes.Count) ? _pm.Recipes[idx] : _pm.Recipes[0];
            if (recipe == null || recipe.outputs == null) { _outIds = null; return null; }

            _outIds ??= new System.Collections.Generic.List<int>();
            _outIds.Clear();
            foreach (var o in recipe.outputs) _outIds.Add(o.itemId);
            return _outIds;
        }

        // ── 헬퍼 ────────────────────────────────────────────────────────

        // 콜라이더(몸체) 바운즈를 매 프레임 갱신 (캐시 수집은 최초 1회 — 매 프레임 할당 없음).
        // 위치/근접 기준으로 콜라이더만 쓴다(파티클·이펙트로 왜곡되는 렌더러 바운즈 회피).
        // 콜라이더가 없는 설비만 렌더러 바운즈로 폴백.
        private void UpdateBounds()
        {
            // ── 렌더러(시각) 바운즈 (콜라이더 없을 때 폴백용) ──
            if (_rends == null)
            {
                var all = GetComponentsInChildren<Renderer>(true);
                var rl = new System.Collections.Generic.List<Renderer>(all.Length);
                foreach (var r in all)
                {
                    if (r == null) continue;
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    rl.Add(r);
                }
                _rends = rl.ToArray();
            }
            bool vhas = false;
            Bounds vb = new Bounds(transform.position, Vector3.zero);
            foreach (var r in _rends)
            {
                if (r == null || !r.enabled) continue;
                if (!vhas) { vb = r.bounds; vhas = true; }
                else vb.Encapsulate(r.bounds);
            }
            if (!vhas) vb = new Bounds(transform.position, Vector3.one);

            // ── 콜라이더 바운즈(BuildPort 제외) ── 없으면 시각 바운즈로 폴백
            if (_cols == null)
            {
                var allCols = GetComponentsInChildren<Collider>(true);
                var list = new System.Collections.Generic.List<Collider>(allCols.Length);
                foreach (var c in allCols)
                {
                    if (c == null) continue;
                    if (c.GetComponentInParent<BuildPort>() != null) continue; // 포트 콜라이더 제외
                    list.Add(c);
                }
                _cols = list.ToArray();
            }
            bool chas = false;
            Bounds cb = new Bounds(transform.position, Vector3.zero);
            foreach (var c in _cols)
            {
                if (c == null || !c.enabled) continue;
                if (!chas) { cb = c.bounds; chas = true; }
                else cb.Encapsulate(c.bounds);
            }
            if (!chas) cb = vb;   // 콜라이더 없으면 시각 바운즈 사용

            _bounds = cb;
            _boundsCenter = cb.center;
            _boundsBaseY  = cb.min.y;
            _boundsTopY   = cb.max.y;

            // 바깥 밀어내기(ext)는 콜라이더(몸체) 기준. 시각 바운즈는 파티클/이펙트(예: 생체 배양기 FX_steam)
            // 등 부수 요소로 왜곡될 수 있어 이름 위치 기준으로 쓰지 않는다.
            _extX = cb.extents.x;
            _extZ = cb.extents.z;
        }

        private void HideAll()
        {
            if (_craftRoot != null && _craftRoot.activeSelf) _craftRoot.SetActive(false);
            if (_nameRoot  != null && _nameRoot.activeSelf)  _nameRoot.SetActive(false);
            if (_blockRoot != null && _blockRoot.activeSelf) _blockRoot.SetActive(false);
        }

        private GameObject CreateWorldCanvas(string n)
        {
            var go = new GameObject(n);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(400f, 400f);
            rt.localScale = Vector3.one * worldScale;
            return go;
        }

        private Image AddImage(Transform parent, Sprite sprite, Color color, Vector2 size, bool onTop = false)
        {
            var go = new GameObject("Img", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (onTop) img.material = OnTopMat;   // 메시에 가려도 항상 위에 표시
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }
    }
}
