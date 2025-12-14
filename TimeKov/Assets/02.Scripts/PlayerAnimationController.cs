using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    [Header("Refs")]
    public PlayerController playerController;

    private Animator anim;
    private static readonly int HashMoveState = Animator.StringToHash("MoveState");

    private void Awake()
    {
        anim = GetComponent<Animator>();
        if(playerController == null)
            playerController = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (playerController == null)
            return;

        int moveState = 0; // idle = 0

        // 입력이 있고, 대쉬 중이 아니면 Walk/Run
        if (!playerController.IsDashing && playerController.MoveInput.sqrMagnitude > 0.001f)
        {
            moveState = playerController.IsRunning ? 2 : 1; // 1 = Walk, 2 = Run
        }

        anim.SetInteger(HashMoveState, moveState);
    }
}
