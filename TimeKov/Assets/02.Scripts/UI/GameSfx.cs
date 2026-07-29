using System.Collections.Generic;
using UnityEngine;

// 게임 UI/이벤트 효과음 ID. GameSfxConfig SO 에서 각 ID에 클립을 꽂는다.
public enum SfxId
{
    None = 0,
    QuestStart,
    TutorialHighlight,
    ToastShow,
    ChestOpenStart,
    ChestOpenComplete,
    FacilityUnlockStart,
    FacilityUnlockComplete,
    CodexOpen,
    CodexClose,
    CodexHover,
    CodexClick,
    CodexTabClick,
    SettingsClick,
    SettingsTabClick,
    MachineOpen,
    MachineClose,
    MachineTabClick,
    MachineTakeOutput,

    // ── 빌드/철거 (BuildManager / FacilityPlacer / BuildDemolisher) ──
    BuildStart,
    BuildComplete,
    Demolish,

    // ── 공용 UI (UISoundManager) ──
    UIButtonClick,
    UIButtonHover,
    UIItemHover,
    UIItemClick,
    UIItemRightClick,
    UIItemDragBegin,
    UIItemDrop,
    UIPanelOpen,
    UIPanelClose,
    UIVolumePreview,

    // ── 메뉴/타이틀 ──
    TitleStart,
    MenuClick,

    // ── 퀘스트/아이템 ──
    QuestComplete,
    ItemPickup,

    // ── 플레이어 (PlayerAudioComponent · 재생은 플레이어 로컬 소스에서, 클립만 여기서 관리) ──
    PlayerFootstepWalk,     // 배열(랜덤)
    PlayerFootstepRun,      // 배열(랜덤)
    PlayerDash,
    PlayerStaminaWarning,
    PlayerAttack1,
    PlayerAttack2,
    PlayerAttack3,
    PlayerAttackHit,
    PlayerSkill1,
    PlayerSkill2,
    PlayerSkill3,
    PlayerSkillHit,
    PlayerSkillUnavailable,
    PlayerHurt,
    PlayerJump,
    PlayerDie,

    // ── 설비/기계 (MachineLoopSound · 루프 재생은 설비 로컬 3D 소스에서) ──
    MachineLoop,
    MachineProductionStart,
    MachineProductionDone,

    // ── 패널 열닫 (WindowManager 중앙 개폐 / 각 UI Open·Close) ──
    PanelStatToggle,          // 스탯창(C) 열·닫 공용(1클립)
    PanelSettingsOpen,        // 설정창(Esc) 열기
    PanelSettingsClose,       // 설정창(Esc) 닫기
    PanelWarehouseOpen,       // 창고(=인벤토리) 열기
    PanelWarehouseClose,      // 창고(=인벤토리) 닫기
    PanelTransmissionToggle,  // 시간에너지 전송기 열·닫 공용(1클립)
    PanelShipRepairToggle,    // 우주선 수리 열·닫 공용(1클립)

    // ── 월드/이벤트 ──
    BuildModeEnter,           // 탑뷰(건설모드) 전환 진입음
    ShipPartPickup,           // 우주선 부품(부품0/1/2) 획득음
    FacilityUnlockReveal,     // 탑뷰에서 설비 잠금 해제 연출(달달→탕) 시작음
    GameOverImmediate,        // 게임오버(사망 화면) 즉시음 1회
    GameOverLoopBg,           // 게임오버 배경 루프(즉시음과 동시 시작 → 리스폰 시 정지). 클립만 여기서 관리.

    // ── 전투 브금 (BattleBgm · 클립만 여기서 관리, 재생은 BattleBgm 전용 소스) ──
    WyvernBattleBgm,          // 와이번 보스전 전용 BGM(교전 시 재생 → 처치/이탈 시 정지)

    // ── 귀환석 (ReturnStoneManager · 재생은 로컬 소스, 클립만 여기서 관리) ──
    ReturnStoneChannelLoop,   // 귀환 준비(채널링) 루프음 — 준비 시작~완료/취소까지
    ReturnStoneArrive,        // 기지 복귀 도착음

