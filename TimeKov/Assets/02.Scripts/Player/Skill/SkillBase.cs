using System.Collections;
using UnityEngine;

public abstract class SkillBase : ScriptableObject
{
    public SkillSheetId SkillSheetId;
    public Sprite SkillIcon;
    public float CoolTime;

    // ─────────────────────────────────────────────────────────────
    // VFX 설정
    // ─────────────────────────────────────────────────────────────
    [Header("VFX - 시전 이펙트")]
    public GameObject CastVfxPrefab;
    // 스폰 기준 뼈대 (RightHand = 검을 쥔 손 위치와 일치)
    public HumanBodyBones CastVfxBone = HumanBodyBones.RightHand;
    public Vector3 CastVfxOffset   = new Vector3(0f, 0f, 0.3f);
    public Vector3 CastVfxRotation = Vector3.zero;
    public float   CastVfxLifeTime = 2f;
    // true = 캐릭터 이동에 따라 이펙트도 함께 이동
    public bool    CastVfxFollowCaster = false;

    [Header("VFX - 피격 이펙트")]
    public GameObject HitVfxPrefab;
    public Vector3    HitVfxOffset   = new Vector3(0f, 1f, 0f);
    public float      HitVfxLifeTime = 1.5f;

    // ─────────────────────────────────────────────────────────────
    // 공개 API
    // ─────────────────────────────────────────────────────────────
    public abstract IEnumerator ExecuteRoutine(GameObject caster);
    public virtual void OnInterrupt(GameObject caster) { }

    // ─────────────────────────────────────────────────────────────
    // VFX 헬퍼 (하위 클래스에서 호출)
    // ─────────────────────────────────────────────────────────────

    /// <summary>시전 시작 시 VFX 스폰 (뼈대 기준)</summary>
    protected void SpawnCastVfx(GameObject caster)
    {
        VfxUtils.SpawnAtBone(
            CastVfxPrefab,
            caster,
            CastVfxBone,
            CastVfxOffset,
            CastVfxRotation,
            CastVfxLifeTime,
            CastVfxFollowCaster
        );
    }

    /// <summary>피격 시 VFX 스폰</summary>
    protected void SpawnHitVfx(Collider hit)
    {
        VfxUtils.SpawnAtHit(HitVfxPrefab, hit, HitVfxOffset, HitVfxLifeTime);
    }
}
