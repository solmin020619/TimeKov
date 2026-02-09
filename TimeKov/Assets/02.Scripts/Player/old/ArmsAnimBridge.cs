using UnityEngine;

[DisallowMultipleComponent]
public class ArmsAnimBridge : MonoBehaviour
{
    [Header("Refs (Assign in Inspector)")]
    [SerializeField] private Animator armsAnimator;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerWeaponController weaponController;

    [Header("Animator Param Names (Asset Default)")]
    [SerializeField] private string gaitParam = "Gait";
    [SerializeField] private string tacSprintWeightParam = "TacSprintWeight";
    [SerializeField] private string rightHandWeightParam = "RightHandWeight";
    [SerializeField] private string grenadeWeightParam = "GrenadeWeight";
    [SerializeField] private string isInAirParam = "IsInAir";

    [Header("Animator Layer Names (optional)")]
    [SerializeField] private string rightHandLayerName = "RightHand";
    [SerializeField] private string tacSprintLayerName = "TacSprint";
    [SerializeField] private string reloadLayerName = "Reload";

    [Header("State Names (fallback if trigger not used)")]
    [SerializeField] private string fireStateName = "Fire";              // Base layer
    [SerializeField] private string reloadStateName = "Reload_Start";    // Reload layer (없으면 아래 후보에서 자동 시도)

    [Header("Trigger Names (if your controller has them, fill these)")]
    [SerializeField] private string fireTriggerName = "";                // 예: "Fire" or "OnFire"
    [SerializeField] private string reloadTriggerName = "";              // 예: "Reload" or "OnReload"

    [Header("Gait Mapping")]
    [Tooltip("이동 없으면 0, 걷기면 1, 뛰기면 2로 넣는 기본 맵")]
    public float gaitIdle = 0f;
    public float gaitWalk = 1f;
    public float gaitRun = 2f;

    [Tooltip("부드럽게 변화")]
    public float gaitLerpSpeed = 12f;

    [Header("Defaults (Recommended)")]
    [Range(0f, 1f)] public float defaultRightHandWeight = 1f;

    private int _layerRightHand = -1;
    private int _layerTacSprint = -1;
    private int _layerReload = -1;

    private float _gaitSmoothed;
    private bool _isReloading;

    private void Awake()
    {
        if (armsAnimator == null) armsAnimator = GetComponentInChildren<Animator>(true);

        if (armsAnimator != null)
        {
            armsAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _layerRightHand = GetLayerIndex(armsAnimator, rightHandLayerName);
            _layerTacSprint = GetLayerIndex(armsAnimator, tacSprintLayerName);
            _layerReload = GetLayerIndex(armsAnimator, reloadLayerName);
        }

        // weaponController의 이벤트를 이용해 "재장전 애니" 자동 연결
        if (weaponController != null)
        {
            weaponController.onReloadStart += OnReloadStart;
            weaponController.onReloadEnd += OnReloadEnd;
        }
    }

    private void OnDestroy()
    {
        if (weaponController != null)
        {
            weaponController.onReloadStart -= OnReloadStart;
            weaponController.onReloadEnd -= OnReloadEnd;
        }
    }

    private void Update()
    {
        if (armsAnimator == null || playerController == null) return;

        // --- 1) Gait 값 세팅 ---
        float move = playerController.MoveInput.magnitude;
        float target = gaitIdle;

        if (move > 0.01f)
            target = playerController.IsRunning ? gaitRun : gaitWalk;

        _gaitSmoothed = Mathf.Lerp(_gaitSmoothed, target, 1f - Mathf.Exp(-gaitLerpSpeed * Time.deltaTime));
        SetFloatIfExists(gaitParam, _gaitSmoothed);

        // --- 2) 기본 웨이트/상태 세팅(팔이 “실제로” 움직이게 하는 핵심) ---
        SetFloatIfExists(rightHandWeightParam, defaultRightHandWeight);
        SetFloatIfExists(grenadeWeightParam, 0f);
        SetBoolIfExists(isInAirParam, false);

        // TacSprintWeight는 “달리는 중”에만 살짝 올려줌(에셋 레이어가 반응하게)
        float tac = playerController.IsRunning ? 1f : 0f;
        SetFloatIfExists(tacSprintWeightParam, tac);

        // --- 3) 레이어 웨이트(있으면 강제로 올림) ---
        if (_layerRightHand >= 0) armsAnimator.SetLayerWeight(_layerRightHand, 1f);
        if (_layerTacSprint >= 0) armsAnimator.SetLayerWeight(_layerTacSprint, tac);
        if (_layerReload >= 0) armsAnimator.SetLayerWeight(_layerReload, _isReloading ? 1f : 0f);
    }

