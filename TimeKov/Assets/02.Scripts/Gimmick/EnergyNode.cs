using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ── 에너지 공급 노드 하나 (F로 연료/시간에너지 주입 → 활성화) ─────────────────────
// 잠긴 문·다리를 여는 '연료 주입구'. 플레이어가 F 를 누를 때마다 가방의 연료 아이템을
//   1개(depositPerPress)씩 소모해 이 노드를 채운다. requiredAmount 만큼 차면 활성화(IsActive).
//
//   • 연료 = 인벤토리 아이템(fuelItemId). 공장에서 만든 충전키트/시간에너지 등.
//   • 주입은 '일방향'(회수 없음) — 넣은 연료는 소모된다. 완료되면 계속 활성(영구 개방).
//   • 연료가 없으면 거부(빨강 점멸 + 토스트). 채워질수록 점점 밝게 발광.
//
//   WarpPoint/GimmickSwitch 와 같은 방식으로 F 알약 + 근접 발광/외곽선을 쓴다.
[RequireComponent(typeof(Collider))]
public class EnergyNode : MonoBehaviour, IInteractable, IInteractHint
{
    [Header("연동 대상 (이 노드만으로 문/다리 열 때)")]
    [Tooltip("가득 차면 바로 열 GimmickTarget 들(문/다리/상자잠금). 단일 노드 퍼즐이면 여기에 문을 직접 연결한다.\n" +
             "여러 노드를 '전부 채워야' 열리게 하려면 여기는 비우고 EnergyConduit 로 묶는다.")]
    [SerializeField] private List<GimmickTarget> targets = new();

    [Header("연료 (인벤토리 아이템)")]
    [Tooltip("이 노드에 넣을 연료 아이템 ID(공장 제작 충전키트/시간에너지 등). 반드시 지정.")]
    [SerializeField] private int fuelItemId;
    [Tooltip("활성화에 필요한 연료 총량.")]
    [Min(1)] [SerializeField] private int requiredAmount = 3;
    [Tooltip("F 한 번에 넣는 연료 개수. 보통 1(한 개씩 넣는 손맛). 크게 하면 한 번에 확 참.")]
    [Min(1)] [SerializeField] private int depositPerPress = 1;

    [Header("충전 게이지 (선택)")]
    [Tooltip("채워질수록 앞에서부터 켜질 오브젝트들(칸 아이콘 등). 개수는 requiredAmount 에 맞추길 권장.")]
    [SerializeField] private GameObject[] fillIndicators;

    [Header("상태 비주얼 (선택)")]
    [Tooltip("활성화(가득 참) 됐을 때만 켤 오브젝트.")]
    [SerializeField] private GameObject activeVisual;
    [Tooltip("비활성(덜 참) 상태에서만 켤 오브젝트.")]
    [SerializeField] private GameObject inactiveVisual;

    [Header("발광 (채워질수록 밝아짐)")]
    [Tooltip("발광에 쓸 Light 들. 비우면 자식 Light 를 자동으로 찾는다(세팅툴이 만든 NodeGlow 포함).")]
    [SerializeField] private Light[] glowLights;
    [Tooltip("추가로 발광시킬 렌더러들(Emission 켜진 머티리얼이어야 보임, 선택).")]
    [SerializeField] private Renderer[] glowEmissiveRenderers;
    [Tooltip("주입/활성화 때 색.")]
    [SerializeField] private Color activeColor = new Color(0.2f, 0.9f, 1f);   // 시안
    [Tooltip("연료 부족(주입 거부) 시 점멸 색.")]
    [SerializeField] private Color deniedColor = new Color(1f, 0.2f, 0.15f);   // 빨강
    [Tooltip("발광 밝기(최대).")]
    [SerializeField] private float glowIntensity = 3f;

    [Header("근접 강조")]
    [Tooltip("체크: 가까이 가면 메시에 외곽선(Outline)을 켠다. ★땅에 박은 오브젝트는 묻힌 부분까지 외곽선이 " +
             "그려져 이상하므로 '끄고' 대신 근접 발광으로 강조한다.")]
    [SerializeField] private bool showOutline = true;

