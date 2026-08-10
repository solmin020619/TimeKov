// UISoundManager.cs
// UI 사운드 전체를 관리하는 싱글톤
// 각 AudioClip 필드에 클립을 할당하면 즉시 테스트 가능
//
// [씬 배치 방법]
//   1. 씬에 빈 게임오브젝트 생성 → 이름: "UISoundManager"
//   2. 이 컴포넌트 추가
//   3. sfxSource 필드에 AudioSource 컴포넌트 드래그 (또는 같은 오브젝트에 AudioSource 추가 후 자동 할당)
//   4. 각 AudioClip 필드에 클립 드래그 → 플레이 버튼으로 바로 테스트

using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[SingleInstance]
public class UISoundManager : MonoBehaviour
{
    public static UISoundManager Instance { get; private set; }

    // ─── 오디오 소스 ─────────────────────────────────────────────────────────
    // 기본 UI 효과음 클립은 모두 GameSfx(SfxId.UI*)로 통합 — GameSfxConfig 에서 관리한다.
    // 이 소스는 컴포넌트별 override 클립(PlayClip)을 직접 재생할 때만 사용한다.
    [Header("오디오 소스 (override 클립 재생용 · 비워두면 자동 생성)")]
    [SerializeField] private AudioSource sfxSource;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (UIDuplicateGuard.Report(Instance, this)) { Destroy(gameObject); return; }
        Instance = this;

        // sfxSource가 연결되지 않으면 같은 오브젝트의 AudioSource 찾거나 새로 추가
        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        // 게임 일시정지(AudioListener.pause) 중에도 UI 조작음은 들려야 한다.
        sfxSource.ignoreListenerPause = true;
    }

    private void Start()
    {
        // 초기 SFX 볼륨 적용
        if (sfxSource != null)
            sfxSource.volume = GlobalSettingsManager.CurrentSFXVolume;
    }

    private void OnEnable()
    {
        // 인벤토리 슬롯 이벤트 구독 (모든 슬롯 자동 처리)
        InventorySlotUI.OnAnySlotHoverEnter   += HandleItemHover;
        InventorySlotUI.OnAnySlotClicked      += HandleItemClick;
        InventorySlotUI.OnAnySlotRightClicked += HandleItemRightClick;
        InventorySlotUI.OnAnySlotDragBegin    += HandleItemDragBegin;
        InventorySlotUI.OnAnySlotDropped      += HandleItemDrop;

        // 실시간 SFX 볼륨 동기화
        GlobalSettingsManager.OnSFXVolumeChanged += ApplySFXVolume;
    }

    private void OnDisable()
    {
        InventorySlotUI.OnAnySlotHoverEnter   -= HandleItemHover;
        InventorySlotUI.OnAnySlotClicked      -= HandleItemClick;
        InventorySlotUI.OnAnySlotRightClicked -= HandleItemRightClick;
        InventorySlotUI.OnAnySlotDragBegin    -= HandleItemDragBegin;
        InventorySlotUI.OnAnySlotDropped      -= HandleItemDrop;

        GlobalSettingsManager.OnSFXVolumeChanged -= ApplySFXVolume;
    }

    // ─── 볼륨 동기화 ─────────────────────────────────────────────────────────
    private void ApplySFXVolume(float vol)
    {
        if (sfxSource != null) sfxSource.volume = vol;
    }

    // ─── 슬롯 이벤트 핸들러 (람다 대신 메서드 — 구독 해제 가능) ─────────────
    private void HandleItemHover(InventorySlotUI _)      => PlayItemHover();
    private void HandleItemClick(InventorySlotUI _)       => PlayItemClick();
    private void HandleItemRightClick(InventorySlotUI _)  => PlayItemRightClick();
    private void HandleItemDragBegin(InventorySlotUI _)   => PlayItemDragBegin();
    private void HandleItemDrop(InventorySlotUI _)        => PlayItemDrop();

    // ─── Public Play 메서드 ──────────────────────────────────────────────────

    /// <summary>지정 클립을 직접 재생. 오버라이드 클립이 있는 컴포넌트에서 호출.</summary>
    public void PlayClip(AudioClip clip)         => Play(clip);

    public void PlayButtonClick()    => GameSfx.Play(SfxId.UIButtonClick);
    public void PlayButtonHover()    => GameSfx.Play(SfxId.UIButtonHover);

    public void PlayItemHover()      => GameSfx.Play(SfxId.UIItemHover);
    public void PlayItemClick()      => GameSfx.Play(SfxId.UIItemClick);
    public void PlayItemRightClick() => GameSfx.Play(SfxId.UIItemRightClick);
    public void PlayItemDragBegin()  => GameSfx.Play(SfxId.UIItemDragBegin);
    public void PlayItemDrop()       => GameSfx.Play(SfxId.UIItemDrop);

    public void PlayPanelOpen()      => GameSfx.Play(SfxId.UIPanelOpen);
    public void PlayPanelClose()     => GameSfx.Play(SfxId.UIPanelClose);

    public void PlayVolumePreview()  => GameSfx.Play(SfxId.UIVolumePreview);

    // ─── 내부 재생 ───────────────────────────────────────────────────────────
    private void Play(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip);
    }
}
