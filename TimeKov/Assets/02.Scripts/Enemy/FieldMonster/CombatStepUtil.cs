using UnityEngine;

/// <summary>
/// 전투 스텝(공격 사이 옆/뒤 이동) 계산 공용부. 신규 필드 몬스터 전용.
/// 스텝 중엔 타깃을 계속 바라본다 — 옆으로 걸으며 노려보는 그림(안 하면 문워크).
/// </summary>
public static class CombatStepUtil
{
    /// <summary>Random 계열을 구체 방향으로 확정. 스텝 시작 시 1회만 호출.</summary>
    public static CombatStepKind Resolve(CombatStepKind kind)
    {
        switch (kind)
        {
            case CombatStepKind.StrafeRandom:
                return Random.value < 0.5f ? CombatStepKind.StrafeLeft : CombatStepKind.StrafeRight;
            case CombatStepKind.BackOrStrafe:
                float r = Random.value;
                if (r < 0.34f) return CombatStepKind.BackStep;
                return r < 0.67f ? CombatStepKind.StrafeLeft : CombatStepKind.StrafeRight;
            default:
                return kind;
        }
    }

    /// <summary>확정된 kind 기준 월드 이동 방향(수평). 타깃 없으면 zero.</summary>
    public static Vector3 Direction(CombatStepKind kind, Transform self, Transform target)
    {
        if (self == null || target == null) return Vector3.zero;

        Vector3 fwd = target.position - self.position;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) return Vector3.zero;
        fwd.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, fwd);   // 타깃을 볼 때의 오른쪽

        switch (kind)
        {
            case CombatStepKind.StrafeLeft:  return -right;
            case CombatStepKind.StrafeRight: return right;
            case CombatStepKind.BackStep:    return -fwd;
            default:                         return Vector3.zero;
        }
    }

    /// <summary>기본값(정면 이동). 스텝이 끝나면 여기로 되돌린다.</summary>
    public static readonly Vector2 Forward = new Vector2(0f, 1f);

    /// <summary>
    /// Locomotion 2D 블렌드트리 좌표. x = StrafeDir(-1 좌 / +1 우), y = MoveDir(+1 정면 / -1 뒤).
    /// 거미 클립: 정면=walk6, 뒤=walk3, 좌=straif_L, 우=straif_R.
    /// </summary>
    public static Vector2 BlendParam(CombatStepKind kind)
    {
        switch (kind)
        {
            case CombatStepKind.StrafeLeft:  return new Vector2(-1f, 0f);
            case CombatStepKind.StrafeRight: return new Vector2( 1f, 0f);
            case CombatStepKind.BackStep:    return new Vector2( 0f, -1f);
            default:                         return Forward;
        }
    }
}
