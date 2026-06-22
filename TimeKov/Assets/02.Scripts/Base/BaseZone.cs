using UnityEngine;

public class BaseZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Player>(out var player))
        {
            player.Stat.SetInBase(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<Player>(out var player))
        {
            player.Stat.SetInBase(false);
        }
    }
}
