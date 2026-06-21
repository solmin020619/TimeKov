using UnityEngine;

// 적이 죽으면 흡수 이펙트가 플레이어(코어) 몸쪽으로 빨려들어가며 체력(=시간)을 회복한다.
// 회복량 = 플레이어 최대HP × 코어 흡수%(CoreUpgradeManager.GetLifestealPercent).
// 아이템 드롭(EnemyDropOnDeath)과는 별개 — 시간 회복은 줍지 않고 바로 흡수된다.
// 적 프리팹에 EnemyDropOnDeath와 나란히 부착해서 쓴다.
[RequireComponent(typeof(EnemyHealth))]
public class EnemyAbsorbOnDeath : MonoBehaviour
{
    [Tooltip("플레이어로 날아가는 흡수 VFX 프리팹(트레일/오브). 비우면 연출 없이 회복만 적용.")]
    [SerializeField] private GameObject absorbVfxPrefab;
    [Tooltip("흡수가 플레이어에 닿기까지 걸리는 시간(초)")]
    [SerializeField] private float flyTime = 0.55f;
    [Tooltip("플레이어 콜라이더 중앙 기준 흡수 지점 미세조정(m). +면 위, -면 아래. 0이면 정확히 몸 중앙.")]
    [SerializeField] private float centerYTweak = 0f;
    [Tooltip("적 가슴 높이 등 흡수 시작 지점 높이 보정(m)")]
    [SerializeField] private float spawnHeight = 1.0f;
    [Tooltip("CoreUpgradeManager가 없을 때 사용할 폴백 흡수 비율(최대HP 대비)")]
    [Range(0f, 1f)][SerializeField] private float fallbackLifestealPercent = 0.03f;
    [Tooltip("이 적 처치 시 추가 흡수 % (보스 등 고정 보너스. 코어 흡수%에 더해짐. 0=일반 적). 확장: 보스 외 특수몹에도 사용 가능.")]
    [Range(0f, 1f)][SerializeField] private float bonusHealPercent = 0f;

    private EnemyHealth _health;

    // 플레이어는 씬에 하나 — 죽을 때마다 Find 안 하도록 캐시
    private static Player _player;

    private void Awake() => _health = GetComponent<EnemyHealth>();

    private void OnEnable()
    {
        if (_health != null) _health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (_health != null) _health.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        Player player = GetPlayer();
        if (player == null || player.Stat == null) return;

        float pct = CoreUpgradeManager.Instance != null
            ? CoreUpgradeManager.Instance.GetLifestealPercent()
            : fallbackLifestealPercent;

        float heal = player.Stat.MaxHp * (pct + bonusHealPercent);   // 보스=큰 보너스로 대량 흡수
        if (heal <= 0f) return;

        // 플레이어가 살아있을 때만 회복 (닿는 순간 1회)
        void DoHeal()
        {
            if (player != null && player.Stat != null)
                player.Stat.Heal(heal);
        }

        if (absorbVfxPrefab == null)
        {
            DoHeal();   // 연출 프리팹 없으면 즉시 회복
            return;
        }

        // 흡수 VFX를 적 위치에서 스폰(적과 분리된 루트 — 적이 Destroy돼도 살아남음) → 플레이어 몸 중앙으로 흡수
        Vector3 from = transform.position + Vector3.up * spawnHeight;
        var vfx = Instantiate(absorbVfxPrefab, from, Quaternion.identity);

        var flyer = vfx.GetComponent<LootBoxCollectFlyer>();
        if (flyer == null) flyer = vfx.AddComponent<LootBoxCollectFlyer>();

        // 피벗이 발이든 중앙이든 항상 '몸 중앙'으로 흡수되도록 콜라이더 바운드 센터를 목표 높이로.
        // scatter=0 → 흩어지지 않고 정확히 중앙 한 점으로. flyer는 매 프레임 player.transform를
        // 추적하므로 죽이고 플레이어가 이동해도 따라가 중앙으로 빨려든다.
        float centerY = GetPlayerCenterYOffset(player) + centerYTweak;
        flyer.Begin(player.transform, flyTime, centerY, DoHeal, 0f);
    }

    // 플레이어 transform 원점 대비 콜라이더 중앙의 y 오프셋
    private static float GetPlayerCenterYOffset(Player player)
    {
        var cap = player.GetComponentInChildren<CapsuleCollider>();
        if (cap != null) return cap.bounds.center.y - player.transform.position.y;

        var col = player.GetComponentInChildren<Collider>();
        if (col != null) return col.bounds.center.y - player.transform.position.y;

        return 0.9f;   // 콜라이더 없을 때 폴백(대략 허리 높이)
    }

    private static Player GetPlayer()
    {
        if (_player == null) _player = FindAnyObjectByType<Player>();
        return _player;
    }
}
