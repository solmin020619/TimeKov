#if UNITY_EDITOR
// =====================================================================
// JumpLayerBuilder.cs
// 점프 애니메이션을 '하체 전용 레이어'로 분리하는 도구.
//   Tools/TIMEKOV/애니메이션/점프를 하체 전용 레이어로 분리  (+ 되돌리기)
//
// [왜 분리하나]
//   이 캐릭터의 기본 자세는 등 뒤로 대검을 든 모습이다. 그런데 점프 클립은 몸 전체를
//   덮어써서, 뛰는 순간 검 든 팔이 풀리고 검이 엉뚱한 데로 간다.
//   하체만 점프 클립을 쓰고 상체는 원래 레이어를 그대로 두면, 검 자세를 유지한 채
//   다리만 점프 동작을 한다. 액션 게임의 표준 방식이다.
//
// [만드는 것]
//   Jump Layer      골반 + 다리 + 발IK   — 점프 동작의 본체
//   Jump Arm Layer  검 안 든 팔          — 가중치로 세기를 조절(마스크는 켜짐/꺼짐뿐이라
//                                          "조금만 올리기"가 안 되기 때문에 따로 뺐다)
//   그리고 Base Layer 에서 Jump 상태를 빼낸다(이동 블렌드 트리만 남는다).
//
//   ★레이어 기본 가중치는 0 이다. 1 로 두면 평소에도 Empty 상태가 그 부위를 덮어써서
//     걷기/달리기 동작이 사라진다. 실제 값은 PlayerAnimatorComponent 가 정한다.
//   ★두 레이어 각각 자기 상태 머신을 갖는다. Synced Layer 로 묶으면 자기 마스크가 아니라
//     원본 레이어의 마스크를 따라가서, 팔 레이어가 아무 반응도 하지 않는다.
//
// 여러 번 돌려도 안전하다 — 이미 되어 있으면 알려주고 끝낸다.
// =====================================================================

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class JumpLayerBuilder
{
    const string ControllerPath = "Assets/04.Animations/Player/Animations/PlayerAnimationCC.controller";
    const string MaskPath       = "Assets/04.Animations/Player/JumpLowerBodyMask.asset";
    const string ArmMaskPath    = "Assets/04.Animations/Player/JumpFreeArmMask.asset";

    public const string LayerName    = "Jump Layer";
    public const string ArmLayerName = "Jump Arm Layer";
    const string JumpStateName  = "Jump";
    const string EmptyStateName = "Empty";
    const string JumpTrigger    = "Jump";

    // 점프 클립의 재생 속도를 코드가 직접 쥔다(0 = 그 자세로 정지, 1 = 정상 재생).
    // 공중에서 다리를 접은 자세로 멈춰 있다가 착지 직전에 나머지를 재생하는 데 쓴다.
    public const string JumpSpeedParam = "JumpSpeed";

    // 낙하 전용 모션을 쓰던 시절의 파라미터. 지금은 공중에서 점프 자세를 그대로 유지하므로
    // 필요 없다 — 남아 있으면 지운다(예전에 만든 컨트롤러 정리용).
    const string ObsoleteFallingParam = "Falling";

    [MenuItem("Tools/TIMEKOV/애니메이션/점프를 하체 전용 레이어로 분리")]
    static void Apply()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
        {
            Debug.LogError($"[점프레이어] 애니메이터를 못 찾았습니다: {ControllerPath}");
            return;
        }

        // 이미 만들어져 있으면 클립만 챙겨서 통째로 다시 만든다.
        //   ★예전엔 여기서 그냥 돌아가고 '되돌리기부터 하라'고 했는데, 구조가 바뀔 때마다
        //     되돌리기 → 다시 적용 두 번을 시켜야 했다. 한 번으로 끝나게 한다.
        Motion jumpClip = TakeClipAndRemoveLayers(ctrl);

        // 없으면 아직 Base Layer 에 있다는 뜻 — 거기서 꺼내 온다.
        if (jumpClip == null) jumpClip = ExtractJumpFromBaseLayer(ctrl);

        if (jumpClip == null)
        {
            Debug.LogError("[점프레이어] Jump 상태를 못 찾았습니다. 이미 옮겼거나 이름이 다릅니다.");
            return;
        }

        EnsureTrigger(ctrl, JumpTrigger);
        EnsureFloat(ctrl, JumpSpeedParam, 1f);         // 기본 1 = 정상 재생(코드가 0 으로 멈춘다)
        RemoveParameter(ctrl, ObsoleteFallingParam);   // 낙하 모션을 걷어내면서 안 쓰게 된 파라미터
        ctrl.AddLayer(MakeJumpLayer(ctrl, LayerName,    CreateOrLoadMask(),    jumpClip));
        ctrl.AddLayer(MakeJumpLayer(ctrl, ArmLayerName, CreateOrLoadArmMask(), jumpClip));

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        Debug.Log($"[점프레이어] '{LayerName}' + '{ArmLayerName}' 를 만들고 Base Layer 의 Jump 를 옮겼습니다.\n" +
                  $"마스크: {MaskPath} / {ArmMaskPath}\n" +
                  "가중치는 PlayerAnimatorComponent 가 정합니다(팔 세기는 Jump Arm Weight).", ctrl);
        Selection.activeObject = ctrl;
    }

    // ==================================================================
    /// <summary>Empty ↔ Jump 두 상태짜리 레이어 하나. 하체용과 팔용이 같은 클립·같은 전환을 쓴다.</summary>
    static AnimatorControllerLayer MakeJumpLayer(AnimatorController ctrl, string name,
                                                 AvatarMask mask, Motion jumpClip)
    {
        var sm = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
        // 상태 머신은 컨트롤러 에셋 안에 같이 저장돼야 한다(안 그러면 참조가 끊긴다).
        AssetDatabase.AddObjectToAsset(sm, ctrl);

        var empty = sm.AddState(EmptyStateName);   // 모션 없음 = 아무것도 안 함
        var jump  = sm.AddState(JumpStateName);
        jump.motion = jumpClip;
        sm.defaultState = empty;

        // ★Write Defaults 를 끈다. 켜져 있으면 '모션 없는 상태'가 가중치를 받았을 때
        //   마스크에 걸린 뼈를 기본 자세(바인드 포즈)로 덮어써서 다리가 통째로 굳는다.
        //   끄면 애니메이션할 게 없는 상태는 아무것도 안 써서 아래 레이어가 그대로 비친다.
        //   Empty 가 실수로 노출돼도 이동 자세가 그냥 보일 뿐 사고가 안 난다.
        empty.writeDefaultValues = false;
        jump.writeDefaultValues  = false;

        // 재생 속도를 파라미터에 연결한다. 코드(PlayerAnimatorComponent)가 이 값을 0/1 로 오가며
        // '체공 자세에서 정지 → 착지 직전에 나머지 재생'을 만든다.
        // ★두 레이어의 Jump 가 같은 파라미터를 보므로 다리와 팔이 항상 같은 프레임에 멈춘다.
        jump.speedParameterActive = true;
        jump.speedParameter = JumpSpeedParam;

        // AnyState → Jump : 트리거를 받으면 어느 상태에서든 클립 처음부터 다시 시작한다.
        //   ★Empty → Jump 로만 두면 안 된다. 점프 클립은 체공 시간보다 길어서, 착지한 뒤에도
        //     한동안 Jump 상태에 머문다. 그때 다시 뛰면 Jump → Jump 로 갈 길이 없어 트리거가
        //     대기하고, 클립은 중간부터 이어져 두 번째 점프의 다리가 엉뚱한 자세로 나온다.
        //   ★canTransitionToSelf 를 켜야 자기 자신으로도 다시 들어간다(그게 '처음부터 다시').
        //   ★전환 시간은 0 이다. Empty 는 모션이 없어서 섞는 동안 다리가 '아무것도 아닌 자세'와
        //     반반으로 나와 툭 튄다. 이 레이어는 곧바로 점프 자세를 내보내고, 걷기 자세에서
        //     점프 자세로 넘어가는 블렌딩은 '레이어 가중치'가 담당한다(Jump Blend In).
        var toJump = sm.AddAnyStateTransition(jump);
        toJump.hasExitTime = false;
        toJump.duration = 0f;
        toJump.canTransitionToSelf = true;
        toJump.AddCondition(AnimatorConditionMode.If, 0f, JumpTrigger);

        // ★Jump 에서 나가는 전환은 '일부러' 두지 않는다.
        //
        //   점프 클립은 반복 재생이 아니라서, 다 재생되고 나면 마지막 프레임 자세로 멈춘다.
        //   나가는 길이 없으니 공중에 있는 동안은 그 자세가 계속 유지된다 = 낙하 자세를 따로
        //   두지 않아도 공중에서 다리가 점프 자세로 멈춰 있다.
        //   착지하면 코드가 레이어 가중치를 0 으로 빼서(Jump Blend Out) 이동 자세로 돌아온다.
        //   다음 점프는 위 AnyState 전환이 클립을 처음부터 다시 돌린다.
        //
        //   ★예전엔 여기에 exitTime 0.9 짜리 Jump → Empty 가 있었다. 그런데 착지 전에
        //     클립이 끝나 버리면 가중치는 1 인 채로 Empty(모션 없음)가 하체를 덮어써서,
        //     공중에서 다리가 굳은 채 미끄러지듯 움직였다.

        return new AnimatorControllerLayer
        {
            name = name,
            avatarMask = mask,
            blendingMode = AnimatorLayerBlendingMode.Override,
            defaultWeight = 0f,
            iKPass = false,
            stateMachine = sm
        };
    }

    /// <summary>하체 마스크 — 골반·다리·발IK 만 켠다.
    ///
    /// ★Root(골반)를 켜는 이유: 휴머노이드에서 골반은 Root 가 담당한다. 끄면 무릎만 굽고
    ///   몸은 그대로라 웅크리는 동작이 안 나온다.
    /// ★상체(Body·Head)와 양팔은 끈다 — 여기가 등 뒤 대검 자세를 지키는 부분이다.
    ///   빈 팔은 아래 전용 레이어가 따로 맡는다(가중치로 세기 조절).</summary>
    static AvatarMask CreateOrLoadMask()
    {
        var mask = LoadOrCreate(MaskPath);
        ClearAll(mask);

        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, true);

        EditorUtility.SetDirty(mask);
        return mask;
    }

    /// <summary>검을 안 든 팔만 켠 마스크. 이 레이어의 가중치가 곧 팔 동작의 세기다.
    /// ★검이 왼손이면 아래 Left/Right 를 바꾸거나, 만들어진 마스크 인스펙터에서 체크를
    ///   뒤집으면 된다(코드 수정 불필요).</summary>
    static AvatarMask CreateOrLoadArmMask()
    {
        var mask = LoadOrCreate(ArmMaskPath);
        ClearAll(mask);

        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);

        EditorUtility.SetDirty(mask);
        return mask;
    }

    static AvatarMask LoadOrCreate(string path)
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (mask != null) return mask;
        mask = new AvatarMask();
        AssetDatabase.CreateAsset(mask, path);
        return mask;
    }

    static void ClearAll(AvatarMask mask)
    {
        foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
            if (part != AvatarMaskBodyPart.LastBodyPart)
                mask.SetHumanoidBodyPartActive(part, false);
    }

    /// <summary>Base Layer 의 Jump 상태에서 클립을 꺼내고 그 상태를 제거한다. 클립을 반환.</summary>
    static Motion ExtractJumpFromBaseLayer(AnimatorController ctrl)
    {
        if (ctrl.layers.Length == 0) return null;
        var sm = ctrl.layers[0].stateMachine;

        foreach (var child in sm.states)
        {
            var st = child.state;
            if (st == null || st.name != JumpStateName) continue;

            var clip = st.motion;
            sm.RemoveState(st);   // 이 상태로 드나드는 전환도 같이 정리된다
            return clip;
        }
        return null;
    }

    static void EnsureTrigger(AnimatorController ctrl, string name)
    {
        foreach (var p in ctrl.parameters)
            if (p.name == name) return;
        ctrl.AddParameter(name, AnimatorControllerParameterType.Trigger);
    }

    /// <summary>이미 만들어져 있는 점프 레이어들에서 클립만 회수하고 레이어는 지운다.
    /// 만들어진 적이 없으면 null. (구조를 바꿔 다시 만들 때 쓴다)</summary>
    static Motion TakeClipAndRemoveLayers(AnimatorController ctrl)
    {
        int idx = FindLayerIndex(ctrl);
        if (idx < 0) return null;

        Motion clip = null;
        foreach (var child in ctrl.layers[idx].stateMachine.states)
            if (child.state != null && child.state.name == JumpStateName) { clip = child.state.motion; break; }

        // 팔 레이어부터 지운다 — 지울 때마다 인덱스가 당겨지므로 매번 다시 찾는다.
        int armIdx = FindLayerIndex(ctrl, ArmLayerName);
        if (armIdx >= 0) ctrl.RemoveLayer(armIdx);

        idx = FindLayerIndex(ctrl);
        if (idx >= 0) ctrl.RemoveLayer(idx);

        return clip;
    }

    static void EnsureFloat(AnimatorController ctrl, string name, float defaultValue)
    {
        foreach (var p in ctrl.parameters)
            if (p.name == name) return;
        ctrl.AddParameter(new AnimatorControllerParameter
        {
            name = name,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = defaultValue
        });
    }

    /// <summary>파라미터가 있으면 지운다(없으면 아무것도 안 함).</summary>
    static void RemoveParameter(AnimatorController ctrl, string name)
    {
        for (int i = ctrl.parameters.Length - 1; i >= 0; i--)
            if (ctrl.parameters[i].name == name) ctrl.RemoveParameter(i);
    }

    static int FindLayerIndex(AnimatorController ctrl, string name = LayerName)
    {
        for (int i = 0; i < ctrl.layers.Length; i++)
            if (ctrl.layers[i].name == name) return i;
        return -1;
    }

    // ==================================================================
    [MenuItem("Tools/TIMEKOV/애니메이션/점프 하체 레이어 되돌리기")]
    static void Revert()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) { Debug.LogError($"[점프레이어] 애니메이터를 못 찾았습니다: {ControllerPath}"); return; }

        // 팔 레이어부터 지운다 — 지울 때마다 인덱스가 당겨지므로 매번 다시 찾는다.
        int armIdx = FindLayerIndex(ctrl, ArmLayerName);
        if (armIdx >= 0) ctrl.RemoveLayer(armIdx);

        int idx = FindLayerIndex(ctrl);
        if (idx < 0)
        {
            if (armIdx >= 0) { EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets(); }
            Debug.Log("[점프레이어] 분리된 레이어가 없습니다.");
            return;
        }

        // 지우기 전에 클립을 회수해 Base Layer 로 돌려놓는다(안 하면 점프가 통째로 사라진다).
        Motion clip = null;
        foreach (var child in ctrl.layers[idx].stateMachine.states)
            if (child.state != null && child.state.name == JumpStateName) { clip = child.state.motion; break; }

        ctrl.RemoveLayer(idx);

        if (clip != null && ctrl.layers.Length > 0)
        {
            var baseSm = ctrl.layers[0].stateMachine;
            var jump = baseSm.AddState(JumpStateName);
            jump.motion = clip;

            EnsureTrigger(ctrl, JumpTrigger);
            var any = baseSm.AddAnyStateTransition(jump);
            any.hasExitTime = false;
            any.duration = 0.05f;
            any.AddCondition(AnimatorConditionMode.If, 0f, JumpTrigger);

            if (baseSm.defaultState != null)
            {
                var back = jump.AddTransition(baseSm.defaultState);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.1f;
            }
            Debug.Log("[점프레이어] Base Layer 로 되돌렸습니다. 전환 조건은 확인해 주세요(원본과 다를 수 있음).", ctrl);
        }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
    }
}
#endif
