using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 워프 시스템 총괄 — 씬 내 텔레포트 + 검은 화면 페이드 + VFX + 활성화 상태 저장.
//   밟으면 자동(WarpPoint 트리거) → OnWarpStepped 로 위임.
//   RegionPoint: 최초=활성화(저장), 이후=기지 복귀.  BaseOutbound: 활성화된 지역 지점 중 랜덤 워프.
//   맵이 단일 World.unity 안 지역이라 씬 로드 없이 트랜스폼만 이동한다.
[SingleInstance]
public class WarpManager : MonoBehaviour, ISaveable
{
    public static WarpManager Instance { get; private set; }

    [Header("기지 복귀 지점 (지역→기지 워프 도착점). 지역별로 하나씩. 비우면 이 오브젝트 위치)")]
    [SerializeField] private Transform baseReturnSnow;    // 설산에서 복귀
    [SerializeField] private Transform baseReturnDesert;  // 사막에서 복귀
    [SerializeField] private Transform baseReturnLava;    // 용암에서 복귀

    [Header("워프 VFX (공용 프리팹, 지역별 색 틴트)")]
    [Tooltip("출발(리콜) VFX. WarpPoint 에 개별 지정이 없으면 이걸 쓴다. 예) Recall_06")]
    [SerializeField] private GameObject defaultDepartureVfx;
    [Tooltip("도착(스폰) VFX. 예) Spawn_06")]
    [SerializeField] private GameObject defaultArrivalVfx;
    [Tooltip("지역별 VFX 색(밝기 유지 + 색조 교체). 설산=흰색(원본 유지), 사막=황토, 용암=빨강.")]
    [SerializeField] private Color snowTint = Color.white;
    [SerializeField] private Color desertTint = new Color(0.82f, 0.60f, 0.28f, 1f);
    [SerializeField] private Color lavaTint = new Color(1.00f, 0.25f, 0.12f, 1f);

    [Header("통 발광 재질 (지역별 교체 — 통의 흰색 'Light_White' 재질을 지역 재질로 바꿈)")]
    [Tooltip("사막 통 발광 재질. 예) Light_Yellow")]
    [SerializeField] private Material tubeLightDesert;
    [Tooltip("용암 통 발광 재질. 예) Light_Red")]
    [SerializeField] private Material tubeLightLava;

    [Header("워프 충전 (원통 안에서 대기)")]
    [Tooltip("원통 안에 들어온 뒤 실제 워프까지 대기하는 시간(출발 VFX 길이에 맞춤). Recall_06 ≈ 5초. 도중에 나가면 취소.")]
    [SerializeField] private float chargeDuration = 5f;

    [Header("페이드")]
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private float blackHold = 0.15f;   // 완전 검은 상태 유지(텔레포트 순간)

