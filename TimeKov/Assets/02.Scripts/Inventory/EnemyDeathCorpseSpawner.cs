using UnityEngine;

public class EnemyDeathCorpseSpawner : MonoBehaviour
{
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private GameObject corpsePrefab;

    private void Reset()
    {
        enemyHealth = GetComponent<EnemyHealth>();
    }

    private void Awake()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null)
            enemyHealth.OnDeath += SpawnCorpse;
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
            enemyHealth.OnDeath -= SpawnCorpse;
    }

    private void SpawnCorpse()
    {
        if (corpsePrefab == null) return;
        Instantiate(corpsePrefab, transform.position, transform.rotation);
    }
}