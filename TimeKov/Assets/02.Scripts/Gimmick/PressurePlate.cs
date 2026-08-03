using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ── 압력판 입력 하나 (밟으면 눌림) ──────────────────────────────────────────────
// 위에 '무게'가 올라가 있는 동안 눌린다. 스위치(GimmickSwitch)의 압력판 버전:
//   자신은 문을 열지 않고 눌림(IsPressed) 상태만 트리거(SequenceTrigger 등)에 알린다.
//
//   무게 = 플레이어(항상) + 물리 프롭(발로 찬 rigidbody) + weightMask 오브젝트(선택).
//   requiredWeight 개 이상 올라가면 '눌림'. 보통 1(밟기만 하면 됨).
//
//   조건 판정은 트리거가 담당: SequenceTrigger(순서대로 밟기) 등이 여러 판을 모아 타깃을 연다.
//
//   ★ 콜라이더는 isTrigger 여야 하고, 플레이어/상자가 실제로 밟을 수 있게 판 표면 위로
//     얇게 덮는 트리거 박스를 권장(너무 얇으면 빠른 이동 시 놓칠 수 있으니 적당히 높게).
[RequireComponent(typeof(Collider))]
public class PressurePlate : MonoBehaviour
{
    [Header("무게 판정")]
    [Tooltip("플레이어가 올라가면 눌림으로 친다(대부분 체크).")]
    [SerializeField] private bool acceptPlayer = true;
    [Tooltip("체크: '발로 찰 수 있는 물리 프롭'(non-kinematic Rigidbody 오브젝트)을 올려도 눌린다.\n" +
             "적은 전부 kinematic 이라 걸러진다. 특정 프롭만 인정하고 싶으면 이걸 끄고 아래 Weight Mask 를 쓴다.")]
    [SerializeField] private bool acceptPhysicsProps = true;
    [Tooltip("추가로 무게로 인정할 오브젝트 레이어(특정 상자만 등). 위 옵션으로 충분하면 비워둔다.")]
    [SerializeField] private LayerMask weightMask;
    [Tooltip("눌리는 데 필요한 무게(올라간 물체) 개수. 보통 1.")]
    [Min(1)] [SerializeField] private int requiredWeight = 1;

    [Header("눌림 비주얼 (선택)")]
    [Tooltip("눌리면 아래로 내려갈 판 부분. 비우면 이 오브젝트를 쓴다. 없으면(None) 안 움직임.")]
    [SerializeField] private Transform pressVisual;
    [Tooltip("눌렸을 때 pressVisual 이 로컬로 내려갈 거리(m). 0 이면 안 내려감.")]
    [SerializeField] private float pressDepth = 0.06f;
    [Tooltip("눌림/복귀 애니메이션 속도(클수록 빠름).")]
    [SerializeField] private float pressLerp = 12f;
    [Tooltip("눌렸을 때만 켤 오브젝트(발광 등, 선택).")]
    [SerializeField] private GameObject onVisual;
    [Tooltip("안 눌렸을 때만 켤 오브젝트(선택).")]
    [SerializeField] private GameObject offVisual;

    [Header("사운드")]
    [Tooltip("밟을(눌릴) 때 재생. None 이면 기본 스위치음으로 폴백.")]
    [SerializeField] private SfxId pressOnSfx = SfxId.None;
    [Tooltip("내려올(풀릴) 때 재생. None 이면 기본 스위치음으로 폴백.")]
    [SerializeField] private SfxId pressOffSfx = SfxId.None;

    [Header("빛 연출 (순서 퍼즐 색 피드백)")]
    [Tooltip("색을 낼 Light 들. 비우면 자식 Light 를 자동으로 찾는다(세팅툴이 만든 PlateGlow 포함).")]
    [SerializeField] private Light[] glowLights;
    [Tooltip("추가로 색(발광)을 낼 렌더러들. Emission 이 켜진 머티리얼이어야 보인다(선택).")]
    [SerializeField] private Renderer[] glowEmissiveRenderers;
    [Tooltip("글로우 밝기.")]
    [SerializeField] private float glowIntensity = 3f;

    // 현재 눌림 여부. 트리거(SequenceTrigger 등)가 구독해서 조건을 판정한다.
    public bool IsPressed { get; private set; }
    // 순서 퍼즐에서 '이미 맞게 밟아 고정된' 판인지(재밟기 무시 판정에 사용).
    public bool IsHeldDown => _heldDown;
    public event Action<PressurePlate> OnChanged;

    private readonly HashSet<Collider> _occupants = new();
    private Vector3 _visRestLocal;   // pressVisual 원위치(로컬)
    private Transform _vis;

    private bool _heldDown;   // true 면 발을 떼도 눌린 채 유지(순서 퍼즐에서 맞게 밟은 판)

    // 글로우 유지 상태(색 피드백). Flash 는 잠깐 덮었다가 이 상태로 복귀한다.
    private bool _glowSteadyOn;
    private Color _glowSteadyColor = Color.white;
    private Coroutine _flash;
    private MaterialPropertyBlock _mpb;
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private void Start()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[PressurePlate] '{name}' 콜라이더가 isTrigger 가 아니다. 밟기 감지가 안 될 수 있다.", this);

        _vis = pressVisual != null ? pressVisual : transform;
        _visRestLocal = _vis.localPosition;