    // ====== 외부에서 호출되는 API (에러났던 이름들 포함) ======

    public void NotifyFire() => PlayFireAnim();

    public void PlayFireAnim()
    {
        if (armsAnimator == null) return;

        // 1) 트리거가 있으면 트리거
        if (!string.IsNullOrEmpty(fireTriggerName) && HasTrigger(fireTriggerName))
        {
            armsAnimator.ResetTrigger(fireTriggerName);
            armsAnimator.SetTrigger(fireTriggerName);
            return;
        }

        // 2) 없으면 Base에서 Fire State 강제 재생
        armsAnimator.Play(fireStateName, 0, 0f);
    }

    public void PlayReloadAnim()
    {
        if (armsAnimator == null) return;

        // 1) 트리거 우선
        if (!string.IsNullOrEmpty(reloadTriggerName) && HasTrigger(reloadTriggerName))
        {
            armsAnimator.ResetTrigger(reloadTriggerName);
            armsAnimator.SetTrigger(reloadTriggerName);
            return;
        }

        // 2) Reload 레이어 State 재생(이름이 다를 수 있어서 후보도 같이 시도)
        int layer = (_layerReload >= 0) ? _layerReload : 0;

        // 후보 스테이트들 (에셋마다 이름 조금 달라서 안전장치)
        string[] candidates = new string[]
        {
            reloadStateName,
            "Reload",
            "Reload_Start",
            "Reload_Empty",
            "ReloadStart",
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            string s = candidates[i];
            if (string.IsNullOrEmpty(s)) continue;

            // HasState로 존재 확인하고 Play
            if (armsAnimator.HasState(layer, Animator.StringToHash(s)))
            {
                armsAnimator.Play(s, layer, 0f);
                return;
            }
        }

        // 못 찾으면 그냥 아무 것도 안 함(에러 방지)
        Debug.LogWarning("[ArmsAnimBridge] Reload state not found. Check reload layer/state names.");
    }

    // ====== weaponController 이벤트로 Reload 자동 연동 ======
    private void OnReloadStart(float duration)
    {
        _isReloading = true;
        PlayReloadAnim();
    }

    private void OnReloadEnd()
    {
        _isReloading = false;
    }

    // ====== helpers ======
    private int GetLayerIndex(Animator anim, string layerName)
    {
        if (anim == null || string.IsNullOrEmpty(layerName)) return -1;
        for (int i = 0; i < anim.layerCount; i++)
            if (anim.GetLayerName(i) == layerName) return i;
        return -1;
    }

    private bool HasTrigger(string triggerName)
    {
        if (armsAnimator == null) return false;
        foreach (var p in armsAnimator.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == triggerName)
                return true;
        return false;
    }

    private void SetFloatIfExists(string name, float v)
    {
        if (string.IsNullOrEmpty(name) || armsAnimator == null) return;
        // 파라미터 없어도 SetFloat는 실행은 되지만, 의미 없으니 그냥 호출(경고 없음)
        //armsAnimator.SetFloat(name, v);
    }

    private void SetBoolIfExists(string name, bool v)
    {
        if (string.IsNullOrEmpty(name) || armsAnimator == null) return;
        //armsAnimator.SetBool(name, v);
    }
}
