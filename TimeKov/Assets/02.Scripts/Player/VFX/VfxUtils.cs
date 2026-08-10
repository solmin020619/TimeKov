using System.Collections.Generic;
using UnityEngine;

public static class VfxUtils
{
    // ─────────────────────────────────────────────────────────
    // 액션 VFX 추적 (대쉬 캔슬 대응)
    //
    // 대쉬로 공격/스킬을 끊으면 모션과 코루틴은 멈추지만, 이미 뿌려진 이펙트는
    // 자기 수명이 끝날 때까지 그대로 남아 "안 나간 공격의 이펙트만 날아가는" 그림이 된다.
    // 그래서 시전 중 '그 시전자가' 낸 VFX 만 모아 두고, 끊길 때 한 번에 거둔다.
    //
    // ★소유자(caster)로 거르므로 같은 순간 다른 적이 낸 VFX 는 영향을 받지 않는다.
    // ★타격 지점 이펙트(SpawnAtHit)는 추적하지 않는다 — 이미 맞은 타격의 흔적이라 지우면 안 된다.
    // ─────────────────────────────────────────────────────────
    private static GameObject _trackOwner;
    private static readonly List<GameObject> _tracked = new List<GameObject>();

    /// 액션 시작 — 이 시전자가 내는 VFX 를 모으기 시작한다.
    public static void BeginTracking(GameObject owner)
    {
        _trackOwner = owner;
        _tracked.Clear();
    }

    /// 액션 정상 종료 — 추적만 끊는다(이미 나간 이펙트는 수명대로 남는다).
    public static void StopTracking()
    {
        _trackOwner = null;
        _tracked.Clear();
    }

    /// 액션 취소(대쉬 캔슬 등) — 모아 둔 이펙트를 즉시 거둔다.
    public static void CancelTracked()
    {
        for (int i = 0; i < _tracked.Count; i++)
        {
            var go = _tracked[i];
            if (go == null) continue;
            // 끄기만 한다. 풀에서 나온 인스턴스는 예정된 반납 코루틴이 그대로 회수하고,
            // 풀 미사용(폴백) 인스턴스는 예약된 Destroy 가 정리한다 — 어느 쪽이든 누수 없음.
            go.SetActive(false);
        }
        StopTracking();
    }

    // 시전자가 추적 대상일 때만 목록에 담는다.
    private static GameObject Track(GameObject caster, GameObject spawned)
    {
        if (spawned != null && caster != null && caster == _trackOwner) _tracked.Add(spawned);
        return spawned;
    }

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
        return Track(caster, Spawn(prefab, worldPos, root.rotation, parent, lifeTime));
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
        return Track(caster, Spawn(prefab, worldPos, worldRot, parent, lifeTime));
    }


    public static GameObject Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null,
        float lifeTime = 2f)
    {
        if (prefab == null) return null;

        // 풀 경유(시전마다 Instantiate/Destroy 제거). 풀 부재(앱 종료 등)면 기존 방식 폴백.
        var pool = VfxPool.I;
        if (pool != null)
            return pool.Spawn(prefab, position, rotation, parent, lifeTime);

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

        return Track(caster, Spawn(prefab, worldPos, t.rotation, parent, lifeTime));
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

        return Track(caster, Spawn(prefab, worldPos, worldRot, parent, lifeTime));
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