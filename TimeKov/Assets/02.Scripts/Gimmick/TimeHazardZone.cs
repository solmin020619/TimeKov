using UnityEngine;

// ── 시간 급속 감소 구역 (위험 건물/지역) ──────────────────────────────────────
// 플레이어가 이 구역(트리거) 안에 있는 동안 시간(HP)이 평소보다 빠르게 줄어든다.
// 나가면 원래 속도로 돌아온다. 구역 안에 TimeSafeZone(안전지대)을 두면 그 안에서는 멈춘다.
//
//   • 시간 감소는 PlayerStatComponent 의 기존 자연 감소를 '배율'로만 가속한다.
//     (HpDrainRate * HpDrainMultiplier) — HUD 의 시간 표시/경고는 기존 것이 그대로 반응한다.
//   • 실제 적용/우선순위 판정은 TimeHazardSystem 이 한다(안전지대와 겹칠 수 있어서).
//   • 기지(결계) 안에서는 원래 시간이 안 줄어든다 → 이 구역은 기지 밖에 두는 게 전제.
//
//   세팅: 빈 오브젝트 + BoxCollider(isTrigger 체크, 건물/지역 범위) + 이 컴포넌트.
//         (Tools/TIMEKOV/시간구역 메뉴로 자동 생성 가능)
[RequireComponent(typeof(Collider))]
public class TimeHazardZone : MonoBehaviour
{
    [Header("시간 감소 속도")]
    [Tooltip("이 구역 안에서의 시간 감소 배율. 1 = 평소와 같음, 3 = 3배 빨리 닳음.\n" +
             "여러 위험 구역이 겹치면 가장 큰 값이 적용된다.")]
    [SerializeField, Min(0f)] private float drainMultiplier = 3f;

    [Header("화면 효과 (구역 안에 있는 동안 계속)")]
    [Tooltip("체크: 구역 안에 있는 동안 화면이 약간 어두워지고 계속 일렁인다.")]
    [SerializeField] private bool useScreenEffect = true;

    [Tooltip("어두워지는 색. 보통 검정, 붉은기를 섞으면 더 위험해 보인다.")]
    [SerializeField] private Color darkColor = Color.black;
    [Tooltip("어두워지는 정도. '약간'이면 0.2 안팎, 0.4 넘으면 꽤 답답해진다.")]
    [SerializeField, Range(0f, 1f)] private float darkAlpha = 0.25f;

    [Tooltip("일렁임 색. 어두운 색이면 그늘이 흐르는 느낌, 밝은 색이면 아지랑이 느낌.")]
    [SerializeField] private Color shimmerColor = new Color(0.0509804f, 0.0196078f, 0.0627451f);   // #0D0510
    [Tooltip("일렁임 세기. 0 = 없음. 0.4 정도면 그늘이 뚜렷하게 흐른다.")]
    [SerializeField, Range(0f, 1f)] private float shimmerAlpha = 0.40f;
    [Tooltip("일렁임이 흐르는 속도. 클수록 빠르게 요동친다.")]
    [SerializeField, Range(0f, 5f)] private float shimmerSpeed = 1.8f;
    [Tooltip("일렁임 왜곡 정도. 화면이 늘었다 줄었다 하며 일그러지는 양.")]
    [SerializeField, Range(0f, 0.3f)] private float warpAmount = 0.10f;

    [Tooltip("구역에 들어오고 나갈 때 효과가 뜨고 사라지는 시간(초).")]
    [SerializeField] private float fadeTime = 0.5f;

    [Header("도전 미션 — 죽지 않고 입구로 복귀")]
    [Tooltip("체크: 이 구역 안에서 시간이 다 닳아도 진짜로 죽지 않는다.\n" +
             "검은 화면으로 페이드되며 입구로 돌아온다 — 아이템도 떨어뜨리지 않는다.")]
    [SerializeField] private bool rescueInsteadOfDeath = true;

    [Tooltip("되돌아갈 입구 위치. 비우면 이 구역 오브젝트의 위치를 쓴다(꼭 지정할 것 — 구역 중심이면 안에 갇힌다).")]
    [SerializeField] private Transform entrancePoint;