    [Header("도착 지면 정렬 (원통 바닥에 끼는 것 방지)")]
    [Tooltip("도착 지점 위에서 아래로 레이캐스트해 지면 위에 세운다. 지면으로 칠 레이어.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float snapRayUp = 3f;     // 도착점에서 이만큼 위에서 아래로 쏨
    [SerializeField] private float snapRayLength = 12f;
    [SerializeField] private float arrivalSkin = 0.05f; // 지면에서 살짝 띄움

    [Header("테스트")]
    [Tooltip("켜면 시작 시 씬의 모든 지역 워프 지점을 활성화 상태로 둔다(저장값 무시). 디버그용.")]
    [SerializeField] private bool activateAllForTest = false;

    private readonly HashSet<string> _activated = new();
    private CanvasGroup _fadeCg;
    private bool _warping;

    // 충전 상태(원통 안에서 대기 중). 나가면 취소.
    private WarpPoint _chargingPoint;
    private Coroutine _chargeCo;
    private GameObject _chargeVfx;

    private void Awake()
    {
        if (UIDuplicateGuard.Report(Instance, this)) { Destroy(gameObject); return; }
        Instance = this;
        SaveSlotManager.Instance?.Register(this);
        RestoreFromSave();
        BuildFadeOverlay();
    }

    private void Start()
    {
        if (activateAllForTest) ActivateAllRegionPoints();
        RecolorAllTubes();
    }

    // 씬의 모든 워프 통 발광 색을 지역 색으로 교체(런타임 머티리얼 인스턴스라 에셋/씬은 불변).
    private void RecolorAllTubes()
    {
        foreach (var wp in FindObjectsByType<WarpPoint>(FindObjectsSortMode.None))
            RecolorTube(wp);
    }

    // 통의 파란 발광 재질(Light_Blue)을 지역 재질로 교체. 설산=교체 안 함(파랑 유지).
    //   sharedMaterials 로 교체 → 런타임 렌더러만 바뀌고 에셋/씬은 불변, 배칭도 유지.
    private void RecolorTube(WarpPoint wp)
    {
        if (wp == null || !wp.recolorTube) return;
        Material target = TubeMatFor(wp.region);
        if (target == null) return;   // 설산 등 지정 없음 → 기본 유지

        var renderers = (wp.tubeRenderers != null && wp.tubeRenderers.Length > 0)
            ? wp.tubeRenderers
            : wp.GetComponentsInChildren<Renderer>(true);
        int swapped = 0;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mats = r.sharedMaterials;   // 복사본 반환 → 수정 후 재할당
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && IsTubeLight(mats[i].name))
                {
                    mats[i] = target;
                    changed = true;
                    swapped++;
                }
            if (changed) r.sharedMaterials = mats;
        }
        if (swapped == 0)
            Debug.LogWarning($"[Warp] '{wp.name}' 통 발광 재질(Light_White)을 못 찾아 색 교체 안 됨. " +
                             "WarpPoint 의 Tube Renderers 에 통의 발광 렌더러를 직접 지정하세요.", wp);
    }

    private Material TubeMatFor(TransmissionRegion r) => r switch
    {
        TransmissionRegion.Desert => tubeLightDesert,
        TransmissionRegion.Lava => tubeLightLava,
        _ => null,   // 설산=기본(흰색) 유지
    };

    // 통의 발광 링 재질 판별(교체 대상). 통은 Light_White 를 쓴다(일부 파란 Light_Blue 도 대비).
    private static bool IsTubeLight(string matName) =>
        matName.StartsWith("Light_White") || matName.StartsWith("Light_Blue");

    private void OnDestroy()
    {
        SaveSlotManager.Instance?.Unregister(this);
        if (Instance == this) Instance = null;
    }

    // 테스트: 씬의 모든 지역 워프 지점을 활성화(저장은 하지 않음 — 런타임 상태만).
    private void ActivateAllRegionPoints()
    {
        foreach (var p in FindObjectsByType<WarpPoint>(FindObjectsSortMode.None))
            if (p != null && p.IsRegionPoint && !string.IsNullOrEmpty(p.warpId))
                _activated.Add(p.warpId);
    }

    public bool IsActivated(string id) => !string.IsNullOrEmpty(id) && _activated.Contains(id);

