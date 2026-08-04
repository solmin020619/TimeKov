using System.Collections;
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

        // ★AudioSource가 아예 없는 옛 몬스터(오크트리/언데드/웨어울프/스켈레톤 등) 대비:
        //   신규 몹(hell/rock 등)과 동일한 3D 설정으로 자동 추가한다. 없으면 소리가 안 나서 "너무 작게" 들리던 문제.
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.minDistance = 6f;      // 신규 몹과 동일(6m 안에서 최대)
            audioSource.maxDistance = 500f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // 3D 소리 강제 (인스펙터에서 미설정한 경우 대비)
        if (audioSource != null && audioSource.spatialBlend < 0.5f)
            audioSource.spatialBlend = 1f;

        // 스폰 시점의 SFX 볼륨을 즉시 반영.
        // VolumeSync 는 볼륨이 '바뀔 때'만 씬을 훑기 때문에, 그 뒤에 스폰된 몬스터는
        // 아무도 볼륨을 걸어주지 않아 슬라이더를 내려도 최대 음량으로 났다.
        // 프리팹 11종(구형 5종 + 거미/자이언트웜/헬 5종)에 VolumeSyncReceiver 가 없어서
        // 여기서 일괄로 보장한다. 위에서 audioSource 존재를 이미 보장했으므로 전 몬스터가 커버된다.
        ApplySfxVolume(GlobalSettingsManager.CurrentSFXVolume);
    }

    private void OnEnable()
    {
        GlobalSettingsManager.OnSFXVolumeChanged += ApplySfxVolume;
    }

    private void ApplySfxVolume(float vol)
    {
        if (audioSource != null) audioSource.volume = vol;
    }

    public void SetData(MeleeEnemyData data)
    {
        this.data = data;
    }

    public void PlaySpawn()
    {
        if (data == null) return;
        // ★스폰 사운드는 재생하지 않는다. 등장 연출은 감지(포효) 사운드가 대신하므로 스폰음은 제거.
        //   VFX 는 그대로 유지(적 자식으로 부착 → 적 따라다님, 3초 후 자동 destroy).
        Play(data.spawnVFX, null, followCaster: true, vfxLifeTime: 3f);
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

        // ★타격감. 전 몹 공통이라 보스도 같이 받는다.
        //   패턴을 끊지 않으므로 보스 설계와 충돌하지 않는다.
        //   (피격 플래시는 시도했다가 뺐다. 신규 몬스터는 노출 프로퍼티가 0개인 셰이더그래프를 써서
        //    색을 칠하는 방식이 아예 안 먹고, 재질을 통째로 갈아끼우는 방식은 그림이 과했다.)
        HitStop.Play(data.hitStopTime, data.hitStopScale);
        if (data.knockbackDistance > 0f) StartKnockback(hitPoint);
    }

    // ── 넉백: 맞은 방향 반대로 살짝 밀림 ──
    private Coroutine _knockCo;

    private void StartKnockback(Vector3 hitPoint)
    {
        Vector3 dir = transform.position - hitPoint;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = -transform.forward;

        if (_knockCo != null) StopCoroutine(_knockCo);
        _knockCo = StartCoroutine(KnockRoutine(dir.normalized * data.knockbackDistance));
    }

    private IEnumerator KnockRoutine(Vector3 push)
    {
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        float dur = Mathf.Max(0.02f, data.knockbackTime);
        float t = 0f;

        while (t < dur)
        {
            float dt = Time.deltaTime;
            t += dt;
            Vector3 step = push * (dt / dur);

            // ★transform 을 직접 옮기면 NavMeshAgent 내부 위치와 어긋나 떨린다.
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(step);
            else transform.position += step;

            yield return null;
        }
        _knockCo = null;
    }

    private void OnDisable()
    {
        GlobalSettingsManager.OnSFXVolumeChanged -= ApplySfxVolume;
        _knockCo = null;
    }

    public void PlayAttack()
    {
        if (data == null) return;
        Play(data.attackVFX, data.attackSound);
        // Attack trigger는 MeleeAttackAction에서 별도 호출 (기존 흐름 유지)
    }

    /// <summary>
    /// 보스처럼 attackSound 한 슬롯으로 부족한 컨트롤러가 자체 AudioClip 필드를 재생할 때 쓰는 범용 재생.
    /// VFX/트리거 없이 소리만 낸다.
    /// </summary>
    public void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
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
