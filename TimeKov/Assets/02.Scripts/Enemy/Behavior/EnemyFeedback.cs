using UnityEngine;

/// <summary>
/// 적의 행동별(Spawn/Hit/Attack/Death) VFX + 사운드 재생.
/// 데이터는 MeleeEnemyData SO 에서 받아오고, EnemyBrain 이 Awake 에서 SetData 로 주입.
/// EnemyHealth / MeleeAttackAction 등 외부에서 Play* 메서드 호출.
///
/// 인스펙터에서 prefab/clip 을 직접 채울 일은 없고 (SO 가 source of truth),
/// AudioSource 와 vfxAnchor (스폰 위치) 만 prefab 단위로 셋팅.
/// </summary>
public class EnemyFeedback : MonoBehaviour
{
    [Header("References")]
    [Tooltip("AudioSource 가 비어있으면 같은 GameObject 의 것을 자동 사용. 없으면 사운드 skip.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("VFX 가 스폰될 기준 위치. 비어있으면 transform.position 사용.")]
    [SerializeField] private Transform vfxAnchor;

    private MeleeEnemyData data;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    /// <summary>EnemyBrain.Awake 에서 SO 데이터 주입.</summary>
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
