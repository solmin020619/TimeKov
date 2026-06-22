using UnityEngine;

public class Player : MonoBehaviour
{
    public PlayerInputComponent Input { get; private set; }
    public PlayerMovementComponent Movement { get; private set; }
    public PlayerStatComponent Stat { get; private set; }
    public PlayerSkillComponent Skill { get; private set; }
    public PlayerAnimatorComponent Anim { get; private set; }
    public PlayerDashComponent Dash { get; private set; }
    public PlayerInteractComponent Interact { get; private set; }
    public PlayerAudioComponent Audio { get; private set; }
    public PlayerQuickSlotComponent QuickSlot { get; private set; }

    void Awake()
    {
        Input = GetComponent<PlayerInputComponent>();
        Movement = GetComponent<PlayerMovementComponent>();
        Stat = GetComponent<PlayerStatComponent>();
        Skill = GetComponent<PlayerSkillComponent>();
        Anim = GetComponent<PlayerAnimatorComponent>();
        Dash = GetComponent<PlayerDashComponent>();
        Interact = GetComponent<PlayerInteractComponent>();
        Audio = GetComponent<PlayerAudioComponent>();

        // 소모품 퀵슬롯 - 프리팹에 없으면 런타임 추가(수동 부착 불필요).
        // 프리팹에 직접 붙이면 인스펙터에서 쿨다운 등 튜닝 가능.
        QuickSlot = GetComponent<PlayerQuickSlotComponent>();
        if (QuickSlot == null) QuickSlot = gameObject.AddComponent<PlayerQuickSlotComponent>();

        // 물(Water 레이어) 닿으면 즉사 - 프리팹에 없으면 런타임 추가.
        // 직접 붙이면 인스펙터에서 레이어명 변경 가능.
        if (GetComponent<PlayerWaterDeath>() == null) gameObject.AddComponent<PlayerWaterDeath>();
    }
}