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

public class UISoundManager : MonoBehaviour
{
    public static UISoundManager Instance { get; private set; }

    // ─── 오디오 소스 ─────────────────────────────────────────────────────────
    [Header("오디오 소스 (SFX 전용 AudioSource를 연결하거나 비워두면 자동 생성)")]
    [SerializeField] private AudioSource sfxSource;

    // ─── 버튼 사운드 ─────────────────────────────────────────────────────────
    [Header("버튼 사운드")]
    [SerializeField] private AudioClip buttonClickClip;   // 버튼 좌클릭
    [SerializeField] private AudioClip buttonHoverClip;   // 버튼에 마우스 올렸을 때

    // ─── 아이템 슬롯 사운드 ──────────────────────────────────────────────────
    [Header("아이템 슬롯 사운드")]
    [SerializeField] private AudioClip itemHoverClip;      // 아이템에 마우스 올렸을 때
    [SerializeField] private AudioClip itemClickClip;      // 아이템 좌클릭
    [SerializeField] private AudioClip itemRightClickClip; // 아이템 우클릭
    [SerializeField] private AudioClip itemDragBeginClip;  // 아이템 드래그 시작
    [SerializeField] private AudioClip itemDropClip;       // 아이템 드랍

    // ─── UI 패널 사운드 ──────────────────────────────────────────────────────
    [Header("UI 패널 사운드")]
    [SerializeField] private AudioClip panelOpenClip;   // 인벤토리·설비UI 열릴 때
    [SerializeField] private AudioClip panelCloseClip;  // 인벤토리·설비UI 닫힐 때

    // ─── 설정 슬라이더 미리듣기 ──────────────────────────────────────────────
    [Header("설정 슬라이더 미리듣기 (BGM·SFX 볼륨 슬라이더 정지 후 재생)")]
    [SerializeField] private AudioClip volumePreviewClip;  // 볼륨 수준 확인용 클립

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // sfxSource가 연결되지 않으면 같은 오브젝트의 AudioSource 찾거나 새로 추가
        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
    }

    private void Start()
    {
        // PlayerPrefs에서 초기 SFX 볼륨 적용
        if (sfxSource != null)
            sfxSource.volume = PlayerPrefs.GetFloat("SFXVolume", 1f);
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

    public void PlayButtonClick()    => Play(buttonClickClip);
    public void PlayButtonHover()    => Play(buttonHoverClip);

    public void PlayItemHover()      => Play(itemHoverClip);
    public void PlayItemClick()      => Play(itemClickClip);
    public void PlayItemRightClick() => Play(itemRightClickClip);
    public void PlayItemDragBegin()  => Play(itemDragBeginClip);
    public void PlayItemDrop()       => Play(itemDropClip);

    public void PlayPanelOpen()      => Play(panelOpenClip);
    public void PlayPanelClose()     => Play(panelCloseClip);

    public void PlayVolumePreview()  => Play(volumePreviewClip);

    // ─── 내부 재생 ───────────────────────────────────────────────────────────
    private void Play(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip);
    }
}
