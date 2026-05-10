using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Quest/Objective/PressKey")]
public class PressKeyObjective : ObjectiveSO
{
    public KeyCode key;
    public int requiredCount = 1;

    [NonSerialized] int _count;

    public override ActivationTiming Timing => ActivationTiming.OnUIActivated;

    public override void Activate() { InputBus.OnKeyDown += OnKey; }
    public override void Deactivate() => InputBus.OnKeyDown -= OnKey;
    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));

    public override string GetDisplayLabel()
        => requiredCount > 1 ? $"{label} ({_count}/{requiredCount})" : label;

    void OnKey(KeyCode k)
    {
        if (IsInGracePeriod || k != key) return;
        _count++;
        ReportProgress(Progress);
        if (_count >= requiredCount) Complete();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        requiredCount = Mathf.Max(1, requiredCount);
        if (key == KeyCode.None)
            Debug.LogError($"[PressKey] '{name}' key=None. 어떤 키도 안 받음", this);
        if (key == KeyCode.Tab || key == KeyCode.Escape)
            Debug.LogWarning($"[PressKey] '{name}' Tab/Escape는 UI 토글/메뉴와 충돌 가능", this);
    }
#endif
}
