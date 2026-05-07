using UnityEngine;

public class EnemyFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform vfxAnchor;

    private MeleeEnemyData data;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void SetData(MeleeEnemyData data)
    {
        this.data = data;
    }

    public void PlaySpawn()
    {
        if (data == null) return;
        Play(data.spawnVFX, data.spawnSound);
    }

    public void PlayDetect()
    {
        if (data == null) return;
        Play(data.detectVFX, data.detectSound);
    }

    public void PlayHit(Vector3 hitPoint)
    {
        if (data == null) return;
        Play(data.hitVFX, data.hitSound, hitPoint);
    }

    public void PlayAttack()
    {
        if (data == null) return;
        Play(data.attackVFX, data.attackSound);
    }

    public void PlayDeath()
    {
        if (data == null) return;
        Play(data.deathVFX, data.deathSound);
    }

    private void Play(GameObject vfxPrefab, AudioClip clip, Vector3? worldPos = null)
    {
        Vector3 pos = worldPos ?? (vfxAnchor != null ? vfxAnchor.position : transform.position);

        if (vfxPrefab != null)
            Instantiate(vfxPrefab, pos, Quaternion.identity);

        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