    // ── 워프 (WarpManager · 귀환석과 동일 구성) ──
    WarpChargeLoop,           // 충전(원통 안 대기) 루프음 — 진입~완료/취소까지
    WarpArrive,               // 워프 도착음(지역 워프 최초 활성화에도 사용)

    // ── 코어 강화 (CoreUpgradeManager) ──
    CoreUpgradeTry,           // 강화 시도(재료 소모·판정 직전)
    CoreUpgradeSuccess,       // 강화 성공(레벨업)
    CoreUpgradeFail,          // 강화 실패

    // ── 시간에너지 전송 (TransmissionManager) ──
    TransmissionSend,         // 전송 실행(키트 전송 확정)
    TransmissionRewardClaim,  // 보상 수령(10% 등 마일스톤 통과)
    TransmissionRateUp,       // 전송률 상승(게이지 증가)

    // ── 퀵슬롯 (PlayerQuickSlotComponent) ──
    QuickSlotUse,             // 퀵슬롯 소모품 사용 성공

    // ── 탑뷰 설비 해금 (QuickSlotIconUI) ──
    FacilityUnlockPop,        // 달달달(Reveal) 이후 팡 터지는 순간

    // ── 코어 강화 미니게임 (CoreUpgradeUI) ──
    CoreUpgradeTick,          // 바늘 회전(타임캐치) 중 째깍째깍 루프

    // ── 일반 몹 전투 BGM (BattleMusicTracker → BattleBgm · 보스 제외 몹 교전 시 재생) ──
    //   ★enum 끝에 append 유지(중간 삽입 금지 — GameSfxConfig id 매핑 보존).
    //   맵 구분 없이 공용 1곡. 볼륨은 GameSfxConfig 의 이 id 볼륨 × BGM 슬라이더.
    NormalBattleBgm,          // 일반 몹 공용 전투 BGM

    // 전투 종료 임팩트 (BattleBgm · 교전 종료 시 필드 BGM 페이드인 직전 샤악- 1회) ──
    //   클립 없으면 무음(안전). SFX 볼륨을 따른다. Resources/Sfx/BattleEndAccent 폴백 가능.
    BattleEndAccent,          // 전투 종료 전환 강조음(엔드필드식 샤악)
}

// 런타임 UI/이벤트 효과음 재생기 (지연 싱글톤, 씬 세팅 불필요).
// 클립 지정 방법 2가지(둘 다 지원):
//   (1) 간단: Resources/Sfx/ 에 SfxId 이름과 같은 클립 파일을 떨군다(예: Resources/Sfx/CodexOpen.wav). SO 불필요.
//   (2) 조절: Resources/Sfx/GameSfxConfig SO 에서 ID별 클립+볼륨 지정(SO가 있으면 SO 우선).
// 클립 없는 ID는 무음(안전). 정지(timeScale 0) 중에도 오디오는 재생됨.
public class GameSfx : MonoBehaviour
{
    private static GameSfx _i;
    private static bool _quitting;

    public static GameSfx I
    {
        get
        {
            if (_i == null && !_quitting)
            {
                var go = new GameObject("[GameSfx]");
                _i = go.AddComponent<GameSfx>();
                DontDestroyOnLoad(go);
                _i.Build();
            }
            return _i;
        }
    }

    private GameSfxConfig _config;
    private AudioSource _source;
    private float _sfxVolume = 1f;   // 현재 SFX 볼륨(설정 슬라이더). 재생 볼륨에 곱해진다.
    private readonly Dictionary<SfxId, AudioClip> _nameCache = new Dictionary<SfxId, AudioClip>();   // 이름 폴백 로드 캐시(null=시도했으나 없음)

    private void Build()
    {
        _source = gameObject.AddComponent<AudioSource>();
        _source.spatialBlend = 0f;   // 2D
        _source.playOnAwake = false;
        _config = Resources.Load<GameSfxConfig>("Sfx/GameSfxConfig");   // 없어도 됨(이름 폴백 사용)

        // SFX 볼륨 설정 반영(지연 생성이라 저장값을 직접 초기화 + 변경 구독)
        _sfxVolume = GlobalSettingsManager.CurrentSFXVolume;
        _source.volume = _sfxVolume;
        GlobalSettingsManager.OnSFXVolumeChanged += OnSfxVolumeChanged;
    }