    // ── 워프 밟음 처리 ────────────────────────────────────────────────
    public void OnWarpStepped(WarpPoint wp, Transform player)
    {
        if (_warping || _chargingPoint != null || wp == null || player == null) return;

        if (wp.kind == WarpPoint.WarpKind.RegionPoint)
        {
            // 최초 활성화는 F 상호작용(ActivateRegionPoint)으로만 한다 - 밟아서 자동활성화하지 않는다.
            //   (소개 영상이 "F로 활성화한 순간"에 딱 뜨도록. 지나가다 밟혀서 뜨는 어색함 제거)
            if (!IsActivated(wp.warpId))
            {
                ToastManager.Info(Loc.Get("먼저 F로 워프를 활성화하세요."));   // 활성화 전엔 밟아도 이동 안 됨
                return;
            }

            // 활성화된 지역 워프 재진입 = 해당 지역의 기지 복귀 지점으로 복귀
            Transform ret = ReturnPointFor(wp.region);
            if (ret == null)
            {
                // 복귀 지점 미할당 → 원점으로 텔레포트하면 물/허공에 박히므로 취소.
                Debug.LogWarning($"[Warp] {RegionName(wp.region)} 기지 복귀 지점(baseReturn*)이 할당되지 않았습니다.", this);
                ToastManager.Warning(Loc.Get("기지 복귀 지점이 설정되지 않았습니다."));
                wp.SuppressUntilExit();
                return;
            }
            BeginCharge(wp, player, ret.position, wp, wp.region);
        }
        else // BaseOutbound
        {
            var dests = ActivatedPointsIn(wp.region);
            if (dests.Count == 0)
            {
                ToastManager.Warning(string.Format(Loc.Get("{0} 지역에서 먼저 워프 지점을 활성화하세요."), Loc.Get(RegionName(wp.region))));
                wp.SuppressUntilExit();
                return;
            }
            var target = dests[Random.Range(0, dests.Count)];
            BeginCharge(wp, player, target.ArrivalPosition, target, wp.region);
        }
    }

    // F 상호작용으로 지역 워프 지점을 최초 활성화(등록·저장·연출·소개영상). 텔레포트는 없다.
    //   WarpPoint.Interact 에서 호출. 이미 활성화됐거나 지역 지점이 아니면 무시.
    //   활성화 이후부터 이 지점을 밟으면 기지로 복귀(OnWarpStepped 가 처리).
    public void ActivateRegionPoint(WarpPoint wp, Transform player)
    {
        if (wp == null || !wp.IsRegionPoint || string.IsNullOrEmpty(wp.warpId)) return;
        if (IsActivated(wp.warpId)) return;

        _activated.Add(wp.warpId);
        SpawnVfx(ResolveArrival(wp), wp.Center, TintFor(wp.region));
        GameSfx.Play(SfxId.WarpArrive, wp.Center);   // 활성화음(도착음 공용)
        ToastManager.Success(string.Format(Loc.Get("워프 지점 활성화 - {0}"), Loc.Get(RegionName(wp.region))));
        SaveSlotManager.Instance?.SaveActive();
        DiscoveryCueManager.TryFire("warp");   // 소개 영상(1회, 이후 지점은 중복 무시)

        // 활성화 직후 바로 워프하지 않고, 소개 영상이 닫힌 뒤(그때도 통 안이면) 워프를 시작한다.
        //   활성화 순간 워프가 시작됐다가 영상이 떠서 끊기는 느낌을 없앤다.
        StartCoroutine(WarpAfterCueRoutine(wp, player));
    }

    private IEnumerator WarpAfterCueRoutine(WarpPoint wp, Transform player)
    {
        yield return null;   // safe 큐는 이 프레임에 열리므로 한 프레임 양보(영상 뜰 시간)
        while (TutorialVideoUI.IsShowing) yield return null;   // 소개 영상 닫힐 때까지 대기
        // 영상 보는 동안 통 밖으로 나갔으면 워프 안 함(다시 밟아야). 여전히 안이면 기지 복귀 워프 시작.
        if (wp != null && wp.PlayerInside && player != null)
            OnWarpStepped(wp, player);
    }

    // 원통에서 나가면 충전 취소.
    public void OnWarpExited(WarpPoint wp)
    {
        if (_chargingPoint == null || _chargingPoint != wp) return;
        if (_chargeCo != null) StopCoroutine(_chargeCo);
        _chargeCo = null;
        _chargingPoint = null;
        CleanupChargeVfx();
        ToastManager.Warning(Loc.Get("워프가 취소되었습니다."));
    }

