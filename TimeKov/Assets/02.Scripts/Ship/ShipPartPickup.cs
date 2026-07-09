using System.Collections;
using UnityEngine;

// 맵에 배치하는 우주선 수리 부품. 플레이어가 가까이 가면 자동 회수(퍼즐 없음).
// 회수 시 ShipRepairManager 에 해당 레벨 부품을 표시(인벤토리로 들어가지 않고 우주선에 모임).
public class ShipPartPickup : MonoBehaviour
{
    [Tooltip("이 부품이 해금하는 수리 레벨(2~5). Lv.N 도달용 부품.")]
    [SerializeField] private int targetLevel = 2;
    [Tooltip("이 거리(m) 안으로 들어오면 자동 회수.")]
    [SerializeField] private float pickupRadius = 2.2f;

    [Header("사라짐 효과")]
    [SerializeField] private float vanishDuration = 0.6f;

    private Transform _player;
    private bool _taken;

    private void Start()
    {
        // 이미 회수/사용된 부품이면 씬에서 조용히 제거(세이브 복원 대응).
        var mgr = ShipRepairManager.Instance;
        if (mgr != null && (mgr.IsPartCollected(targetLevel) || mgr.IsPartUsed(targetLevel)))
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
        ShipRepairManager.Instance?.CollectPart(targetLevel);

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
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, $"Ship Part -> Lv.{targetLevel}");
    }
#endif
}
