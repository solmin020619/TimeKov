using UnityEngine;

public class PlayerBuildZoneChecker : MonoBehaviour
{
    public bool IsInBuildZone { get; private set; }

    private int zoneCount = 0;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<BuildZone>() != null)
        {
            zoneCount++;
            IsInBuildZone = zoneCount > 0;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<BuildZone>() != null)
        {
            zoneCount--;
            if (zoneCount < 0)
                zoneCount = 0;

            IsInBuildZone = zoneCount > 0;
        }
    }
}