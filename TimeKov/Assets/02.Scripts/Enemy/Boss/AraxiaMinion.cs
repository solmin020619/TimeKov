using UnityEngine;

public class AraxiaMinion : MonoBehaviour
{
    [HideInInspector]
    public AraxiaBoss boss;
    public float hp = 50f;

    public void TakeDamage(float amount)
    {
        hp -= amount;
        if (hp <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (boss != null)
        {
            boss.OnMinionDead(this);
        }

        Destroy(gameObject);
    }
}