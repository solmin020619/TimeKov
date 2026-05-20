using UnityEngine;

public class PlayerInputComponent : MonoBehaviour
{
    public Vector2 MoveInput     { get; private set; }  // WASD �̵� �Է�
    public bool    JumpPressed   { get; private set; }  // ���� �Է�
    public bool    AttackPressed { get; private set; }  // �⺻ ���� �Է� (��Ŭ��)
    public bool    DashPressed   { get; private set; }  // ��� �Է� (��Ŭ��)
    public bool    Skill1Pressed { get; private set; }  // Q ��ų �Է�
    public bool    Skill2Pressed { get; private set; }  // E ��ų �Է�
    public bool    Skill3Pressed { get; private set; }  // R ��ų �Է�
    public bool InteractPressed { get; private set; } // ��ȣ�ۿ� �Է� (FŰ)

    public static bool IsBlocked = false; // UI�� �÷���

    void Update()
    {
        if (IsBlocked) // UI 열림 시 모든 입력 차단
        {
            MoveInput = Vector2.zero;
            JumpPressed = false;
            AttackPressed = false;
            DashPressed = false;
            Skill1Pressed = false;
            Skill2Pressed = false;
            Skill3Pressed = false;
            InteractPressed = false;  // 누락 — 미초기화 시 true 고착되어 창고/설비 스팸 열림 발생
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