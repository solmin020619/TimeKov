using UnityEngine;

// 수집 VFX를 박스에서 플레이어 몸쪽으로 빨아들이듯 날린다.
// 여러 개가 한 점에 안 뭉치게 몸통 근처 각자 다른 지점을 향하고,
// 도착 직전 작아지며 몸 안으로 흡수되듯 사라진다.
public class LootBoxCollectFlyer : MonoBehaviour
{
    private const float LingerTime = 0.3f;   // 도착 후 트레일 꼬리가 사라질 여유

    private Transform _target;
    private Vector3 _start;
    private Vector3 _targetOffset;
    private Vector3 _baseScale;
    private float _duration;
    private float _elapsed;
    private float _linger;
    private bool _arrived;
    private System.Action _onArrived;   // 플레이어 몸에 닿는 순간 1회 호출 (예: 체력 흡수)

    public void Begin(Transform target, float flyTime, float targetHeight, System.Action onArrived = null, float scatter = 0.3f)
    {
        _target = target;
        _start = transform.position;
        _baseScale = transform.localScale;
        _duration = Mathf.Max(0.01f, flyTime);
        _elapsed = 0f;
        _linger = LingerTime;
        _arrived = false;
        _onArrived = onArrived;

        // 목표 지점 근처의 살짝씩 다른 지점 — 여러 개가 한 점에 안 뭉치게 (scatter=0이면 정확히 한 점)
        Vector3 r = Random.insideUnitSphere * scatter;
        _targetOffset = new Vector3(r.x, targetHeight + r.y, r.z);
    }

    void Update()
    {
        if (_arrived)
        {
            // 작아진 채로 잠깐 — 트레일 꼬리가 자연스럽게 사라지게
            _linger -= Time.deltaTime;
            if (_linger <= 0f) Destroy(gameObject);
            return;
        }

        if (_target == null)
        {
            Arrive();
            return;
        }

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        // 가속하며 플레이어 몸쪽으로 빨려든다
        Vector3 goal = _target.position + _targetOffset;
        transform.position = Vector3.Lerp(_start, goal, t * t);

        // 후반부에 작아지며 몸 안으로 흡수되는 느낌
        float shrink = t < 0.5f ? 1f : Mathf.InverseLerp(1f, 0.5f, t);
        transform.localScale = _baseScale * shrink;

        if (t >= 1f)
        {
            transform.localScale = Vector3.zero;
            Arrive();
        }
    }

    // 도착 처리: 1회만 onArrived 발화 후 linger 단계로
    void Arrive()
    {
        if (_arrived) return;
        _arrived = true;
        var cb = _onArrived;
        _onArrived = null;
        cb?.Invoke();
    }
}
