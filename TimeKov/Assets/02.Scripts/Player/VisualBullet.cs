using UnityEngine;

public class VisualBullet : MonoBehaviour
{
    private Vector3 dir;
    private float speed;
    private float lifeTime;

    private bool hasTarget;
    private Vector3 targetPoint;

    private float t;

    // playerWeaponController에서 초기화

    public void Init(Vector3 origin,Vector3 dir,float speed,float lifeTime,Vector3? hitPoint)
    {
        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(dir);

        this.dir = dir.normalized;
        this.speed = Mathf.Max(0.01f,speed);
        this.lifeTime = Mathf.Max(0.01f, lifeTime);

        if (hitPoint.HasValue)
        {
            hasTarget = true;
            targetPoint = hitPoint.Value;
        }
        else
        {
            hasTarget = false;
        }

        t = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        t += dt;

        // 기본 수명
        if(t >= lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        // 직선 이동
        transform.position += dir * speed * dt;

        // 목표 지점(명중점)이 있으면 그 근처 도달시 삭제
        if (hasTarget)
        {
            // 한 프레임에 목표를 지나칠 수 있으니 "가까워지면" 삭제 (터널링 방지)
            float sqr = (transform.position - targetPoint).sqrMagnitude;
            if(sqr <= 0.01f) // 10cm 정도
            {
                Destroy(gameObject);
                return;
            }
        }
    }
}
