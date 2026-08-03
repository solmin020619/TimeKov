using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnManager : MonoBehaviour, ISaveLoadListener
{
    public Transform RespawnPoint;
    public float RespawnDelay = 2f;

    [Tooltip("부활 시 시작 체력(=시간) 비율. CoreUpgradeManager가 있으면 코어 레벨 기반 값이 우선, 없을 때만 이 값 사용(폴백).")]
    [Range(0f, 1f)]
    [SerializeField] private float respawnHpPercent = 0.5f;

    public Player _player;

    [Header("Death Overlay UI")]
    [Tooltip("사망 시 화면 중앙에 표시할 GameOver 오버레이 (선택)")]
    [SerializeField] private DeathOverlayUI deathOverlay;

    [Header("아이템 드롭")]
    [Tooltip("죽으면 인벤토리의 모든 아이템을 필드에 드롭합니다")]
    public bool DropItemsOnDeath = true;

    [Tooltip("드롭할 LootBox 프리팹 (Inspector에서 연결)")]
    [SerializeField] private GameObject lootBoxPrefab;

    // 리스폰 중복 실행 방지 플래그
    // OnDead 이벤트가 짧은 시간에 여러 번 호출돼도 코루틴이 하나만 돌도록 보장
    private bool _isRespawning = false;

    // 사망 시 캐릭터를 서서히 사라지게 하는 페이드(있으면 사용). Player 에 부착돼 있을 때만.
    private PlayerDeathFade _deathFade;

    void Awake()
    {
        SaveSlotManager.Instance?.RegisterListener(this);
    }

    void Start()
    {
        _player.Stat.OnDead += HandleDead;
        _deathFade = _player.GetComponent<PlayerDeathFade>();

        // deathOverlay 미연결 시 씬에서 자동 탐색
        if (deathOverlay == null)
            deathOverlay = Object.FindAnyObjectByType<DeathOverlayUI>(FindObjectsInactive.Include);
    }

    // ISaveLoadListener — SaveSlotManager가 씬 로드 후 Start() 전체 완료 다음 프레임에 호출.
    // 죽은 채 저장된 세이브를 로드하면 ApplyCoreStats()가 CurrentHp=0으로 복원하지만
    // OnDead는 다시 발화하지 않는다 — 여기서 직접 확인해 부활 흐름을 태운다.
    public void OnAfterLoad()
    {
        if (_player != null && _player.Stat.IsDead) HandleDead();
    }

    void HandleDead()
    {
        if (_isRespawning) return;   // 이미 리스폰 진행 중이면 무시
        StartCoroutine(RespawnRoutine());
    }

    // 부활하기 버튼 클릭 신호
    private bool _respawnRequested = false;

    // 게임오버 배경 루프 전용 AudioSource(즉시음은 GameSfx 원샷).
    private AudioSource _gameOverLoopSrc;

    // 게임오버(사망 화면): 즉시음 1회 + 배경 루프 시작. 루프는 리스폰 시 StopGameOverAudio 로 정지.
    private void StartGameOverAudio()
    {
        BattleBgm.SuspendForGameOver();   // 보스전 중이면 전투 BGM 페이드아웃(게임오버 사운드와 안 겹치게)
        GameSfx.Play(SfxId.GameOverImmediate);   // 즉시음(2D 원샷)

        if (!GameSfx.TryGet(SfxId.GameOverLoopBg, out var clip, out var vol) || clip == null) return;
        if (_gameOverLoopSrc == null)
        {
            var go = new GameObject("[GameOverLoop]");
            go.transform.SetParent(transform);
            _gameOverLoopSrc = go.AddComponent<AudioSource>();
            _gameOverLoopSrc.spatialBlend = 0f;      // 2D
            _gameOverLoopSrc.playOnAwake = false;
            _gameOverLoopSrc.loop = false;   // 루프 없이 1회만 재생(리스폰 전에 끝나면 그대로 멈춤)
            _gameOverLoopSrc.ignoreListenerPause = true;   // timeScale/pause 중에도 재생
        }
        _gameOverLoopSrc.clip = clip;
        _gameOverLoopSrc.volume = vol * GlobalSettingsManager.CurrentBGMVolume;   // 배경음이라 BGM 볼륨 반영
        _gameOverLoopSrc.Play();
    }

    private void StopGameOverAudio()
    {
        if (_gameOverLoopSrc != null) _gameOverLoopSrc.Stop();
        BattleBgm.ResumeAfterGameOver();   // 리스폰: 정지해 뒀던 필드 BGM 재개
    }

    IEnumerator RespawnRoutine()
    {
        _isRespawning     = true;
        _respawnRequested = false;

        // 사망 즉시 열려있던 UI 닫기 — 부활 버튼을 인벤/설비 UI가 가리지 않도록 (#27)
        GameUIController.Instance?.CloseAll();

        // 진행 중이던 공격/스킬 중단 — 그 코루틴이 끝에서 ReturnToLocomotion/ResetToIdle 로
        // 죽는 애니를 덮어써 가끔 사망 모션이 안 나오던 문제 방지 (#11)
        _player.Skill?.Interrupt();

        // 1. 죽는 애니메이션 + 이동 잠금 + 서서히 사라지기(페이드 아웃 + 작은 이펙트)
        _player.Anim.PlayDie();
        _player.Movement.LockMovement(true);
        _deathFade?.FadeOut();

        // 2. 사망 즉시 아이템 드롭.
        //    예전엔 튜토리얼 진행 중(IsTutorialActive)엔 드롭을 막았으나, 그게 튜토리얼
        //    기간 내내 OFF가 돼 "죽어도 안 떨군다"로 보였음. LootBox는 회수 가능하고
        //    (가방 못 담으면 창고로 넘어감) 소멸도 없어 하드 소프트락이 안 나므로 게이트 제거.
        if (DropItemsOnDeath)
            DropInventoryItems();

        // 3. DEFEAT 오버레이 표시 + 카운트다운 후 버튼 활성화
        StartGameOverAudio();   // 즉시음 1회 + 배경 루프(리스폰 시 정지)
        if (deathOverlay != null)
        {
            deathOverlay.Show(RespawnDelay, () => _respawnRequested = true);
            yield return new WaitUntil(() => _respawnRequested);
        }
        else
        {
            Debug.LogWarning("[RespawnManager] deathOverlay가 null → 딜레이로 대체");
            yield return new WaitForSeconds(RespawnDelay);
        }
        StopGameOverAudio();

        // 2. 스탯 회복 (IsDead → false). 부활 체력은 코어 레벨 기반(없으면 인스펙터 폴백).
        float respawnPct = CoreUpgradeManager.Instance != null
            ? CoreUpgradeManager.Instance.GetRespawnHpPercent()
            : respawnHpPercent;
        _player.Stat.Respawn(respawnPct);

        // 3. 리지드바디 위치 직접 설정
        //    transform.position 대신 rb.position 사용:
        //    Unity Physics가 transform 변경을 즉시 동기화하지 않는 경우가 있어
        //    rb.position은 물리 엔진의 내부 위치를 직접 갱신하므로 신뢰할 수 있음
        if (RespawnPoint == null)
        {
            Debug.LogError("[RespawnManager] RespawnPoint가 null입니다! Inspector에서 연결하세요.");
        }
        else
        {
            var rb = _player.GetComponent<Rigidbody>();
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position        = RespawnPoint.position;  // 물리 공간 위치 직접 갱신
        }

        // 4. 오버레이 숨김
        deathOverlay?.Hide();

        // 5. 애니메이션 리셋 (Base Layer → Blend Tree, Action Layer → Empty)
        _player.Anim.ResetToIdle();

        // 페이드로 사라졌던 캐릭터를 다시 보이게 복구(렌더러 on + 원본 머티리얼).
        _deathFade?.RestoreImmediate();

        // 5. 이동 잠금 해제
        _player.Movement.LockMovement(false);

        // 6. 스킬 쿨다운타이머 초기화
        _player.Skill.ResetAll();

        _isRespawning = false;
    }

    // 인벤토리 아이템 전부 드롭
    // 전리품 상자를 놓을 자리.
    //
    // 죽은 자리를 그대로 쓰면 바다에 빠져 죽었을 때 상자가 수면 위에 뜬다(물 콜라이더가 솔리드라
    // 상자 착지 로직이 수면을 바닥으로 인정한다). 가지러 들어가면 또 죽으니 사실상 회수 불가다.
    //
    // 그래서 "마지막으로 땅을 밟은 자리" 를 기준으로 잡는다. 낭떠러지에서 떨어졌으면 발을 뗀
    // 절벽 위에 뜨고, 물가에서 실수로 빠졌으면 밟고 있던 물가에 뜬다. 평지에서 죽으면 죽은 자리와 같다.
    //   ★죽은 자리 기준으로 "가장 가까운 NavMesh" 를 찾는 방식은 쓰지 않는다. 절벽 밑이 바로 바다면
    //     낙차까지 거리에 포함돼서, 물가 대신 바다 건너 섬이 뽑힐 수 있다.
    //   기준점이 이미 땅 위라 NavMesh 보정 반경은 짧아도 된다(지형 틈새 메우는 용도).
    private Vector3 ResolveDropPosition()
    {
        Vector3 basePos = _player.Movement != null
            ? _player.Movement.LastGroundedPosition
            : _player.transform.position;

        if (UnityEngine.AI.NavMesh.SamplePosition(basePos, out var hit, DropNavSampleRadius,
                                                  UnityEngine.AI.NavMesh.AllAreas))
            basePos = hit.position;

        return basePos + Vector3.up * 0.2f;
    }

    // 마지막 접지 지점 근처에서 걸어갈 수 있는 자리를 찾는 반경(m).
    // 기준점이 이미 땅이라 짧게 잡는다. 못 찾으면 기준점 그대로 쓴다.
    private const float DropNavSampleRadius = 5f;

    void DropInventoryItems()
    {
        var inv = InventoryManager.Instance;
        if (inv == null)
        {
            Debug.LogWarning("[RespawnManager] InventoryManager.Instance 없음 — 드롭 스킵");
            return;
        }

        // lootBoxPrefab 미설정 시 씬의 EnemyDropOnDeath에서 자동으로 가져옴
        if (lootBoxPrefab == null)
        {
            var enemyDrop = Object.FindAnyObjectByType<EnemyDropOnDeath>();
            if (enemyDrop != null)
            {
                lootBoxPrefab = enemyDrop.BoxPrefab;
            }
        }

        if (lootBoxPrefab == null)
        {
            Debug.LogWarning("[RespawnManager] lootBoxPrefab을 찾을 수 없음 — Inspector에서 직접 연결하세요.");
            return;
        }

        // 인벤토리 전체 수거 & 클리어
        List<(int itemId, int amount)> items = inv.TakeAll();
        if (items.Count == 0) return;   // 빈 인벤토리면 드롭 없음

        var go = Instantiate(lootBoxPrefab, ResolveDropPosition(), Quaternion.identity);

        var lootBox = go.GetComponentInChildren<LootBox>(true);
        if (lootBox != null)
            lootBox.Initialize(items);
        else
            Debug.LogWarning("[RespawnManager] 생성된 LootBox 프리팹에 LootBox 컴포넌트가 없습니다.");
    }

    void OnDestroy()
    {
        _player.Stat.OnDead -= HandleDead;
        SaveSlotManager.Instance?.UnregisterListener(this);
    }
}