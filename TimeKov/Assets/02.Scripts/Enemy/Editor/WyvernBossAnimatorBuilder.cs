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
    const string BossPrefab = "Assets/05.Prefabs/Enemy/Enemy_Wyvern_Boss.prefab";

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

        // 공중 다이브 시퀀스 = 컨트롤러(DiveSlam)가 단계별로 직접 CrossFade. 자동복귀 없음(착지만 Idle 복귀).
        var diveSeq = new (string state, string clip)[]
        {
            ("TakeOff",  "Wyvern_TakeOff"),            // 이륙(상승)
            ("FlyHover", "Wyvern_FlyStationary"),      // 제자리 비행(체공/조준)
            ("DiveFall", "Wyvern_Falling"),            // 급강하
        };
        foreach (var (stateName, clipName) in diveSeq)
            sm.AddState(stateName).motion = Clip(clipName);

        // 페이즈3 회복(웅크림/충전) - 자동복귀X, 컨트롤러(HealPhase)가 끝나면 Idle로 CrossFade. 클립 바꾸려면 여기.
        sm.AddState("Heal").motion = Clip("Wyvern_FlyStationaryRoar");

        var landing = sm.AddState("Landing");
        landing.motion = Clip("Wyvern_FlyStationaryToLanding");
        var landEx = landing.AddTransition(idle);
        landEx.hasExitTime = true; landEx.exitTime = 0.85f; landEx.duration = 0.15f;

        // 사망(터미널). EnemyFeedback.PlayDeath가 "Die" 상태를 직접 CrossFade.
        var die = sm.AddState("Die");
        die.motion = Clip("Wyvern_DeathHitTheGround");

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RelinkBossPrefab(ctrl);   // 재생성으로 guid 바뀜 -> 보스 프리팹 Animator 재연결(다른 배선 보존)

        Debug.Log($"[WyvernBossAnimator] 생성 완료: {CtrlPath}\n  states: Idle/Locomotion/Bite/Fireball/SpreadFire/Stinger/FinishBite/Roar/TakeOff/FlyHover/DiveFall/Heal/Landing/Die");
    }

    // 컨트롤러를 지우고 새로 만들면 guid가 바뀌어 보스 프리팹의 Animator 참조가 끊긴다.
    // -> 보스 프리팹을 열어 새 컨트롤러로 재연결(WyvernBossController의 VFX 등 다른 배선은 그대로 보존).
    static void RelinkBossPrefab(AnimatorController ctrl)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefab) == null) return;   // 최초 빌드(보스 프리팹 아직 없음)
        var root = PrefabUtility.LoadPrefabContents(BossPrefab);
        if (root == null) { Debug.LogWarning($"[WyvernBossAnimator] 보스 프리팹 로드 실패(애니 재연결 스킵): {BossPrefab}"); return; }
        var anim = root.GetComponentInChildren<Animator>(true);
        if (anim != null) anim.runtimeAnimatorController = ctrl;
        PrefabUtility.SaveAsPrefabAsset(root, BossPrefab);
        PrefabUtility.UnloadPrefabContents(root);
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