        if ((glowLights == null || glowLights.Length == 0) &&
            (glowEmissiveRenderers == null || glowEmissiveRenderers.Length == 0))
            glowLights = GetComponentsInChildren<Light>(true);
        _mpb = new MaterialPropertyBlock();
        ApplyGlow(false, Color.black);   // 시작은 소등

        ApplyVisual();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Counts(other)) return;
        if (_occupants.Add(other)) Recount();
    }

    private void OnTriggerExit(Collider other)
    {
        if (_occupants.Remove(other)) Recount();
    }

    // 무게로 인정되는가: 플레이어(옵션) / 물리 프롭(옵션) / weightMask 레이어.
    private bool Counts(Collider other)
    {
        if (acceptPlayer && other.GetComponentInParent<Player>() != null) return true;

        // 발로 차는 물리 프롭 = non-kinematic Rigidbody. (적은 전부 kinematic 이라 자동 제외)
        if (acceptPhysicsProps)
        {
            var rb = other.attachedRigidbody;
            if (rb != null && !rb.isKinematic) return true;
        }

        return (weightMask.value & (1 << other.gameObject.layer)) != 0;
    }

    private void Update()
    {
        // 판 위 물체가 파괴/비활성되면 OnTriggerExit 를 못 받을 수 있어 방어적으로 정리.
        if (_occupants.Count > 0) PruneDead();

        // 눌림 상태에 맞춰 판을 부드럽게 오르내림. (_heldDown 이면 발을 떼도 계속 내려간 채)
        if (pressDepth != 0f && _vis != null)
        {
            bool down = IsPressed || _heldDown;
            Vector3 target = down ? _visRestLocal + Vector3.down * pressDepth : _visRestLocal;
            _vis.localPosition = Vector3.Lerp(_vis.localPosition, target, Time.deltaTime * pressLerp);
        }
    }

    private void PruneDead()
    {
        bool changed = false;
        _occupants.RemoveWhere(c =>
        {
            bool dead = c == null || !c.gameObject.activeInHierarchy;
            if (dead) changed = true;
            return dead;
        });
        if (changed) Recount();
    }

    private void Recount()
    {
        bool pressed = _occupants.Count >= Mathf.Max(1, requiredWeight);
        if (pressed == IsPressed) return;
        IsPressed = pressed;

        SfxId sfx = pressed
            ? (pressOnSfx  == SfxId.None ? SfxId.GimmickSwitchOn  : pressOnSfx)
            : (pressOffSfx == SfxId.None ? SfxId.GimmickSwitchOff : pressOffSfx);
        GameSfx.Play(sfx, transform.position);

        ApplyVisual();
        OnChanged?.Invoke(this);
    }

    private void ApplyVisual()
    {
        bool down = IsPressed || _heldDown;
        if (onVisual  != null) onVisual.SetActive(down);
        if (offVisual != null) offVisual.SetActive(!down);
    }

    /// <summary>발을 떼도 눌린 채 유지할지(순서 맞음=유지, 틀림/리셋=해제). SequenceTrigger 가 호출.</summary>
    public void SetHeldDown(bool held)
    {
        if (_heldDown == held) return;
        _heldDown = held;
        ApplyVisual();
    }

    // ── 글로우(색) API — SequenceTrigger 가 순서 정오에 맞춰 호출 ────────────────
    // SetGlow: 계속 그 색으로 켜둠(맞게 밟은 판=파랑 고정). ClearGlow: 소등.
    // Flash: 잠깐 그 색으로 점등했다가 원래 유지색으로 복귀(틀림=전체 빨강 한 번).

    /// <summary>이 판을 해당 색으로 계속 켜 둔다(진행 표시).</summary>
    public void SetGlow(Color color)
    {
        _glowSteadyOn = true; _glowSteadyColor = color;
        StopFlash();
        ApplyGlow(true, color);
    }

    /// <summary>글로우 소등(진행 초기화).</summary>
    public void ClearGlow()
    {
        _glowSteadyOn = false;
        StopFlash();
        ApplyGlow(false, Color.black);
    }

    /// <summary>잠깐 점등했다가 원래 유지 상태로 복귀(오답 점멸 등).</summary>
    public void Flash(Color color, float seconds)
    {
        StopFlash();
        _flash = StartCoroutine(FlashRoutine(color, seconds));
    }

    private IEnumerator FlashRoutine(Color color, float seconds)
    {
        ApplyGlow(true, color);
        yield return new WaitForSeconds(seconds);
        ApplyGlow(_glowSteadyOn, _glowSteadyColor);   // 유지 상태로 복귀
        _flash = null;
    }

    private void StopFlash()
    {
        if (_flash != null) { StopCoroutine(_flash); _flash = null; }
    }

    private void ApplyGlow(bool on, Color color)
    {
        if (glowLights != null)
            foreach (var l in glowLights)
            {
                if (l == null) continue;
                l.enabled = on;
                if (on) { l.color = color; l.intensity = glowIntensity; }
            }

        if (glowEmissiveRenderers != null && glowEmissiveRenderers.Length > 0)
        {
            _mpb ??= new MaterialPropertyBlock();
            Color emis = on ? color * glowIntensity : Color.black;
            foreach (var r in glowEmissiveRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionId, emis);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
