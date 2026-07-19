using UnityEngine;

/// <summary>
/// VFX가 작게 시작해 지정 시간 동안 목표 크기로 '차오르게' 한다.
/// 원거리 공격 전조에 붙여 "기를 모으다 발사"로 읽히게 하는 용도(기존 스크립트 수정 없이 런타임 부착).
/// </summary>
public class FieldMonsterVfxGrow : MonoBehaviour
{
    private Vector3 from, to;
    private float dur, t;

    public void Play(Vector3 fromScale, Vector3 toScale, float duration)
    {
        from = fromScale;
        to = toScale;
        dur = Mathf.Max(0.01f, duration);
        t = 0f;
        transform.localScale = from;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / dur);
        // 살짝 가속(ease-in)해서 마지막에 '팍' 차오르는 느낌
        k = k * k;
        transform.localScale = Vector3.LerpUnclamped(from, to, k);
        if (t >= dur) enabled = false;
    }
}
