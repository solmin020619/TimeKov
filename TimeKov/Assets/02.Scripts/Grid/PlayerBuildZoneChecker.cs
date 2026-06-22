using System.Collections.Generic;
using UnityEngine;

public class PlayerBuildZoneChecker : MonoBehaviour
{
    private readonly HashSet<BuildZone> zones = new();

    public bool IsInBuildZone => zones.Count > 0;

    private void OnTriggerEnter(Collider other)
    {
        BuildZone zone = other.GetComponent<BuildZone>();
        if (zone == null) return;

        zones.Add(zone);
    }

    private void OnTriggerExit(Collider other)
    {
        BuildZone zone = other.GetComponent<BuildZone>();
        if (zone == null) return;

        zones.Remove(zone);
    }
}