using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 화염보스(고정형) 전용 AnimatorController 생성. ★얼음/모래와 최대 차이 = 멀티레이어.
/// 원본은 4레이어 무마스크(body_/face_eyes_/face_mouse_/vortex_ 동시 재생) 구조 -> 그대로 재현한다.
///   각 레이어 클립이 서로 겹치지 않는(disjoint) 트랜스폼을 애니 + WriteDefaults=1 + 상위 레이어 default=빈 stay
///   -> Override 여도 상위가 자기 클립이 만진 프로퍼티만 덮고 나머지는 body 가 보임 = 동시 재생.
///
/// 벤더 데모(ActionCTRL Bool 구동)는 버리고 상태이름 + 레이어 인덱스 CrossFade 방식으로 새로 굽는다.
///   L0 Body = BossMotor.PlayState(레이어0) / L1~L3 = FireBossController.PlayLayer(state, layer).
/// 사망 = body_disappear 를 "Die" 상태로(EnemyHealth.PlayDeath 가 자동 CrossFade).
/// 클립은 .anim 파일(FBX 내장 아님).
/// 메뉴: Tools > Enemy > Build Fire Boss Animator
/// </summary>
public static class FireBossAnimatorBuilder
{
    const string ClipFolder = "Assets/00.창동에셋/몬스터/화염보스/Animations";
    public const string CtrlPath = "Assets/00.창동에셋/몬스터/화염보스/화염보스_Boss.controller";
    const string BossPrefab = "Assets/05.Prefabs/Enemy/Enemy_Fire_Boss.prefab";

    [MenuItem("Tools/Enemy/Build Fire Boss Animator")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath) != null)
            AssetDatabase.DeleteAsset(CtrlPath);

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        // 이동은 없지만 BossMotor.TickSpeedParam/EnemyFeedback 호환용으로 선언만.
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);

        // ---- L0 Body (base layer) ----
        var body = ctrl.layers[0].stateMachine;

        var appear = AddState(body, "Appear", Clip("body_appear"));
        body.defaultState = appear;
        var idle = AddState(body, "Idle", Clip("body_idle_001"));
        AddState(body, "IdleAlt", Clip("body_idle_002"));

        // Entry -> Appear -> Idle (등장 후 대기 허브)
        var toIdle = appear.AddTransition(idle);
        toIdle.hasExitTime = true; toIdle.exitTime = 0.95f; toIdle.duration = 0.2f;

        // 단발 상태: 끝나면(exitTime) Idle 복귀. 컨트롤러가 이름으로 CrossFade.
        var oneShots = new (string state, string clip)[]
        {
            ("Attack1", "body_attack_001"),   // 지면 배러지
            ("Attack2", "body_attack_002"),   // 파이어볼
            ("Attack3", "body_attack_003"),   // 유성비(시그니처)
            ("Attack4", "body_attack_004"),   // 근접 강타
            ("Attack5", "body_attack_005"),   // 전방 분출
            ("Attack6", "body_attack_006"),   // 궁극 브레스
            ("Roar",    "body_roar"),         // 포효
            ("Stunned", "body_stunned"),      // 스턴(옵션)
        };
        foreach (var (stateName, clipName) in oneShots)
        {
            var st = AddState(body, stateName, Clip(clipName));
            var ex = st.AddTransition(idle);
            ex.hasExitTime = true; ex.exitTime = 0.9f; ex.duration = 0.12f;
        }

        // 사망(터미널, 전이 없음). EnemyFeedback.PlayDeath 가 "Die" 를 직접 CrossFade.
        AddState(body, "Die", Clip("body_disappear"));

        // ---- L1 Face_Eyes (default = 빈 stay) ----
        var eyes = AddLayer(ctrl, "Face_Eyes");
        var eyesStay = AddState(eyes, "EyesStay", null);
        eyes.defaultState = eyesStay;
        AddState(eyes, "Eyes1", Clip("face_eyes_001"));
        AddState(eyes, "Eyes2", Clip("face_eyes_002"));
        AddState(eyes, "Eyes3", Clip("face_eyes_003"));
        AddState(eyes, "Eyes4", Clip("face_eyes_004"));

        // ---- L2 Face_Mouth (default = 빈 stay) ----
        var mouth = AddLayer(ctrl, "Face_Mouth");
        var mouthStay = AddState(mouth, "MouthStay", null);
        mouth.defaultState = mouthStay;
        AddState(mouth, "Mouth1", Clip("face_mouse_001"));
        AddState(mouth, "Mouth2", Clip("face_mouse_002"));
        AddState(mouth, "Mouth3", Clip("face_mouse_003"));

        // ---- L3 Vortex (하반신 소용돌이 상시 루프. 컨트롤러 안 건드림) ----
        var vortex = AddLayer(ctrl, "Vortex");
        var vortexState = AddState(vortex, "Vortex", Clip("vortex_rotation"));
        vortex.defaultState = vortexState;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RelinkBossPrefab(ctrl);

        Debug.Log($"[FireBossAnimator] 생성 완료: {CtrlPath}\n" +
                  "  L0 Body: Appear/Idle/IdleAlt/Attack1~6/Roar/Stunned/Die\n" +
                  "  L1 Face_Eyes(빈stay+Eyes1~4) / L2 Face_Mouth(빈stay+Mouth1~3) / L3 Vortex(상시)\n" +
                  "  ★확인: 4레이어 / 무마스크 / 상위3레이어 WriteDefaults / Face default=빈 stay / Vortex 단독 루프.");
    }

    // 새 레이어 = 새 StateMachine 서브에셋 + Override/무마스크/weight1.
    static AnimatorStateMachine AddLayer(AnimatorController ctrl, string name)
    {
        var sm = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(sm, ctrl);
        ctrl.AddLayer(new AnimatorControllerLayer
        {
            name = name,
            defaultWeight = 1f,
            stateMachine = sm,
            blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = null,
        });
        return sm;
    }

    // 무마스크 멀티레이어의 핵심 = WriteDefaults 반드시 ON(상위 레이어가 자기 클립 프로퍼티만 덮게).
    static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion motion)
    {
        var st = sm.AddState(name);
        st.motion = motion;
        st.writeDefaultValues = true;
        return st;
    }

    static void RelinkBossPrefab(AnimatorController ctrl)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefab) == null) return;   // 최초 빌드
        var root = PrefabUtility.LoadPrefabContents(BossPrefab);
        if (root == null) { Debug.LogWarning($"[FireBossAnimator] 프리팹 로드 실패(애니 재연결 스킵): {BossPrefab}"); return; }
        var anim = root.GetComponentInChildren<Animator>(true);
        if (anim != null) anim.runtimeAnimatorController = ctrl;
        PrefabUtility.SaveAsPrefabAsset(root, BossPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }

    public static void EnsureBuilt()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath) == null) Build();
    }

    static AnimationClip Clip(string name)
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/{name}.anim");
        if (c == null) Debug.LogWarning($"[FireBossAnimator] 클립 없음: {ClipFolder}/{name}.anim");
        return c;
    }
}
