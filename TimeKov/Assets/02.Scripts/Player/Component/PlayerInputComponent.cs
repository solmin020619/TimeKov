using UnityEngine;

// 입력은 매 프레임 가장 먼저 수집한다(실행순서 -100). 그래야 소비자(상호작용/UI)가
// 같은 프레임에 IsBlocked를 바꿔도, 그 프레임의 InteractPressed는 바뀌기 전 값으로 고정돼
// "UI 닫는 F가 다음 프레임에 다시 열어버리는" 입력 지연 버그가 안 생긴다.
[DefaultExecutionOrder(-100)]
public class PlayerInputComponent : MonoBehaviour
{
    public Vector2 MoveInput     { get; private set; }  // WASD 이동 입력
    public bool    JumpPressed   { get; private set; }  // 점프 입력 (프레임 단위)
    public bool    JumpHeld      { get; private set; }  // 점프 홀드 (누르는 동안 true)
    public bool    JumpUp        { get; private set; }  // 점프 키 뗀 순간 (프레임 단위)
    public bool    AttackPressed { get; private set; }  // 기본 공격 입력 (좌클릭)
    public bool    DashPressed   { get; private set; }  // 대시 입력 (우클릭)
    public bool    Skill1Pressed { get; private set; }  // Q 스킬 입력
    public bool    Skill2Pressed { get; private set; }  // E 스킬 입력
    public bool    Skill3Pressed { get; private set; }  // R 스킬 입력
    public bool    InteractPressed { get; private set; } // 상호작용 입력 (F키, 누른 순간)
    public bool    InstantPressed  { get; private set; } // 즉시완료 입력 (G키, 누른 순간)
    public bool    QuickSlotPressed { get; private set; } // 퀵슬롯 소모품 사용 (기본 V, 전투 중에도 가능)

    public KeyCode QuickSlotKey => KeyBindings.QuickSlot;

    public static bool IsBlocked = false; // UI가 열림

    private PlayerSkillComponent _skill;
    private PlayerDashComponent _dash;

    void Awake()
    {
        // static 필드는 씬 리로드 시 초기화되지 않으므로 여기서 명시적으로 리셋
        // (UI 열린 채 사망 → 씬 재로드 시 IsBlocked = true 고착 방지)
        IsBlocked = false;
        _skill = GetComponent<PlayerSkillComponent>();
        _dash  = GetComponent<PlayerDashComponent>();
    }

    void Update()
    {
        if (IsBlocked) // UI 열림 시 모든 입력 차단
        {
            MoveInput       = Vector2.zero;
            JumpPressed     = false;
            JumpHeld        = false;
            JumpUp          = false;
            AttackPressed   = false;
            DashPressed     = false;
            Skill1Pressed   = false;
            Skill2Pressed   = false;
            Skill3Pressed   = false;
            InteractPressed = false;
            InstantPressed  = false;
            QuickSlotPressed = false;
            return;
        }

        MoveInput       = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        JumpPressed     = Input.GetKeyDown(KeyBindings.Jump);
        JumpHeld        = Input.GetKey(KeyBindings.Jump);
        JumpUp          = Input.GetKeyUp(KeyBindings.Jump);
        Skill1Pressed   = Input.GetKeyDown(KeyBindings.Skill1);
        Skill2Pressed   = Input.GetKeyDown(KeyBindings.Skill2);
        Skill3Pressed   = Input.GetKeyDown(KeyBindings.Skill3);
        InteractPressed = Input.GetKeyDown(KeyBindings.Interact);
        InstantPressed  = Input.GetKeyDown(KeyBindings.Instant);
        // 퀵슬롯은 전투 중(스킬/대시)에도 써야 하므로 아래 차단 블록에서 0으로 만들지 않는다.
        QuickSlotPressed = Input.GetKeyDown(KeyBindings.QuickSlot);

        // 스킬/콤보 실행 중 모든 행동 입력 차단 (이동/점프/공격/대시/스킬 전부).
        // 스킬은 끝까지 재생되어야 하며, 끝나는 순간(IsExecuting=false) 다시 입력을 받아
        // 그 입력의 애니로 즉시 전환되게 한다 → 스킬 후 미끄러짐 방지.
        bool skillExecuting = _skill != null && _skill.IsExecuting;
        if (skillExecuting)
        {
            MoveInput     = Vector2.zero;  // WASD 이동 차단
            JumpPressed   = false;         // 점프 차단
            JumpHeld      = false;
            JumpUp        = false;
            AttackPressed = false;         // 좌클릭 차단
            DashPressed   = false;         // 우클릭 차단
            Skill1Pressed = false;         // 스킬 입력도 차단 (기존 누락 — 스킬 중 스킬 재입력 방지)
            Skill2Pressed = false;
            Skill3Pressed = false;
        }
        else
        {
            AttackPressed = Input.GetKeyDown(KeyBindings.Attack);
            DashPressed   = Input.GetKeyDown(KeyBindings.Dash);
        }

        // 대시 중에는 공격·스킬 입력만 차단한다.
        // 대시(우클릭)와 공격/스킬(좌클릭·QER)이 동시에 돌면 Action 애니 레이어가 꼬여
        // ComboAttackBase의 WaitUntil(!IsInActionAnim)이 안 풀려 플레이어가 멈추고
        // 이펙트만 나가는 버그가 생긴다. (이동/점프는 그대로 둠)
        if (_dash != null && _dash.IsDashing)
        {
            AttackPressed = false;
            Skill1Pressed = false;
            Skill2Pressed = false;
            Skill3Pressed = false;
        }
    }
}