    [Header("사운드")]
    [Tooltip("연료 한 번 넣을 때 재생. None 이면 기본 스위치음으로 폴백.")]
    [SerializeField] private SfxId depositSfx = SfxId.None;
    [Tooltip("가득 차 활성화될 때 재생(선택). None 이면 무음.")]
    [SerializeField] private SfxId completeSfx = SfxId.None;
    [Tooltip("연료가 없어 거부될 때 재생(선택). None 이면 무음.")]
    [SerializeField] private SfxId deniedSfx = SfxId.None;

    [Header("근접 힌트 (F)")]
    [Tooltip("가까이 가면 켤 F 알약 UI. 비우면 씬 공용 패널을 자동으로 찾아 쓴다.")]
    [SerializeField] private GameObject hintUI;
    [Tooltip("알약에 표시할 이름(진행도 n/N 이 자동으로 뒤에 붙는다).")]
    [SerializeField] private string hintLabel = "연료 주입";
    [Tooltip("알약 왼쪽 아이콘. 비우면 패널 기본 아이콘 사용.")]
    [SerializeField] private Sprite hintIcon;
    [Tooltip("가까이 가면 외곽선을 켤 대상들. 비우면 이 오브젝트를 쓴다(showOutline 켜졌을 때만).")]
    [SerializeField] private Transform[] outlineTargets;

    // 현재 채워진 연료량 / 활성화 여부. EnergyConduit 가 IsActive 를 구독해 조건을 판정한다.
    public int Filled => _filled;
    public int Required => requiredAmount;
    public bool IsActive { get; private set; }
    public event Action<EnergyNode> OnChanged;

    private const float IdleGlow = 0.45f;   // 근접 시 은은한 발광 레벨(외곽선 대체). 너무 약하면 안 보여서 올림

    private int _filled;
    private bool _showing;       // 현재 힌트 표시 중(주입 후 라벨 갱신용)
    private float _glowLevel;    // 유지 발광 레벨(0~1). Flash 는 잠깐 덮었다 이 값으로 복귀.

    private InteractHighlight _highlight;
    private Coroutine _flash;
    private MaterialPropertyBlock _mpb;
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private void Start()
    {
        // PlayerInteractComponent 의 OverlapSphere 는 Interactable 레이어만 훑는다.
        int il = LayerMask.NameToLayer("Interactable");
        if (il >= 0) gameObject.layer = il;

        if (hintUI == null) hintUI = InteractHintPanel.FindSharedPanel();
        // 땅에 박은 오브젝트는 통짜 메시 외곽선이 이상하므로 showOutline 을 끄고 근접 발광으로 대체한다.
        if (showOutline) _highlight = new InteractHighlight(outlineTargets, transform);
        InteractHintPanel.Prime(hintUI, this);

        if (glowLights == null || glowLights.Length == 0)
            glowLights = GetComponentsInChildren<Light>(true);
        _mpb = new MaterialPropertyBlock();

        ApplyVisual();
        ApplyGlow(0f, activeColor);
    }

    // 활성화(가득 참)되면 F 후보에서 빠진다(더 넣을 필요 없음).
    public bool CanInteract => !IsActive;

    public void Interact(Player player)
    {
        if (IsActive) return;

        var inv = InventoryManager.Instance;
        int need = Mathf.Min(depositPerPress, requiredAmount - _filled);

        // 가방에 연료가 있는 만큼만 넣는다(need 보다 적으면 있는 만큼).
        int have = inv != null ? inv.GetTotalItemCount(fuelItemId) : 0;
        int take = Mathf.Min(need, have);

        if (take <= 0)
        {
            DeniedFlash();
            GameSfx.Play(deniedSfx, transform.position);
            // 연료가 없어 아무 반응이 없으면 버그처럼 보이므로 토스트로 안내.
            var data = ItemDatabase.GetItem(fuelItemId);
            string fuelName = data != null ? data.itemName : "연료";
            ToastManager.Warning($"{fuelName}이(가) 없습니다");
            return;
        }

        inv.TryConsumeItem(fuelItemId, take);
        _filled += take;
        UpdateIndicators();

        if (_filled >= requiredAmount)
        {
            _filled = requiredAmount;
            IsActive = true;
            GameSfx.Play(completeSfx == SfxId.None ? SfxId.GimmickSwitchOn : completeSfx, transform.position);
            ApplyVisual();
            SetGlowLevel(1f);                 // 완료 = 최대 밝기 유지
            Flash(activeColor, 0.2f);         // 완료 펄스
            _highlight?.Set(false);
            InteractHintPanel.Show(hintUI, false, hintLabel, hintIcon);   // 완료 → 힌트 닫음

            // 이 노드에 직접 연결된 타깃을 연다(단일 노드 퍼즐).
            foreach (var t in targets)
                if (t != null) t.SetOpen(true);

            OnChanged?.Invoke(this);   // 여러 노드를 묶는 EnergyConduit 에도 완료 통지
        }
        else
        {
            GameSfx.Play(depositSfx == SfxId.None ? SfxId.GimmickSwitchOn : depositSfx, transform.position);
            // 채운 만큼 발광 레벨 상승(첫 주입도 눈에 띄게 최소 0.3) + 한 번 펄스.
            float p = _filled / (float)requiredAmount;
            SetGlowLevel(Mathf.Lerp(0.3f, 0.85f, p));
            Flash(activeColor, 0.15f);
            RefreshHint();   // 진행도(n/N) 갱신
        }
    }

