using System.Collections;
using UnityEngine;

// ── 공용 개폐 대상: 결계 / 봉인 / 문 / 다리 전부 이거 하나로 ────────────────────
// GimmickTarget 구현체. "열림(통행 가능)/닫힘(막힘)"을 오브젝트 켜고 끄기로 표현한다.
//   • blockObjects   : 열리면 사라지는 것들 = 레이저 결계 메시/파티클, 원형 봉인, 닫힌 문.
//                      (그 안에 Collider 가 있으면 통행 차단도 같이 해제된다 — 오브젝트째 On/Off)
//   • passObjects    : 열리면 나타나는 것들 = 다리, 발판 등(막힘 상태에선 없음).
//   보통 blockObjects 만 쓴다. 다리류만 passObjects 를 쓴다. 둘 다 써도 됨(문 열리며 다리 생성 등).
//
//   사라지는 방식 셋 중 하나로 동작한다(위가 우선):
//     fadeOut=true    → 서서히 옅어지다 사라짐("스르륵"). 재질 색·발광을 낮춘다.
//     flickerOut=true → 깜빡깜빡 하다 꺼짐(전력 내려가는 연출). 재질 무관.
//     둘 다 해제      → 즉시 사라짐.
//   ★이 컴포넌트는 blockObjects 와 '별개' 오브젝트(빈 부모)에 두는 걸 권장 — 연출 코루틴 호스트가
//     꺼지면 안 되기 때문(자기 자신을 blockObjects 에 넣지 말 것).
public class GimmickBarrier : GimmickTarget
{
    [Header("열리면 사라짐 (결계/봉인/닫힌 문 — 통행 차단 오브젝트)")]
    [Tooltip("열림 시 비활성화. 이 안의 Collider 가 통행을 막고 있었다면 함께 해제된다.")]
    [SerializeField] private GameObject[] blockObjects;

    [Header("열리면 나타남 (다리/발판 등 — 선택)")]
    [Tooltip("열림 시 활성화. 다리 생성처럼 '열려야 생기는' 것에 쓴다. 없으면 비워둔다.")]
    [SerializeField] private GameObject[] passObjects;

    [Header("스르륵 사라짐 (서서히 옅어지는 연출)")]
    [Tooltip("체크: 열릴 때 blockObjects 가 서서히 옅어지다 사라진다.\n" +
             "★깜빡임보다 우선한다. 둘 다 꺼져 있으면 즉시 사라진다.\n" +
             "재질의 대표 색 알파와 발광색(_EmissionColor)을 함께 낮춘다 — 셰이더 종류를 가리지 않는다.")]
    [SerializeField] private bool fadeOut = false;
    [Tooltip("옅어지는 시간(초).")]
    [SerializeField] private float fadeDuration = 1.1f;

    [Header("깜빡이며 사라짐 (레이저 전력 다운 연출)")]
    [Tooltip("체크: 열릴 때 blockObjects 가 깜빡깜빡 하다 꺼진다. 해제: 즉시 꺼짐.")]
    [SerializeField] private bool flickerOut = true;
    [Tooltip("깜빡임 총 지속 시간(초).")]
    [SerializeField] private float flickerDuration = 0.7f;
    [Tooltip("한 번 깜빡이는 간격(초). 작을수록 빠르게 점멸. 실제론 약간 랜덤하게 흔들린다.")]
    [SerializeField] private float flickerInterval = 0.08f;

    [Header("소멸 사운드")]
    [Tooltip("결계가 열릴(사라질) 때 재생. 레이저 결계는 기본 전자식 전환음(LaserBarrierDown).\n" +
             "문/다리 등 다른 용도로 쓸 땐 None 으로 바꾸거나 다른 사운드로 교체.")]
    [SerializeField] private SfxId powerDownSfx = SfxId.LaserBarrierDown;

    private Coroutine _flicker;

    protected override void ApplyState(bool open, bool instant)
    {
        if (!open)
        {
            // 닫힘(막힘)으로 복귀: 즉시.
            StopFlicker();
            RestoreColors();          // 페이드로 낮춰 둔 색을 원래대로
            SetActiveAll(blockObjects, true);
            SetActiveAll(passObjects, false);
            return;
        }

        // 열림: 통과용(다리)은 즉시 켜고, 막던 것은 깜빡이며(또는 즉시) 끈다.
        SetActiveAll(passObjects, true);

        // 실제 개방(연출)일 때만 소멸음. 시작 시 초기화(instant)에선 무음.
        //   powerDownSfx 가 None(기존 프리팹이 신규필드 기본값을 못 받은 경우)이면 기본 레이저음으로 폴백.
        //   ★문/다리 등 다른 용도로 쓰며 무음을 원하면 이 폴백 대신 별도 사운드를 지정할 것.
        if (!instant)
        {
            SfxId sfx = powerDownSfx == SfxId.None ? SfxId.LaserBarrierDown : powerDownSfx;
            GameSfx.Play(sfx, transform.position);   // 레이저 위치에서 3D 재생
        }

        if (instant || (!flickerOut && !fadeOut))
        {
            SetActiveAll(blockObjects, false);
        }
        else
        {
            StopFlicker();
            _flicker = StartCoroutine(fadeOut ? FadeThenOff() : FlickerThenOff());
        }
    }

