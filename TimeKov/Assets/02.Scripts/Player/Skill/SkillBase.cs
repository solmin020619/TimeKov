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
    [Tooltip("체크하면 뼈 대신 캐릭터 발밑(root)에서 스폰한다.\n" +
             "★바닥에 원이 그려지는 광역 VFX 는 반드시 켜라. 이 프리팹들은 원점이 지면(y=0)이라고 보고\n" +
             "만들어져 있어서, 손 뼈에서 스폰하면 원이 1m 넘게 떠오르고 스윙 중 손을 따라 흔들린다\n" +
             "= 판정 원(발밑 기준)과 중심이 어긋난다. 검 궤적처럼 손에서 나야 하는 것만 끈다.")]
    public bool CastVfxAtRoot = false;
    // 스폰 기준 뼈대 (RightHand = 검을 쥔 손 위치와 일치). CastVfxAtRoot 가 켜져 있으면 무시된다.
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

    /// <summary>
    /// SkillData 시트 값을 이 스킬에 적용한다(SheetStatOverride 가 로드 직후 호출).
    ///
    /// 스킬마다 타격 구성이 달라서(2히트 / 회전4+점프1) 여기서 각자 해석한다.
    /// 공통인 쿨타임만 여기서 처리하고, 피해/반경은 하위 클래스가 맡는다.
    /// ★타격 타이밍(Hit1Time 등)은 애니 클립에 맞춘 값이라 시트에 없다. 건드리지 않는다.
    /// </summary>
    public virtual void ApplySheetValues(SkillDataSheetData row)
    {
        CoolTime = row.coolTime;
    }

    // ─────────────────────────────────────────────────────────────
    // VFX 헬퍼 (하위 클래스에서 호출)
    // ─────────────────────────────────────────────────────────────

    /// <summary>시전 시작 시 VFX 스폰 (뼈대 기준)</summary>
    protected void SpawnCastVfx(GameObject caster)
    {
        if (CastVfxAtRoot)
        {
            // 발밑 기준. 바닥 원 광역기는 이쪽이어야 판정 원과 중심이 같다.
            VfxUtils.SpawnAtCaster(
                CastVfxPrefab,
                caster,
                CastVfxOffset,
                CastVfxRotation,
                CastVfxLifeTime,
                CastVfxFollowCaster
            );
            return;
        }

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