    // 연료가 없어 주입이 거부됐을 때 — 빨강 한 번 점멸.
    private void DeniedFlash() => Flash(deniedColor, 0.25f);

    // ── 발광 ────────────────────────────────────────────────────────────
    // SetGlowLevel: 유지 레벨(0~1) 설정. Flash: 잠깐 밝게 덮었다가 유지 레벨로 복귀.
    private void SetGlowLevel(float level)
    {
        _glowLevel = Mathf.Clamp01(level);
        if (_flash == null) ApplyGlow(_glowLevel, activeColor);
    }

    private void Flash(Color color, float seconds)
    {
        if (_flash != null) StopCoroutine(_flash);
        _flash = StartCoroutine(FlashRoutine(color, seconds));
    }

    private IEnumerator FlashRoutine(Color color, float seconds)
    {
        ApplyGlow(1.2f, color);   // 잠깐 최대보다 살짝 밝게
        yield return new WaitForSeconds(seconds);
        _flash = null;
        ApplyGlow(_glowLevel, activeColor);   // 유지 레벨로 복귀
    }

    private void ApplyGlow(float level, Color color)
    {
        bool on = level > 0.001f;
        if (glowLights != null)
            foreach (var l in glowLights)
            {
                if (l == null) continue;
                l.enabled = on;
                if (on) { l.color = color; l.intensity = glowIntensity * level; }
            }

        if (glowEmissiveRenderers != null && glowEmissiveRenderers.Length > 0)
        {
            _mpb ??= new MaterialPropertyBlock();
            Color emis = on ? color * (glowIntensity * level) : Color.black;
            foreach (var r in glowEmissiveRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionId, emis);
                r.SetPropertyBlock(_mpb);
            }
        }
    }

    private void ApplyVisual()
    {
        if (activeVisual   != null) activeVisual.SetActive(IsActive);
        if (inactiveVisual != null) inactiveVisual.SetActive(!IsActive);
    }

    private void UpdateIndicators()
    {
        if (fillIndicators == null || fillIndicators.Length == 0) return;
        for (int i = 0; i < fillIndicators.Length; i++)
        {
            if (fillIndicators[i] == null) continue;
            int threshold = Mathf.CeilToInt((i + 1) / (float)fillIndicators.Length * requiredAmount);
            fillIndicators[i].SetActive(_filled >= threshold);
        }
    }

    public void ShowHint(bool show)
    {
        _showing = show && !IsActive;
        if (IsActive) { _highlight?.Set(false); return; }

        InteractHintPanel.Show(hintUI, show, ProgressLabel(), hintIcon);
        _highlight?.Set(show);

        // 외곽선을 안 쓰는(땅에 박은) 노드는 근접 시 은은한 발광으로 강조. 이미 연료가 들어가 있으면 그대로.
        if (!showOutline && _filled == 0)
            SetGlowLevel(show ? IdleGlow : 0f);
    }

    private void RefreshHint()
    {
        if (_showing && !IsActive)
            InteractHintPanel.Show(hintUI, true, ProgressLabel(), hintIcon);
    }

    private string ProgressLabel() => $"{hintLabel} ({_filled}/{requiredAmount})";
}