    // ── 충전(원통 안 대기) ────────────────────────────────────────────
    //   출발 VFX/사운드를 켜고 chargeDuration 만큼 대기 → 완료 시 실제 워프(페이드+텔레포트).
    //   대기 중 입력은 막지 않는다(밖으로 걸어나가 취소할 수 있어야 하므로).
    private void BeginCharge(WarpPoint from, Transform player, Vector3 dest, WarpPoint arriveAt, TransmissionRegion theme)
    {
        _chargingPoint = from;
        _chargeVfx = SpawnVfxTracked(ResolveDeparture(from), from.Center, TintFor(theme));
        StartChargeLoop(from.Center);   // 충전 중 도는 루프음(SfxId.WarpChargeLoop)
        CastGaugeUI.Ensure()?.Begin(Loc.Get("워프 중"));   // 상단 게이지(얼마나 워프됐는지 표시)
        _chargeCo = StartCoroutine(ChargeRoutine(player, dest, arriveAt, theme));
    }

    private IEnumerator ChargeRoutine(Transform player, Vector3 dest, WarpPoint arriveAt, TransmissionRegion theme)
    {
        // 원통 안에서 대기(나가면 OnWarpExited 가 취소). 매 프레임 게이지를 채운다.
        float t = 0f;
        while (t < chargeDuration)
        {
            t += Time.deltaTime;
            CastGaugeUI.Instance?.Report(t / chargeDuration, chargeDuration - t);
            yield return null;
        }

        _chargeCo = null;
        _chargingPoint = null;
        CleanupChargeVfx();
        // 출발 연출은 충전 때 이미 재생했으므로 여기선 페이드/텔레포트/도착만.
        yield return StartCoroutine(WarpRoutine(player, dest, arriveAt, theme));
    }

    private void CleanupChargeVfx()
    {
        if (_chargeVfx != null) Destroy(_chargeVfx);
        _chargeVfx = null;
        StopChargeLoop();   // 충전 종료(취소/완료 공통) — 루프음 정지
        CastGaugeUI.Instance?.Hide();   // 게이지 숨김(취소/완료 공통)
    }

    private List<WarpPoint> ActivatedPointsIn(TransmissionRegion region)
    {
        var list = new List<WarpPoint>();
        foreach (var p in FindObjectsByType<WarpPoint>(FindObjectsSortMode.None))
            if (p != null && p.IsRegionPoint && p.region == region && IsActivated(p.warpId)) list.Add(p);
        return list;
    }

    // ── 워프 연출/이동 ────────────────────────────────────────────────
    // 실제 워프 — 페이드 + 텔레포트 + 도착 연출. (출발 연출은 충전 때 이미 재생됨)
    private IEnumerator WarpRoutine(Transform player, Vector3 dest, WarpPoint arriveAt, TransmissionRegion theme)
    {
        _warping = true;
        PlayerInputComponent.IsBlocked = true;

        yield return Fade(0f, 1f);                // 검게
        yield return new WaitForSecondsRealtime(blackHold);

        TeleportPlayer(player, dest);             // 순간이동(검은 화면 중)
        SpawnVfx(ResolveArrival(arriveAt), dest, TintFor(theme));   // 도착 VFX
        GameSfx.Play(SfxId.WarpArrive, dest);     // 워프 도착음
        if (arriveAt != null) arriveAt.SuppressUntilExit();   // 도착 지점 즉시 재발동 방지

        yield return Fade(1f, 0f);                // 밝게
        PlayerInputComponent.IsBlocked = false;
        _warping = false;
    }

    private GameObject ResolveDeparture(WarpPoint p) => p != null && p.departureVfx != null ? p.departureVfx : defaultDepartureVfx;
    private GameObject ResolveArrival(WarpPoint p) => p != null && p.arrivalVfx != null ? p.arrivalVfx : defaultArrivalVfx;

    private Color TintFor(TransmissionRegion r) => r switch
    {
        TransmissionRegion.Desert => desertTint,
        TransmissionRegion.Lava => lavaTint,
        _ => snowTint,
    };

    // 충전 루프음 — 클립은 GameSfxConfig(SfxId.WarpChargeLoop)에서, 재생은 이 로컬 3D 소스에서(통 위치).
    private AudioSource _chargeLoop;

