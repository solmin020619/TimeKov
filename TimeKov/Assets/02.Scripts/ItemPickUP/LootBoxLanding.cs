using UnityEngine;

// 떨어진 박스가 공중에 머무르지 않고 바닥으로 떨어지게 한다.
public class LootBoxLanding : MonoBehaviour
{
    [Tooltip("바닥에서 이 높이까지 올린 뒤 떨어뜨린다 (m)")]
    [SerializeField] private float dropHeight = 1.6f;

    [Tooltip("낙하 가속도")]
    [SerializeField] private float gravity = 22f;

    [Tooltip("박스가 바닥에 박혀 보이면 이만큼 올려준다 (m)")]
    [SerializeField] private float restOffset = 0f;

    [Tooltip("바닥을 찾는 레이 최대 거리 (m)")]
    [SerializeField] private float groundRayDistance = 50f;

    [Tooltip("바닥으로 인정할 레이어 (Ground). 비워두면 박스/몬스터/플레이어만 빼고 다 검사")]
    [SerializeField] private LayerMask groundMask;

    private float _targetY;
    private float _velocity;
    private bool _falling;

    void Start()
    {
        _targetY = FindGroundY() + restOffset;

        Vector3 p = transform.position;
        p.y = _targetY + dropHeight;
        transform.position = p;

        _velocity = 0f;
        _falling = true;
    }

    void Update()
    {
        if (!_falling) return;

        _velocity += gravity * Time.deltaTime;

        Vector3 p = transform.position;
        p.y -= _velocity * Time.deltaTime;

        if (p.y <= _targetY)
        {
            p.y = _targetY;
            _falling = false;
        }
        transform.position = p;
    }

    // 박스 바로 아래 바닥의 Y를 찾는다.
    // groundMask 가 지정돼 있으면 그 레이어만, 아니면 박스/몬스터/플레이어를 빼고 검사.
    private float FindGroundY()
    {
        Vector3 from = transform.position + Vector3.up * 0.5f;

        int mask = groundMask.value != 0 ? groundMask.value : ~0;
        RaycastHit[] hits = Physics.RaycastAll(
            from, Vector3.down, groundRayDistance, mask, QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestY = transform.position.y;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;

            // 박스 자신
            if (col.transform.IsChildOf(transform)) continue;
            // 박스보다 위에 있는 건 바닥이 아니다
            if (hits[i].point.y > transform.position.y + 0.05f) continue;
            // 박스 / 몬스터 / 플레이어 위엔 얹히지 않는다
            if (col.GetComponentInParent<EnemyHealth>() != null) continue;
            if (col.GetComponentInParent<LootBox>() != null) continue;
            if (col.GetComponentInParent<Player>() != null) continue;

            if (!found || hits[i].point.y > bestY)
            {
                bestY = hits[i].point.y;
                found = true;
            }
        }
        return found ? bestY : transform.position.y;
    }
}