    [Tooltip("복귀 후 시간(HP)을 '구역에 들어올 때 체력'의 몇 %로 맞출지. 0.5 = 절반.\n" +
             "★최대 체력이 아니라 진입 시점 체력 기준이다 — 최대치 기준이면 체력이 바닥일 때\n" +
             "  일부러 죽어서 시간을 채우는 악용이 가능해진다. 진입 기준이면 항상 손해라 이득이 없다.")]
    [SerializeField, Range(0.05f, 1f)] private float rescueHpPercent = 0.5f;

    // ★기준은 '최대 체력'이 아니라 '구역에 들어올 때의 체력'이다.
    //   최대치 기준으로 주면, 체력이 바닥일 때 일부러 들어와 죽는 것이 시간 충전이 돼 버린다
    //   (HP = 시간 = 핵심 자원이라 치명적). 진입 시점 기준이면 복귀 체력이 항상 진입 체력보다
    //   적으므로 어떤 경우에도 이득이 될 수 없다. 반복해서 죽으면 50% → 25% → 12.5% 로 계속 줄어든다.

    [Tooltip("검게/밝게 페이드되는 시간(초).")]
    [SerializeField] private float rescueFadeTime = 0.6f;
    [Tooltip("완전히 검은 상태로 머무는 시간(초). 이 동안 순간이동한다.")]
    [SerializeField] private float rescueBlackHold = 0.35f;

    [Header("사운드")]
    // ★None 이면 기본음으로 폴백한다(GimmickBarrier·GimmickSwitch 와 같은 방식).
    //   신규 필드가 생기기 전에 씬에 배치된 구역은 None 으로 저장돼 있어 그냥 두면 무음이 되기 때문.
    //   정말 무음으로 두고 싶으면 아래 muteSfx 를 체크한다.
    [Tooltip("구역에 들어올 때 1회 재생. None 이면 기본음(TimeHazardEnter).")]
    [SerializeField] private SfxId enterSfx = SfxId.TimeHazardEnter;
    // 이탈음은 기본 무음(유저 결정) — 진입음/루프만으로 충분하고, 나갈 때 소리는 거슬린다.
    //   ★여기엔 폴백을 걸지 않는다. 다시 넣고 싶으면 인스펙터에서 직접 고르면 된다.
    [Tooltip("구역에서 나갈 때 1회 재생. 기본 없음(무음) — 필요하면 직접 고른다.")]
    [SerializeField] private SfxId exitSfx = SfxId.None;

    [Tooltip("구역 안에 있는 동안 계속 깔리는 루프음(저음 드론). None 이면 기본음(TimeHazardAmbientLoop).\n" +
             "화면 일렁임과 짝을 이루는 '분위기' 담당 — 2D로 재생돼 구역 안 어디서나 같은 크기로 들린다.")]
    [SerializeField] private SfxId ambientLoop = SfxId.TimeHazardAmbientLoop;

    [Tooltip("체크: 이 구역은 소리를 내지 않는다(위 폴백도 무시).")]
    [SerializeField] private bool muteSfx = false;

