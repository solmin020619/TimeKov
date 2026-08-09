using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 얼음정령(설산 보스) 전용 AnimatorController 생성.
/// 공유 EnemyBase 컨트롤러는 슬롯이 6개(Idle/Locomotion/Attack/Hit/Detect/Die)뿐이라
/// 클립 24종을 가진 보스를 못 담는다 -> 공격별 상태를 가진 전용 컨트롤러.
///
/// 와이번(WyvernBossAnimatorBuilder)과 같은 구조지만 클립 로드 방식이 다르다:
///   와이번 = .anim 파일 / 얼음정령 = FBX 내장 클립(FBX 1개 = 클립 1개).
///
/// IceElementalBossController 가 상태 이름으로 CrossFade 해서 재생.
/// Idle <-> Locomotion 은 Speed float 로 전이(Walk/Run 은 BlendTree 로 섞음).
/// 사망(Die)은 EnemyFeedback.PlayDeath 가 "Die" 해시로 직접 재생.
/// 메뉴: Tools > Enemy > Build Ice Elemental Animator
/// </summary>
public static class IceElementalAnimatorBuilder
{
    const string ClipFolder = "Assets/00.창동에셋/몬스터/얼음정령/Animations";
    public const string CtrlPath = "Assets/00.창동에셋/몬스터/얼음정령/얼음정령_Boss.controller";
    const string BossPrefab = "Assets/05.Prefabs/Enemy/Enemy_IceElemental_Boss.prefab";

    [MenuItem("Tools/TIMEKOV/적/보스/얼음 정령 애니메이터 생성")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath) != null)
            AssetDatabase.DeleteAsset(CtrlPath);

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        // Hit 트리거는 상태에 안 묶는다(보스 하이퍼아머 = 피격 경직 없음).
        // EnemyFeedback 이 SetTrigger("Hit") 를 부를 때 파라미터가 없으면 경고가 나므로 선언만 해둔다.
        ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;

        var idle = sm.AddState("Idle");
        idle.motion = Clip("Idle1");
        sm.defaultState = idle;

        // Locomotion = Walk/Run 을 Speed 로 섞는 BlendTree.
        // 와이번은 Walk 단일이었지만 얼음정령은 Run 이 따로 있어 섞는 게 자연스럽다.
        var loco = sm.AddState("Locomotion");
        var bt = new BlendTree
        {
            name = "LocoBlend",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Speed",
            useAutomaticThresholds = false,
        };
        AssetDatabase.AddObjectToAsset(bt, ctrl);   // BlendTree 는 컨트롤러의 서브에셋이어야 저장된다
        bt.AddChild(Clip("Walk"), 1.5f);
        bt.AddChild(Clip("Run"), 5f);
        loco.motion = bt;

        var toLoco = idle.AddTransition(loco);
        toLoco.hasExitTime = false; toLoco.duration = 0.12f;
        toLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        var toIdle = loco.AddTransition(idle);
        toIdle.hasExitTime = false; toIdle.duration = 0.12f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // 단발 상태: 컨트롤러가 이름으로 CrossFade, 끝나면(exitTime) Idle 복귀.
        var oneShots = new (string state, string clip)[]
        {
            ("Attack1",  "Attack1"),      // 찌르기 런지(hips 전진 68cm)
            ("Attack2",  "Attack2"),      // 광역 스윙
            ("CastNova", "Cast2"),        // 자기중심 시전(시전 중 떠오름)
            ("CastBig",  "Cast3"),        // 양팔 대시전(궁극)
            ("Roar",     "BattleRoar"),   // 페이즈 전환 포효
            ("Block",    "Block"),        // 가드
            ("Dodge",    "Dodge"),        // 회피(시각만 - 실제 이동은 코드가 밀어야 함)
            ("Stun",     "Stun"),         // 스태거(패턴 파훼 보상)
        };
        foreach (var (stateName, clipName) in oneShots)
        {
            var st = sm.AddState(stateName);
            st.motion = Clip(clipName);
            var ex = st.AddTransition(idle);
            ex.hasExitTime = true; ex.exitTime = 0.9f; ex.duration = 0.12f;
        }

        // 시퀀스 상태: 컨트롤러가 단계별로 직접 CrossFade 한다. 자동 복귀 없음.
        // 벤더가 텔레그래프(Start)와 발사(본체)를 이미 나눠놨다 = 예고->발동 리듬이 공짜.
        var sequences = new (string state, string clip)[]
        {
            ("BeamStart", "CastToTargetStart"),   // 표적 시전 준비(hips 115cm 젖힘 = 큰 예고)
            ("Beam",      "CastToTarget"),        // 표적 시전 발사(FrostRay)
            ("RainStart", "CastUpStart"),         // 위로 시전 준비(hips 84cm 치솟음)
            ("Rain",      "CastUp"),              // 하늘에서 낙하(SkyLine 계열)
            ("Fly",       "Fly"),                 // 활강 돌진(몸 65도 눕힌 포즈) - 시그니처
            ("Cower",     "Cower"),               // 웅크림(재결빙/무적 구간용)
        };
        foreach (var (stateName, clipName) in sequences)
            sm.AddState(stateName).motion = Clip(clipName);

        // 사망(터미널). EnemyFeedback.PlayDeath 가 "Die" 상태를 직접 CrossFade.
        // Death2 선택 이유: 스스로 지면 아래로 56cm 가라앉아 "녹아 스며듦"이 코드 0줄로 나온다.
        var die = sm.AddState("Die");
        die.motion = Clip("Death2");

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RelinkBossPrefab(ctrl);   // 재생성으로 guid 가 바뀌므로 프리팹 Animator 재연결

        Debug.Log($"[IceElementalAnimator] 생성 완료: {CtrlPath}\n" +
                  "  states: Idle/Locomotion(Walk+Run 블렌드)/Attack1/Attack2/CastNova/CastBig/Roar/Block/Dodge/Stun/BeamStart/Beam/RainStart/Rain/Fly/Cower/Die");
    }

    // 컨트롤러를 지우고 새로 만들면 guid 가 바뀌어 프리팹의 Animator 참조가 끊긴다.
    // -> 프리팹을 열어 새 컨트롤러로 재연결(다른 배선은 그대로 보존).
    static void RelinkBossPrefab(AnimatorController ctrl)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefab) == null) return;   // 최초 빌드(프리팹 아직 없음)
        var root = PrefabUtility.LoadPrefabContents(BossPrefab);
        if (root == null) { Debug.LogWarning($"[IceElementalAnimator] 프리팹 로드 실패(애니 재연결 스킵): {BossPrefab}"); return; }
        var anim = root.GetComponentInChildren<Animator>(true);
        if (anim != null) anim.runtimeAnimatorController = ctrl;
        PrefabUtility.SaveAsPrefabAsset(root, BossPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }

    public static void EnsureBuilt()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath) == null) Build();
    }

    // 얼음정령 클립은 FBX 내장(FBX 1개 = 클립 1개)이라 서브에셋에서 꺼내야 한다.
    // __preview__ 로 시작하는 건 에디터가 만든 미리보기 클립이라 걸러낸다.
    static AnimationClip Clip(string fbxName)
    {
        string path = $"{ClipFolder}/{fbxName}.FBX";
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        if (all == null || all.Length == 0)
        {
            Debug.LogWarning($"[IceElementalAnimator] FBX 없음: {path}");
            return null;
        }
        foreach (var a in all)
        {
            if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                return c;
        }
        Debug.LogWarning($"[IceElementalAnimator] FBX 안에 클립 없음: {path}");
        return null;
    }
}
