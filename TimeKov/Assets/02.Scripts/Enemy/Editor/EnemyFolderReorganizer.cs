using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 03.Model/Enemy 폴더의 17마리를 Detect 보유 여부로 재번호 매김.
/// 01~10 = Detect 클립 보유 (깨어남/포효/부활/Threat 등)
/// 11~17 = Detect 클립 없음 (그냥 idle → 즉시 추격형)
/// 충돌 방지 위해 임시 이름 거침. 자산 GUID는 유지되어 참조 안 깨짐.
/// 메뉴: Tools > Enemy > Reorganize Folders By Detect
/// </summary>
public static class EnemyFolderReorganizer
{
    const string Root = "Assets/03.Model/Enemy";

    // Detect 보유 10마리 (기존 폴더 이름 → 새 폴더 이름)
    static readonly (string from, string to)[] DetectEnemies = new[]
    {
        ("01.Evil Watcher",      "01.Evil Watcher"),
        ("03.Skeleton Knight",   "02.Skeleton Knight"),
        ("04.Undead",            "03.Undead"),
        ("07.Darkness Spider",   "04.Darkness Spider"),
        ("09.Giant Rat",         "05.Giant Rat"),
        ("10.Fantasy Wolf",      "06.Fantasy Wolf"),
        ("11.Oak Tree Ent",      "07.Oak Tree Ent"),
        ("12.Werewolf",          "08.Werewolf"),
        ("Mummy",                "09.Mummy"),
        ("Wyvern",               "10.Wyvern"),
    };

    // Detect 없음 7마리
    static readonly (string from, string to)[] NonDetectEnemies = new[]
    {
        ("02.Ghoul",             "11.Ghoul"),
        ("05.Vampire",           "12.Vampire"),
        ("06.Chimera",           "13.Chimera"),
        ("08.Dragonide",         "14.Dragonide"),
        ("13.Golem",             "15.Golem"),
        ("Griffin",              "16.Griffin"),
        ("Mountain Dragon",      "17.Mountain Dragon"),
    };

    [MenuItem("Tools/Enemy/Delete Non-Detect Enemies (11~17)")]
    public static void DeleteNonDetect()
    {
        // 11~17 폴더 후보 (재정렬 후 이름 + 재정렬 전 이름 둘 다 시도)
        string[] candidates = {
            // 재정렬 후 이름
            $"{Root}/11.Ghoul",
            $"{Root}/12.Vampire",
            $"{Root}/13.Chimera",
            $"{Root}/14.Dragonide",
            $"{Root}/15.Golem",
            $"{Root}/16.Griffin",
            $"{Root}/17.Mountain Dragon",
            // 재정렬 전 이름 (사용자가 아직 재정렬 안 했을 경우)
            $"{Root}/02.Ghoul",
            $"{Root}/05.Vampire",
            $"{Root}/06.Chimera",
            $"{Root}/08.Dragonide",
            $"{Root}/13.Golem",
            $"{Root}/Griffin",
            $"{Root}/Mountain Dragon",
        };

        // 실제로 존재하는 폴더만 추림
        var existing = new List<string>();
        foreach (var path in candidates)
        {
            if (AssetDatabase.IsValidFolder(path))
                existing.Add(path);
        }

        if (existing.Count == 0)
        {
            Debug.LogWarning("[Enemy] Detect 없는 적 폴더 못 찾음. 이미 삭제됐거나 경로 다름.");
            return;
        }

        // 사용자 확인
        bool ok = EditorUtility.DisplayDialog(
            "Detect 없는 적 7마리 삭제",
            $"아래 {existing.Count}개 폴더를 영구 삭제합니다:\n\n" +
            string.Join("\n", existing.ConvertAll(p => "  " + System.IO.Path.GetFileName(p))) +
            "\n\n복구는 git에서만 가능. 진행?",
            "삭제", "취소");
        if (!ok) return;

        int deleted = 0;
        foreach (var path in existing)
        {
            if (AssetDatabase.DeleteAsset(path))
                deleted++;
            else
                Debug.LogError($"[Enemy] 삭제 실패: {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Enemy] Detect 없는 적 {deleted}/{existing.Count}개 폴더 삭제 완료.\n" +
                  "다른 곳에서 참조하던 자산이 있으면 Console에 missing reference 경고가 뜸. 확인 권장.");
    }

    [MenuItem("Tools/Enemy/Reorganize Folders By Detect")]
    public static void Reorganize()
    {
        bool ok = EditorUtility.DisplayDialog(
            "Enemy 폴더 재정렬",
            "03.Model/Enemy 안 17개 폴더를 Detect 보유 여부로 재번호 매김.\n\n" +
            "01~10: Detect 보유 (깨어남/포효/부활/Threat)\n" +
            "11~17: Detect 없음\n\n" +
            "자산 GUID는 유지되어 다른 곳 참조 안 깨짐. 폴더 이름만 변경.",
            "실행", "취소");
        if (!ok) return;

        var all = new List<(string from, string to)>();
        all.AddRange(DetectEnemies);
        all.AddRange(NonDetectEnemies);

        // 1단계: 모든 폴더를 임시 이름으로 (충돌 방지)
        int step1 = 0;
        foreach (var (from, _) in all)
        {
            string fromPath = $"{Root}/{from}";
            string tempPath = $"{Root}/_TEMP_{from}";
            if (AssetDatabase.IsValidFolder(fromPath))
            {
                string err = AssetDatabase.MoveAsset(fromPath, tempPath);
                if (string.IsNullOrEmpty(err)) step1++;
                else Debug.LogWarning($"[Reorg] 임시 이름 변경 실패: {from} → {err}");
            }
            else
            {
                Debug.LogWarning($"[Reorg] 폴더 없음: {fromPath} (스킵)");
            }
        }

        // 2단계: 임시 이름 → 최종 이름
        int step2 = 0;
        var summary = new List<string>();
        foreach (var (from, to) in all)
        {
            string tempPath = $"{Root}/_TEMP_{from}";
            string toPath = $"{Root}/{to}";
            if (AssetDatabase.IsValidFolder(tempPath))
            {
                string err = AssetDatabase.MoveAsset(tempPath, toPath);
                if (string.IsNullOrEmpty(err))
                {
                    step2++;
                    string tag = System.Array.Exists(DetectEnemies, e => e.from == from) ? "[Detect]" : "[NoDetect]";
                    summary.Add($"  {tag} {from} → {to}");
                }
                else
                {
                    Debug.LogError($"[Reorg] 최종 이름 변경 실패: {tempPath} → {toPath}: {err}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[EnemyFolderReorganizer] 재정렬 완료. " +
            $"1단계 {step1}개 / 2단계 {step2}개.\n" +
            string.Join("\n", summary));
    }
}
