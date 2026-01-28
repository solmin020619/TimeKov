using UnityEngine;

public class AraxiaCore : MonoBehaviour
{
    public AraxiaBoss boss;

    public void OnHit(float damage)
    {
        if (boss != null && boss.isCoreExposed)
        {
            boss.TakeDamage(damage);
            Debug.Log("Core Hit! Damage Dealt.");
        }
        else
        {
            Debug.Log("The Core is protected!");
        }
    }
}