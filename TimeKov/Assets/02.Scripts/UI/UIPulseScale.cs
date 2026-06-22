using UnityEngine;

// 미세 스케일 펄스 - "지금 누를 수 있다"는 주의 환기용.
// 도감/설정 등 timeScale=0 정지 UI 위에서도 움직여야 하므로 unscaledDeltaTime을 쓴다.
// OnEnable에서 기준 스케일을 잡고, OnDisable에서 원복(레이아웃에 영향 없게 localScale만 건드림).
public class UIPulseScale : MonoBehaviour
{
    public float amplitude = 0.04f;   // 1.0 기준 +- 비율
    public float speed = 3.2f;        // 라디안/초

    private Vector3 _base = Vector3.one;
    private float _t;

    void OnEnable()
    {
        _base = transform.localScale;
        _t = 0f;
    }

    void Update()
    {
        _t += Time.unscaledDeltaTime * speed;
        float s = 1f + Mathf.Sin(_t) * amplitude;
        transform.localScale = _base * s;
    }

    void OnDisable()
    {
        transform.localScale = _base;
    }
}
