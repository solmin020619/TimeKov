using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerSkillComponent : MonoBehaviour
{
    [Header("Combo Attacks")]
    public List<ComboAttackBase> ComboAttackAssets; // 인스펙터에서 Attack1/2/3 SO 등록

    [Header("Skills")]
    public SkillBase Skill1Asset;   // 인스펙터에서 Skill1_ReaperSlash.asset 연결
    public SkillBase Skill2Asset;   // 인스펙터에서 Skill2_CycloneBreak.asset 연결
    public SkillBase Skill3Asset;   // 인스펙터에서 Skill3_ExecutionFall.asset 연결

    private Player _player;

    // 일반 스킬
    private Dictionary<SkillSheetId, SkillBase> _skillDatabase = new();
    private Dictionary<SkillSheetId, float> _cooldownTimers = new();

    // 스킬 게이지 (최대 100)
    private Dictionary<SkillSheetId, float> _skillGauges = new();

    // 콤보 공격
    private List<ComboAttackBase> _comboAttacks = new();
    private int _comboIndex = 0;
    private float _comboTimer = 0f;
    private bool _comboInputReceived = false;

    // 공통
    private Coroutine _currentRoutine;
    private SkillBase _currentSkill;
    private ComboAttackBase _currentCombo;

    public bool IsExecuting => _currentRoutine != null;

    void Awake()
    {
        _player = GetComponent<Player>();

        // 게이지 초기화
        foreach (SkillSheetId id in Enum.GetValues(typeof(SkillSheetId)))
            _skillGauges[id] = 0f;

        // 스킬 등록
        if (Skill1Asset != null) AddSkill(Skill1Asset);
        if (Skill2Asset != null) AddSkill(Skill2Asset);
        if (Skill3Asset != null) AddSkill(Skill3Asset);

        // 콤보 등록
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

    // 게이지 추가, 적 적중 시 ComboAttackBase에서 호출
    public void AddGauge(SkillSheetId id, float amount)
    {
        if (!_skillGauges.ContainsKey(id)) return;
        _skillGauges[id] = Mathf.Min(100f, _skillGauges[id] + amount);
    }

    // 현재 게이지 조회
    public float GetGauge(SkillSheetId id)
    {
        return _skillGauges.TryGetValue(id, out float val) ? val : 0f;
    }

    // 콤보
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

        // 3타 이후 콤보 끝나면 버퍼 초기화
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

    // 일반 스킬
    public void AddSkill(SkillBase skill)
    {
        if (!_skillDatabase.ContainsKey(skill.SkillSheetId))
            _skillDatabase.Add(skill.SkillSheetId, skill);
    }

    public void TryExecute(SkillSheetId id)
    {
        Debug.Log($"TryExecute: {id} | IsExecuting={IsExecuting} | HasSkill={_skillDatabase.ContainsKey(id)} | Gauge={_skillGauges[id]} | Cooldown={(_cooldownTimers.TryGetValue(id, out float r) ? r : 0f)}");

        if (IsExecuting) return;
        if (!_skillDatabase.TryGetValue(id, out var skill)) return;
        if (_cooldownTimers.TryGetValue(id, out float remaining) && remaining > 0) return;

        // 게이지 100 미만이면 사용 불가
        if (_skillGauges[id] < 100f) return;

        // 사용 시 게이지 초기화
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