    private void StartChargeLoop(Vector3 pos)
    {
        if (!GameSfx.TryGet(SfxId.WarpChargeLoop, out var clip, out var vol) || clip == null) return;
        if (_chargeLoop == null)
        {
            var go = new GameObject("[WarpChargeLoop]");
            go.transform.SetParent(transform, false);
            _chargeLoop = go.AddComponent<AudioSource>();
            _chargeLoop.loop = true;
            _chargeLoop.playOnAwake = false;
            _chargeLoop.spatialBlend = 1f;                       // 3D — 통에서 거리감
            _chargeLoop.minDistance = 3f;
            _chargeLoop.maxDistance = 20f;
            _chargeLoop.rolloffMode = AudioRolloffMode.Logarithmic;
        }
        _chargeLoop.transform.position = pos;
        _chargeLoop.clip = clip;
        _chargeLoop.volume = vol * GlobalSettingsManager.CurrentSFXVolume;
        _chargeLoop.Play();
    }

    private void StopChargeLoop()
    {
        if (_chargeLoop != null && _chargeLoop.isPlaying) _chargeLoop.Stop();
    }

    private void TeleportPlayer(Transform player, Vector3 dest)
    {
        Vector3 pos = SnapToGround(player, dest);
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = pos;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        player.position = pos;
    }

    // 도착점 위에서 아래로 레이캐스트해, 플레이어 콜라이더 바닥이 지면에 닿도록 y를 보정한다.
    //   (도착점이 원통 바닥이라 그대로 두면 캡슐이 바닥을 관통해 물리로 튕기거나 끼는 걸 방지)
    private Vector3 SnapToGround(Transform player, Vector3 dest)
    {
        Vector3 origin = dest + Vector3.up * snapRayUp;
        if (!Physics.Raycast(origin, Vector3.down, out var hit, snapRayLength, groundMask, QueryTriggerInteraction.Ignore))
            return dest;   // 지면 못 찾으면 원래 위치

        // 피벗이 발보다 얼마나 위에 있는지(현재 자세 기준) 만큼 올려, 콜라이더 바닥=지면이 되게.
        var col = player.GetComponentInChildren<Collider>();
        float pivotAboveFeet = col != null ? Mathf.Max(0f, player.position.y - col.bounds.min.y) : 0f;

        Vector3 pos = dest;
        pos.y = hit.point.y + pivotAboveFeet + arrivalSkin;
        return pos;
    }

