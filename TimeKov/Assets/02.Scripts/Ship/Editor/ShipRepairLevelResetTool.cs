// =====================================================================
// ShipRepairLevelResetTool.cs
// 열려 있는 씬의 ShipRepairManager 레벨 표를 코드 기본값으로 다시 채운다.
//
// 왜 필요한가: levels 는 씬에 직렬화돼 있어서 코드 기본값만 고치면 반영이 안 된다.
//   인스펙터에서 컴포넌트 우클릭 -> Reset 을 눌러도 되지만, 표가 10줄 x 6칸이라
//   손으로 확인하기 번거로워서 메뉴 한 번으로 끝나게 만들었다.
//
// ★레벨 값 자체는 여기 없다. ShipRepairManager.Reset() 이 단일 소스이고
//   이 툴은 리스트를 비운 뒤 그 Reset() 을 대신 불러줄 뿐이다.
//   표를 두 벌로 만들면 반드시 어긋나기 때문에 일부러 이렇게 했다.
//   -> 값을 바꾸려면 ShipRepairManager.Reset() 을 고치고 이 메뉴를 다시 실행한다.
//
// 일회성 성격의 툴이다. 레벨 표가 확정되면 지워도 된다.
// =====================================================================

using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ShipRepairLevelResetTool
{
    private const string MenuPath = "Tools/TIMEKOV/우주선 수리 레벨 초기화";

    // 자연맵에 배치된 복구 에너지 개수. Lv.5 까지 이걸로만 도달해야 한다(데모 설계).
    //   자연맵 픽업만 자동으로 골라낼 방법이 없어(맵 구분 정보가 픽업에 없다) 이 값은 수동 유지한다.
    //   전체 배치량은 씬에서 실제로 세므로 이쪽만 맞춰두면 된다.
    private const int NatureParts = 15;
    private const int NatureMaxLevel = 5;

    // 씬에 놓인 ShipPartPickup 이 주는 복구 에너지 총합. 픽업마다 amount 가 다를 수 있어 개수가 아니라 합을 센다.
    private static int CountPlacedParts(out int pickupCount)
    {
        var pickups = Object.FindObjectsByType<ShipPartPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        pickupCount = pickups.Length;
        int sum = 0;
        foreach (var p in pickups)
        {
            var amt = new SerializedObject(p).FindProperty("amount");
            sum += amt != null ? Mathf.Max(0, amt.intValue) : 1;
        }
        return sum;
    }

    [MenuItem(MenuPath)]
    private static void ResetLevels()
    {
        var mgr = Object.FindFirstObjectByType<ShipRepairManager>(FindObjectsInactive.Include);
        if (mgr == null)
        {
            EditorUtility.DisplayDialog("우주선 수리 레벨 초기화",
                "열려 있는 씬에서 ShipRepairManager 를 못 찾았다.\nWorld 씬을 열고 다시 실행해라.", "확인");
            return;
        }

        Undo.RecordObject(mgr, "우주선 수리 레벨 초기화");

        // 1) 리스트를 비운다 - Reset() 안의 "이미 값이 있으면 건너뛴다" 가드를 통과시키기 위해서다.
        var so = new SerializedObject(mgr);
        var levelsProp = so.FindProperty("levels");
        if (levelsProp == null)
        {
            Debug.LogError("[ShipRepairLevelReset] levels 필드를 못 찾았다. 필드 이름이 바뀌었는지 확인해라.");
            return;
        }
        levelsProp.ClearArray();
        so.ApplyModifiedPropertiesWithoutUndo();

        // 2) 코드 기본값(단일 소스)을 그대로 불러 채운다.
        var reset = typeof(ShipRepairManager).GetMethod("Reset",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (reset == null)
        {
            Debug.LogError("[ShipRepairLevelReset] ShipRepairManager.Reset() 을 못 찾았다. "
                         + "#if UNITY_EDITOR 로 감싼 채 남아 있는지 확인해라.");
            return;
        }
        reset.Invoke(mgr, null);

        EditorUtility.SetDirty(mgr);
        EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);

        Debug.Log(BuildReport(mgr));
    }

    // 결과를 콘솔에 표로 찍는다 - 인스펙터를 10줄 펼쳐 확인하지 않아도 되게.
    // 부품 누적이 맵 배치량을 넘으면(= 진행 불가) 경고까지 같이 낸다.
    private static string BuildReport(ShipRepairManager mgr)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[ShipRepairLevelReset] 레벨 {mgr.MaxLevel}단계로 초기화 완료. 씬 저장(Ctrl+S) 해야 남는다.");
        sb.AppendLine("Lv  단계명                     부품  누적  공장속도  연료(s)  건축칸  특수부품");

        int sum = 0;
        int natureNeed = 0;
        for (int lv = 1; lv <= mgr.MaxLevel; lv++)
        {
            var d = mgr.GetLevel(lv);
            if (d == null) continue;
            sum += Mathf.Max(0, d.requiredParts);
            if (lv <= NatureMaxLevel) natureNeed = sum;

            string extra = string.IsNullOrEmpty(d.extraPartName) ? "-" : d.extraPartName;
            sb.AppendLine($"{lv,2}  {d.title,-24} {d.requiredParts,4} {sum,5} {d.factorySpeed,9:0.00} "
                        + $"{d.fuelSeconds,8:0} {d.zoneCells,7}  {extra}");
        }

        int placed = CountPlacedParts(out int pickupCount);
        sb.AppendLine($"복구 에너지 총 필요 = {sum}개");
        sb.AppendLine($"씬에 실제 배치 = {placed}개 (픽업 오브젝트 {pickupCount}개)");
        sb.AppendLine($"Lv.{NatureMaxLevel} 까지 필요 = {natureNeed}개 / 자연맵 배치 {NatureParts}개");

        if (natureNeed > NatureParts)
            sb.AppendLine($"★경고: 자연맵({NatureParts}개)만으로 Lv.{NatureMaxLevel} 에 못 간다. "
                        + "데모가 여기서 막히니 requiredParts 를 낮추거나 자연맵에 부품을 더 놔라.");
        if (sum > placed)
            sb.AppendLine($"★경고: 배치({placed})보다 {sum - placed}개 더 필요하다. 최종 레벨에 도달할 수 없다. "
                        + "맵에 ShipPartPickup 을 더 놓아라.");
        else if (sum > placed - 5)
            sb.AppendLine($"주의: 여유가 {placed - sum}개뿐이다. 픽업을 몇 개 놓치면 막힌다. 5개 이상 여유를 두는 게 안전하다.");

        for (int lv = 2; lv <= Mathf.Min(NatureMaxLevel, mgr.MaxLevel); lv++)
        {
            var d = mgr.GetLevel(lv);
            if (d != null && !string.IsNullOrEmpty(d.extraPartName))
                sb.AppendLine($"★경고: Lv.{lv} 에 특수부품('{d.extraPartName}')이 걸려 있다. "
                            + "특수부품은 전송 마일스톤에서만 나오므로 자연맵만으로 Lv.5 도달이 깨진다.");
        }

        // 뒷 레벨이 앞 레벨보다 싸면 플레이어는 그냥 버그로 읽는다. 예산 맞추다 흔히 생기는 실수라 검사한다.
        for (int lv = 3; lv <= mgr.MaxLevel; lv++)
        {
            var prev = mgr.GetLevel(lv - 1);
            var cur  = mgr.GetLevel(lv);
            if (prev != null && cur != null && cur.requiredParts < prev.requiredParts)
                sb.AppendLine($"★경고: Lv.{lv} 부품({cur.requiredParts})이 Lv.{lv - 1}({prev.requiredParts})보다 적다. "
                            + "수리 비용은 줄어들면 안 된다.");
        }

        return sb.ToString();
    }
}
