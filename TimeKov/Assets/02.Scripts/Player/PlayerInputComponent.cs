using UnityEngine;

public class PlayerInputComponent : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool AttackPressed { get; private set; }
    public bool Skill1Pressed { get; private set; }
    public bool Skill2Pressed { get; private set; }
    public bool Skill3Pressed { get; private set; }

    void Update()
    {
        MoveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
        JumpPressed = Input.GetKeyDown(KeyCode.Space);
        AttackPressed = Input.GetMouseButtonDown(0);
        Skill1Pressed = Input.GetKeyDown(KeyCode.Q);
        Skill2Pressed = Input.GetKeyDown(KeyCode.E);
        Skill3Pressed = Input.GetKeyDown(KeyCode.R);
    }
}