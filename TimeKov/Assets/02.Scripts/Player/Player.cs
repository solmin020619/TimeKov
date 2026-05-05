using UnityEngine;

public class Player : MonoBehaviour
{
    public PlayerInputComponent Input { get; private set; }
    public PlayerMovementComponent Movement { get; private set; }
    public PlayerStatComponent Stat { get; private set; }
    public PlayerSkillComponent Skill { get; private set; }

    void Awake()
    {
        Input = GetComponent<PlayerInputComponent>();
        Movement = GetComponent<PlayerMovementComponent>();
        Stat = GetComponent<PlayerStatComponent>();
        Skill = GetComponent<PlayerSkillComponent>();
    }
}