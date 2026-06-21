using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 와이번 보스 전용 AnimatorController 생성. 공유 EnemyBase는 Attack 상태가 하나뿐이라
/// 보스(물기/파이어볼/화염방사/꼬리침/강타/포효)를 못 담는다 -> 공격별 상태를 가진 전용 컨트롤러.
/// WyvernBossController가 상태 이름으로 CrossFade해서 재생. Idle/Locomotion은 Speed float로 전이.
/// 사망(Die)은 EnemyFeedback.PlayDeath가 "Die" 해시로 직접 재생.
/// 메뉴: Tools > Enemy > Build Wyvern Boss Animator
/// </summary>
public static class WyvernBossAnimatorBuilder
{
    const string ClipFolder = "Assets/03.Model/Enemy/10.Wyvern/Animations";
    public const string CtrlPath = "Assets/03.Model/Enemy/10.Wyvern/WyvernBoss.controller";

    [MenuItem("Tools/Enemy/Build Wyvern Boss Animator")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath) != null)
            AssetDatabase.DeleteAsset(CtrlPath);

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        // Hit 트리거는 상태에 안 묶음(보스 슈퍼아머=피격 경직 없음). EnemyFeedback.PlayDeath의 ResetTrigger("Hit") 경고 방지용.
        ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;

        var idle = sm.AddState("Idle");       idle.motion = Clip("Wyvern_Idle");
        var loco = sm.AddState("Locomotion"); loco.motion = Clip("Wyvern_Walk");
        sm.defaultState = idle;

        // Idle <-> Locomotion (NavMeshAgent 속도가 Speed로 들어옴)
        var toLoco = idle.AddTransition(loco);
        toLoco.hasExitTime = false; toLoco.duration = 0.1f;
        toLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        var toIdle = loco.AddTransition(idle);
        toIdle.hasExitTime = false; toIdle.duration = 0.1f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // 공격/포효 = 단발 상태. 컨트롤러가 이름으로 CrossFade, 끝나면(exitTime) Idle 복귀.
        var oneShots = new (string state, string clip)[]
        {
            ("Bite",       "Wyvern_SimpleBiteAttack"),
            ("Fireball",   "Wyvern_SpitFireball"),
            ("SpreadFire", "Wyvern_SpreadFire"),
            ("Stinger",    "Wyvern_StingerAttack"),
            ("FinishBite", "Wyvern_SpecialFinishBiteAttack"),
            ("Roar",       "Wyvern_FlyStationaryRoar"),
        };
        foreach (var (stateName, clipName) in oneShots)
        {
            var st = sm.AddState(stateName);
            st.motion = Clip(clipName);
            var ex = st.AddTransition(idle);
            ex.hasExitTime = true; ex.exitTime = 0.9f; ex.duration = 0.12f;
        }

        // 사망(터미널). EnemyFeedback.PlayDeath가 "Die" 상태를 직접 CrossFade.
        var die = sm.AddState("Die");
        die.motion = Clip("Wyvern_DeathHitTheGround");

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[WyvernBossAnimator] 생성 완료: {CtrlPath}\n  states: Idle/Locomotion/Bite/Fireball/SpreadFire/Stinger/FinishBite/Roar/Die");
    }

    public static void EnsureBuilt()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath) == null) Build();
    }

    static AnimationClip Clip(string clipName)
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/{clipName}.anim");
        if (c == null) Debug.LogWarning($"[WyvernBossAnimator] 클립 없음: {clipName}.anim");
        return c;
    }
}
