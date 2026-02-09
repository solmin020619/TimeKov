using UnityEngine;

public class TimeKovWeaponAnimDriver : MonoBehaviour
{
    [Header("Animator (optional)")]
    [SerializeField] private Animator characterAnimator;

    [Header("Default controller (optional)")]
    [SerializeField] private RuntimeAnimatorController defaultController;

    [Header("Force apply in LateUpdate (recommended ON)")]
    [SerializeField] private bool forceLocomotionInLateUpdate = true;

    // Asset states/params
    private static readonly int RELOAD_EMPTY = Animator.StringToHash("Reload_Empty");
    private static readonly int RELOAD_TAC = Animator.StringToHash("Reload_Tac");
    private static readonly int FIRE = Animator.StringToHash("Fire");
    private static readonly int FIREOUT = Animator.StringToHash("FireOut");
    private static readonly int EQUIP = Animator.StringToHash("Equip");
    private static readonly int EQUIP_OVERRIDE = Animator.StringToHash("Equip_Override");
    private static readonly int IDLE = Animator.StringToHash("Idle");
    private static readonly int UNEQUIP_TRG = Animator.StringToHash("UnEquip");

    private static readonly int GAIT = Animator.StringToHash("Gait");
    private static readonly int TACSPRINT = Animator.StringToHash("TacSprint");
    private static readonly int ISINAIR = Animator.StringToHash("IsInAir");

    private WeaponAnimSettings currentSettings;

    // cached locomotion
    private float _gait01;
    private bool _tacSprint;
    private bool _isInAir;

    private bool _hasGait;
    private bool _hasTacSprint;
    private bool _hasIsInAir;

    private void Awake()
    {
        EnsureAnimator();
        if (characterAnimator != null && defaultController == null)
            defaultController = characterAnimator.runtimeAnimatorController;
    }

    private void EnsureAnimator()
    {
        if (characterAnimator != null) return;

        // 1) 'Gait' 파라미터가 있는 Animator 우선 탐색
        var anims = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            if (a == null || a.runtimeAnimatorController == null) continue;
            if (HasParam(a, "Gait"))
            {
                characterAnimator = a;
                break;
            }
        }

        // 2) fallback
        if (characterAnimator == null && anims.Length > 0)
            characterAnimator = anims[0];

        CacheParamExistence();
    }

    private void CacheParamExistence()
    {
        if (characterAnimator == null) return;
        _hasGait = HasParam(characterAnimator, "Gait");
        _hasTacSprint = HasParam(characterAnimator, "TacSprint");
        _hasIsInAir = HasParam(characterAnimator, "IsInAir");
    }

    private bool HasParam(Animator a, string name)
    {
        var ps = a.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].name == name) return true;
        return false;
    }

    public void Equip(WeaponAnimSettings settings, bool fastEquip = false)
    {
        EnsureAnimator();
        if (characterAnimator == null) return;

        currentSettings = settings;

        if (currentSettings != null && currentSettings.characterController != null)
            characterAnimator.runtimeAnimatorController = currentSettings.characterController;

        // 컨트롤러 바뀌면 파라미터 캐시 갱신
        CacheParamExistence();

        // 즉시 반영
        characterAnimator.Rebind();
        characterAnimator.Update(0f);

        characterAnimator.Play(IDLE, -1, 0f);

        if (currentSettings != null && currentSettings.hasEquipOverride)
            characterAnimator.Play(fastEquip ? EQUIP : EQUIP_OVERRIDE, -1, 0f);
        else
            characterAnimator.Play(EQUIP, -1, 0f);
    }

    public void UnEquip(bool restoreDefault = false)
    {
        EnsureAnimator();
        if (characterAnimator == null) return;

        characterAnimator.ResetTrigger(UNEQUIP_TRG);
        characterAnimator.SetTrigger(UNEQUIP_TRG);

        if (restoreDefault && defaultController != null)
        {
            characterAnimator.runtimeAnimatorController = defaultController;
            CacheParamExistence();
            characterAnimator.Rebind();
            characterAnimator.Update(0f);
        }
    }

    public void PlayFire(int ammoAfterConsume)
    {
        EnsureAnimator();
        if (characterAnimator == null) return;

        bool useFire = currentSettings == null || currentSettings.useFireClip;
        bool useOut = currentSettings != null && currentSettings.hasFireOut;

        if (useFire) characterAnimator.Play(FIRE, -1, 0f);
        if (useOut && ammoAfterConsume <= 0) characterAnimator.Play(FIREOUT, -1, 0f);
    }

    public void PlayReload(bool isEmpty)
    {
        EnsureAnimator();
        if (characterAnimator == null) return;
        characterAnimator.Play(isEmpty ? RELOAD_EMPTY : RELOAD_TAC, -1, 0f);
    }

    public void SetLocomotion(float gait01, bool tacSprint, bool isInAir)
    {
        EnsureAnimator();
        if (characterAnimator == null) return;

        _gait01 = gait01;
        _tacSprint = tacSprint;
        _isInAir = isInAir;

        ApplyLocomotionNow();
    }

    private void ApplyLocomotionNow()
    {
        if (characterAnimator == null) return;

        if (_hasGait) characterAnimator.SetFloat(GAIT, _gait01);
        if (_hasTacSprint) characterAnimator.SetFloat(TACSPRINT, _tacSprint ? 1f : 0f);
        if (_hasIsInAir) characterAnimator.SetBool(ISINAIR, _isInAir);
    }

    private void LateUpdate()
    {
        if (!forceLocomotionInLateUpdate) return;
        ApplyLocomotionNow();
    }
}
