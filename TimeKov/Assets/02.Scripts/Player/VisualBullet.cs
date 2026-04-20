using UnityEngine;

public class VisualBullet : MonoBehaviour
{
    private Vector3 dir;
    private float speed;
    private float lifeTime;
    private bool hasTarget;
    private Vector3 targetPoint;
    private float t;
    private PlayerWeaponController owner;

    public void SetOwner(PlayerWeaponController owner)
    {
        this.owner = owner;
    }

    public void Init(Vector3 origin, Vector3 dir, float speed, float lifeTime, Vector3? hitPoint)
    {
        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(dir);

        this.dir = dir.normalized;
        this.speed = Mathf.Max(0.01f, speed);
        this.lifeTime = Mathf.Max(0.01f, lifeTime);

        hasTarget = hitPoint.HasValue;
        if(hasTarget)
            targetPoint = hitPoint.Value;

        t = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        t += dt;

        if(t >= lifeTime)
        {
            Despawn();
            return;
        }

        Vector3 moveStep = dir * speed * dt;

        if (hasTarget)
        {
            float distToTarget = Vector3.Distance(transform.position, targetPoint);
            if (moveStep.magnitude >= distToTarget)
            {
                transform.position = targetPoint;
                Despawn();
                return;
            }
        }
        transform.position += moveStep;
    }

    private void Despawn()
    {
        if (owner != null)
            owner.ReturnBullet(this);
        else
            gameObject.SetActive(false);
    }
}