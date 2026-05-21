using UnityEngine;

public class EnemyFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform vfxAnchor;
    [Tooltip("Animator trigger 호출용. 비워두면 자식에서 자동 검색.")]
    [SerializeField] private Animator animator;

    private MeleeEnemyData data;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // 3D 소리 강제 (인스펙터에서 미설정한 경우 대비)
        if (audioSource != null && audioSource.spatialBlend < 0.5f)
            audioSource.spatialBlend = 1f;
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
        TrySetTrigger(data.detectTrigger);
    }

    public void PlayHit(Vector3 hitPoint)
    {
        if (data == null) return;
        Play(data.hitVFX, data.hitSound, hitPoint);
        TrySetTrigger(data.hitTrigger);
    }

    public void PlayAttack()
    {
        if (data == null) return;
        Play(data.attackVFX, data.attackSound);
        // Attack trigger는 MeleeAttackAction에서 별도 호출 (기존 흐름 유지)
    }

    public void PlayDeath()
    {
        if (data == null) return;
        Play(data.deathVFX, data.deathSound);
        TrySetTrigger(data.dieTrigger);
    }

    /// <summary>
    /// Animator에 trigger 호출. 파라미터가 존재할 때만. 없으면 silent skip (warning 안 띄움).
    /// </summary>
    private void TrySetTrigger(string trigger)
    {
        if (animator == null || string.IsNullOrEmpty(trigger)) return;
        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == trigger)
            {
                animator.SetTrigger(trigger);
                return;
            }
        }
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