    // ── 스르륵 사라짐 ────────────────────────────────────────────────
    // 재질의 '대표 색'(Material.color — 셰이더가 지정한 주 색 프로퍼티로 자동 연결된다) 알파와
    // 발광색을 함께 낮춘다. URP Lit/Unlit·파티클·레거시를 따로 분기하지 않아도 되는 이유다.
    //
    //   ★발광까지 낮춰야 한다 — 레이저 결계 재질(Light_Yellow)은 밝기가 거의 전부
    //     _EmissionColor(4.5, 2.0, 0.3 — HDR)에서 온다. 알파만 내리면 그대로 환하게 남는다.
    //   ★불투명 재질은 페이드 동안만 반투명으로 바꿔 준다 — 같은 재질이 _Surface:0(Opaque),
    //     _ZWrite:1, SrcBlend:One 이라 손대지 않으면 알파를 0 까지 내려도 그림이 1픽셀도 안 변한다.
    //     ApplyState 가 만든 '재질 인스턴스'만 고치므로 프로젝트의 .mat 원본은 그대로다.
    private struct FadeItem
    {
        public Material mat;
        public bool hasColor, hasEmission, madeTransparent;
        public Color color, emission;
        public float srcBlend, dstBlend, zWrite, surface;
        public int queue;
    }
    private FadeItem[] _fadeItems;

    private IEnumerator FadeThenOff()
    {
        CacheFadeItems();
        SetTransparent(true);

        float t = 0f;
        float dur = Mathf.Max(0.05f, fadeDuration);
        while (t < dur)
        {
            t += Time.deltaTime;
            // 끝에서 급히 꺼지지 않게 살짝 늦춘다(EaseOut) — 이게 '스르륵'으로 읽히는 부분.
            float k = 1f - Mathf.Clamp01(t / dur);
            ApplyFade(k * k);
            yield return null;
        }

        SetActiveAll(blockObjects, false);
        RestoreColors();      // 꺼진 뒤 원래 상태로 되돌려 둔다(다시 닫힐 때를 위해)
        _flicker = null;
    }

    private void CacheFadeItems()
    {
        if (_fadeItems != null) return;

        var list = new System.Collections.Generic.List<FadeItem>();
        if (blockObjects != null)
            foreach (var go in blockObjects)
            {
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.materials)      // 재질 인스턴스가 생긴다(1회)
                    {
                        if (m == null) continue;
                        var item = new FadeItem { mat = m };
                        item.hasColor = m.HasProperty("_BaseColor") || m.HasProperty("_Color");
                        if (item.hasColor) item.color = m.color;
                        item.hasEmission = m.HasProperty("_EmissionColor");
                        if (item.hasEmission) item.emission = m.GetColor("_EmissionColor");
                        if (!item.hasColor && !item.hasEmission) continue;

                        // 불투명(_Surface:0)일 때만 반투명 전환 대상. 파티클·가산합성 재질은
                        // 이미 알파가 먹으니 건드리지 않는다.
                        if (item.hasColor && m.HasProperty("_Surface") && m.GetFloat("_Surface") < 0.5f)
                        {
                            item.madeTransparent = true;
                            item.surface = m.GetFloat("_Surface");
                            item.srcBlend = m.HasProperty("_SrcBlend") ? m.GetFloat("_SrcBlend") : 1f;
                            item.dstBlend = m.HasProperty("_DstBlend") ? m.GetFloat("_DstBlend") : 0f;
                            item.zWrite = m.HasProperty("_ZWrite") ? m.GetFloat("_ZWrite") : 1f;
                            item.queue = m.renderQueue;
                        }
                        list.Add(item);
                    }
            }

        _fadeItems = list.ToArray();
        if (_fadeItems.Length == 0)
            Debug.LogWarning($"[{name}] 옅어지게 할 색 프로퍼티를 찾지 못했습니다. " +
                             "blockObjects 에 Renderer 가 있는지, 그 재질이 _BaseColor/_Color/_EmissionColor 를 " +
                             "가지는지 확인하세요(그동안은 시간이 지나면 그냥 꺼집니다).", this);
    }

    /// <summary>불투명 재질 인스턴스를 알파 블렌딩으로 전환/복구한다.
    /// URP 는 프로퍼티 값만 바꿔선 안 되고 키워드와 렌더 큐까지 같이 맞춰야 실제로 섞인다.</summary>
    private void SetTransparent(bool on)
    {
        if (_fadeItems == null) return;
        foreach (var it in _fadeItems)
        {
            if (!it.madeTransparent || it.mat == null) continue;
            var m = it.mat;
            if (on)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                m.SetFloat("_Surface", it.surface);
                m.SetFloat("_SrcBlend", it.srcBlend);
                m.SetFloat("_DstBlend", it.dstBlend);
                m.SetFloat("_ZWrite", it.zWrite);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = it.queue;
            }
        }
    }

    private void ApplyFade(float k)
    {
        if (_fadeItems == null) return;
        foreach (var it in _fadeItems)
        {
            if (it.mat == null) continue;
            if (it.hasColor) it.mat.color = new Color(it.color.r, it.color.g, it.color.b, it.color.a * k);
            if (it.hasEmission) it.mat.SetColor("_EmissionColor", it.emission * k);
        }
    }

    private void RestoreColors()
    {
        SetTransparent(false);
        ApplyFade(1f);
    }

    private IEnumerator FlickerThenOff()
    {
        float t = 0f;
        bool on = true;
        while (t < flickerDuration)
        {
            on = !on;
            SetActiveAll(blockObjects, on);
            // 간격을 약간 랜덤하게 → 기계적이지 않은 '전력 불안정' 느낌
            float step = flickerInterval * Random.Range(0.6f, 1.4f);
            yield return new WaitForSeconds(step);
            t += step;
        }
        SetActiveAll(blockObjects, false);   // 최종 소멸
        _flicker = null;
    }

    private void StopFlicker()
    {
        if (_flicker != null) { StopCoroutine(_flicker); _flicker = null; }
    }

    private static void SetActiveAll(GameObject[] arr, bool active)
    {
        if (arr == null) return;
        foreach (var go in arr)
            if (go != null) go.SetActive(active);
    }
}
