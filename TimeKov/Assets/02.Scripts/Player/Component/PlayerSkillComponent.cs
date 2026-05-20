using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerSkillComponent : MonoBehaviour
{
    [Header("Combo Attacks")]
    public List<ComboAttackBase> ComboAttackAssets;

    [Header("Skills")]
    public SkillBase Skill1Asset;
    public SkillBase Skill2Asset;
    public SkillBase Skill3Asset;

    private Player _player;

    private Dictionary<SkillSheetId, SkillBase> _skillDatabase = new();
    private Dictionary<SkillSheetId, float> _cooldownTimers = new();
    private Dictionary<SkillSheetId, float> _skillGauges = new();
    private List<ComboAttackBase> _comboAttacks = new();

    private int _comboIndex = 0;
    private float _comboTimer = 0f;
    private bool _comboInputReceived = false;

    private Coroutine _currentRoutine;
    private SkillBase _currentSkill;
    private ComboAttackBase _currentCombo;

    // Skill3 선딜 중 피격 인터럽트 허용 플래그
    public bool CurrentSkillIsInterruptible { get; set; }

    public bool IsExecuting => _currentRoutine != null;

    void Awake()
    {
        _player = GetComponent<Player>();

        foreach (SkillSheetId id in Enum.GetValues(typeof(SkillSheetId)))
            _skillGauges[id] = 0f;

        if (Skill1Asset != null) AddSkill(Skill1Asset);
        if (Skill2Asset != null) AddSkill(Skill2Asset);
        if (Skill3Asset != null) AddSkill(Skill3Asset);

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

    // 게이지 충전 (ComboAttackBase에서 호출)
    public void AddGauge(SkillSheetId id, float amount)
    {
        if (!_skillGauges.ContainsKey(id)) return;
        _skillGauges[id] = Mathf.Min(100f, _skillGauges[id] + amount);
    }

    // 게이지 반환 (UI용)
    public float GetGauge(SkillSheetId id)
    {
        return _skillGauges.TryGetValue(id, out float val) ? val : 0f;
    }

    // 쿨타임 반환 (UI용)
    public float GetCooldown(SkillSheetId id)
    {
        return _cooldownTimers.TryGetValue(id, out float val) ? Mathf.Max(0f, val) : 0f;
    }

    // 최대 쿨타임 반환 (UI 비율 계산용)
    public float GetMaxCooldown(SkillSheetId id)
    {
        return _skillDatabase.TryGetValue(id, out var skill) ? skill.CoolTime : 1f;
    }

    public void RegisterComboAttack(ComboAttackBase attack)
    {
        _comboAttacks.Add(attack);
        _comboAttacks.Sort((a, b) => a.ComboIndex.CompareTo(b.ComboIndex));
    }

    void TryComboAttack()
    {
        // 점프·Dead·Hurt 상태 차단
        if (_player.Movement.IsJumping) return;
        if (_player.Stat.IsDead) return;
        if (_player.Stat.IsHurt) return;

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

        // 3타 완료 후 버퍼 초기화
        if (_comboIndex == 0)
        {
            _comboInputReceived = false;
            yield break;
        }

        if (_comboInputReceived && _comboTimer > 0)
            ExecuteComboAttack();
    }

    void TickComboTimer()
    {
        if (_comboTimer <= 0 || IsExecuting) return;
        _comboTimer -= Time.deltaTime;
        if (_comboTimer <= 0) _comboIndex = 0;
    }

    public void AddSkill(SkillBase skill)
    {
        if (!_skillDatabase.ContainsKey(skill.SkillSheetId))
            _skillDatabase.Add(skill.SkillSheetId, skill);
    }

    public void TryExecute(SkillSheetId id)
    {
        // 점프·Dead·Hurt 상태 차단
        if (_player.Movement.IsJumping) return;
        if (_player.Stat.IsDead) return;
        if (_player.Stat.IsHurt) return;

        if (IsExecuting) return;
        if (!_skillDatabase.TryGetValue(id, out var skill)) return;
        if (_cooldownTimers.TryGetValue(id, out float remaining) && remaining > 0) return;
        if (_skillGauges[id] < 100f) return;

        _skillGauges[id] = 0f;
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
        CurrentSkillIsInterruptible = false;
    }

    private IEnumerator SkillFlow(SkillBase skill)
    {
        yield return skill.ExecuteRoutine(gameObject);
        CurrentSkillIsInterruptible = false;
        _currentRoutine = null;
        _currentSkill = null;
    }

    void TickCooldowns()
    {
        foreach (var key in _cooldownTimers.Keys.ToList())
            if (_cooldownTimers[key] > 0)
                _cooldownTimers[key] -= Time.deltaTime;
    }

    // 리스폰 시 전체 초기화 (3단계)
    public void ResetAll()
    {
        // 게이지 초기화
        foreach (var key in _skillGauges.Keys.ToList())
            _skillGauges[key] = 0f;

        // 쿨타임 초기화
        foreach (var key in _cooldownTimers.Keys.ToList())
            _cooldownTimers[key] = 0f;

        // 콤보 상태 초기화
        _comboIndex = 0;
        _comboTimer = 0f;
        _comboInputReceived = false;
        CurrentSkillIsInterruptible = false;

        if (_currentRoutine != null) Interrupt();
    }
}