    [Tooltip("체크: 구역 안에 있는 동안 BGM(필드·전투 음악)을 끈다. 루프음과 정적만 남아 더 위험하게 느껴진다.\n" +
             "나가면 원래대로 돌아온다.")]
    [SerializeField] private bool muteBgmInside = true;
    [Tooltip("루프음 볼륨 배율(구역별 조절). 클립 자체 볼륨은 GameSfxConfig 에서 관리하고, 여기선 그 위에 곱해진다.")]
    [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.45f;
    [Tooltip("루프음이 서서히 커지고 작아지는 시간(초). 구역 경계에서 뚝 끊기지 않게 한다.")]
    [SerializeField] private float ambientFadeTime = 0.8f;

    // 시스템이 읽는 값들
    public float DrainMultiplier => drainMultiplier;
    public bool  UseScreenEffect => useScreenEffect;

    /// 플레이어가 지금 이 구역(트리거) 안에 있는가.
    ///   건물 표면 표식(TimeHazardSurfaceFx)이 이걸 보고 켜고 끈다 — 구역 콜라이더가
    ///   이미 건물에 맞춰져 있으므로 별도 경계를 또 계산할 필요가 없다.
    public bool PlayerInside => _inside;

    public TimeHazardScreenFx.Config BuildFxConfig() => new TimeHazardScreenFx.Config
    {
        darkColor    = darkColor,
        darkAlpha    = darkAlpha,
        shimmerColor = shimmerColor,
        shimmerAlpha = shimmerAlpha,
        shimmerSpeed = shimmerSpeed,
        warpAmount   = warpAmount,
        fadeTime     = fadeTime,
    };

    private bool _inside;
    private PlayerStatComponent _stat;   // 구역 안에 있는 동안의 플레이어 스탯(사망 가로채기용)
    private float _hpOnEnter;            // 구역 진입 시점 체력 — 구조 복귀량의 기준(악용 방지)

    // 루프음: 이 오브젝트의 로컬 AudioSource 에서 2D 로 재생(설비 루프음과 같은 방식).
    private AudioSource _ambientSrc;
    private Coroutine _ambientFade;

    private void Reset()
    {
        // 컴포넌트를 막 붙였을 때 트리거로 잡아준다(까먹기 쉬운 부분).
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[시간구역] {name}: Collider 가 isTrigger 가 아니라 플레이어를 감지하지 못한다.", this);
    }

    private void OnEnable()  => GlobalSettingsManager.OnSFXVolumeChanged += ApplySfxVolume;

    // 설정에서 SFX 볼륨을 바꾸면 재생 중인 루프에도 즉시 반영.
    private void ApplySfxVolume(float _)
    {
        if (_ambientSrc != null && _ambientSrc.isPlaying) _ambientSrc.volume = TargetAmbientVolume;
    }

    // 최종 루프 볼륨 = GameSfxConfig 의 클립 볼륨 × 구역별 배율 × 설정의 SFX 볼륨.
    private float _clipVolume = 1f;   // GameSfxConfig 에서 받은 값
    private float TargetAmbientVolume => _clipVolume * ambientVolume * GlobalSettingsManager.CurrentSFXVolume;

    // None 이면 기본음으로 대체. muteSfx 면 전부 무음.
    private SfxId Sfx(SfxId chosen, SfxId fallback) =>
        muteSfx ? SfxId.None : (chosen == SfxId.None ? fallback : chosen);

    private void OnTriggerEnter(Collider other)
    {
        if (_inside) return;
        var stat = FindStat(other);
        if (stat == null) return;

        _inside = true;
        _stat = stat;
        _hpOnEnter = stat.CurrentHp;   // 구조 시 이 값을 기준으로 되살린다(악용 방지)
        // 구역 안에 있는 동안만 사망을 가로챈다(도전 미션 = 죽어도 입구로 복귀).
        if (rescueInsteadOfDeath) stat.DeathInterceptor = RescueInsteadOfDying;

        TimeHazardSystem.EnterHazard(this, stat);
        GameSfx.Play(Sfx(enterSfx, SfxId.TimeHazardEnter));   // 2D — 구역 진입 알림이라 위치감 없이 또렷하게
        StartAmbient();
        if (muteBgmInside) BgmMute.Push(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_inside) return;
        if (FindStat(other) == null) return;

        _inside = false;
        ReleaseDeathInterceptor();
        TimeHazardSystem.ExitHazard(this);
        GameSfx.Play(Sfx(exitSfx, SfxId.None));   // 폴백 없음 = 지정 안 하면 무음
        StopAmbient();
        BgmMute.Pop(this);   // 켠 적 없으면 무시된다
    }

    // 구역이 꺼지거나 파괴돼도 배율/루프음이 남지 않게 정리.
    private void OnDisable()
    {
        GlobalSettingsManager.OnSFXVolumeChanged -= ApplySfxVolume;
        BgmMute.Pop(this);              // ★구역이 꺼지거나 파괴돼도 BGM 이 음소거로 남지 않게
        ReleaseDeathInterceptor();      // ★가로채기가 남으면 구역 밖에서도 안 죽는 버그가 된다

        if (_ambientSrc != null) { _ambientSrc.Stop(); _ambientSrc.volume = 0f; }
        _ambientFade = null;   // 오브젝트가 꺼지면 코루틴은 이미 멈춘다

        if (!_inside) return;
        _inside = false;
        ReleaseDeathInterceptor();
        TimeHazardSystem.ExitHazard(this);
    }

    // ── 도전 미션: 죽는 대신 입구로 복귀 ─────────────────────────────────────
    // PlayerStatComponent 가 사망 직전에 부른다. true 를 돌려주면 실제 사망(OnDead)이 취소된다
    // → RespawnManager 의 아이템 드롭·게임오버 흐름이 아예 시작되지 않는다.
    //   ★HP 를 0 인 채로 두면 IsDead 가 계속 true 라 다음 프레임에 또 죽는다. 여기서 바로 회복시킨다.
    private bool RescueInsteadOfDying()
    {
        if (!rescueInsteadOfDeath || _stat == null) return false;   // 가로채지 않음 = 정상 사망
        if (TimeHazardRescue.IsBusy) return true;                   // 이미 구조 중(중복 발동 방지)

        // ★즉시 되살려야 한다. HP 가 0 이면 IsDead 가 true 라, 페이드가 도는 사이
        //   RespawnManager 의 Update 감시(IsDead 확인)가 진짜 사망 흐름을 태워버린다.
        //   Heal() 은 IsDead 면 무시되므로(좀비 방지 가드) 전용 통로인 ReviveWith 를 쓴다.
        //   ReviveWith 는 '더하기'가 아니라 '이 값으로 설정'.
        //
        // ★기준은 진입 시점 체력 — 최대치 기준이면 '빈사로 들어와 죽어서 시간 충전'이 된다.
        //   구조 후에는 기준을 지금 값으로 낮춘다 → 안에서 반복해 죽으면 50%→25%→12.5% 로 계속 깎인다.
        float target = _hpOnEnter * rescueHpPercent;
        _stat.ReviveWith(target);
        _hpOnEnter = _stat.CurrentHp;   // ReviveWith 의 최소치 보정까지 반영된 실제 값으로 갱신

        Vector3 dest = entrancePoint != null ? entrancePoint.position : transform.position;
        TimeHazardRescue.Run(_stat.transform, dest, rescueFadeTime, rescueBlackHold);   // ★플레이어 트랜스폼
        return true;
    }

    // 구역을 벗어나면 가로채기를 반드시 해제한다 — 안 그러면 밖에서도 안 죽는 버그가 된다.
    private void ReleaseDeathInterceptor()
    {
        if (_stat == null) return;
        if (_stat.DeathInterceptor == RescueInsteadOfDying) _stat.DeathInterceptor = null;
        _stat = null;
    }

    // ── 루프음 ───────────────────────────────────────────────────────────────
    private void StartAmbient()
    {
        SfxId id = Sfx(ambientLoop, SfxId.TimeHazardAmbientLoop);
        if (id == SfxId.None) return;
        if (!GameSfx.TryGet(id, out var clip, out var cfgVol)) return;   // 클립 없으면 무음(안전)
        _clipVolume = cfgVol;

        if (_ambientSrc == null)
        {
            _ambientSrc = gameObject.AddComponent<AudioSource>();
            _ambientSrc.loop         = true;
            _ambientSrc.playOnAwake  = false;
            _ambientSrc.spatialBlend = 0f;   // 2D — 넓은 구역이라 어디서나 같은 크기
        }

        _ambientSrc.clip = clip;
        if (!_ambientSrc.isPlaying) { _ambientSrc.volume = 0f; _ambientSrc.Play(); }
        FadeAmbient(TargetAmbientVolume);
    }

    private void StopAmbient()
    {
        if (_ambientSrc == null || !_ambientSrc.isPlaying) return;
        FadeAmbient(0f, stopAtEnd: true);
    }

    private void FadeAmbient(float target, bool stopAtEnd = false)
    {
        if (_ambientFade != null) StopCoroutine(_ambientFade);
        _ambientFade = StartCoroutine(FadeAmbientRoutine(target, stopAtEnd));
    }

    private System.Collections.IEnumerator FadeAmbientRoutine(float target, bool stopAtEnd)
    {
        float from = _ambientSrc.volume;
        float dur  = Mathf.Max(0.01f, ambientFadeTime);
        float t    = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _ambientSrc.volume = Mathf.Lerp(from, target, Mathf.Clamp01(t / dur));
            yield return null;
        }
        _ambientSrc.volume = target;
        if (stopAtEnd) _ambientSrc.Stop();
        _ambientFade = null;
    }

    private static PlayerStatComponent FindStat(Collider other)
    {
        var player = other.GetComponentInParent<Player>();
        return player != null ? player.Stat : null;
    }
}
