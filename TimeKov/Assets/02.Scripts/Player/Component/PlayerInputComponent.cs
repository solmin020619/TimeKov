using UnityEngine;

public class PlayerInputComponent : MonoBehaviour
{
    public Vector2 MoveInput     { get; private set; }  // WASD 이동 입력
    public bool    JumpPressed   { get; private set; }  // 점프 입력
    public bool    AttackPressed { get; private set; }  // 기본 공격 입력 (좌클릭)
    public bool    DashPressed   { get; private set; }  // 대시 입력 (우클릭)
    public bool    Skill1Pressed { get; private set; }  // Q 스킬 입력
    public bool    Skill2Pressed { get; private set; }  // E 스킬 입력
    public bool    Skill3Pressed { get; private set; }  // R 스킬 입력
    public bool InteractPressed { get; private set; } // 상호작용 입력 (F키)

    public static bool IsBlocked = false; // UI용 플래그

    void Update()
    {
        if (IsBlocked) // UI 열리면 모든 입력 차단
        {
            MoveInput = Vector2.zero;
            JumpPressed = false;
            AttackPressed = false;
            Skill1Pressed = false;
            Skill2Pressed = false;
            Skill3Pressed = false;
            return;
        }

        MoveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
        JumpPressed   = Input.GetKeyDown(KeyCode.Space);
        AttackPressed = Input.GetMouseButtonDown(0);
        DashPressed   = Input.GetMouseButtonDown(1);
        Skill1Pressed = Input.GetKeyDown(KeyCode.Q);
        Skill2Pressed = Input.GetKeyDown(KeyCode.E);
        Skill3Pressed = Input.GetKeyDown(KeyCode.R);
        InteractPressed = Input.GetKeyDown(KeyCode.F);
    }
}