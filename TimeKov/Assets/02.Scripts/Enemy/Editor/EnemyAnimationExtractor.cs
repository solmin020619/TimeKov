using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 03.Model/Enemy 안 모든 적 폴더의 FBX Files/ 안 fbx에서 AnimationClip만 추출,
/// 별도 .anim 자산으로 복사 + 폴더 이름 prefix 자동 부여.
/// 원본 fbx는 손대지 않음.
/// 메뉴: Tools > Enemy > Extract Animation Clips From FBX
/// </summary>
public static class EnemyAnimationExtractor
{
    const string RootFolder = "Assets/03.Model/Enemy";

    [MenuItem("Tools/Enemy/Extract Animation Clips From FBX")]
    public static void ExtractAll()
    {
        if (!AssetDatabase.IsValidFolder(RootFolder))
        {
            Debug.LogError($"[EnemyAnimationExtractor] 폴더 못 찾음: {RootFolder}");
            return;
        }

        var enemyFolders = AssetDatabase.GetSubFolders(RootFolder);
        int totalClips = 0;
        int totalEnemies = 0;
        var summary = new List<string>();

        foreach (var enemyFolder in enemyFolders)
        {
            string folderName = Path.GetFileName(enemyFolder);
            string prefix = StripNumberPrefix(folderName);

            // FBX Files 서브폴더 (대소문자 무관)
            string fbxFolder = FindFbxSubfolder(enemyFolder);
            if (fbxFolder == null)
            {
                summary.Add($"  {folderName} → SKIP (FBX Files 폴더 없음)");
                continue;
            }

            // 출력 폴더 (Animations 서브폴더)
            string outFolder = $"{enemyFolder}/Animations";
            if (!AssetDatabase.IsValidFolder(outFolder))
                AssetDatabase.CreateFolder(enemyFolder, "Animations");

            int enemyClips = 0;
            var fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { fbxFolder });
            foreach (var guid in fbxGuids)
            {
                string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
                var subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
                foreach (var asset in subAssets)
                {
                    var clip = asset as AnimationClip;
                    if (clip == null) continue;
                    if (clip.name.StartsWith("__preview__")) continue;

                    string newName = $"{prefix}_{clip.name}";
                    string outPath = $"{outFolder}/{newName}.anim";
                    if (AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath) != null)
                        continue;   // 이미 추출됨

                    var newClip = Object.Instantiate(clip);
                    newClip.name = newName;
                    AssetDatabase.CreateAsset(newClip, outPath);
                    enemyClips++;
                    totalClips++;
                }
            }

            if (enemyClips > 0)
            {
                summary.Add($"  {folderName} → {enemyClips}개 ({prefix}_*.anim)");
                totalEnemies++;
            }
            else
            {
                summary.Add($"  {folderName} → 0개 (이미 추출됐거나 클립 없음)");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[EnemyAnimationExtractor] 추출 완료.\n" +
            $"  총 {totalEnemies}개 폴더 / {totalClips}개 AnimationClip\n" +
            string.Join("\n", summary));
    }

    /// <summary>"01.Evil Watcher" → "Evil Watcher"</summary>
    static string StripNumberPrefix(string name)
    {
        int dotIdx = name.IndexOf('.');
        if (dotIdx > 0)
        {
            string before = name.Substring(0, dotIdx);
            if (int.TryParse(before, out _))
                return name.Substring(dotIdx + 1).Trim();
        }
        return name;
    }

    /// <summary>FBX Files 서브폴더 찾기 (대소문자/공백 무관)</summary>
    static string FindFbxSubfolder(string enemyFolder)
    {
        var subs = AssetDatabase.GetSubFolders(enemyFolder);
        foreach (var sub in subs)
        {
            string n = Path.GetFileName(sub);
            if (n.Equals("FBX Files", System.StringComparison.OrdinalIgnoreCase)
             || n.Equals("FBX", System.StringComparison.OrdinalIgnoreCase))
                return sub;
        }
        return null;
    }
}
