using System.Collections;
using UnityEngine;

// 맵에 배치하는 우주선 수리 부품(단일 종류). 플레이어가 가까이 가면 자동 회수(퍼즐 없음).
// 회수 시 ShipRepairManager 에 수량으로 쌓인다(인벤토리로 들어가지 않음).
// pickupIndex 는 씬 안에서 픽업마다 유니크하게(0~31) - 세이브 복원 후 중복 회수 방지 키.
public class ShipPartPickup : MonoBehaviour
{
    [Tooltip("이 픽업의 고유 번호(0~31). 씬 안에서 겹치면 안 됨 - 하나 먹으면 같은 번호는 다음 로드 때 같이 사라진다.")]
    [SerializeField] private int pickupIndex = 0;
    [Tooltip("회수 시 지급할 부품 개수.")]
    [SerializeField] private int amount = 1;
    [Tooltip("이 거리(m) 안으로 들어오면 자동 회수.")]
    [SerializeField] private float pickupRadius = 2.2f;

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
        ShipRepairManager.Instance?.CollectPickup(pickupIndex, amount);

        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        StartCoroutine(Vanish());
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
