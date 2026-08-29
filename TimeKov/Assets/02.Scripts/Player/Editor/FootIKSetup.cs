#if UNITY_EDITOR
// =====================================================================
// FootIKSetup.cs
// 발 IK 를 쓸 수 있게 두 가지를 한 번에 맞춰 준다.
//   Tools/TIMEKOV/애니메이션/발 IK 켜기   (+ 끄기)
//
// [하는 일]
//   1) 애니메이터 Base Layer 의 IK Pass 를 켠다.
//      ★이게 꺼져 있으면 OnAnimatorIK 자체가 호출되지 않는다. 코드가 아무리 맞아도 안 먹는다.
//   2) 열려 있는 씬에서 플레이어의 Animator 를 찾아 PlayerFootIK 를 붙인다.
//      ★Animator 와 '같은' 오브젝트여야 한다 — 이 프로젝트는 Animator 가 플레이어의 자식이라
//        루트에 붙이면 아무 일도 일어나지 않는다. 헷갈리기 쉬워서 도구로 만들었다.
//   3) 이동 컴포넌트의 Ground Mask 를 그대로 물려준다(둘이 다른 바닥을 보면 발이 엉뚱한 데 붙는다).
//
// 붙인 뒤 값 조정은 인스펙터에서 한다. Ankle Height 하나가 제일 중요하다 —
// 발이 땅에 묻히면 올리고, 떠 보이면 내린다.
// =====================================================================

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class FootIKSetup
{
    const string ControllerPath = "Assets/04.Animations/Player/Animations/PlayerAnimationCC.controller";
    const string BaseLayerName  = "Base Layer";

    [MenuItem("Tools/TIMEKOV/애니메이션/발 IK 켜기")]
    static void Enable()
    {
        bool ikPass = SetBaseLayerIKPass(true);
        var  comp   = AttachToPlayer();

        if (!ikPass && comp == null)
        {
            EditorUtility.DisplayDialog("발 IK",
                "애니메이터도 못 찾고 씬에서 플레이어도 못 찾았습니다.\n" +
                "플레이어가 있는 씬을 열고 다시 실행해 주세요.", "확인");
            return;
        }

        Debug.Log($"[발IK] 준비 완료.\n" +
                  $"  Base Layer IK Pass : {(ikPass ? "켬" : "실패(애니메이터 못 찾음)")}\n" +
                  $"  PlayerFootIK       : {(comp != null ? $"'{comp.gameObject.name}' 에 붙임" : "실패(씬에서 플레이어 Animator 못 찾음)")}\n" +
                  "  값은 인스펙터에서 조정하세요 — 발이 묻히면 Ankle Height ↑, 떠 보이면 ↓.",
                  comp);

        if (comp != null) Selection.activeObject = comp.gameObject;
    }

    [MenuItem("Tools/TIMEKOV/애니메이션/발 IK 끄기")]
    static void Disable()
    {
        SetBaseLayerIKPass(false);

        var comp = FindExisting();
        if (comp != null) Undo.DestroyObjectImmediate(comp);

        Debug.Log("[발IK] IK Pass 를 끄고 PlayerFootIK 를 제거했습니다.");
    }

    // ==================================================================
    /// <summary>Base Layer 의 IK Pass 를 켜거나 끈다. 애니메이터를 못 찾으면 false.</summary>
    static bool SetBaseLayerIKPass(bool on)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
        {
            Debug.LogError($"[발IK] 애니메이터를 못 찾았습니다: {ControllerPath}");
            return false;
        }

        // layers 는 복사본을 돌려주는 프로퍼티라, 고친 배열을 통째로 다시 넣어야 저장된다.
        var layers = ctrl.layers;
        bool changed = false;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name != BaseLayerName) continue;
            if (layers[i].iKPass == on) return true;   // 이미 그 상태
            layers[i].iKPass = on;
            changed = true;
        }

        if (!changed)
        {
            Debug.LogError($"[발IK] '{BaseLayerName}' 레이어를 못 찾았습니다.");
            return false;
        }

        ctrl.layers = layers;
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return true;
    }

    /// <summary>씬의 플레이어 Animator 에 PlayerFootIK 를 붙인다. 이미 있으면 그걸 돌려준다.</summary>
    static PlayerFootIK AttachToPlayer()
    {
        var existing = FindExisting();
        if (existing != null) { SyncGroundMask(existing); return existing; }

        var move = Object.FindFirstObjectByType<PlayerMovementComponent>();
        if (move == null) return null;

        var anim = move.GetComponentInChildren<Animator>();
        if (anim == null)
        {
            Debug.LogError("[발IK] 플레이어 밑에서 Animator 를 못 찾았습니다.");
            return null;
        }

        var comp = Undo.AddComponent<PlayerFootIK>(anim.gameObject);
        SyncGroundMask(comp);
        EditorUtility.SetDirty(comp);
        return comp;
    }

    static PlayerFootIK FindExisting() => Object.FindFirstObjectByType<PlayerFootIK>();

    /// <summary>이동 컴포넌트가 쓰는 지면 레이어를 그대로 맞춰 준다.</summary>
    static void SyncGroundMask(PlayerFootIK comp)
    {
        var move = comp.GetComponentInParent<PlayerMovementComponent>();
        if (move == null || move.GroundMask.value == 0) return;
        comp.groundMask = move.GroundMask;
    }
}
#endif
