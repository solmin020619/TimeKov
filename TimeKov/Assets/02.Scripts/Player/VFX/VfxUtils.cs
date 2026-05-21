using UnityEngine;

public static class VfxUtils
{
    // ─────────────────────────────────────────────────────────
    // 뼈대 기준 스폰
    //
    // 위치(origin) = 지정한 뼈 월드 위치 → 실제 검 위치와 일치
    // 회전(worldRot) = root.rotation (캐릭터 공격 방향)
    //   → Hovl Studio 등 슬래시 VFX 는 Z+ 가 "앞면"이므로
    //     root.rotation 그대로 써야 공격 방향과 이펙트가 정렬됨
    //   → boneTrans.rotation 을 쓰면 손뼈의 복잡한 내부 축이
    //     그대로 VFX 에 적용되어 뒤집히거나 옆을 향하게 됨
    //
    // localOffset = root 축 기준 추가 위치 오프셋
    // eulerOffset = root.rotation 위에 추가 회전 (인스펙터 미세 조정용)
    // ─────────────────────────────────────────────────────────
    public static GameObject SpawnAtBone(
        GameObject prefab,
        GameObject caster,
        HumanBodyBones bone,
        Vector3 localOffset,
        float lifeTime = 2f,
        bool parentToCaster = false)
    {
        if (prefab == null || caster == null) return null;

        Transform root = caster.transform;
        var anim = caster.GetComponentInChildren<Animator>();
        Transform boneTrans = anim != null ? anim.GetBoneTransform(bone) : null;

        // 위치: 뼈 기준 (정확한 검 위치), 없으면 root 폴백
        Vector3 origin = boneTrans != null ? boneTrans.position : root.position;

        // offset: root(캐릭터) 축 기준으로 적용
        Vector3 worldPos = origin
            + root.forward * localOffset.z
            + root.right   * localOffset.x
            + root.up      * localOffset.y;

        // 회전: root.rotation (캐릭터 공격 방향 = Z+ 앞면과 정렬)
        Transform parent = parentToCaster ? root : null;
        return Spawn(prefab, worldPos, root.rotation, parent, lifeTime);
    }

    public static GameObject SpawnAtBone(
        GameObject prefab,
        GameObject caster,
        HumanBodyBones bone,
        Vector3 localOffset,
        Vector3 eulerOffset,
        float lifeTime = 2f,
        bool parentToCaster = false)
    {
        if (prefab == null || caster == null) return null;

        Transform root = caster.transform;
        var anim = caster.GetComponentInChildren<Animator>();
        Transform boneTrans = anim != null ? anim.GetBoneTransform(bone) : null;

        Vector3 origin = boneTrans != null ? boneTrans.position : root.position;

        Vector3 worldPos = origin
            + root.forward * localOffset.z
            + root.right   * localOffset.x
            + root.up      * localOffset.y;

        // eulerOffset: root 기준 추가 회전 (슬래시 각도 미세 조정)
        Quaternion worldRot = root.rotation * Quaternion.Euler(eulerOffset);

        Transform parent = parentToCaster ? root : null;
        return Spawn(prefab, worldPos, worldRot, parent, lifeTime);
    }


    public static GameObject Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null,
        float lifeTime = 2f)
    {
        if (prefab == null) return null;

        GameObject obj = Object.Instantiate(prefab, position, rotation, parent);

        if (lifeTime > 0f)
            Object.Destroy(obj, lifeTime);

        return obj;
    }

    public static GameObject SpawnAtCaster(
        GameObject prefab,
        GameObject caster,
        Vector3 offset,
        float lifeTime = 2f,
        bool parentToCaster = false)
    {
        if (prefab == null || caster == null) return null;

        Transform t = caster.transform;

        Vector3 worldPos =
            t.position +
            t.forward * offset.z +
            t.right * offset.x +
            t.up * offset.y;

        Transform parent = parentToCaster ? t : null;

        return Spawn(prefab, worldPos, t.rotation, parent, lifeTime);
    }

    public static GameObject SpawnAtCaster(
        GameObject prefab,
        GameObject caster,
        Vector3 offset,
        Vector3 eulerOffset,
        float lifeTime = 2f,
        bool parentToCaster = false)
    {
        if (prefab == null || caster == null) return null;

        Transform t = caster.transform;

        Vector3 worldPos =
            t.position +
            t.forward * offset.z +
            t.right * offset.x +
            t.up * offset.y;

        Quaternion worldRot = t.rotation * Quaternion.Euler(eulerOffset);

        Transform parent = parentToCaster ? t : null;

        return Spawn(prefab, worldPos, worldRot, parent, lifeTime);
    }

    public static GameObject SpawnAtHit(
        GameObject prefab,
        Collider hit,
        Vector3 offset,
        float lifeTime = 2f)
    {
        if (prefab == null || hit == null) return null;

        Vector3 pos = hit.bounds.center + offset;
        Quaternion rot = Quaternion.identity;

        return Spawn(prefab, pos, rot, null, lifeTime);
    }

    public static GameObject SpawnAtHit(
        GameObject prefab,
        Collider hit,
        Vector3 offset,
        Vector3 eulerOffset,
        float lifeTime = 2f)
    {
        if (prefab == null || hit == null) return null;

        Vector3 pos = hit.bounds.center + offset;
        Quaternion rot = Quaternion.Euler(eulerOffset);

        return Spawn(prefab, pos, rot, null, lifeTime);
    }
}