using UnityEngine;

// 기지 오브젝트에 Collider (IsTrigger = true) 붙이고 이 스크립트 추가
public class BaseZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Player>(out var player))
            player.Stat.SetInBase(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<Player>(out var player))
            player.Stat.SetInBase(false);
    }
}