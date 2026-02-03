using UnityEngine;

public class BossProjectile : MonoBehaviour
{
    public float damage = 20f;
    public bool isExplosion = false; // 체크하면: 폭발(범위공격), 끄면: 미사일(직격)
    public float duration = 2.0f;    // 폭발 이펙트 유지 시간

    void Start()
    {
        Destroy(gameObject, duration); // 일정 시간 뒤 자동 삭제
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // 플레이어 체력 깎기
            PlayerTime pt = other.GetComponent<PlayerTime>();
            if (pt != null)
            {
                pt.TakeDamage(damage);
            }
            Debug.Log("플레이어 피격!");

            // 미사일이면 맞았을 때 사라짐 (폭발은 사라지면 안 됨)
            if (!isExplosion) Destroy(gameObject);
        }
    }
}