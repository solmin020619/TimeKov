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
    public static KeyCode Attack    = KeyCode.Mouse0;
    public static KeyCode Dash      = KeyCode.Mouse1;
    public static KeyCode Inventory = KeyCode.Tab;
    public static KeyCode Stat      = KeyCode.C;
    public static KeyCode Codex     = KeyCode.K;

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
        Attack    = data.attack;
        Dash      = data.dash;
        Inventory = data.inventory;
        Stat      = data.stat;
        Codex     = data.codex;
    }
}
