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
    [Tooltip("플레이어 HUD(체력/시간바) 루트, 우측하단 스킬바 루트 등. 전투 외엔 이들이 페이드아웃.")]
    [SerializeField] private GameObject[] fadeRoots;

    [Header("튜닝")]
    [Tooltip("전투 신호 후 HUD를 유지하는 시간(초). 이 시간 지나면 다시 숨김.")]
    [SerializeField] private float showHoldSeconds = 3.5f;
    [Tooltip("페이드 속도(클수록 빠름)")]
    [SerializeField] private float fadeSpeed = 4f;
    [Tooltip("숨김 상태 알파(0=완전히 안 보임)")]
    [Range(0f, 1f)][SerializeField] private float hiddenAlpha = 0f;

    private CanvasGroup[] _groups;
    private PlayerStatComponent _stat;
    private PlayerSkillComponent _skill;
    private float _showTimer;
    private float _alpha = 1f;

    private void Start()
    {
        // 루트에 CanvasGroup get-or-add
        if (fadeRoots != null)
        {
            _groups = new CanvasGroup[fadeRoots.Length];
            for (int i = 0; i < fadeRoots.Length; i++)
            {
                if (fadeRoots[i] == null) continue;
                _groups[i] = fadeRoots[i].GetComponent<CanvasGroup>();
                if (_groups[i] == null) _groups[i] = fadeRoots[i].AddComponent<CanvasGroup>();
            }
        }

        var player = FindAnyObjectByType<Player>();
        if (player != null)
        {
            _stat  = player.Stat;
            _skill = player.Skill;
            if (_stat != null) _stat.OnHurt += PulseShow;   // 피격 시 표시
        }

        _showTimer = showHoldSeconds;   // 시작은 보이게
    }

    private void OnDestroy()
    {
        if (_stat != null) _stat.OnHurt -= PulseShow;
    }

    private void PulseShow() => _showTimer = showHoldSeconds;

    private void Update()
    {
        // 결계 안에서의 잠깐 표시용 타이머 (평타/스킬 시)
        if (_skill != null && _skill.IsExecuting) _showTimer = showHoldSeconds;
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

        if (_groups == null) return;
        bool interactable = _alpha > 0.5f;
        foreach (var g in _groups)
        {
            if (g == null) continue;
            g.alpha = _alpha;
            g.blocksRaycasts = interactable;
            g.interactable   = interactable;
        }
    }
}
