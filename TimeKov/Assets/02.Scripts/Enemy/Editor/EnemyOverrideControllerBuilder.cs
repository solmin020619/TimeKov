using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// EnemyBase.controller 기반 Override Controller 10마리분 자동 생성.
/// 각 적 폴더 Animations/ 안의 .anim 클립을 휴리스틱으로 매핑 (이름 기반).
/// 메뉴: Tools > Enemy > Build Override Controllers For 10 Enemies
/// </summary>
public static class EnemyOverrideControllerBuilder
{
    const string BaseControllerPath = "Assets/04.Animations/AnimationController/Enemy/EnemyBase.controller";

    // (folder, prefix). prefix는 .anim 파일명 prefix (Extract 메뉴가 폴더명 기반으로 부여한 것)
    static readonly (string folder, string prefix)[] Enemies =
    {
        ("01.Evil Watcher",     "Evil Watcher"),
        ("02.Skeleton Knight",  "Skeleton Knight"),
        ("03.Undead",           "Undead"),
        ("04.Darkness Spider",  "Darkness Spider"),
        ("05.Giant Rat",        "Giant Rat"),
        ("06.Fantasy Wolf",     "Fantasy Wolf"),
        ("07.Oak Tree Ent",     "Oak Tree Ent"),
        ("08.Werewolf",         "Werewolf"),
        ("09.Mummy",            "Mummy"),
        ("10.Wyvern",           "Wyvern"),
    };

    // state 이름 → 클립 후보 키워드 (순서대로 매칭. 첫 매치 사용)
    // 변형(Forward/_RM/ShieldOnly 등)은 우선순위 낮춤
    static readonly Dictionary<string, string[]> StateClipCandidates = new()
    {
        { "Idle",       new[] { "_Idle", "_IdleBreathe", "_IdleNormal", "_Idle1Handed", "_idleNormal", "_IdleAggressive" } },
        { "Locomotion", new[] { "_Walk", "_WalkSlow", "_walk", "_WalkBareHands", "_1HandedWalk", "_CrawlNormal", "_WalkHoldRock" } },
        { "Attack",     new[] { "_Attack1", "_ClawsAttackR", "_LeftClawsAttack", "_RightClawsAttack", "_Bite", "_JumpBite", "_StompAttack", "_1HandedAttack1", "_Attack1Weapon", "_SimpleBiteAttack", "_BiteAttackBareHands" } },
        { "Hit",        new[] { "_GetHit", "_GetHit1", "_GetHitFront", "_GetHit1Normal", "_1HandedGetHit", "_GetHitHeavy", "_GetHitLight1" } },
        { "Die",        new[] { "_Death", "_death1", "_DeathNormal", "_1HandedDeath", "_DeathUnarmed", "_DeathWeapon" } },
        { "Detect",     new[] { "_WakesUp", "_Awakening", "_Roar", "_Howl", "_Taunt", "_RiseFromTheGround", "_ComeOutOfTheGround1Handed_A", "_IdleThreat", "_FlyStationaryRoar", "_Hide1" } },
    };

    [MenuItem("Tools/Enemy/Build Override Controllers For 10 Enemies")]
    public static void BuildAll()
    {
        var baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
        if (baseController == null)
        {
            Debug.LogError($"[OverrideBuilder] Base controller 못 찾음: {BaseControllerPath}\n먼저 'Build Base Animator Controller (6 state)' 메뉴 실행.");
            return;
        }

        int created = 0;
        var summary = new List<string>();

        foreach (var (folder, prefix) in Enemies)
        {
            string animsFolder = $"Assets/03.Model/Enemy/{folder}/Animations";
            if (!AssetDatabase.IsValidFolder(animsFolder))
            {
                summary.Add($"  {folder} → SKIP (Animations 폴더 없음. Extract 메뉴 먼저)");
                continue;
            }

            // 폴더 안 모든 .anim 모음
            var clipGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { animsFolder });
            var clips = new List<AnimationClip>();
            foreach (var g in clipGuids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                if (c != null) clips.Add(c);
            }
            if (clips.Count == 0)
            {
                summary.Add($"  {folder} → SKIP (.anim 없음)");
                continue;
            }

            // Override Controller 생성
            var overrideController = new AnimatorOverrideController(baseController);
            overrideController.name = $"{prefix}_Override";

            // 매핑
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);

            var mappedStates = new List<string>();
            for (int i = 0; i < overrides.Count; i++)
            {
                string stateName = overrides[i].Key.name;
                if (!StateClipCandidates.TryGetValue(stateName, out var candidates)) continue;

                AnimationClip best = FindBestClip(clips, candidates);
                if (best != null)
                {
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, best);
                    mappedStates.Add($"{stateName}={best.name}");
                }
            }
            overrideController.ApplyOverrides(overrides);

            // 저장
            string outPath = $"Assets/03.Model/Enemy/{folder}/{prefix}_Override.overrideController";
            if (AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(outPath) != null)
                AssetDatabase.DeleteAsset(outPath);
            AssetDatabase.CreateAsset(overrideController, outPath);
            created++;
            summary.Add($"  {folder} → {prefix}_Override ({string.Join(", ", mappedStates)})");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[OverrideBuilder] 완료. Override Controller {created}/10개 생성.\n" +
            string.Join("\n", summary) + "\n\n" +
            "다음 단계:\n" +
            "1. 각 적 폴더의 *_Override.overrideController 열어서 매핑 결과 확인\n" +
            "2. 매핑 안 된 state (None 표시)는 인스펙터에서 직접 클립 드래그\n" +
            "3. TestEnemy.prefab의 Animator → Controller 슬롯에 적절한 Override 드래그");
    }

    /// <summary>후보 키워드 순서대로 매칭. 첫 매치 + Forward/_RM 변형 제외.</summary>
    static AnimationClip FindBestClip(List<AnimationClip> clips, string[] candidates)
    {
        foreach (var keyword in candidates)
        {
            foreach (var c in clips)
            {
                if (c.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0) continue;
                // 변형 제외 (Forward, _RM, Combat, Special 등)
                if (c.name.IndexOf("Forward", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (c.name.EndsWith("_RM", StringComparison.OrdinalIgnoreCase)) continue;
                return c;
            }
        }
        return null;
    }
}
