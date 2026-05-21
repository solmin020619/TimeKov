using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 적 공통 Animator Controller (Base) 자동 생성.
/// 6 state (Idle/Locomotion/Attack/Hit/Detect/Die) + 5 parameter + transition.
/// 모션 클립은 사용자가 인스펙터에서 직접 드래그.
/// 메뉴: Tools > Enemy > Build Base Animator Controller (6 state)
/// </summary>
public static class EnemyAnimatorControllerBuilder
{
    const string OutPath = "Assets/04.Animations/AnimationController/Enemy/EnemyBase.controller";

    [MenuItem("Tools/Enemy/Build Base Animator Controller (6 state)")]
    public static void Build()
    {
        EnsureFolder("Assets/04.Animations", "AnimationController");
        EnsureFolder("Assets/04.Animations/AnimationController", "Enemy");

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OutPath) != null)
        {
            bool ok = EditorUtility.DisplayDialog(
                "EnemyBase.controller 덮어쓰기",
                $"{OutPath} 이미 존재.\n덮어쓰면 기존 셋업 사라짐.\n계속?",
                "덮어쓰기", "취소");
            if (!ok) return;
            AssetDatabase.DeleteAsset(OutPath);
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(OutPath);

        // Parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Detect", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;

        // States
        var idle = sm.AddState("Idle", new Vector3(300, 100, 0));
        var locomotion = sm.AddState("Locomotion", new Vector3(300, 200, 0));
        var attack = sm.AddState("Attack", new Vector3(550, 50, 0));
        var hit = sm.AddState("Hit", new Vector3(550, 150, 0));
        var detect = sm.AddState("Detect", new Vector3(550, 250, 0));
        var die = sm.AddState("Die", new Vector3(550, 350, 0));

        sm.defaultState = idle;

        // 빈 dummy 클립 6개 — Override Controller가 매핑하려면 base에 motion clip이 있어야 함
        // 각 state별 placeholder. Override에서 적별 클립으로 교체됨.
        idle.motion = CreateOrGetDummyClip("_Base_Idle");
        locomotion.motion = CreateOrGetDummyClip("_Base_Locomotion");
        attack.motion = CreateOrGetDummyClip("_Base_Attack");
        hit.motion = CreateOrGetDummyClip("_Base_Hit");
        detect.motion = CreateOrGetDummyClip("_Base_Detect");
        die.motion = CreateOrGetDummyClip("_Base_Die");

        // Locomotion 이동 (Speed > 0.1)
        AddTransition(idle, locomotion, "Speed", AnimatorConditionMode.Greater, 0.1f, hasExit: false, duration: 0.15f);
        AddTransition(locomotion, idle, "Speed", AnimatorConditionMode.Less, 0.1f, hasExit: false, duration: 0.15f);

        // Any State → Trigger 기반 전환 (즉시)
        AddAnyTransition(sm, attack, "Attack", duration: 0.05f);
        AddAnyTransition(sm, hit, "Hit", duration: 0.05f);
        AddAnyTransition(sm, detect, "Detect", duration: 0.05f);
        AddAnyTransition(sm, die, "Die", duration: 0.05f);

        // 액션 state → Idle (exit time)
        AddExitTransition(attack, idle, exitTime: 0.85f, duration: 0.1f);
        AddExitTransition(hit, idle, exitTime: 0.85f, duration: 0.1f);
        AddExitTransition(detect, idle, exitTime: 0.85f, duration: 0.1f);
        // Die는 종료 transition 없음 (사망 후 멈춤. EnemyHealth가 deathAnimDuration 후 GameObject Destroy)

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[EnemyAnimatorControllerBuilder] 생성 완료: {OutPath}\n" +
            "다음 단계:\n" +
            "1. Animator 창에서 각 State 클릭 → Motion 슬롯에 클립 드래그\n" +
            "   - Idle: 적별 Idle 클립 (예: Skeleton Knight_Idle1Handed.anim)\n" +
            "   - Locomotion: Walk 클립\n" +
            "   - Attack: 기본 공격 클립\n" +
            "   - Hit: GetHit 클립\n" +
            "   - Detect: Roar/Howl/WakesUp 등 (적별 차이)\n" +
            "   - Die: Death 클립\n" +
            "2. TestEnemy.prefab의 Animator 컴포넌트의 Controller 슬롯에 이 controller 드래그\n" +
            "3. 10마리에 적용 시: 각 적별 Override Controller 생성 (다음 단계 자동화 가능)");
    }

    // ----- Helpers -----
    static void AddTransition(AnimatorState from, AnimatorState to, string param, AnimatorConditionMode mode, float threshold, bool hasExit, float duration)
    {
        var t = from.AddTransition(to);
        t.AddCondition(mode, threshold, param);
        t.hasExitTime = hasExit;
        t.duration = duration;
    }

    static void AddAnyTransition(AnimatorStateMachine sm, AnimatorState to, string trigger, float duration)
    {
        var t = sm.AddAnyStateTransition(to);
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        t.hasExitTime = false;
        t.duration = duration;
        t.canTransitionToSelf = false;
    }

    static void AddExitTransition(AnimatorState from, AnimatorState to, float exitTime, float duration)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = exitTime;
        t.duration = duration;
    }

    /// <summary>
    /// Override Controller가 매핑할 수 있게 base controller의 각 state에 박을 dummy AnimationClip.
    /// 빈 클립이지만 이름으로 식별. Override Controller에서 적별 클립으로 교체됨.
    /// </summary>
    static AnimationClip CreateOrGetDummyClip(string clipName)
    {
        const string dummyFolder = "Assets/04.Animations/AnimationController/Enemy/_BaseDummies";
        EnsureFolder("Assets/04.Animations/AnimationController/Enemy", "_BaseDummies");

        string path = $"{dummyFolder}/{clipName}.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) return existing;

        var clip = new AnimationClip();
        clip.name = clipName;
        clip.legacy = false;
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(parent, name);
    }
}
