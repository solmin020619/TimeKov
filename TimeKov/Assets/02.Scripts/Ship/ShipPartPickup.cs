using System.Collections;
using UnityEngine;

// 맵에 배치하는 우주선 수리 부품(단일 종류). 플레이어가 가까이 가면 자동 회수(퍼즐 없음).
// 회수 시 ShipRepairManager 에 수량으로 쌓인다(인벤토리로 들어가지 않음).
// pickupIndex 는 씬 안에서 픽업마다 유니크하게(0~31) - 세이브 복원 후 중복 회수 방지 키.
public class ShipPartPickup : MonoBehaviour
{
    [Tooltip("이 픽업의 고유 번호(0~31). 씬 안에서 겹치면 안 됨 - 하나 먹으면 같은 번호는 다음 로드 때 같이 사라진다.")]
    [SerializeField] private int pickupIndex = 0;
    [Tooltip("회수 시 지급할 복구 에너지 개수. (extraForLevel 이 0 일 때만 쓰인다)")]
    [SerializeField] private int amount = 1;

    [Tooltip("★평소엔 0(복구 에너지 픽업) 그대로 둔다.\n\n" +
             "1 이상이면 '그 레벨 도달에 필요한 전용 추가 재료' 픽업이 된다(3=선체 보강재, 4=동력 안정기, 5=엔진).\n" +
             "다만 추가 재료는 원래 시간에너지 보상으로 나오는 것이라 맵에 깔 물건이 아니다. " +
             "테스트가 목적이면 이걸 쓰지 말고 개발자키 F5(다음 수리 재료 전부 지급)를 써라.\n" +
             "이 옵션은 나중에 추가 재료를 맵에서도 주고 싶어질 때를 위한 여지로만 남겨둔다.")]
    [SerializeField] private int extraForLevel = 0;
    [Tooltip("이 거리(m) 안으로 들어오면 자동 회수.")]
    [SerializeField] private float pickupRadius = 2.2f;

    [Header("흡수 연출")]
    [Tooltip("회수 시 플레이어 몸으로 빨려가는 트레일 VFX (예: VFX_Item_Trail_Unique). 비우면 제자리 소멸만.")]
    [SerializeField] private GameObject absorbTrailPrefab;
    [Tooltip("트레일이 플레이어에 닿기까지 걸리는 시간(초).")]
    [SerializeField] private float absorbFlyTime = 0.55f;
    [Tooltip("트레일 출발 높이 보정(m) - 픽업 VFX 중심 높이에 맞춘다.")]
    [SerializeField] private float absorbSpawnHeight = 0.5f;

    [Header("사라짐 효과")]
    [SerializeField] private float vanishDuration = 0.6f;

    private Transform _player;
    private bool _taken;

    private void Start()
    {
        // 이미 회수한 픽업이면 씬에서 조용히 제거(세이브 복원 대응).
        var mgr = ShipRepairManager.Instance;
        if (mgr != null && mgr.IsPickupTaken(pickupIndex))
        {
            Destroy(gameObject);
            return;
        }

        var p = FindFirstObjectByType<Player>();
        if (p != null) _player = p.transform;
    }

    private void Update()
    {
        if (_taken || _player == null) return;
        if (Vector3.Distance(transform.position, _player.position) <= pickupRadius)
            Collect();
    }

    private void Collect()
    {
        _taken = true;

        GameSfx.Play(SfxId.ShipPartPickup, transform.position);   // 우주선 부품 전용 획득음(3D)

        var mgr = ShipRepairManager.Instance;
        if (mgr != null)
        {
            // 회수 기록(중복 방지)은 두 종류가 공통으로 pickupIndex 를 쓴다.
            // 지급 내용만 갈린다: 복구 에너지 N개 / 레벨 전용 추가 재료 1개.
            if (extraForLevel > 0)
            {
                if (!mgr.IsPickupTaken(pickupIndex))
                {
                    mgr.MarkPickupTaken(pickupIndex);
                    mgr.CollectExtraPart(extraForLevel);
                }
            }
            else
            {
                mgr.CollectPickup(pickupIndex, amount);
            }
        }

        // 흡수 트레일: 픽업 위치 -> 플레이어 몸 중앙 (적 시간흡수와 동일한 LootBoxCollectFlyer 재사용)
        if (absorbTrailPrefab != null && _player != null)
        {
            Vector3 from = transform.position + Vector3.up * absorbSpawnHeight;
            var vfx = Instantiate(absorbTrailPrefab, from, Quaternion.identity);
            var flyer = vfx.GetComponent<LootBoxCollectFlyer>();
            if (flyer == null) flyer = vfx.AddComponent<LootBoxCollectFlyer>();
            flyer.Begin(_player, absorbFlyTime, GetPlayerCenterY(), null, 0f);
        }

        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        // VFX 전용 픽업 대응: 새 파티클 방출은 끊고(기존 입자는 자연 소멸) 스케일 축소와 함께 사라지게
        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        StartCoroutine(Vanish());
    }

    // 플레이어 transform 원점 대비 콜라이더 중앙의 y 오프셋 (피벗이 발이어도 몸 중앙으로 빨려들게)
    private float GetPlayerCenterY()
    {
        var col = _player != null ? _player.GetComponentInChildren<Collider>() : null;
        return col != null ? col.bounds.center.y - _player.position.y : 0.9f;
    }

    private IEnumerator Vanish()
    {
        Vector3 s0 = transform.localScale;
        float t = 0f;
        while (t < vanishDuration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(s0, Vector3.zero, t / vanishDuration);
            yield return null;
        }
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, $"Ship Part #{pickupIndex} x{amount}");
    }
#endif
}
