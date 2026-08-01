using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 모래정령(사막 보스) 전용 AnimatorController 생성.
/// 얼음정령과 같은 구조지만 클립 세트가 다르다:
///   모래정령 = Cast1~5(캐스트 5종) + Parry1/2(카운터) + Fly(활공) + Death(단일 사망), 다리뼈 없는 부유형.
///
/// SandElementalBossController 가 상태 이름으로 CrossFade 해서 재생.
/// Idle <-> Locomotion 은 Speed float 로 전이(Walk/Run 은 BlendTree, 부유형이라 제자리 재생).
/// 사망(Die)은 EnemyFeedback.PlayDeath 가 "Die" 해시로 직접 재생.
/// 메뉴: Tools > Enemy > Build Sand Elemental Animator
/// </summary>
public static class SandElementalAnimatorBuilder
{
    const string ClipFolder = "Assets/00.창동에셋/몬스터/모래정령/Animations";
    public const string CtrlPath = "Assets/00.창동에셋/몬스터/모래정령/모래정령_Boss.controller";
    const string BossPrefab = "Assets/05.Prefabs/Enemy/Enemy_Sand_Elemental_Boss.prefab";

    [MenuItem("Tools/Enemy/Build Sand Elemental Animator")]
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

        // Locomotion = Walk/Run 을 Speed 로 섞는 BlendTree(부유형이라 발디딤이 아닌 몸통 스웨이).
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
            ("Attack1",    "Attack1"),      // 근접 A
            ("Attack2",    "Attack2"),      // 근접 B(광역/반격)
            ("CastWave",   "Cast1"),        // 모래 파도(전방 부채꼴)
            ("CastCoffin", "Cast2"),        // 지면 지목(감금/늪 공용)
            ("CastBig",    "Cast3"),        // 긴 채널(궁극)
            ("Roar",       "BattleRoar"),   // 페이즈 전환 포효
            ("Guard",      "Parry1"),       // 가드/패리(무적 후 반격)
            ("Stun",       "Stun"),         // 스태거(패턴 파훼 보상)
        };
        foreach (var (stateName, clipName) in oneShots)
        {
            var st = sm.AddState(stateName);
            st.motion = Clip(clipName);
            var ex = st.AddTransition(idle);
            ex.hasExitTime = true; ex.exitTime = 0.9f; ex.duration = 0.12f;
        }

        // 시퀀스 상태: 컨트롤러가 단계별로 직접 CrossFade 한다. 자동 복귀 없음.
        // 모래정령은 벤더가 예고/발사를 나눠놓지 않아 Cast4=예비 / Cast5=발사 로 짝지어 쓴다.
        var sequences = new (string state, string clip)[]
        {
            ("SpikeStart", "Cast4"),        // 가시 다발 예비동작
            ("Spike",      "Cast5"),        // 가시 다발 발사
            ("Fly",        "Fly"),          // 활공(다이브 상승/체공/낙하 공용) - 시그니처 포즈
            ("SandAbsorb", "Cower"),        // [07-30] 모래 흡수 회복 채널(웅크려 끌어모으는 포즈)
        };
        foreach (var (stateName, clipName) in sequences)
            sm.AddState(stateName).motion = Clip(clipName);

        // 사망(터미널). EnemyFeedback.PlayDeath 가 "Die" 상태를 직접 CrossFade.
        var die = sm.AddState("Die");
        die.motion = Clip("Death");

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        RelinkBossPrefab(ctrl);   // 재생성으로 guid 가 바뀌므로 프리팹 Animator 재연결

        Debug.Log($"[SandElementalAnimator] 생성 완료: {CtrlPath}\n" +
                  "  states: Idle/Locomotion(Walk+Run 블렌드)/Attack1/Attack2/CastWave/CastCoffin/CastBig/Roar/Guard/Stun/SpikeStart/Spike/Fly/SandAbsorb/Die");
    }

    // 컨트롤러를 지우고 새로 만들면 guid 가 바뀌어 프리팹의 Animator 참조가 끊긴다.
    // -> 프리팹을 열어 새 컨트롤러로 재연결(다른 배선은 그대로 보존).
    static void RelinkBossPrefab(AnimatorController ctrl)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefab) == null) return;   // 최초 빌드(프리팹 아직 없음)
        var root = PrefabUtility.LoadPrefabContents(BossPrefab);
        if (root == null) { Debug.LogWarning($"[SandElementalAnimator] 프리팹 로드 실패(애니 재연결 스킵): {BossPrefab}"); return; }
        var anim = root.GetComponentInChildren<Animator>(true);
        if (anim != null) anim.runtimeAnimatorController = ctrl;
        PrefabUtility.SaveAsPrefabAsset(root, BossPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }

    public static void EnsureBuilt()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath) == null) Build();
    }

    // 모래정령 클립은 FBX 내장(FBX 1개 = 클립 1개)이라 서브에셋에서 꺼낸다.
    // 실제 파일은 소문자 .fbx 지만 대문자도 폴백으로 시도. __preview__ 는 에디터 미리보기라 거른다.
    static AnimationClip Clip(string fbxName)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath($"{ClipFolder}/{fbxName}.fbx");
        if (all == null || all.Length == 0)
            all = AssetDatabase.LoadAllAssetsAtPath($"{ClipFolder}/{fbxName}.FBX");
        if (all == null || all.Length == 0)
        {
            Debug.LogWarning($"[SandElementalAnimator] FBX 없음: {ClipFolder}/{fbxName}.fbx");
            return null;
        }
        foreach (var a in all)
        {
            if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                return c;
        }
        Debug.LogWarning($"[SandElementalAnimator] FBX 안에 클립 없음: {ClipFolder}/{fbxName}.fbx");
        return null;
    }
}
