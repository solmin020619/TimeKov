using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerSkillComponent : MonoBehaviour
{
    [Header("Combo Attacks")]
    public List<ComboAttackBase> ComboAttackAssets;

    private Player _player;

    // ── 일반 스킬 ────────────────────────────────────────────
    private Dictionary<SkillSheetId, SkillBase> _skillDatabase = new();
    private Dictionary<SkillSheetId, float> _cooldownTimers = new();

    // ── 콤보 공격 ────────────────────────────────────────────
    private List<ComboAttackBase> _comboAttacks = new();
    private int _comboIndex = 0;
    private float _comboTimer = 0f;
    private bool _comboInputReceived = false;

    // ── 공통 ─────────────────────────────────────────────────
    private Coroutine _currentRoutine;
    private SkillBase _currentSkill;        // 일반 스킬용
    private ComboAttackBase _currentCombo;      // 콤보 공격용

    public bool IsExecuting => _currentRoutine != null;

    void Awake()
    {
        _player = GetComponent<Player>();

        foreach (var attack in ComboAttackAssets)
            RegisterComboAttack(attack);
    }

    void Update()
    {
        TickCooldowns();
        TickComboTimer();

        if (_player.Input.AttackPressed) TryComboAttack();
        if (_player.Input.Skill1Pressed) TryExecute(SkillSheetId.Skill1);
        if (_player.Input.Skill2Pressed) TryExecute(SkillSheetId.Skill2);
        if (_player.Input.Skill3Pressed) TryExecute(SkillSheetId.Skill3);
    }

    // ── 콤보 ─────────────────────────────────────────────────
    public void RegisterComboAttack(ComboAttackBase attack)
    {
        _comboAttacks.Add(attack);
        _comboAttacks.Sort((a, b) => a.ComboIndex.CompareTo(b.ComboIndex));
    }

    void TryComboAttack()
    {
        if (_comboAttacks.Count == 0) return;

        if (IsExecuting)
        {
            _comboInputReceived = true;
            return;
        }

        ExecuteComboAttack();
    }

    void ExecuteComboAttack()
    {
        if (_comboIndex >= _comboAttacks.Count) _comboIndex = 0;

        var attack = _comboAttacks[_comboIndex];
        _currentCombo = attack;
        _currentRoutine = StartCoroutine(ComboFlow(attack));
    }

    IEnumerator ComboFlow(ComboAttackBase attack)
    {
        _comboInputReceived = false;

        yield return attack.ExecuteRoutine(gameObject);

        _comboTimer = attack.ComboWindow;
        _comboIndex = (_comboIndex + 1) % _comboAttacks.Count;
        _currentRoutine = null;
        _currentCombo = null;

        if (_comboInputReceived && _comboTimer > 0)
            ExecuteComboAttack();
    }

    void TickComboTimer()
    {
        if (_comboTimer <= 0 || IsExecuting) return;

        _comboTimer -= Time.deltaTime;

        if (_comboTimer <= 0)
            _comboIndex = 0;
    }

    // ── 일반 스킬 ─────────────────────────────────────────────
    public void AddSkill(SkillBase skill)
    {
        if (!_skillDatabase.ContainsKey(skill.SkillSheetId))
            _skillDatabase.Add(skill.SkillSheetId, skill);
    }

    public void TryExecute(SkillSheetId id)
    {
        if (IsExecuting) return;
        if (!_skillDatabase.TryGetValue(id, out var skill)) return;
        if (_cooldownTimers.TryGetValue(id, out float remaining) && remaining > 0) return;

        _cooldownTimers[id] = skill.CoolTime;
        _currentSkill = skill;
        _currentRoutine = StartCoroutine(SkillFlow(skill));
    }

    public void Interrupt()
    {
        if (_currentRoutine == null) return;

        StopCoroutine(_currentRoutine);
        _currentSkill?.OnInterrupt(gameObject);
        _currentCombo?.OnInterrupt(gameObject);
        _currentRoutine = null;
        _currentSkill = null;
        _currentCombo = null;
        _comboIndex = 0;
        _comboTimer = 0;
    }

    private IEnumerator SkillFlow(SkillBase skill)
    {
        yield return skill.ExecuteRoutine(gameObject);
        _currentRoutine = null;
        _currentSkill = null;
    }

    void TickCooldowns()
    {
        foreach (var key in _cooldownTimers.Keys.ToList())
            _cooldownTimers[key] -= Time.deltaTime;
    }
}