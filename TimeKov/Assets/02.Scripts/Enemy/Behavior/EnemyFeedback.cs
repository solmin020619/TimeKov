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
        // Spawn VFX는 적 자식으로 부착 → 적 따라다님. 3초 후 자동 destroy.
        Play(data.spawnVFX, data.spawnSound, followCaster: true, vfxLifeTime: 3f);
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
        if (animator == null) return;

        // 죽음 애니는 트리거 경쟁에 묻히지 않게 Die 상태를 직접 강제 재생한다.
        // (TakeDamage 가 매 타격마다 Hit 트리거를 걸어서, 스킬 연속타격 시 Die 트리거가
        //  Any State 전이 우선순위에서 Hit 에 밀려 죽음 애니가 안 나오던 문제. 평타=단발이라 정상)
        int dieHash = Animator.StringToHash("Die");
        if (animator.HasState(0, dieHash))
        {
            animator.ResetTrigger(data.hitTrigger);
            animator.CrossFadeInFixedTime(dieHash, 0.05f, 0);
        }
        else
        {
            TrySetTrigger(data.dieTrigger);  // "Die" state 명이 다른 경우 fallback
        }
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

    private void Play(GameObject vfxPrefab, AudioClip clip, Vector3? worldPos = null, bool followCaster = false, float vfxLifeTime = 0f)
    {
        Vector3 pos = worldPos ?? (vfxAnchor != null ? vfxAnchor.position : transform.position);

        if (vfxPrefab != null)
        {
            // followCaster=true면 적 transform 자식으로 부착 (적 움직임 따라옴).
            // false면 월드 좌표 고정 (피격 지점, 사망 위치 등).
            GameObject vfx = followCaster
                ? Instantiate(vfxPrefab, pos, Quaternion.identity, transform)
                : Instantiate(vfxPrefab, pos, Quaternion.identity);

            // vfxLifeTime > 0이면 그 시간 후 자동 destroy. 0이면 ParticleSystem 자체 설정 따름.
            if (vfxLifeTime > 0f) Destroy(vfx, vfxLifeTime);
        }

        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
