using UnityEngine;

// 엔드필드식 HUD 자동 페이드 - TimeKov 규칙(시간=체력):
//   - 결계 밖 = 매 순간 시간이 줄어 위험 -> HUD 항상 표시.
//   - 결계 안 = 안전(시간 안 줆) -> 평화로운 탐험 중 플레이어 HUD(체력/시간바)+스킬바를 스르르 숨겨
//     화면을 깔끔하게. 평타/스킬/피격/C 스탯창 열림 시 잠깐 떴다가 몇 초 뒤 다시 숨김.
//   (적은 결계 밖에만 있고 밖은 어차피 항상 표시라 적 어그로 감지는 불필요.)
//
// fadeRoots에 숨길 묶음(플레이어 HUD 루트, 스킬바 루트)을 드래그하면 CanvasGroup 자동 부착.
public class HudAutoFader : MonoBehaviour
{
    [Header("숨길 HUD 묶음 (CanvasGroup 자동 부착)")]
    [Tooltip("전투 외엔 페이드아웃할 묶음. 스킬바 루트 등. (체력/시간바는 아래 alwaysOnRoots로 옮겨 항상 표시)")]
    [SerializeField] private GameObject[] fadeRoots;

    [Header("항상 표시 묶음 (기지에서도 안 숨김)")]
    [Tooltip("결계 안에서도 페이드 없이 항상 보일 묶음. 체력/시간바 HUD 루트를 여기에 넣는다(무적 표시). CanvasGroup 자동 부착.")]
    [SerializeField] private GameObject[] alwaysOnRoots;

    [Header("튜닝")]
    [Tooltip("전투 신호 후 HUD를 유지하는 시간(초). 이 시간 지나면 다시 숨김.")]
    [SerializeField] private float showHoldSeconds = 3.5f;
    [Tooltip("페이드 속도(클수록 빠름)")]
    [SerializeField] private float fadeSpeed = 4f;
    [Tooltip("숨김 상태 알파(0=완전히 안 보임)")]
    [Range(0f, 1f)][SerializeField] private float hiddenAlpha = 0f;

    private CanvasGroup[] _groups;
    private CanvasGroup[] _alwaysOnGroups;
    private PlayerStatComponent _stat;
    private PlayerSkillComponent _skill;
    private float _showTimer;
    private float _alpha = 1f;
    private float _lastHp = float.NaN;     // 직전 프레임 체력(시간) - 변화 감지용
    private float _lastMaxHp = float.NaN;  // 직전 프레임 최대 체력 - 코어강화 등 변화 감지용

    private void Start()
    {
        // 루트에 CanvasGroup get-or-add
        _groups = EnsureGroups(fadeRoots);
        _alwaysOnGroups = EnsureGroups(alwaysOnRoots);

        var player = FindAnyObjectByType<Player>();
        if (player != null)
        {
            _stat  = player.Stat;
            _skill = player.Skill;
            if (_stat != null)
            {
                _stat.OnHurt += PulseShow;            // 피격 시 표시
                _stat.OnDamaged += OnDamagedPulse;    // 시간(HP) 감소 시 표시 (즉시완료 SpendHp 포함 - 결계 안에서도 체력바 뜨게)
                _lastHp    = _stat.CurrentHp;
                _lastMaxHp = _stat.MaxHp;
            }
        }

        _showTimer = showHoldSeconds;   // 시작은 보이게
    }

    private void OnDestroy()
    {
        if (_stat != null)
        {
            _stat.OnHurt -= PulseShow;
            _stat.OnDamaged -= OnDamagedPulse;
        }
    }

    private void PulseShow() => _showTimer = showHoldSeconds;
    private void OnDamagedPulse(float _) => PulseShow();   // OnDamaged(Action<float>) 래퍼

    private static CanvasGroup[] EnsureGroups(GameObject[] roots)
    {
        if (roots == null) return null;
        var groups = new CanvasGroup[roots.Length];
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null) continue;
            groups[i] = roots[i].GetComponent<CanvasGroup>();
            if (groups[i] == null) groups[i] = roots[i].AddComponent<CanvasGroup>();
        }
        return groups;
    }

    private void Update()
    {
        // 결계 안에서의 잠깐 표시용 타이머 (평타/스킬 시)
        if (_skill != null && _skill.IsExecuting) _showTimer = showHoldSeconds;

        // 시간바 값이 조금이라도 바뀌면 표시 — 힐/딜/소모/코어강화(최대치) 전부 한 곳에서 커버.
        // (결계 밖 드레인은 매 프레임 바뀌지만 그땐 outside로 이미 표시 중이라 무해)
        if (_stat != null && (_stat.CurrentHp != _lastHp || _stat.MaxHp != _lastMaxHp))
        {
            _showTimer = showHoldSeconds;
            _lastHp    = _stat.CurrentHp;
            _lastMaxHp = _stat.MaxHp;
        }

        if (_showTimer > 0f) _showTimer -= Time.deltaTime;

        bool outside  = _stat == null || !_stat.IsInBase;   // 결계 밖 = 항상 표시(시간 감소 위험)
        bool statOpen = GameUIController.Instance != null && GameUIController.Instance.IsPlayerStatOpen;
        // 코치마크(멈춰서 읽는 UI 설명)가 떠있을 때만 강제 표시 - 설명이 가리키는 스킬바 등이 사라지면 안 됨.
        // (튜토 전체가 아니라 설명 코치마크 표시 중에만. 일반 퀘로 넘어가면 정상 페이드)
        bool coachmark = TutorialOverlay.HasInstance && TutorialOverlay.I.IsActive;

        // 결계 밖 / 스탯창 / 코치마크 설명 중 / 결계 안에서 방금 평타,피격 -> 표시
        bool show = outside || statOpen || coachmark || _showTimer > 0f;

        float target = show ? 1f : hiddenAlpha;
        _alpha = Mathf.MoveTowards(_alpha, target, fadeSpeed * Time.deltaTime);

        // 페이드 묶음 (전투 외 숨김)
        if (_groups != null)
        {
            bool interactable = _alpha > 0.5f;
            foreach (var g in _groups)
            {
                if (g == null) continue;
                g.alpha = _alpha;
                g.blocksRaycasts = interactable;
                g.interactable   = interactable;
            }
        }

        // 항상 표시 묶음 (체력/시간바) - 결계 안에서도 안 숨김. 표시 전용이라 클릭 가로채지 않게 raycast 끔.
        if (_alwaysOnGroups != null)
        {
            foreach (var g in _alwaysOnGroups)
            {
                if (g == null) continue;
                g.alpha = 1f;
                g.blocksRaycasts = false;
                g.interactable   = false;
            }
        }
    }
}