    private void OnSfxVolumeChanged(float vol)
    {
        _sfxVolume = vol;
        if (_source != null) _source.volume = vol;
    }

    private void OnApplicationQuit() => _quitting = true;
    private void OnDestroy()
    {
        GlobalSettingsManager.OnSFXVolumeChanged -= OnSfxVolumeChanged;
        if (_i == this) _i = null;
    }

    // 효과음 재생(2D). 클립 없으면 조용히 무시.
    public static void Play(SfxId id)
    {
        if (_quitting || id == SfxId.None) return;
        I.PlayInternal(id);
    }

    // 3D(공간) 효과음 재생. 지정한 월드 위치에서 거리감 있게 들린다.
    public static void Play(SfxId id, Vector3 position)
    {
        if (_quitting || id == SfxId.None) return;
        I.PlayInternal3D(id, position);
    }

    // 해당 효과음 클립의 길이(초). 클립이 없으면 0. 재생 길이만큼 대기해야 하는 곳(타이틀 시작 등)에서 사용.
    public static float Length(SfxId id)
    {
        if (_quitting || id == SfxId.None) return 0f;
        return I.Resolve(id, out var clip, out _) ? clip.length : 0f;
    }

    private void PlayInternal(SfxId id)
    {
        if (Resolve(id, out var clip, out var volume)) _source.PlayOneShot(clip, volume);
    }

    private void PlayInternal3D(SfxId id, Vector3 position)
    {
        if (!Resolve(id, out var clip, out var volume)) return;
        var go = new GameObject("[GameSfx3D]");
        go.transform.position = position;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume * _sfxVolume;               // SFX 볼륨 반영
        src.spatialBlend = 1f;                          // 3D
        src.minDistance  = 3f;                          // 설비 제작음(MachineLoopSound)과 동일한 거리 감쇠
        src.maxDistance  = 20f;
        src.rolloffMode  = AudioRolloffMode.Logarithmic;
        src.playOnAwake = false;
        src.Play();
        Destroy(go, clip.length + 0.1f);   // 재생 끝나면 임시 오브젝트 정리
    }

    // 외부(플레이어·설비 등 자체 소스에서 재생하는 컴포넌트)용 클립 조회.
    // 클립을 자기 AudioSource로 직접 재생하되, 클립·볼륨은 GameSfxConfig 한 곳에서 관리하려는 경우 사용.
    // clips 배열이 지정된 ID(발소리 등)는 호출마다 랜덤 클립을 반환한다.
    public static bool TryGet(SfxId id, out AudioClip clip, out float volume)
    {
        clip = null; volume = 1f;
        if (_quitting || id == SfxId.None) return false;
        return I.Resolve(id, out clip, out volume);
    }

    // 클립+볼륨 해석. (1) SO 설정 우선(볼륨 조절 가능, clips 배열이면 랜덤) (2) Resources/Sfx/<이름> 폴백.
    private bool Resolve(SfxId id, out AudioClip clip, out float volume)
    {
        volume = 1f;
        clip = null;
        if (_config != null)
        {
            var e = _config.Get(id);
            if (e != null)
            {
                volume = e.volume;
                if (e.clips != null && e.clips.Count > 0)
                {
                    clip = e.clips[Random.Range(0, e.clips.Count)];   // 랜덤(발소리 등)
                    if (clip != null) return true;
                }
                if (e.clip != null) { clip = e.clip; return true; }
            }
        }
        clip = LoadByName(id);
        return clip != null;
    }

    private AudioClip LoadByName(SfxId id)
    {
        if (_nameCache.TryGetValue(id, out var cached)) return cached;   // null 포함(중복 로드 방지)
        var clip = Resources.Load<AudioClip>("Sfx/" + id.ToString());
        _nameCache[id] = clip;
        return clip;
    }
}