    private void SpawnVfx(GameObject prefab, Vector3 pos, Color tint)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, pos, Quaternion.identity);
        ApplyTint(go, tint);
        Destroy(go, 5f);
    }

    // 충전 중 계속 재생되는 출발 VFX — 취소/완료 시 직접 파괴하므로 자동 파괴 없이 인스턴스를 반환.
    private GameObject SpawnVfxTracked(GameObject prefab, Vector3 pos, Color tint)
    {
        if (prefab == null) return null;
        var go = Instantiate(prefab, pos, Quaternion.identity);
        ApplyTint(go, tint);
        return go;
    }

    // VFX 인스턴스의 색상을 지역 색으로 '교체'한다(원본이 파란색이므로 곱연산은 부적합).
    //   각 색의 밝기(HDR 강도)는 유지하고 색조(hue)만 tint 로 바꾼다. 흰색 tint(설산)는 원본 유지.
    //   파티클 startColor(그라디언트 포함) + ParticleSystemRenderer 머티리얼의 모든 컬러 프로퍼티에 적용.
    private static void ApplyTint(GameObject root, Color tint)
    {
        if (root == null || tint == Color.white) return;

        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = RecolorStartColor(main.startColor, tint);
        }
        foreach (var r in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            var mats = r.materials;   // 인스턴스(오브젝트 파괴 시 함께 정리됨)
            foreach (var m in mats) RecolorMaterialColors(m, tint);
        }
    }

    // 밝기 유지 + 색조 교체. tint 는 내부에서 최대성분=1로 정규화해 원본 강도를 보존한다.
    private static Color Recolor(Color c, Color tint)
    {
        float tmax = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
        if (tmax <= 0f) return c;
        float intensity = Mathf.Max(c.r, Mathf.Max(c.g, c.b));   // HDR 글로우 강도
        float k = intensity / tmax;
        return new Color(tint.r * k, tint.g * k, tint.b * k, c.a);   // 알파 유지
    }

    private static ParticleSystem.MinMaxGradient RecolorStartColor(ParticleSystem.MinMaxGradient g, Color t)
    {
        switch (g.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(Recolor(g.color, t));
            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(Recolor(g.colorMin, t), Recolor(g.colorMax, t));
            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(RecolorGradient(g.gradient, t));
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(RecolorGradient(g.gradientMin, t), RecolorGradient(g.gradientMax, t));
            default:
                return g;
        }
    }

    private static Gradient RecolorGradient(Gradient src, Color t)
    {
        if (src == null) return null;
        var keys = src.colorKeys;
        for (int i = 0; i < keys.Length; i++) keys[i].color = Recolor(keys[i].color, t);
        var ng = new Gradient { mode = src.mode };
        ng.SetKeys(keys, src.alphaKeys);
        return ng;
    }

    private static void RecolorMaterialColors(Material m, Color t)
    {
        if (m == null || m.shader == null) return;
        int n = m.shader.GetPropertyCount();
        for (int i = 0; i < n; i++)
        {
            if (m.shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Color) continue;
            string name = m.shader.GetPropertyName(i);
            Color c = m.GetColor(name);
            if (Mathf.Max(c.r, Mathf.Max(c.g, c.b)) <= 0f) continue;   // 검정(발광 없음)은 그대로
            m.SetColor(name, Recolor(c, t));
        }
    }

    private IEnumerator Fade(float from, float to)
    {
        if (_fadeCg == null) yield break;
        _fadeCg.blocksRaycasts = true;
        yield return CoreUtilities.FadeUnscaled(_fadeCg, from, to, fadeDuration);
        _fadeCg.blocksRaycasts = _fadeCg.alpha > 0.99f;
    }

    // 전체 화면 검은 오버레이(런타임 생성, 씬 세팅 불필요).
    private void BuildFadeOverlay()
    {
        var go = new GameObject("[WarpFade]");
        go.transform.SetParent(transform, false);   // WarpManager 수명과 함께(씬 리로드 시 중복 방지)
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;   // 대부분의 UI 위(사망 오버레이보다 낮게 필요하면 조정)
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        var imgGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        imgGo.GetComponent<Image>().color = Color.black;
        _fadeCg = imgGo.AddComponent<CanvasGroup>();
        _fadeCg.alpha = 0f; _fadeCg.blocksRaycasts = false; _fadeCg.interactable = false;
    }

    private Transform ReturnPointFor(TransmissionRegion region) => region switch
    {
        TransmissionRegion.Snow => baseReturnSnow,
        TransmissionRegion.Desert => baseReturnDesert,
        TransmissionRegion.Lava => baseReturnLava,
        _ => null,
    };

    private static string RegionName(TransmissionRegion r) => r switch
    {
        TransmissionRegion.Snow => "설산",
        TransmissionRegion.Desert => "사막",
        TransmissionRegion.Lava => "용암",
        _ => "자연",
    };

    // ── 저장 (ISaveable) ──────────────────────────────────────────────
    public void Capture(GameSaveData data)
    {
        if (data != null) data.activatedWarpIds = new List<string>(_activated);
    }

    private void RestoreFromSave()
    {
        var mgr = SaveSlotManager.Instance;
        if (mgr == null || !mgr.HasActiveSlot) return;
        _activated.Clear();
        if (mgr.Data.activatedWarpIds != null)
            foreach (var id in mgr.Data.activatedWarpIds)
                if (!string.IsNullOrEmpty(id)) _activated.Add(id);
    }
}
