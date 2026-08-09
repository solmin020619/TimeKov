using System;
using UnityEngine;

[CreateAssetMenu(menuName = "TIMEKOV/퀘스트/목표/이동 거리")]
public class MoveDistanceObjective : ObjectiveSO
{
    public float requiredDistance = 3f;

    [NonSerialized] float _accumulated;

    public override ActivationTiming Timing => ActivationTiming.OnUIActivated;

    public override void Activate() { GameEvents.OnPlayerMovedDelta += OnMoved; }
    public override void Deactivate() => GameEvents.OnPlayerMovedDelta -= OnMoved;
    public override float Progress => Mathf.Clamp01(_accumulated / requiredDistance);

    void OnMoved(float d)
    {
        _accumulated += d;
        ReportProgress(Progress);
        if (_accumulated >= requiredDistance) Complete();
    }
}
