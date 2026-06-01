using UnityEngine;

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
    public bool    InteractPressed { get; private set; } // 상호작용 입력 (F키)

    public static bool IsBlocked = false; // UI가 열림

    private PlayerSkillComponent _skill;

    void Awake()
    {
        // static 필드는 씬 리로드 시 초기화되지 않으므로 여기서 명시적으로 리셋
        // (UI 열린 채 사망 → 씬 재로드 시 IsBlocked = true 고착 방지)
        IsBlocked = false;
        _skill = GetComponent<PlayerSkillComponent>();
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
            return;
        }

        MoveInput       = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        JumpPressed     = Input.GetKeyDown(KeyCode.Space);
        JumpHeld        = Input.GetKey(KeyCode.Space);
        JumpUp          = Input.GetKeyUp(KeyCode.Space);
        Skill1Pressed   = Input.GetKeyDown(KeyCode.Q);
        Skill2Pressed   = Input.GetKeyDown(KeyCode.E);
        Skill3Pressed   = Input.GetKeyDown(KeyCode.R);
        InteractPressed = Input.GetKeyDown(KeyCode.F);

        // 스킬/콤보 실행 중 입력 차단
        // LockMovement 만으로 부족한 경우 입력 레벨에서 이중 차단
        bool skillExecuting = _skill != null && _skill.IsExecuting;
        if (skillExecuting)
        {
            MoveInput     = Vector2.zero;  // WASD 이동 차단
            JumpPressed   = false;         // 점프 차단
            JumpHeld      = false;
            JumpUp        = false;
            AttackPressed = false;         // 좌클릭 차단
            DashPressed   = false;         // 우클릭 차단
        }
        else
        {
            AttackPressed = Input.GetMouseButtonDown(0);
            DashPressed   = Input.GetMouseButtonDown(1);
        }
    }
}