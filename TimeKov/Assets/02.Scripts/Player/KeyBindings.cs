// =====================================================================
// KeyBindings.cs
// 현재 적용 중인 키 바인딩(static). PlayerInputComponent가 매 프레임 참조.
// GlobalSettingsManager가 SettingsData 로드/리바인딩 시 Apply()로 갱신.
// =====================================================================

using UnityEngine;

public static class KeyBindings
{
    public static KeyCode Jump      = KeyCode.Space;
    public static KeyCode Skill1    = KeyCode.Q;
    public static KeyCode Skill2    = KeyCode.E;
    public static KeyCode Skill3    = KeyCode.R;
    public static KeyCode Interact  = KeyCode.F;
    public static KeyCode Instant   = KeyCode.G;
    public static KeyCode QuickSlot = KeyCode.V;

    public static void Apply(KeyBindingData data)
    {
        if (data == null) return;
        Jump      = data.jump;
        Skill1    = data.skill1;
        Skill2    = data.skill2;
        Skill3    = data.skill3;
        Interact  = data.interact;
        Instant   = data.instant;
        QuickSlot = data.quickSlot;
    }

    /// <summary>code가 이미 다른 액션에 쓰이고 있으면 그 액션 이름을 반환하고 true.</summary>
    public static bool IsConflict(KeyCode code, string excludeAction, out string conflictAction)
    {
        conflictAction = null;
        if (excludeAction != "Jump"      && Jump      == code) conflictAction = "점프";
        else if (excludeAction != "Skill1"    && Skill1    == code) conflictAction = "스킬1";
        else if (excludeAction != "Skill2"    && Skill2    == code) conflictAction = "스킬2";
        else if (excludeAction != "Skill3"    && Skill3    == code) conflictAction = "스킬3";
        else if (excludeAction != "Interact"  && Interact  == code) conflictAction = "상호작용";
        else if (excludeAction != "Instant"   && Instant   == code) conflictAction = "즉시완료";
        else if (excludeAction != "QuickSlot" && QuickSlot == code) conflictAction = "퀵슬롯";

        return conflictAction != null;
    }
}
